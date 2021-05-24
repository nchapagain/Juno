namespace Juno.Execution.Providers.Payloads
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml;
    using System.Xml.Serialization;
    using Juno.Contracts;
    using Juno.Execution.TipIntegration;
    using Juno.Extensions.Telemetry;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using TipGateway.Entities;
    using TipGateway.FabricApi.Requests;

    /// <summary>
    /// Provider to power cycle the node to configure a payload
    /// </summary>
    [ExecutionConstraints(SupportedStepType.Payload, SupportedStepTarget.ExecuteRemotely)]
    [ProviderInfo(Name = "Power Cycle the Nodes", Description = "Power cycle the nodes/blades in the experiment group", FullDescription = "Power cycle should only be done from impactful experiments where impactType=ImpactfulAutoCleanup or impactType=ImpactfulManualCleanup. To successfully power cycle the node, Juno will first reset the node health, request power cycle and then wait for the node to report ready and InGoalState. This provider Should not be used from non-impactful experiments that do not touch firmware.")]
    public class TipNodePowerCycleProvider : ExperimentProvider
    {
        private const int DefaultMaxInstallationAttempts = 5;
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan RetryTime = TimeSpan.FromSeconds(60);
        private static Regex nodeStatusRegex = new Regex("<NodeStatus.+", RegexOptions.Compiled);

        /// <summary>
        /// Initializes a new instance of the <see cref="MicrocodeActivationProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public TipNodePowerCycleProvider(IServiceCollection services)
           : base(services)
        {
        }

        /// <inheritdoc/>
        public override Task ConfigureServicesAsync(ExperimentContext context, ExperimentComponent component)
        {
            ITipClient tipClient;
            if (!this.Services.TryGetService<ITipClient>(out tipClient))
            {
                this.Services.AddTransient<ITipClient>((provider) => new TipClient(context.Configuration));
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        protected override async Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));

            ExecutionResult result = new ExecutionResult(ExecutionStatus.InProgress);
            if (!cancellationToken.IsCancellationRequested)
            {
                TimeSpan timeout = component.Parameters.GetTimeSpanValue(StepParameters.Timeout, TipNodePowerCycleProvider.DefaultTimeout);
                TipNodePowerCycleProviderState state = await this.GetStateAsync<TipNodePowerCycleProviderState>(context, cancellationToken).ConfigureDefaults()
                    ?? new TipNodePowerCycleProviderState()
                    {
                        StepTimeout = DateTime.UtcNow.Add(timeout),
                        ResetHealthState = new TipChangeState(),
                        PowerCycleState = new TipChangeState()
                    };

                if (DateTime.UtcNow > state.StepTimeout)
                {
                    throw new ProviderException(
                       $"Timeout expired. The powercycle and verification process did not complete within the time range " +
                       $"allowed (timeout = '{state.StepTimeout}')",
                       ErrorReason.Timeout);
                }

                // we must reset health before we power cycle the node to prevent OFR
                if (!state.ResetHealthState.RequestInitiated)
                {
                    await this.RequestResetNodeHealthAsync(context, state, telemetryContext, cancellationToken).ConfigureDefaults();
                    await this.SaveStateAsync(context, state, cancellationToken).ConfigureDefaults();
                }

                // if we already requested the reset, wait for that to complete
                else if (state.ResetHealthState.RequestInitiated && !state.ResetHealthState.RequestCompleted)
                {
                    await this.WaitForResetHealthRequestCompletionAsync(state, telemetryContext, cancellationToken).ConfigureDefaults();
                    await this.SaveStateAsync(context, state, cancellationToken).ConfigureDefaults();
                }

                // request the power cycle
                else if (!state.PowerCycleState.RequestInitiated)
                {
                    await this.RequestPowerCycleAsync(context, state, telemetryContext, cancellationToken).ConfigureDefaults();
                    await this.SaveStateAsync(context, state, cancellationToken).ConfigureDefaults();
                }

                // if we've already requested the power cycle, wait for it to "finish" the request
                else if (state.PowerCycleState.RequestInitiated && !state.PowerCycleState.RequestCompleted)
                {
                    await this.WaitForPowerCycleRequestCompletionAsync(state, telemetryContext, cancellationToken).ConfigureDefaults();
                    await this.SaveStateAsync(context, state, cancellationToken).ConfigureDefaults();
                }

                // power cycle may return finish without the node coming back up, wait for node to come to healthy state
                else if (!state.PowerCycleCompleted)
                {
                    await this.WaitForHealthyNodeStatusAsync(state, telemetryContext, cancellationToken).ConfigureDefaults();
                    await this.SaveStateAsync(context, state, cancellationToken).ConfigureDefaults();
                }

                if (state.PowerCycleCompleted)
                {
                    result = new ExecutionResult(ExecutionStatus.Succeeded);
                }

            }
            else
            {
                result = new ExecutionResult(ExecutionStatus.Cancelled);
            }

            return result;
        }

        private async Task WaitForHealthyNodeStatusAsync(TipNodePowerCycleProviderState state, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                await this.Logger.LogTelemetryAsync($"{nameof(TipNodePowerCycleProvider)}.WaitForHealthyNodeStatus", telemetryContext, async () =>
                {
                    ITipClient tipClient = this.Services.GetService<ITipClient>();

                    var statusChange = await tipClient.GetNodeStatusAsync(state.PowerCycleState.TipNodeSessionId, state.PowerCycleState.TipNodeId, cancellationToken)
                        .ConfigureDefaults();

                    var result = await tipClient.GetTipSessionChangeAsync(state.PowerCycleState.TipNodeSessionId, statusChange.TipNodeSessionChangeId, cancellationToken)
                        .ConfigureDefaults();

                    var statusMessage = result.Note;
                    var nodeStatus = TipNodePowerCycleProvider.nodeStatusRegex.Match(statusMessage).Value;
                    // it is possible for the match to be empty if the request is still in progress
                    if (!string.IsNullOrEmpty(nodeStatus))
                    {
                        var nodeStatusResult = TipNodePowerCycleProvider.GetNodeStatusResult(nodeStatus);

                        if (nodeStatusResult.InGoalState == "true" && nodeStatusResult.State == "Ready")
                        {
                            state.PowerCycleCompleted = true;
                        }
                    }
                }).ConfigureDefaults();

            }
        }

        private async Task WaitForPowerCycleRequestCompletionAsync(TipNodePowerCycleProviderState state, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                await this.Logger.LogTelemetryAsync($"{nameof(TipNodePowerCycleProvider)}.WaitForPowerCycleRequestCompletion", telemetryContext, async () =>
                {
                    ITipClient tipClient = this.Services.GetService<ITipClient>();
                    var powerCycleStatus = await tipClient.GetTipSessionChangeAsync(state.PowerCycleState.TipNodeSessionId, state.PowerCycleState.TipNodeSessionChangeId, cancellationToken).ConfigureDefaults(); 

                    if (await tipClient.IsTipSessionChangeFailedAsync(state.PowerCycleState.TipNodeSessionId, state.PowerCycleState.TipNodeSessionChangeId, cancellationToken).ConfigureDefaults())
                    {
                        if (state.PowerCycleAttempt > TipNodePowerCycleProvider.DefaultMaxInstallationAttempts)
                        {
                            throw new ProviderException($"PowerCycling the node failed failed, {powerCycleStatus.ErrorMessage}", ErrorReason.DependencyFailure);
                        }

                        if (state.PowerCycleAttempt <= TipNodePowerCycleProvider.DefaultMaxInstallationAttempts && state.LastRetryTime.HasValue && state.LastRetryTime.Value.Add(TipNodePowerCycleProvider.RetryTime) < DateTime.UtcNow)
                        {
                            state.ResetPowerCycleWorkFlow();
                        }

                    }

                    if (powerCycleStatus.Status == TipNodeSessionChangeStatus.Finished)
                    {
                        state.PowerCycleState.RequestCompleted = true;
                        state.PowerCycleCompleted = true;
                    }

                }).ConfigureDefaults();
            }
        }

        private static NodeStatus GetNodeStatusResult(string nodeStatus)
        {
            using (var xmlReader = XmlReader.Create(new StringReader(nodeStatus)))
            {
                var serializer = new XmlSerializer(typeof(NodeStatus));
                return (NodeStatus)serializer.Deserialize(xmlReader);
            }
        }

        private async Task RequestPowerCycleAsync(ExperimentContext context, TipNodePowerCycleProviderState state, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                await this.Logger.LogTelemetryAsync($"{nameof(TipNodePowerCycleProvider)}.RequestPowerCycle", telemetryContext, async () =>
                {
                    IEnumerable<EnvironmentEntity> entitiesProvisioned = await this.GetEntitiesProvisionedAsync(context, cancellationToken).ConfigureDefaults();
                    IEnumerable<EnvironmentEntity> tipSessions = entitiesProvisioned?.GetEntities(EntityType.TipSession, context.ExperimentStep.ExperimentGroup);

                    if (tipSessions == null || !tipSessions.Any())
                    {
                        throw new ProviderException(
                            $"Expected environment entities not found. There are no TiP session/node entities registered in the experiment environment " +
                            $"for experiment group '{context.ExperimentStep.ExperimentGroup}' ",
                            ErrorReason.ExpectedEnvironmentEntitiesNotFound);
                    }

                    List<Task> setPowerStateTasks = new List<Task>();

                    foreach (EnvironmentEntity tipSession in tipSessions)
                    {
                        setPowerStateTasks.Add(Task.Run(async () =>
                        {
                            if (!cancellationToken.IsCancellationRequested)
                            {
                                ITipClient tipClient = this.Services.GetService<ITipClient>();
                                TipNodeSessionChange tipResult = await tipClient.SetNodePowerStateAsync(tipSession.Id, tipSession.NodeId().ToString(), PowerAction.PowerCycle, cancellationToken)
                                    .ConfigureDefaults();

                                state.PowerCycleState.TipNodeId = tipSession.NodeId();
                                state.PowerCycleState.TipNodeSessionId = tipSession.Id;
                                state.PowerCycleState.TipNodeSessionChangeId = tipResult.TipNodeSessionChangeId;
                            }
                        }));
                    }

                    state.PowerCycleAttempt++;
                    state.PowerCycleState.RequestInitiated = true;
                    state.LastRetryTime = DateTime.UtcNow;
                    await Task.WhenAll(setPowerStateTasks).ConfigureDefaults();
                }).ConfigureDefaults();
            }
        }

        private async Task WaitForResetHealthRequestCompletionAsync(TipNodePowerCycleProviderState state, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                await this.Logger.LogTelemetryAsync($"{nameof(TipNodePowerCycleProvider)}.WaitForResetHealthRequestCompletion", telemetryContext, async () =>
                {
                    ITipClient tipClient = this.Services.GetService<ITipClient>();

                    var resetStatus = await tipClient.GetTipSessionChangeAsync(state.ResetHealthState.TipNodeSessionId, state.ResetHealthState.TipNodeSessionChangeId, cancellationToken)
                        .ConfigureDefaults();

                    if (await tipClient.IsTipSessionChangeFailedAsync(state.ResetHealthState.TipNodeSessionId, state.ResetHealthState.TipNodeSessionChangeId, cancellationToken).ConfigureDefaults())
                    {
                        throw new ProviderException($"Reset Node health failed, {resetStatus.ErrorMessage}", ErrorReason.DependencyFailure);
                    }

                    if (resetStatus.Status == TipNodeSessionChangeStatus.Finished)
                    {
                        state.ResetHealthState.RequestCompleted = true;
                    }
                }).ConfigureDefaults();
            }
        }

        private async Task RequestResetNodeHealthAsync(
             ExperimentContext context,
             TipNodePowerCycleProviderState state,
             EventContext telemetryContext,
             CancellationToken cancellationToken)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                await this.Logger.LogTelemetryAsync($"{nameof(TipNodePowerCycleProvider)}.RequestResetNodeHealth", telemetryContext, async () =>
                {
                    IEnumerable<EnvironmentEntity> entitiesProvisioned = await this.GetEntitiesProvisionedAsync(context, cancellationToken).ConfigureDefaults();
                    IEnumerable<EnvironmentEntity> tipSessions = entitiesProvisioned?.GetEntities(EntityType.TipSession, context.ExperimentStep.ExperimentGroup);

                    if (tipSessions?.Any() != true)
                    {
                        throw new ProviderException(
                            $"Expected environment entities not found. There are no TiP session/node entities registered in the experiment environment " +
                            $"for experiment group '{context.ExperimentStep.ExperimentGroup}' ",
                            ErrorReason.ExpectedEnvironmentEntitiesNotFound);
                    }

                    List<Task> resetHealthTasks = new List<Task>();

                    foreach (EnvironmentEntity tipSession in tipSessions)
                    {
                        resetHealthTasks.Add(Task.Run(async () =>
                        {
                            if (!cancellationToken.IsCancellationRequested)
                            {
                                ITipClient tipClient = this.Services.GetService<ITipClient>();
                                TipNodeSessionChange tipResult = await tipClient.ResetNodeHealthAsync(tipSession.Id, tipSession.NodeId(), cancellationToken)
                                    .ConfigureDefaults();

                                state.ResetHealthState.TipNodeId = tipSession.NodeId();
                                state.ResetHealthState.TipNodeSessionId = tipSession.Id;
                                state.ResetHealthState.TipNodeSessionChangeId = tipResult.TipNodeSessionChangeId;
                            }
                        }));
                    }

                    await Task.WhenAll(resetHealthTasks).ConfigureDefaults();
                }).ConfigureDefaults();

                state.ResetHealthState.RequestInitiated = true;
            }
        }
    }
}