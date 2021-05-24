namespace Juno.Execution.Providers.Environment
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.Execution.Providers.AutoTriage;
    using Juno.Execution.TipIntegration;
    using Juno.Extensions.Telemetry;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using TipGateway.Entities;

    /// <summary>
    /// Provider that apply PF service via TIP.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.EnvironmentSetup, SupportedStepTarget.ExecuteRemotely)]
    [SupportedParameter(Name = StepParameters.PilotfishServiceName, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = StepParameters.PilotfishServicePath, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = StepParameters.IsSocService, Type = typeof(bool), Required = false)]
    [ProviderInfo(Name = "Install CRC watchdog agent", Description = "Installs the CRC watchdog agent on physical nodes/blades in the specified experiment group", FullDescription = "Step to apply a pilotfish service on physical nodes/blades in the environment as part of Juno experiment. The following 'parameters' will be used creating experiment step: pilotfishServiceName, pilotfishServicePath, timeout.")]
    public partial class ApplyPilotFishProvider : ExperimentProvider
    {
        private const int DefaultMaxInstallationAttempts = 5;
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan RetryTime = TimeSpan.FromSeconds(180);
        private ITipClient tipClient;
        private TipSettings tipSettings;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApplyPilotFishProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public ApplyPilotFishProvider(IServiceCollection services)
            : base(services)
        {
        }

        /// <inheritdoc/>
        protected override async Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            ExecutionResult result = new ExecutionResult(ExecutionStatus.InProgress);
            EnvironmentSettings settings = EnvironmentSettings.Initialize(context.Configuration);
            this.tipSettings = settings.TipSettings;

            if (!this.Services.TryGetService<ITipClient>(out ITipClient tipClient))
            {
                tipClient = new TipClient(context.Configuration);
            }

            this.tipClient = tipClient;

            ApplyPilotFishState state = await this.GetStateAsync<ApplyPilotFishState>(context, cancellationToken).ConfigureDefaults()
                ?? new ApplyPilotFishState();

            if (!cancellationToken.IsCancellationRequested)
            {
                if (!state.InstallationRequested)
                {
                    IList<TipSession> tipSessions = await this.GetTipSessionsAsync(context, component, cancellationToken).ConfigureDefaults();
                    await this.RequestInstallationAsync(context, component, tipSessions, state, telemetryContext, cancellationToken).ConfigureDefaults();
                    await this.SaveStateAsync<ApplyPilotFishState>(context, state, cancellationToken).ConfigureDefaults();
                }

                if (!state.InstallationConfirmed)
                {
                    await this.VerifyInstallationAsync(context, component, state, telemetryContext, cancellationToken).ConfigureDefaults();
                    await this.SaveStateAsync<ApplyPilotFishState>(context, state, cancellationToken).ConfigureDefaults();
                }

                if (state.InstallationRequested && state.InstallationConfirmed)
                {
                    result = new ExecutionResult(ExecutionStatus.Succeeded);
                }
            }

            return result;
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

        private async Task RequestInstallationAsync(ExperimentContext context, ExperimentComponent component, IEnumerable<TipSession> tipSessions, ApplyPilotFishState state, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            List<KeyValuePair<string, string>> autopilotServices = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>(
                    component.Parameters.GetValue<string>(StepParameters.PilotfishServiceName),
                    component.Parameters.GetValue<string>(StepParameters.PilotfishServicePath))
            };

            bool isSessionOnSoc = component.Parameters.GetValue<bool>(StepParameters.IsSocService, false);

            foreach (TipSession session in tipSessions)
            {
                EventContext relatedContext = telemetryContext.Clone();
                await this.Logger.LogTelemetryAsync($"{nameof(ApplyPilotFishProvider)}.RequestInstallation", telemetryContext, async () =>
                {
                    TipNodeSessionChange tipSessionChangeResponse = isSessionOnSoc
                        ? await this.tipClient.ApplyPilotFishServicesOnSocAsync(session.TipSessionId, autopilotServices, cancellationToken).ConfigureDefaults()
                        : await this.tipClient.ApplyPilotFishServicesAsync(session.TipSessionId, autopilotServices, cancellationToken).ConfigureDefaults();

                    session.ChangeIdList.Add(tipSessionChangeResponse.TipNodeSessionChangeId);
                    relatedContext.AddContext(nameof(tipSessionChangeResponse), tipSessionChangeResponse);
                }).ConfigureDefaults();
            }

            await this.SaveTipSessionsAsync(tipSessions, context, component, cancellationToken).ConfigureDefaults();
            state.InstallationRequested = true;
            state.InstallationRequestTime = DateTime.UtcNow;
        }

        private Task SaveTipSessionsAsync(IEnumerable<TipSession> tipSessions, ExperimentContext context, ExperimentComponent component, CancellationToken cancellationToken)
        {
            IEnumerable<EnvironmentEntity> tipEntities = TipSession.ToEnvironmentEntities(tipSessions.ToList(), component.Group);

            return this.UpdateEntitiesProvisionedAsync(context, tipEntities, cancellationToken);
        }

        [SuppressMessage("Naming", "AZCA1004:AsyncResultProhibited Rule", Justification = "The analyzer mistakenly thought 'tipChange.Result' is task.Result but it's not.")]
        private async Task VerifyInstallationAsync(ExperimentContext context, ExperimentComponent component, ApplyPilotFishState state, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            EventContext relatedContext = telemetryContext.Clone(withProperties: true);
            IList<TipSession> tipSessions = await this.GetTipSessionsAsync(context, component, cancellationToken).ConfigureDefaults();

            state.InstallationConfirmed = await this.Logger.LogTelemetryAsync($"{nameof(ApplyPilotFishProvider)}.VerifyInstallation", relatedContext, async () =>
            {
                bool result = true;
                foreach (TipSession tipSession in tipSessions)
                {
                    TipNodeSessionChangeDetails tipSessionChangeResponse = await this.tipClient.GetTipSessionChangeAsync(tipSession.TipSessionId, tipSession.ChangeIdList.LastOrDefault(), cancellationToken)
                        .ConfigureDefaults();

                    relatedContext.AddContext(nameof(tipSessionChangeResponse), tipSessionChangeResponse);

                    if (tipSessionChangeResponse.Result == TipNodeSessionChangeResult.Failed &&
                        tipSessionChangeResponse.IsRetryableScenario() &&
                        state.InstallationRetries++ <= ApplyPilotFishProvider.DefaultMaxInstallationAttempts)
                    {
                        // do not retry if it's less than 2.5 min ago. TiP caches the errors for 2.5 min.
                        if (state.InstallationRequestTime.HasValue && state.InstallationRequestTime.Value.Add(ApplyPilotFishProvider.RetryTime) > DateTime.UtcNow)
                        {
                            // do nothing, wait for next provider attempt
                            result = false;
                            break;
                        }

                        // There are certain scenarios where the TiP Service has failed to deploy the agent PF service for which
                        // we want to retry on. The TiP team confirms the acceptability of some amount of these failures; however,
                        // the Juno team does not consider them acceptable. Hence we are going to retry with the hope of surviving
                        // these failures and avoiding a failed experiment.
                        state.ResetInstallationWorkflow();
                        relatedContext.AddContext("installationRetries", state.InstallationRetries);

                        await this.Logger.LogTelemetryAsync(
                            $"{nameof(ApplyPilotFishProvider)}.RetryPilotFishDeployment",
                            LogLevel.Warning,
                            relatedContext).ConfigureDefaults();

                        result = false;
                    }
                    else if (tipSessionChangeResponse.Result == TipNodeSessionChangeResult.Failed)
                    {
                        if (context.Experiment.IsDiagnosticsEnabled())
                        {
                            await this.RequestDiagnosticsAsync(context, tipSession, relatedContext, cancellationToken).ConfigureDefaults();
                        }

                        // This will include both non-tryable error and retries exceeds limit.
                        throw new ProviderException(
                            $"Failed to install PilotFish service '{component.Parameters.GetValue<string>(StepParameters.PilotfishServiceName)}' on node for TiP session ID " +
                            $"'{tipSession.TipSessionId}' with error '{tipSessionChangeResponse.ErrorMessage}'.");
                    }
                    else if (tipSessionChangeResponse.Result != TipNodeSessionChangeResult.Succeeded)
                    {
                        result = false;
                        break;
                    }
                }

                return result;
            }).ConfigureDefaults();

            TimeSpan pilotFishInstallTimeout = component.Parameters.GetTimeSpanValue(StepParameters.Timeout, ApplyPilotFishProvider.DefaultTimeout);
            if (!state.InstallationConfirmed && state.IsServiceInstallationRequestTimedOut(pilotFishInstallTimeout))
            {
                if (context.Experiment.IsDiagnosticsEnabled())
                {
                    tipSessions?.ToList().ForEach(async tipSession => await this.RequestDiagnosticsAsync(context, tipSession, relatedContext, cancellationToken).ConfigureDefaults());
                }

                throw new ProviderException(
                    $"Pilotfish installation started at '{state.InstallationRequestTime}' has not successfully reported success " +
                    $"within the allowable time (timeout='{pilotFishInstallTimeout}').",
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
                    await this.Logger.LogTelemetryAsync($"{nameof(ApplyPilotFishProvider)}.RequestDiagnostics", relatedContext, async () =>
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
                                     { DiagnosticsParameter.ProviderName, nameof(ApplyPilotFishProvider) }
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

        internal class ApplyPilotFishState
        {
            public bool InstallationRequested { get; set; }

            public bool InstallationConfirmed { get; set; }

            public int InstallationRetries { get; set; }

            public DateTime? InstallationRequestTime { get; set; }

            public bool IsServiceInstallationRequestTimedOut(TimeSpan timeout)
            {
                return this.InstallationRequestTime != null
                    && (DateTime.UtcNow - this.InstallationRequestTime) > timeout;
            }

            public void ResetInstallationWorkflow()
            {
                this.InstallationConfirmed = false;
                this.InstallationRequested = false;
                this.InstallationRequestTime = null;
            }
        }
    }
}