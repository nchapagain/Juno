namespace Juno.Execution.Providers.Environment
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Execution.ArmIntegration;
    using Juno.Execution.TipIntegration;
    using Juno.Extensions.Telemetry;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using Newtonsoft.Json;
    using TipGateway.Entities;
    using TipGateway.FabricApi.Requests;

    /// <summary>
    /// Provider forces the node state for existing TiP session nodes to control
    /// fabric auto-healing behaviors.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.EnvironmentSetup, SupportedStepTarget.ExecuteRemotely)]
    [SupportedParameter(Name = StepParameters.NodeState, Type = typeof(NodeState), Required = true)]
    [ProviderInfo(Name = "Create TiP sessions", Description = "Create TiP sessions for the all experiment groups to isolate physical nodes/blades in the Azure fleet", FullDescription = "Step to create TiP sessions associated with an experiment. Once a set of clusters that have physical nodes whose characteristics match the requirements of the experiment has been identified, is to estable TiP sessions to the nodes so that an experiment can be run. The node to which the change will be applied is called a `treatment group`.")]
    public partial class TipStateProvider : ExperimentProvider
    {
        // The timeout for the entirety of the TiP node state change process.
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromHours(1);

        /// <summary>
        /// Initializes a new instance of the <see cref="TipStateProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public TipStateProvider(IServiceCollection services)
            : base(services)
        {
        }

        /// <summary>
        /// An entity manager used to manage the state of TiP node entities for
        /// the experiment.
        /// </summary>
        protected EntityManager EntityManager { get; set; }

        /// <summary>
        /// A client used to interact with the TiP API service.
        /// </summary>
        protected ITipClient TipClient { get; set; }

        /// <inheritdoc />
        public override Task ConfigureServicesAsync(ExperimentContext context, ExperimentComponent component)
        {
            context.ThrowIfNull(nameof(context));

            if (!this.Services.TryGetService<ITipClient>(out ITipClient tipClient))
            {
                this.Services.AddSingleton<ITipClient>(new TipClient(context.Configuration));
            }

            if (!this.Services.TryGetService<EntityManager>(out EntityManager entityManager))
            {
                this.Services.AddSingleton(new EntityManager(this.Services.GetService<IProviderDataClient>()));
            }

            return base.ConfigureServicesAsync(context, component);
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
                this.EntityManager = this.Services.GetService<EntityManager>();
                this.TipClient = this.Services.GetService<ITipClient>();

                TimeSpan timeout = component.Parameters.GetTimeSpanValue(StepParameters.Timeout, TipStateProvider.DefaultTimeout);
                NodeState nodeState = component.Parameters.GetEnumValue<NodeState>(StepParameters.NodeState);

                State state = await this.GetStateAsync<State>(context, cancellationToken).ConfigureDefaults()
                    ?? new State
                    {
                        StepTimeout = DateTime.UtcNow.Add(timeout)
                    };

                await this.EntityManager.LoadEntitiesProvisionedAsync(context.Experiment.Id, cancellationToken).ConfigureDefaults();

                try
                {
                    if (!state.Requests.Any())
                    {
                        await this.RequestStateChangeAsync(context, nodeState, state, telemetryContext, cancellationToken)
                            .ConfigureDefaults();
                    }
                    else
                    {
                        await this.ConfirmStateChangeAsync(nodeState, state, telemetryContext, cancellationToken)
                            .ConfigureDefaults();

                        if (state.Requests.All(request => request.IsCompleted))
                        {
                            result = new ExecutionResult(ExecutionStatus.Succeeded);
                        }
                    }

                    TipStateProvider.ThrowOnTimeout(state, timeout);
                }
                finally
                {
                    await this.SaveStateAsync(context, state, cancellationToken).ConfigureDefaults();
                    await this.EntityManager.SaveEntitiesProvisionedAsync(context.ExperimentId, cancellationToken).ConfigureDefaults();
                }
            }

            return result;
        }

        private static IEnumerable<string> GetTargetEnvironmentGroups(ExperimentContext context)
        {
            List<string> experimentGroups = new List<string>();
            if (context.ExperimentStep.ExperimentGroup == ExperimentComponent.AllGroups)
            {
                experimentGroups.AddRange(context.GetExperimentGroups());
            }
            else
            {
                experimentGroups.Add(context.ExperimentStep.ExperimentGroup);
            }

            return experimentGroups;
        }

        private static void ThrowOnTimeout(State state, TimeSpan timeout)
        {
            if (state.IsTimeoutExpired)
            {
                throw new TimeoutException(
                    $"TiP state change attempt timed out. The time allowed in which to set the TiP session node state expired (timeout = '{timeout}')");
            }
        }

        private async Task ConfirmStateChangeAsync(NodeState expectedState, State state, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            if (state.Requests?.Any() == true)
            {
                foreach (TipChangeRequest changeRequest in state.Requests)
                {
                    if (!changeRequest.IsCompleted && !cancellationToken.IsCancellationRequested)
                    {
                        EventContext relatedContext = telemetryContext.Clone()
                            .AddContext("tipSessionId", changeRequest.TipSessionId)
                            .AddContext("tipSessionNodeId", changeRequest.TipNodeId)
                            .AddContext("tipSessionChangeId", changeRequest.TipRequestChangeId);

                        await this.Logger.LogTelemetryAsync($"{this.TelemetryEventNamePrefix}.ConfirmStateChange", relatedContext, async () =>
                        {
                            bool isFailed = await this.TipClient.IsTipSessionChangeFailedAsync(
                                changeRequest.TipSessionId,
                                changeRequest.TipRequestChangeId,
                                CancellationToken.None).ConfigureDefaults();

                            TipNodeSessionChangeDetails response = await this.TipClient.GetTipSessionChangeAsync(
                                changeRequest.TipSessionId,
                                changeRequest.TipRequestChangeId,
                                CancellationToken.None).ConfigureDefaults();

                            relatedContext.AddContext("tipResponse", response);

                            if (isFailed)
                            {
                                throw new ProviderException(
                                    $"The state change request failed for TiP session '{changeRequest.TipSessionId}' and node '{changeRequest.TipNodeId}'. {response.ErrorMessage}",
                                    ErrorReason.TipRequestFailure);
                            }

                            EnvironmentEntity tipSession = this.EntityManager.EntitiesProvisioned.GetTipSessions()
                                ?.FirstOrDefault(tip => string.Equals(tip.TipSessionId(), changeRequest.TipSessionId, StringComparison.OrdinalIgnoreCase));

                            if (tipSession == null)
                            {
                                    // This should NEVER happen, but we want to ensure we catch it if it ever does.
                                    throw new ProviderException(
                                        $"Unexpected scenario. The TiP session entity related to the node state change request '{changeRequest.TipSessionId}' does not exist in the entities provisioned.",
                                        ErrorReason.ExpectedEnvironmentEntitiesNotFound);
                            }

                            tipSession.NodeState(expectedState.ToString());

                            if (response.Status == TipNodeSessionChangeStatus.Finished)
                            {
                                changeRequest.IsCompleted = true;
                            }

                        }).ConfigureDefaults();
                    }
                }
            }
        }

        private async Task RequestStateChangeAsync(ExperimentContext context, NodeState nodeState, State state, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            IEnumerable<string> experimentGroups = TipStateProvider.GetTargetEnvironmentGroups(context);

            foreach (string group in experimentGroups)
            {
                IEnumerable<EnvironmentEntity> tipSessions = this.EntityManager.EntitiesProvisioned.GetTipSessions(group);
                if (tipSessions?.Any() == true)
                {
                    foreach (EnvironmentEntity tipSession in tipSessions)
                    {
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            EventContext relatedContext = telemetryContext.Clone()
                            .AddContext("node", tipSession)
                            .AddContext("tipSessionId", tipSession.TipSessionId())
                            .AddContext("tipSessionRegion", tipSession.Region())
                            .AddContext("tipSessionCluster", tipSession.ClusterName())
                            .AddContext("tipSessionRack", tipSession.RackLocation())
                            .AddContext("tipSessionNodeId", tipSession.NodeId())
                            .AddContext("tipNodeState", nodeState.ToString())
                            .AddContext("experimentGroup", tipSession.EnvironmentGroup);

                            await this.Logger.LogTelemetryAsync($"{this.TelemetryEventNamePrefix}.RequestStateChange", relatedContext, async () =>
                            {
                                TipNodeSessionChange response = await this.TipClient.SetNodeStateAsync(tipSession.TipSessionId(), tipSession.NodeId(), nodeState, CancellationToken.None)
                                    .ConfigureDefaults();

                                state.Requests.Add(new TipChangeRequest
                                {
                                    TipSessionId = tipSession.TipSessionId(),
                                    TipNodeId = tipSession.NodeId(),
                                    TipRequestChangeId = response.TipNodeSessionChangeId,
                                    EnvironmentGroup = group
                                });

                                relatedContext.AddContext("tipSessionChangeId", response.TipNodeSessionChangeId);
                                relatedContext.AddContext("tipResponse", response);

                            }).ConfigureDefaults();
                        }
                    }
                }
            }
        }

        internal class State
        {
            [JsonIgnore]
            public bool IsTimeoutExpired => DateTime.UtcNow > this.StepTimeout;

            public List<TipChangeRequest> Requests { get; } = new List<TipChangeRequest>();

            public DateTime StepTimeout { get; set; }
        }

        internal class TipChangeRequest
        {
            public bool IsCompleted { get; set; }

            public string EnvironmentGroup { get; set; }

            public string TipSessionId { get; set; }

            public string TipNodeId { get; set; }

            public string TipRequestChangeId { get; set; }
        }
    }
}