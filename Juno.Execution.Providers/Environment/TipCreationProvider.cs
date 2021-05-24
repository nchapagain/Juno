namespace Juno.Execution.Providers.Environment
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.Execution.ArmIntegration;
    using Juno.Execution.TipIntegration;
    using Juno.Extensions.Telemetry;
    using Juno.Hosting.Common;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using TipGateway.Entities;

    /// <summary>
    /// Provider that creates Tip session across groups.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.EnvironmentSetup, SupportedStepTarget.ExecuteRemotely)]
    [SupportedParameter(Name = StepParameters.IsAmberNodeRequest, Type = typeof(bool), Required = false)]
    [ProviderInfo(Name = "Create TiP sessions", Description = "Create TiP sessions for the all experiment groups to isolate physical nodes/blades in the Azure fleet", FullDescription = "Step to create TiP sessions associated with an experiment. Once a set of clusters that have physical nodes whose characteristics match the requirements of the experiment has been identified, is to estable TiP sessions to the nodes so that an experiment can be run. The node to which the change will be applied is called a `treatment group`.")]
    public partial class TipCreationProvider : ExperimentProvider
    {
        // The timeout for the entirety of the TiP creation process.
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromHours(2);

        private ITipClient tipClient;
        private TipSettings tipSettings;
        private IEnvironmentClient environmentClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="TipCreationProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public TipCreationProvider(IServiceCollection services)
            : base(services)
        {
        }

        /// <summary>
        /// Returns rack entities that have available nodes remaining that have not been attempted.
        /// </summary>
        /// <param name="rackEntities">The set of all rack entities.</param>
        /// <param name="nodesAttempted">The set of all nodes/node IDs that have already been attempted.</param>
        /// <param name="minimumAvailableNodes">The minimum number of nodes that must be available on a rack for it to be selected.</param>
        /// <returns>
        /// The set of rack entities containing available nodes.
        /// </returns>
        protected static IEnumerable<EnvironmentEntity> GetRacksWithAvailableNodes(IEnumerable<EnvironmentEntity> rackEntities, IEnumerable<string> nodesAttempted, int minimumAvailableNodes)
        {
            rackEntities.ThrowIfNull(nameof(rackEntities));
            nodesAttempted.ThrowIfNull(nameof(nodesAttempted));

            List<EnvironmentEntity> racksWithAvailableNodes = new List<EnvironmentEntity>();
            foreach (EnvironmentEntity entity in rackEntities)
            {
                IEnumerable<string> remainingNodes = TipCreationProvider.GetAvailableNodes(entity, nodesAttempted);
                if (remainingNodes?.Any() == true && remainingNodes.Count() >= minimumAvailableNodes)
                {
                    racksWithAvailableNodes.Add(entity);
                }
            }

            return racksWithAvailableNodes;
        }

        /// <summary>
        /// Returns the set of nodes from the rack entity that are still available.
        /// </summary>
        /// <param name="rackEntity">The rack entity.</param>
        /// <param name="nodesAttempted">The set of all nodes/node IDs that have already been attempted.</param>
        /// <returns>
        /// The set of available nodes remaining on the rack.
        /// </returns>
        protected static IEnumerable<string> GetAvailableNodes(EnvironmentEntity rackEntity, IEnumerable<string> nodesAttempted)
        {
            nodesAttempted.ThrowIfNull(nameof(nodesAttempted));

            List<string> availableNodes = new List<string>();

            if (rackEntity != null)
            {
                char[] delimiters = new char[] { ';', ',' };
                List<string> originalNodeList = rackEntity.Metadata[nameof(TipRack.NodeList)]?.ToString().Split(delimiters, StringSplitOptions.RemoveEmptyEntries).ToList();
                if (originalNodeList?.Any() == true)
                {
                    if (nodesAttempted?.Any() == true)
                    {
                        IEnumerable<string> remainingNodes = originalNodeList.Select(node => node.ToLowerInvariant()).Except(nodesAttempted.Select(node => node.ToLowerInvariant()));
                        if (remainingNodes?.Any() == true)
                        {
                            availableNodes.AddRange(remainingNodes);
                        }
                    }
                    else
                    {
                        availableNodes.AddRange(originalNodeList);
                    }
                }
            }

            return availableNodes;
        }

        /// <inheritdoc />
        public override Task ConfigureServicesAsync(ExperimentContext context, ExperimentComponent component)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));

            if (!this.Services.TryGetService<ITipClient>(out ITipClient tipClient))
            {
                this.Services.AddSingleton<ITipClient>(new TipClient(context.Configuration));
            }

            if (!this.Services.TryGetService<IEnvironmentClient>(out IEnvironmentClient environmentClient))
            {
                EnvironmentSettings settings = EnvironmentSettings.Initialize(context.Configuration);
                environmentClient = HostDependencies.CreateEnvironmentApiClient(
                    settings.ExecutionSettings.AadPrincipals.Get(Setting.ExecutionSvc),
                    settings.ExecutionSettings.AadPrincipals.Get(Setting.EnvironmentsApi), 
                    settings.ExecutionSettings.EnvironmentApiUri);

                this.Services.AddSingleton<IEnvironmentClient>(environmentClient);
            }

            return Task.CompletedTask;
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
                State state = await this.GetStateAsync<State>(context, cancellationToken).ConfigureDefaults()
                    ?? new State()
                    {
                        NodesAttempted = new List<string>()
                    };

                if (state.NodesAttempted == null)
                {
                    // For backwards compatibility with experiments already running.
                    state.NodesAttempted = new List<string>();
                }

                EnvironmentSettings settings = EnvironmentSettings.Initialize(context.Configuration);
                this.tipSettings = settings.TipSettings;
                this.tipClient = this.Services.GetService<ITipClient>();
                this.environmentClient = this.Services.GetService<IEnvironmentClient>();

                if (!cancellationToken.IsCancellationRequested)
                {
                    IEnumerable<EnvironmentEntity> provisionedEntities = await this.GetEntitiesProvisionedAsync(context, cancellationToken)
                        .ConfigureDefaults();

                    IList<EnvironmentEntity> tipSessionRequestsInProgress = provisionedEntities?.GetEntities(EntityType.TipSession).ToList();

                    try
                    {
                        if (state.TargetRack == null || tipSessionRequestsInProgress?.Any() != true)
                        {
                            // If there are no TiP session creation requests in progress, select a rack from the whole pool and
                            // start the requests.
                            List<EnvironmentEntity> tipSessionsRequests = new List<EnvironmentEntity>();
                            IEnumerable<string> experimentGroups = context.GetExperimentGroups();
                            EnvironmentEntity candidateRack = await this.SelectCandidateRackAsync(context, state, experimentGroups.Count(), cancellationToken)
                                .ConfigureDefaults();

                            state.TargetRack = candidateRack;

                            string preferredVmSku = candidateRack.SupportedVmSkus().Shuffle().First();
                            state.TargetRack.PreferredVmSku(preferredVmSku);

                            IEnumerable<string> availableNodes = TipCreationProvider.GetAvailableNodes(candidateRack, state.NodesAttempted);

                            for (int i = 0; i < experimentGroups.Count(); i++)
                            {
                                string experimentGroup = experimentGroups.ElementAt(i);
                                string targetNodeId = availableNodes.ElementAt(i);
                                tipSessionsRequests.Add(await this.CreateTipSessionAsync(candidateRack, component, experimentGroup, targetNodeId, preferredVmSku, state, telemetryContext, cancellationToken)
                                    .ConfigureDefaults());
                            }

                            state.Timeout = component.Parameters.GetTimeSpanValue(StepParameters.Timeout, TipCreationProvider.DefaultTimeout);
                            state.StepTimeout = DateTime.UtcNow.Add(state.Timeout);

                            await this.UpdateEntitiesProvisionedAsync(context, tipSessionsRequests, cancellationToken).ConfigureDefaults();
                        }
                        else if (state.TargetRack != null)
                        {
                            List<TipSessionStatus> statuses = new List<TipSessionStatus>();
                            List<EnvironmentEntity> tipSessionEntitiesToUpdate = new List<EnvironmentEntity>();
                            List<EnvironmentEntity> tipSessionEntitiesToRemove = new List<EnvironmentEntity>();
                            List<EnvironmentEntity> nodeEntitiesToRemove = new List<EnvironmentEntity>();

                            foreach (EnvironmentEntity tipSessionEntity in tipSessionRequestsInProgress)
                            {
                                // If there are in progress tip request, check their progress first.
                                TipSessionStatus status = await this.GetTipSessionStatusAsync(tipSessionEntity, telemetryContext, cancellationToken).ConfigureDefaults();
                                statuses.Add(status);

                                if (status == TipSessionStatus.Created)
                                {
                                    tipSessionEntitiesToUpdate.Add(tipSessionEntity);

                                    AgentIdentification nodeAgentId = new AgentIdentification(
                                        tipSessionEntity.ClusterName(),
                                        tipSessionEntity.NodeId(),
                                        null,
                                        tipSessionEntity.TipSessionId());

                                    EnvironmentEntity nodeEntity = EnvironmentEntity.Node(nodeAgentId.ToString(), tipSessionEntity.EnvironmentGroup, tipSessionEntity.Metadata);
                                    nodeEntity.AgentId(nodeAgentId.ToString());

                                    tipSessionEntitiesToUpdate.Add(nodeEntity);
                                }
                                else if (status == TipSessionStatus.Failed)
                                {
                                    // Remove the TiP session entity from the set of entities provisioned.
                                    statuses.Add(TipSessionStatus.Failed);

                                    IEnumerable<string> remainingNodes = TipCreationProvider.GetAvailableNodes(state.TargetRack, state.NodesAttempted);
                                    if (remainingNodes.Any())
                                    {
                                        string targetNodeId = remainingNodes.First();
                                        string preferredVmSku = state.TargetRack.PreferredVmSku();
                                        if (string.IsNullOrWhiteSpace(preferredVmSku))
                                        {
                                            // We have to account for experiments that are already in motion.
                                            preferredVmSku = state.TargetRack.SupportedVmSkus().Shuffle().First();
                                            state.TargetRack.PreferredVmSku(preferredVmSku);
                                        }

                                        // So long as there are nodes remaining on the rack, try to stay on the rack.
                                        EnvironmentEntity newTipSessionEntity = await this.CreateTipSessionAsync(
                                            state.TargetRack,
                                            component,
                                            tipSessionEntity.EnvironmentGroup,
                                            targetNodeId,
                                            state.TargetRack.PreferredVmSku(),
                                            state,
                                            telemetryContext,
                                            cancellationToken).ConfigureDefaults();
                                        EnvironmentEntity nodeEntity = EnvironmentEntity.Node(tipSessionEntity.NodeId(), tipSessionEntity.EnvironmentGroup, tipSessionEntity.Metadata);
                                        nodeEntitiesToRemove.Add(nodeEntity);
                                        tipSessionEntitiesToRemove.Add(tipSessionEntity);
                                        tipSessionEntitiesToUpdate.Add(newTipSessionEntity);
                                    }
                                    else
                                    {
                                        // We can no longer meet the requirement of having nodes on the same rack. At this point, we have to select another
                                        // rack and start over. All previously acquired TiP sessions will be deleted.
                                        tipSessionEntitiesToRemove.AddRange(tipSessionRequestsInProgress);
                                        IEnumerable<EnvironmentEntity> tipSessionSessionsToDelete = tipSessionRequestsInProgress
                                            .Where(session => TipSession.FromEnvironmentEntity(session).Status != TipSessionStatus.Failed);

                                        tipSessionSessionsToDelete?.ToList().ForEach(async (tipSession) => 
                                        {
                                            EnvironmentEntity nodeEntity = EnvironmentEntity.Node(tipSession.NodeId(), tipSession.EnvironmentGroup, tipSession.Metadata);
                                            nodeEntitiesToRemove.Add(nodeEntity);
                                            await this.DeleteTipSessionAsync(tipSession, telemetryContext, cancellationToken)
                                            .ConfigureDefaults();
                                            });

                                        // Reset the state to start from the beginning.
                                        state.TargetRack = null;
                                        break;
                                    }
                                }
                            }

                            // Entities associated with failed TiP session requests are removed from the entities
                            // provisioned pool.
                            if (tipSessionEntitiesToRemove.Any())
                            {
                                await this.RemoveFromEntitiesProvisionedAsync(context, tipSessionEntitiesToRemove, cancellationToken)
                                    .ConfigureDefaults();
                            }

                            // Nodes associated with failed tip session requests are removed from the entities provisioned pool
                            if (nodeEntitiesToRemove.Any())
                            {
                                await this.RemoveFromEntitiesProvisionedAsync(context, nodeEntitiesToRemove, cancellationToken)
                                    .ConfigureDefaults();
                            }

                            // Entities associated with new TiP session requests issued removed from the entities
                            // provisioned pool.
                            if (tipSessionEntitiesToUpdate.Any())
                            {
                                await this.UpdateEntitiesProvisionedAsync(context, tipSessionEntitiesToUpdate, cancellationToken)
                                    .ConfigureDefaults();
                            }

                            if (statuses.All(status => status == TipSessionStatus.Created))
                            {
                                result = new ExecutionResult(ExecutionStatus.Succeeded);

                                IEnumerable<EnvironmentCandidate> tipNodeIds = tipSessionEntitiesToUpdate.Select(t => new EnvironmentCandidate(node: t.NodeId(), cluster: t.ClusterName()));
                                await this.environmentClient.CreateReservedNodesAsync(new ReservedNodes(tipNodeIds), cancellationToken)
                                    .ConfigureDefaults();
                            }
                            else
                            {
                                this.ThrowOnTimeout(state);
                            }
                        }
                        else
                        {
                            this.ThrowOnTimeout(state);
                        }
                    }
                    finally
                    {
                        telemetryContext.AddContext("requestAttempts", state.Attempts);
                        await this.SaveStateAsync(context, state, cancellationToken).ConfigureDefaults();
                    }
                }
            }

            return result;
        }

        private async Task<EnvironmentEntity> SelectCandidateRackAsync(ExperimentContext context, State state, int minimumAvailableNodes, CancellationToken cancellationToken)
        {
            IEnumerable<EnvironmentEntity> entityPool = await this.GetEntityPoolAsync(context, cancellationToken).ConfigureDefaults();
            IEnumerable<EnvironmentEntity> racks = entityPool.GetEntities(EntityType.Rack, ExperimentComponent.AllGroups);

            IEnumerable<EnvironmentEntity> racksWithAvailableNodes = TipCreationProvider.GetRacksWithAvailableNodes(racks, state.NodesAttempted, minimumAvailableNodes);
            if (!racksWithAvailableNodes.Any())
            {
                throw new ProviderException("No racks exist or remain in the entity pool with available nodes.");
            }

            EnvironmentEntity selectedCandidate = racksWithAvailableNodes.Shuffle().First();
            return selectedCandidate;
        }

        private async Task DeleteTipSessionAsync(EnvironmentEntity tipSessionEntity, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            TipSession tipSession = TipSession.FromEnvironmentEntity(tipSessionEntity);
            EventContext relatedContext = telemetryContext.Clone()
                .AddContext("experimentGroup", tipSessionEntity.EnvironmentGroup)
                .AddContext(tipSession);

            await this.Logger.LogTelemetryAsync($"{nameof(TipCreationProvider)}.RequestTipSessionDelete", relatedContext, async () =>
            {
                TipNodeSessionChange tipResponse = await this.tipClient.DeleteTipSessionAsync(tipSession.TipSessionId, cancellationToken)
                    .ConfigureAwait(false);

                tipSessionEntity.Metadata[nameof(TipSession.Status)] = TipSessionStatus.Deleting.ToString();
                relatedContext.AddContext("tipResponse", tipResponse);
            }).ConfigureDefaults();
        }

        private async Task<EnvironmentEntity> CreateTipSessionAsync(EnvironmentEntity rackEntity, ExperimentComponent component, string experimentGroup, string targetNodeId, string preferredVmSku, State state, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            preferredVmSku.ThrowIfNullOrEmpty(nameof(preferredVmSku));

            TipRack rack = TipRack.FromEnvironmentEntity(rackEntity);
            EnvironmentEntity tipSessionEntity = null;

            state.Attempts++;
            bool isAmberNode = component.Parameters.GetValue<bool>(StepParameters.IsAmberNodeRequest, false);

            EventContext relatedContext = telemetryContext.Clone()
                .AddContext("rack", rack)
                .AddContext("targetNodeId", targetNodeId)
                .AddContext("experimentGroup", rackEntity.EnvironmentGroup)
                .AddContext("isAmberNode", isAmberNode);

            await this.Logger.LogTelemetryAsync($"{nameof(TipCreationProvider)}.RequestTipSession", relatedContext, async () =>
            {
                TipParameters tipParameters = new TipParameters()
                {
                    CandidateNodesId = new List<string>() { targetNodeId },
                    NodeCount = 1,
                    ClusterName = rack.ClusterName,
                    Region = rack.Region,
                    MachinePoolNames = new List<string>() { rack.MachinePoolName },
                    IsAmberNodeRequest = isAmberNode
                };

                relatedContext.AddContext(nameof(tipParameters), tipParameters);

                TipNodeSessionChange tipResponse = await this.tipClient.CreateTipSessionAsync(tipParameters, cancellationToken).ConfigureAwait(false);
                state.NodesAttempted.Add(targetNodeId);
                relatedContext.AddContext("tipResponse", tipResponse);

                TipSession session = new TipSession()
                {
                    TipSessionId = tipResponse.TipNodeSessionId,
                    ClusterName = rack.ClusterName,
                    Region = rack.Region,
                    GroupName = experimentGroup,
                    NodeId = targetNodeId,
                    ChangeIdList = new List<string>() { tipResponse.TipNodeSessionChangeId },
                    Status = TipSessionStatus.Creating,
                    CreatedTimeUtc = DateTime.MaxValue,
                    ExpirationTimeUtc = DateTime.MaxValue,
                    SupportedVmSkus = rack.SupportedVmSkus,

                    // Select a preferred VM SKU for experiments that require the VM SKU match across
                    // all experiment groups (e.g. a typical A/B experiment).
                    PreferredVmSku = preferredVmSku
                };

                tipSessionEntity = TipSession.ToEnvironmentEntity(session, experimentGroup);
                relatedContext.AddContext(session);
            }).ConfigureDefaults();

            return tipSessionEntity;
        }

        private async Task<TipSessionStatus> GetTipSessionStatusAsync(EnvironmentEntity tipSessionEntity, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            TipSessionStatus status = TipSessionStatus.Creating;
            TipSession tipSession = TipSession.FromEnvironmentEntity(tipSessionEntity);
            bool isFailed = await this.tipClient.IsTipSessionChangeFailedAsync(tipSession.TipSessionId, tipSession.ChangeIdList.First(), cancellationToken)
                .ConfigureDefaults();

            if (!isFailed)
            {
                TipNodeSession tipNode = await this.tipClient.GetTipSessionAsync(tipSession.TipSessionId, cancellationToken)
                    .ConfigureDefaults();

                if (tipNode.Status == TipNodeSessionStatus.Created)
                {
                    status = TipSessionStatus.Created;
                    tipSessionEntity.Metadata[nameof(TipSession.CreatedTimeUtc)] = tipNode.CreatedTimeUtc;
                    tipSessionEntity.Metadata[nameof(TipSession.ExpirationTimeUtc)] = tipNode.ExpirationTimeUtc;
                    tipSessionEntity.Metadata[nameof(TipSession.DeletedTimeUtc)] = tipNode.DeletedTimeUtc ?? DateTime.MaxValue;
                    tipSessionEntity.Metadata[nameof(TipSession.Status)] = tipNode.Status.ToString();

                    await this.Logger.LogTelemetryAsync($"{nameof(TipCreationProvider)}.TipSessionCreated", LogLevel.Information, telemetryContext.Clone()
                        .AddContext("experimentGroup", tipSessionEntity.EnvironmentGroup)
                        .AddContext(tipSession)
                        .AddContext("tipNode", tipNode)).ConfigureDefaults();
                }
            }
            else
            {
                status = TipSessionStatus.Failed;
                tipSessionEntity.Metadata[nameof(TipSession.Status)] = TipSessionStatus.Failed.ToString();

                await this.Logger.LogTelemetryAsync($"{nameof(TipCreationProvider)}.TipSessionCreationFailed", LogLevel.Warning, telemetryContext.Clone()
                    .AddContext("experimentGroup", tipSessionEntity.EnvironmentGroup)
                    .AddContext(tipSession)).ConfigureDefaults();
            }

            return status;
        }

        private void ThrowOnTimeout(State state)
        {
            if (state.IsTimeoutExpired)
            {
                throw new TimeoutException(
                    $"TiP session creation attempt timed out. The time allowed for which to create the TiP session expired (timeout = '{state.Timeout}')");
            }
        }

        internal class State
        {
            [JsonIgnore]
            public bool IsTimeoutExpired => DateTime.UtcNow > this.StepTimeout;

            public int Attempts { get; set; }

            public DateTime StepTimeout { get; set; }

            public TimeSpan Timeout { get; set; }

            public EnvironmentEntity TargetRack { get; set; }

            public List<string> NodesAttempted { get; set; }
        }
    }
}
