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
    using Juno.Execution.ArmIntegration;
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
    /// Provider that deploy hosting environment via TIP.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.EnvironmentSetup, SupportedStepTarget.ExecuteRemotely)]
    [SupportedParameter(Name = Parameters.ComponentType, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.ComponentLocation, Type = typeof(string), Required = true)]
    [ProviderInfo(Name = "Update Host Operating System", Description = "Updates the operating system on the physical nodes/blades in the experiment group", FullDescription = "Step to update the operating system on the physical nodes/blades in the experiment group.")]
    public partial class DeployHostingEnvironmentProvider : ExperimentProvider
    {
        private const int DefaultMaxInstallationAttempts = 3;
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(30);
        private ITipClient tipClient;
        private TipSettings tipSettings;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeployHostingEnvironmentProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public DeployHostingEnvironmentProvider(IServiceCollection services)
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
            DeployHostingEnvironmentState state = null;

            if (!cancellationToken.IsCancellationRequested)
            {
                EnvironmentSettings settings = EnvironmentSettings.Initialize(context.Configuration);
                this.tipSettings = settings.TipSettings;

                if (!this.Services.TryGetService<ITipClient>(out ITipClient tipClient))
                {
                    tipClient = new TipClient(context.Configuration);
                }

                this.tipClient = tipClient;

                state = await this.GetStateAsync<DeployHostingEnvironmentState>(context, cancellationToken).ConfigureDefaults()
                    ?? new DeployHostingEnvironmentState();

                if (!cancellationToken.IsCancellationRequested)
                {
                    if (!state.InstallationRequested)
                    {
                        IList<TipSession> tipSessions = await this.GetTipSessionsAsync(context, component, cancellationToken).ConfigureDefaults();
                        await this.RequestInstallationAsync(context, component, tipSessions, state, telemetryContext, cancellationToken).ConfigureDefaults();
                        await this.SaveStateAsync<DeployHostingEnvironmentState>(context, state, cancellationToken).ConfigureDefaults();
                    }
                    else if (!state.InstallationConfirmed)
                    {
                        await this.VerifyInstallationAsync(context, component, state, telemetryContext, cancellationToken).ConfigureDefaults();
                        await this.SaveStateAsync<DeployHostingEnvironmentState>(context, state, cancellationToken).ConfigureDefaults();
                    }

                    if (state.InstallationRequested && state.InstallationConfirmed)
                    {
                        result = new ExecutionResult(ExecutionStatus.Succeeded);
                    }
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

        private async Task RequestInstallationAsync(ExperimentContext context, ExperimentComponent component, IEnumerable<TipSession> tipSessions, DeployHostingEnvironmentState state, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            List<HostingEnvironmentLineItem> heComponent = new List<HostingEnvironmentLineItem>
            {
                new HostingEnvironmentLineItem()
                {
                    Component = component.Parameters.GetEnumValue<HostingEnvironmentComponent>(Parameters.ComponentType),
                    Location = component.Parameters.GetValue<string>(Parameters.ComponentLocation)
                }
            };

            foreach (TipSession session in tipSessions)
            {
                await this.Logger.LogTelemetryAsync($"{nameof(DeployHostingEnvironmentProvider)}.RequestInstallation", telemetryContext, async () =>
                {
                    TipNodeSessionChange updatedSession = await this.tipClient.DeployHostingEnvironmentAsync(session.TipSessionId, heComponent, cancellationToken)
                        .ConfigureDefaults();

                    session.ChangeIdList.Add(updatedSession.TipNodeSessionChangeId);
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
        private async Task VerifyInstallationAsync(ExperimentContext context, ExperimentComponent component, DeployHostingEnvironmentState state, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            EventContext relatedContext = telemetryContext.Clone(withProperties: true);
            IList<TipSession> tipSessions = await this.GetTipSessionsAsync(context, component, cancellationToken).ConfigureDefaults();

            state.InstallationConfirmed = await this.Logger.LogTelemetryAsync($"{nameof(DeployHostingEnvironmentProvider)}.VerifyInstallation", relatedContext, async () =>
            {
                bool result = true;
                foreach (TipSession tipSession in tipSessions)
                {
                    TipNodeSessionChangeDetails tipChange = await this.tipClient.GetTipSessionChangeAsync(tipSession.TipSessionId, tipSession.ChangeIdList.LastOrDefault(), cancellationToken)
                        .ConfigureDefaults();

                    if (tipChange.Result == TipNodeSessionChangeResult.Failed &&
                        tipChange.IsRetryableScenario() &&
                        state.InstallationRetries++ <= DeployHostingEnvironmentProvider.DefaultMaxInstallationAttempts)
                    {
                        // There are certain scenarios where the TiP Service has failed to deploy the agent PF service for which
                        // we want to retry on. The TiP team confirms the acceptability of some amount of these failures; however,
                        // the Juno team does not consider them acceptable. Hence we are going to retry with the hope of surviving 
                        // these failures and avoiding a failed experiment.
                        state.ResetInstallationWorkflow();
                        telemetryContext.AddContext("installationRetries", state.InstallationRetries);

                        await this.Logger.LogTelemetryAsync(
                            $"{nameof(DeployHostingEnvironmentProvider)}.RetryDeployHostingEnvironment",
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
                            $"Failed to deploy HostingEnvironment '{component.Parameters.GetEnumValue<HostingEnvironmentComponent>(Parameters.ComponentType).ToString()}' " +
                            $"at '{component.Parameters.GetValue<string>(Parameters.ComponentLocation)}' on Session '{tipSession.TipSessionId}' with error '{tipChange.ErrorMessage}'.");
                    }
                    else if (tipChange.Result != TipNodeSessionChangeResult.Succeeded)
                    {
                        result = false;
                        break;
                    }
                }

                return result;
            }).ConfigureDefaults();

            TimeSpan heDeployTimeout = component.Parameters.GetTimeSpanValue(StepParameters.Timeout, DeployHostingEnvironmentProvider.DefaultTimeout);
            if (!state.InstallationConfirmed && state.IsInstallationRequestTimedOut(heDeployTimeout))
            {
                throw new ProviderException(
                    $"HostingEnvironment deployment started '{state.InstallationRequestTime}' has not successfully finished " +
                    $"within '{heDeployTimeout}'. Timing out the experiment.",
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
                    await this.Logger.LogTelemetryAsync($"{nameof(DeployHostingEnvironmentProvider)}.RequestDiagnostics", relatedContext, async () =>
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
                                    { DiagnosticsParameter.ProviderName, nameof(DeployHostingEnvironmentProvider) }
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

        internal class DeployHostingEnvironmentState
        {
            public bool InstallationRequested { get; set; }

            public bool InstallationConfirmed { get; set; }

            public int InstallationRetries { get; set; }

            public DateTime? InstallationRequestTime { get; set; }

            public bool IsInstallationRequestTimedOut(TimeSpan timeout)
            {
                return (DateTime.UtcNow - this.InstallationRequestTime) > timeout;
            }

            public void ResetInstallationWorkflow()
            {
                this.InstallationConfirmed = false;
                this.InstallationRequested = false;
                this.InstallationRequestTime = null;
            }
        }

        private static class Parameters
        {
            internal const string ComponentType = "componentType";
            internal const string ComponentLocation = "componentLocation";
        }
    }
}
