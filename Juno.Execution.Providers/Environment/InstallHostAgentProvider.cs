namespace Juno.Execution.Providers.Environment
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.Execution.ArmIntegration;
    using Juno.Execution.Providers.AutoTriage;
    using Juno.Execution.TipIntegration;
    using Juno.Extensions.Telemetry;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Identity;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.IdentityModel.Clients.ActiveDirectory;
    using TipGateway.Entities;

    /// <summary>
    /// Provider that boots straps host agent through PF service via TIP.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.EnvironmentSetup, SupportedStepTarget.ExecuteRemotely)]
    [SupportedParameter(Name = StepParameters.PilotfishServicePath, Type = typeof(string))]
    [ProviderInfo(Name = "Install Juno Host Agent", Description = "Install the Juno Host agent on the physical nodes/blades in the specified experiment group", FullDescription = "Step to install the Juno Host agent on physical nodes in the environment as part of Juno experiment. The Juno system uses simple agent executables (.exe) that run as services on nodes and VMs to manage work associated with experiment steps on those individual systems. The Juno Host agent runs on physical nodes associated with the experiment and is responsible for handling the application of the Intel microcode update as part of this experiment. The Juno Host agent additionally monitors certain aspects of the system as it is running producing data that can be analyzed after the experiment runs.")]
    public partial class InstallHostAgentProvider : ExperimentProvider
    {
        private const string KeyVaultAuthenticationResourceId = "https://vault.azure.net";
        private const int DefaultMaxInstallationAttempts = 3;
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(30);

        private AadPrincipalSettings hostAgentAadSettings;
        private ITipClient tipClient;
        private TipSettings tipSettings;

        /// <summary>
        /// Initializes a new instance of the <see cref="InstallHostAgentProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public InstallHostAgentProvider(IServiceCollection services)
            : base(services)
        {
        }

        /// <inheritdoc />
        protected override async Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            ExecutionResult result = new ExecutionResult(ExecutionStatus.InProgress);

            if (!cancellationToken.IsCancellationRequested)
            {
                EnvironmentSettings settings = EnvironmentSettings.Initialize(context.Configuration);
                this.tipSettings = settings.TipSettings;
                this.hostAgentAadSettings = settings.AgentSettings.AadPrincipals.Get(Setting.HostAgent);

                if (!this.Services.TryGetService<ITipClient>(out ITipClient tipClient))
                {
                    tipClient = new TipClient(context.Configuration);
                }

                this.tipClient = tipClient;

                State state = await this.GetStateAsync<State>(context, cancellationToken).ConfigureDefaults()
                    ?? new State();

                if (!state.JwtDropRequested)
                {
                    await this.RequestTokenDropAsync(context, component, state, telemetryContext, cancellationToken).ConfigureDefaults();
                    await this.SaveStateAsync<State>(context, state, cancellationToken).ConfigureDefaults();
                }

                if (state.JwtDropRequested && !state.JwtDropConfirmed)
                {
                    await this.VerifyTokenDropAsync(context, component, state, telemetryContext, cancellationToken).ConfigureDefaults();
                    await this.SaveStateAsync<State>(context, state, cancellationToken).ConfigureDefaults();
                }

                if (state.JwtDropConfirmed && !state.AgentInstallationRequested)
                {
                    if (!state.AgentRegistered)
                    {
                        await this.RegisterAgentsWithExperimentAsync(context, component, telemetryContext, cancellationToken)
                            .ConfigureDefaults();
                    }

                    await this.RequestAgentInstallationAsync(context, component, state, telemetryContext, cancellationToken)
                        .ConfigureDefaults();

                    await this.SaveStateAsync<State>(context, state, cancellationToken).ConfigureDefaults();
                }
                
                if (state.AgentInstallationRequested && !state.AgentInstallationConfirmed)
                {
                    await this.VerifyAgentInstallationAsync(context, component, state, telemetryContext, cancellationToken)
                        .ConfigureDefaults();

                    await this.SaveStateAsync<State>(context, state, cancellationToken).ConfigureDefaults();
                }

                if (state.AgentInstallationConfirmed && !state.AgentHeartbeatConfirmed)
                {
                    await this.VerifyAgentHeartbeatAsync(context, component, state, telemetryContext, cancellationToken)
                        .ConfigureDefaults();

                    await this.SaveStateAsync<State>(context, state, cancellationToken).ConfigureDefaults();
                }

                if (state.AgentHeartbeatConfirmed)
                {
                    result = new ExecutionResult(ExecutionStatus.Succeeded);
                }
                
            }

            return result;
        }

        private static string EncryptToken(string token, TipSession session)
        {
            AgentIdentification agentId = new AgentIdentification(session.ClusterName, session.NodeId, context: session.TipSessionId);
            // Encryption key is agent id, IV is tipsession id.
            byte[] encryptedToken = AesCrypto.Encrypt(token, agentId.ToString(), session.TipSessionId);
            return Convert.ToBase64String(encryptedToken);
        }

        private async Task<string> GetBootstrapAccessTokenAsync()
        {
            if (!this.Services.TryGetService<IAuthenticationProvider<AuthenticationResult>>(out IAuthenticationProvider<AuthenticationResult> tokenProvider))
            {
                tokenProvider = new AadAuthenticationProvider(
                    this.hostAgentAadSettings.AuthorityUri,
                    this.hostAgentAadSettings.PrincipalId,
                    InstallHostAgentProvider.KeyVaultAuthenticationResourceId,
                    this.hostAgentAadSettings.PrincipalCertificateThumbprint);
            }

            AuthenticationResult authResult = await tokenProvider.AuthenticateAsync().ConfigureAwait(false);
            return authResult.AccessToken;
        }

        private async Task<IList<TipSession>> GetTipSessionsAsync(ExperimentContext context, ExperimentComponent component, CancellationToken cancellationToken)
        {
            IEnumerable<EnvironmentEntity> entitiesProvisioned = await this.GetEntitiesProvisionedAsync(context, cancellationToken)
                .ConfigureDefaults();

            List<EnvironmentEntity> tipEntities = entitiesProvisioned.GetEntities(EntityType.TipSession, component.Group).ToList();
            IList<TipSession> tipSessions = TipSession.FromEnvironmentEntities(tipEntities);

            if (!tipSessions.Any())
            {
                throw new ProviderException($"No tip sessions are provisioned for this group: {component.Group}.", ErrorReason.ExpectedEnvironmentEntitiesNotFound);
            }
            else if (tipSessions.Any(t => t.Status != TipSessionStatus.Created))
            {
                throw new ProviderException($"One of more Tip sessions provisioned for this group is not in created state.", ErrorReason.ExpectedEnvironmentEntitiesInvalid);
            }

            return tipSessions;
        }

        private async Task RequestAgentInstallationAsync(ExperimentContext context, ExperimentComponent component, State state, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            IList<TipSession> tipSessions = await this.GetTipSessionsAsync(context, component, cancellationToken).ConfigureDefaults();

            string hostAgentPFPath = component.Parameters.GetValue<string>(StepParameters.PilotfishServicePath, this.tipSettings.HostAgentBootstrapPilotfishPath);

            List<KeyValuePair<string, string>> autopilotServices = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>(
                    this.tipSettings.HostAgentBootstrapPilotfishName,
                    hostAgentPFPath)
            };

            foreach (TipSession tipSession in tipSessions)
            {
                EventContext relatedContext = telemetryContext.Clone()
                    .AddContext(nameof(tipSession), tipSession);

                await this.Logger.LogTelemetryAsync($"{nameof(InstallHostAgentProvider)}.RequestInstallation", relatedContext, async () =>
                {
                    TipNodeSessionChange tipSessionChangeResponse = await this.tipClient.ApplyPilotFishServicesAsync(tipSession.TipSessionId, autopilotServices, cancellationToken)
                        .ConfigureDefaults();

                    tipSession.ChangeIdList.Add(tipSessionChangeResponse.TipNodeSessionChangeId);
                    relatedContext.AddContext(nameof(tipSessionChangeResponse), tipSessionChangeResponse);
                }).ConfigureDefaults();
            }

            await this.SaveTipSessionsAsync(tipSessions, context, component, cancellationToken).ConfigureDefaults();
            state.AgentInstallationRequested = true;
            state.AgentInstallationRequestTime = DateTime.UtcNow;
        }

        private async Task RegisterAgentsWithExperimentAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            IList<TipSession> tipSessions = await this.GetTipSessionsAsync(context, component, cancellationToken).ConfigureDefaults();
            if (tipSessions?.Any() == true)
            {
                IProviderDataClient dataClient = this.Services.GetService<IProviderDataClient>();
                foreach (TipSession tipSession in tipSessions)
                {
                    AgentIdentification agentId = new AgentIdentification(tipSession.ClusterName, tipSession.NodeId, tipSession.TipSessionId);

                    EventContext relatedContext = telemetryContext.Clone()
                        .AddContext(agentId);

                    await this.Logger.LogTelemetryAsync($"{nameof(InstallHostAgentProvider)}.RegisterAgent", relatedContext, async () =>
                    {
                        ExperimentComponent agentRegistrationStep = new ExperimentComponent(
                            typeof(HostAgentRegistrationProvider).FullName,
                            "Register Agent",
                            "Registers/associates the Host Agent with the experiment",
                            group: component.Group);

                        await dataClient.CreateAgentStepsAsync(context.ExperimentStep, agentRegistrationStep, agentId.ToString(), cancellationToken)
                            .ConfigureDefaults();
                    }).ConfigureDefaults();
                }
            }
        }

        private async Task RequestTokenDropAsync(ExperimentContext context, ExperimentComponent component, State state, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            IEnumerable<TipSession> tipSessions = await this.GetTipSessionsAsync(context, component, cancellationToken).ConfigureDefaults();
            TimeSpan injectionTimeout = TimeSpan.FromSeconds(60);
            string token = await this.GetBootstrapAccessTokenAsync().ConfigureAwait(false);

            foreach (TipSession session in tipSessions)
            {
                string encryptedToken = InstallHostAgentProvider.EncryptToken(token, session);
                // The command will go into xml, which means these needs to be xml unescaped, the cmd /c is also required.
                string command = $@"cmd /c echo {encryptedToken} >> C:\\Data\\Juno.AccessToken.txt";
                string escapedCommand = new XText(command).ToString();

                EventContext relatedContext = telemetryContext.Clone();
                await this.Logger.LogTelemetryAsync($"{nameof(InstallHostAgentProvider)}.RequestTokenDrop", relatedContext, async () =>
                {
                    TipNodeSessionChange tipSessionChangeResponse = await this.tipClient.ExecuteNodeCommandAsync(session.TipSessionId, session.NodeId, escapedCommand, injectionTimeout, cancellationToken)
                        .ConfigureDefaults();

                    session.ChangeIdList.Add(tipSessionChangeResponse.TipNodeSessionChangeId);
                    relatedContext.AddContext(nameof(tipSessionChangeResponse), tipSessionChangeResponse);
                }).ConfigureDefaults();
            }

            await this.SaveTipSessionsAsync(tipSessions, context, component, cancellationToken).ConfigureDefaults();
            state.JwtDropRequested = true;
            state.JwtDropRequestTime = DateTime.UtcNow;
        }

        private Task SaveTipSessionsAsync(IEnumerable<TipSession> tipSessions, ExperimentContext context, ExperimentComponent component, CancellationToken cancellationToken)
        {
            IEnumerable<EnvironmentEntity> tipEntities = TipSession.ToEnvironmentEntities(tipSessions.ToList(), component.Group);

            return this.UpdateEntitiesProvisionedAsync(context, tipEntities, cancellationToken);
        }

        private async Task VerifyAgentHeartbeatAsync(ExperimentContext context, ExperimentComponent component, State state, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            EventContext relatedContext = telemetryContext.Clone(withProperties: true);
            IList<TipSession> tipSessions = await this.GetTipSessionsAsync(context, component, cancellationToken).ConfigureDefaults();

            state.AgentHeartbeatConfirmed = await this.Logger.LogTelemetryAsync($"{nameof(InstallHostAgentProvider)}.CheckHeartbeat", relatedContext, async () =>
            {
                bool heartbeatExists = false;

                try
                {
                    IProviderDataClient apiClient = this.Services.GetService<IProviderDataClient>();
                    foreach (TipSession tipSession in tipSessions)
                    {
                        var agentId = new AgentIdentification(tipSession.ClusterName, tipSession.NodeId, context: tipSession.TipSessionId).ToString();
                        var heartbeat = await apiClient.GetAgentHeartbeatAsync(agentId, cancellationToken).ConfigureDefaults();
                        heartbeatExists = heartbeat != null;
                    }
                }
                catch (Exception exception)
                {
                    relatedContext.AddError(exception);
                }

                return heartbeatExists;
            }).ConfigureAwait(false);

            TimeSpan agentInstallationTimeout = component.Parameters.GetTimeSpanValue(StepParameters.Timeout, InstallHostAgentProvider.DefaultTimeout);
            if (!state.AgentHeartbeatConfirmed && state.IsAgentInstallationRequestTimedOut(agentInstallationTimeout))
            {
                throw new ProviderException(
                    $"Hostagent deployment started at '{state.AgentInstallationRequestTime}' has not sent heartbeat " +
                    $"within '{agentInstallationTimeout}'. Timing out the experiment.",
                    ErrorReason.Timeout);
            }
        }

        [SuppressMessage("Naming", "AZCA1004:AsyncResultProhibited Rule", Justification = "The analyzer mistakenly thought 'tipChange.Result' is task.Result but it's not.")]
        private async Task VerifyAgentInstallationAsync(ExperimentContext context, ExperimentComponent component, State state, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            EventContext relatedContext = telemetryContext.Clone(withProperties: true);
            IList<TipSession> tipSessions = await this.GetTipSessionsAsync(context, component, cancellationToken).ConfigureDefaults();
            
            state.AgentInstallationConfirmed = await this.Logger.LogTelemetryAsync($"{nameof(InstallHostAgentProvider)}.VerifyAgentInstallation", relatedContext, async () =>
            {
                bool result = true;
                foreach (TipSession tipSession in tipSessions)
                {
                    TipNodeSessionChangeDetails tipChange = await this.tipClient.GetTipSessionChangeAsync(tipSession.TipSessionId, tipSession.ChangeIdList.LastOrDefault(), cancellationToken)
                        .ConfigureDefaults();

                    if (tipChange.Result == TipNodeSessionChangeResult.Failed && 
                        tipChange.IsRetryableScenario() && 
                        state.AgentInstallationRetries++ <= InstallHostAgentProvider.DefaultMaxInstallationAttempts)
                    {
                        // There are certain scenarios where the TiP Service has failed to deploy the agent PF service for which
                        // we want to retry on. The TiP team confirms the acceptability of some amount of these failures; however,
                        // the Juno team does not consider them acceptable. Hence we are going to retry with the hope of surviving 
                        // these failures and avoiding a failed experiment.
                        state.ResetInstallationWorkflow();
                        telemetryContext.AddContext("installationRetries", state.AgentInstallationRetries);

                        await this.Logger.LogTelemetryAsync(
                            $"{nameof(InstallHostAgentProvider)}.RetryAgentInstallation",
                            Microsoft.Extensions.Logging.LogLevel.Warning,
                            telemetryContext).ConfigureDefaults();

                        result = false;
                    }
                    else if (tipChange.Result == TipNodeSessionChangeResult.Failed)
                    {
                        if (context.Experiment.IsDiagnosticsEnabled())
                        {
                            await this.RequestDiagnosticsAsync(context, tipSession, relatedContext, cancellationToken).ConfigureDefaults();
                        }

                        // This will include both non-tryable error and retries exceeds limit.
                        throw new ProviderException(
                            $"Failed to apply HostAgent pilotfish service on Session '{tipSession.TipSessionId}' with error '{tipChange.ErrorMessage}'.");
                    }
                    else if (tipChange.Result != TipNodeSessionChangeResult.Succeeded)
                    {
                        result = false;
                        break;
                    }
                }

                return result;
            }).ConfigureDefaults();

            TimeSpan timeout = component.Parameters.GetTimeSpanValue(StepParameters.Timeout, InstallHostAgentProvider.DefaultTimeout);
            if (!state.AgentInstallationConfirmed && state.IsAgentInstallationRequestTimedOut(timeout))
            {
                if (context.Experiment.IsDiagnosticsEnabled())
                {
                    tipSessions?.ToList().ForEach(async tipSession => await this.RequestDiagnosticsAsync(context, tipSession, relatedContext, cancellationToken).ConfigureDefaults());
                }

                throw new ProviderException(
                    $"HostAgent Pilot fish service started at '{state.AgentInstallationRequestTime}' has not successfully finished " +
                    $"within '{timeout}'. Timing out the experiment.",
                    ErrorReason.Timeout);
            }
        }

        [SuppressMessage("Naming", "AZCA1004:AsyncResultProhibited Rule", Justification = "The analyzer mistakenly thought 'tipChange.Result' is task.Result but it's not.")]
        private async Task VerifyTokenDropAsync(ExperimentContext context, ExperimentComponent component, State state, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            EventContext relatedContext = telemetryContext.Clone(withProperties: true);
            IList<TipSession> tipSessions = await this.GetTipSessionsAsync(context, component, cancellationToken).ConfigureDefaults();

            state.JwtDropConfirmed = await this.Logger.LogTelemetryAsync($"{nameof(InstallHostAgentProvider)}.VerifyTokenDrop", relatedContext, async () =>
            {
                bool result = true;

                foreach (TipSession tipSession in tipSessions)
                {
                    TipNodeSessionChangeDetails tipChange = await this.tipClient.GetTipSessionChangeAsync(tipSession.TipSessionId, tipSession.ChangeIdList.LastOrDefault(), cancellationToken)
                        .ConfigureDefaults();

                    if (tipChange.Result == TipNodeSessionChangeResult.Failed)
                    {
                        if (context.Experiment.IsDiagnosticsEnabled())
                        {
                            await this.RequestDiagnosticsAsync(context, tipSession, relatedContext, cancellationToken).ConfigureDefaults();
                        }

                        throw new ProviderException(
                            $"Failed to inject JWT token on Session '{tipSession.TipSessionId}' with error '{tipChange.ErrorMessage}'.");
                    }
                    else if (tipChange.Result != TipNodeSessionChangeResult.Succeeded)
                    {
                        result = false;
                        break;
                    }
                }

                return result;
            }).ConfigureDefaults();

            TimeSpan timeout = component.Parameters.GetTimeSpanValue(StepParameters.Timeout, InstallHostAgentProvider.DefaultTimeout);
            if (!state.JwtDropConfirmed && state.IsJwtDropRequestTimedOut(timeout))
            {
                if (context.Experiment.IsDiagnosticsEnabled())
                {
                    tipSessions?.ToList().ForEach(async tipSession => await this.RequestDiagnosticsAsync(context, tipSession, relatedContext, cancellationToken).ConfigureDefaults());
                }

                throw new ProviderException(
                    $"Host JWT injection started at '{state.JwtDropRequestTime}' has not successfully finished " +
                    $"within '{timeout}'. Timing out the experiment.",
                    ErrorReason.Timeout);
            }
        }

        private async Task RequestDiagnosticsAsync(ExperimentContext context, TipSession tipSession, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            tipSession.ThrowIfNull(nameof(tipSession));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            if (!cancellationToken.IsCancellationRequested)
            {
                EventContext relatedContext = telemetryContext.Clone(withProperties: true);
                try
                {
                    await this.Logger.LogTelemetryAsync($"{nameof(InstallHostAgentProvider)}.RequestDiagnostics", relatedContext, async () =>
                    {
                        List<DiagnosticsRequest> diagnosticsRequests = new List<DiagnosticsRequest>()
                        {
                            new DiagnosticsRequest(
                            context.Experiment.Id,
                            Guid.NewGuid().ToString(),
                            DiagnosticsIssueType.TipPilotfishPackageDeploymentFailure,
                            DateTime.UtcNow.AddHours(-2),
                            DateTime.UtcNow,
                            new Dictionary<string, IConvertible>()
                            {
                                { DiagnosticsParameter.TipNodeId, tipSession.NodeId },
                                { DiagnosticsParameter.TipSessionId, tipSession.TipSessionId },
                                { DiagnosticsParameter.ExperimentId, context.Experiment.Id },
                                { DiagnosticsParameter.ProviderName, nameof(InstallHostAgentProvider) }
                            })
                        };

                        relatedContext.AddContext("requests", diagnosticsRequests);
                        await this.AddDiagnosticsRequestAsync(context, diagnosticsRequests, cancellationToken).ConfigureDefaults();
                    }).ConfigureDefaults();
                }
                catch
                {
                    // The logging framework already captures the details of any exceptions thrown by the logic within.
                    // The exception should not surface beyond this as it may create confusion around the reason for the provider's failure.            
                }
            }
        }

        internal class State
        {
            public bool AgentHeartbeatConfirmed { get; set; }

            public bool AgentInstallationConfirmed { get; set; }

            public bool AgentInstallationRequested { get; set; }

            public DateTime? AgentInstallationRequestTime { get; set; }

            public int AgentInstallationRetries { get; set; }

            public bool AgentRegistered { get; set; }

            public DateTime? JwtDropRequestTime { get; set; }

            public bool JwtDropConfirmed { get; set; }

            public bool JwtDropRequested { get; set; }

            public bool IsAgentInstallationRequestTimedOut(TimeSpan timeout)
            {
                return this.AgentInstallationRequestTime != null && (DateTime.UtcNow - this.AgentInstallationRequestTime) > timeout;
            }

            public bool IsJwtDropRequestTimedOut(TimeSpan timeout)
            {
                return this.JwtDropRequestTime != null && (DateTime.UtcNow - this.JwtDropRequestTime) > timeout;
            }

            public void ResetInstallationWorkflow()
            {
                this.AgentHeartbeatConfirmed = false;
                this.AgentInstallationConfirmed = false;
                this.AgentInstallationRequested = false;
                this.AgentInstallationRequestTime = null;
            }
        }
    }
}