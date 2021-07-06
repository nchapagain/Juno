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
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using TipGateway.Entities;

    /// <summary>
    /// Provider that creates Tip session across groups.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.EnvironmentSetup, SupportedStepTarget.ExecuteRemotely)]
    [SupportedParameter(Name = StepParameters.NodeAffinity, Type = typeof(NodeAffinity), Required = false)]
    [SupportedParameter(Name = StepParameters.IsAmberNodeRequest, Type = typeof(bool), Required = false)]
    [SupportedParameter(Name = StepParameters.Count, Type = typeof(int), Required = false)]
    [SupportedParameter(Name = StepParameters.NodeTag, Type = typeof(string), Required = false)]
    [ProviderInfo(Name = "Create TiP sessions", Description = "Create TiP sessions for the all experiment groups to isolate physical nodes/blades in the Azure fleet", FullDescription = "Step to create TiP sessions associated with an experiment. Once a set of clusters that have physical nodes whose characteristics match the requirements of the experiment has been identified, is to estable TiP sessions to the nodes so that an experiment can be run. The node to which the change will be applied is called a `treatment group`.")]
    public partial class TipCreationProvider2 : ExperimentProvider
    {
        // The timeout for the entirety of the TiP creation process.
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromHours(2);
        private static readonly TimeSpan ReevaluationExtension = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Initializes a new instance of the <see cref="TipCreationProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public TipCreationProvider2(IServiceCollection services)
            : base(services)
        {
            this.TelemetryEventNamePrefix = nameof(TipCreationProvider2);
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

            ExecutionResult result = new ExecutionResult(ExecutionStatus.InProgress, extensionTimeout: TipCreationProvider2.ReevaluationExtension);
            this.EntityManager = this.Services.GetService<EntityManager>();
            this.TipClient = this.Services.GetService<ITipClient>();

            if (!cancellationToken.IsCancellationRequested)
            {
                TimeSpan timeout = component.Parameters.GetTimeSpanValue(StepParameters.Timeout, TipCreationProvider2.DefaultTimeout);
                State state = await this.GetStateAsync<State>(context, cancellationToken).ConfigureDefaults()
                    ?? new State
                    {
                        Timeout = timeout,
                        StepTimeout = DateTime.UtcNow.Add(timeout),
                        CountPerGroup = component.Parameters.GetValue<int>(StepParameters.Count, 1)
                    };

                await this.EntityManager.LoadEntityPoolAsync(context.Experiment.Id, cancellationToken).ConfigureDefaults();
                await this.EntityManager.LoadEntitiesProvisionedAsync(context.Experiment.Id, cancellationToken).ConfigureDefaults();

                try
                {
                    // The node affinity describes how "close" nodes need to be to meet the requirements of the experiment. For example,
                    // SameRack affinity means that the nodes selected must be in the same rack in a cluster, SameCluster means they only
                    // need to be in the same cluster.
                    NodeAffinity nodeAffinity = component.Parameters.GetEnumValue<NodeAffinity>(StepParameters.NodeAffinity, NodeAffinity.SameRack);

                    // TiP sessions that are not discarded and that have not been confirmed to be created.
                    IEnumerable<EnvironmentEntity> tipNodesRequested = this.EntityManager.EntityPool.GetNodes()
                        ?.Where(node => !node.Discarded())
                        ?.Where(node => node.TipSessionStatus() == TipSessionStatus.Creating.ToString());

                    if (tipNodesRequested?.Any() != true)
                    {
                        await this.RequestTipSessionsAsync(component, nodeAffinity, state, telemetryContext, cancellationToken)
                            .ConfigureDefaults();

                        state.Timeout = component.Parameters.GetTimeSpanValue(StepParameters.Timeout, TipCreationProvider2.DefaultTimeout);
                        state.StepTimeout = DateTime.UtcNow.Add(state.Timeout);
                    }
                    else
                    {
                        await this.CheckTipSessionStatusesAsync(context, tipNodesRequested, telemetryContext, cancellationToken)
                            .ConfigureDefaults();
                    }

                    IEnumerable<string> environmentGroups = context.GetExperimentGroups();
                    IEnumerable<EnvironmentEntity> nodesWithTipSessions = this.EntityManager.EntityPool.GetNodes()
                        ?.Where(node => !node.Discarded())
                        ?.Where(node => node.TipSessionStatus() == TipSessionStatus.Created.ToString());

                    // This if statement checks for success criteria.
                    if (nodesWithTipSessions.Count() == environmentGroups.Count() * state.CountPerGroup)
                    {
                        // We have successfully created the TiP sessions necessary for all environment
                        // groups in the experiment.
                        result = new ExecutionResult(ExecutionStatus.Succeeded);
                        nodesWithTipSessions.ToList().ForEach(tipNode =>
                        {
                            EnvironmentEntity tipSession = EnvironmentEntity.TipSession(tipNode.TipSessionId(), tipNode.EnvironmentGroup, tipNode.Metadata);
                            tipSession.NodeId(tipNode.Id);

                            this.EntityManager.EntitiesProvisioned.Add(tipSession);
                            this.EntityManager.EntitiesProvisioned.Add(tipNode);
                        });

                        // Cleanup any TiP nodes that were acquired but that cannot be used as part of the
                        // experiment. This happens when a given node affinity cannot be met for a subset of the 
                        // nodes available but after 1 or more nodes have successful TiP sessions established.
                        await this.DeleteTipSessionsAsync(
                            this.GetTipNodeSessionsCreatedButDiscarded(),
                            telemetryContext,
                            cancellationToken).ConfigureDefaults();
                    }

                    // If we haven't created the necessary TiP sessions within the time allotted, we will timeout
                    // the step + experiment.
                    TipCreationProvider2.ThrowOnTimeout(state);
                }
                catch (TimeoutException)
                {
                    await this.DeleteTipSessionsAsync(this.GetTipNodeSessions(TipSessionStatus.Created, TipSessionStatus.Creating, TipSessionStatus.Pending), telemetryContext, cancellationToken)
                        .ConfigureDefaults();

                    throw;
                }
                catch (ProviderException exc) when (exc.Reason == ErrorReason.ExpectedEnvironmentEntitiesNotFound)
                {
                    // If we cannot meet the requirements of the node affinity with the pool of entities we have,
                    // we need to request any TiP sessions that we did successfully create to be deleted.
                    await this.DeleteTipSessionsAsync(
                        this.GetTipNodeSessions(TipSessionStatus.Created, TipSessionStatus.Creating, TipSessionStatus.Pending),
                        telemetryContext,
                        cancellationToken).ConfigureDefaults();

                    throw;
                }
                finally
                {
                    telemetryContext.AddContext("requestAttempts", state.Attempts);
                    EventContext relatedContext = telemetryContext.Clone()
                        .AddContext("entityPool", this.EntityManager.EntityPool)
                        .AddContext("entitiesProvisioned", this.EntityManager.EntitiesProvisioned);

                    await this.Logger.LogTelemetryAsync($"{this.TelemetryEventNamePrefix}.Entities", LogLevel.Information, relatedContext)
                        .ConfigureDefaults();

                    this.TagTipSessions(component);
                    await this.EntityManager.SaveEntityPoolAsync(context.Experiment.Id, cancellationToken).ConfigureDefaults();
                    await this.EntityManager.SaveEntitiesProvisionedAsync(context.Experiment.Id, cancellationToken).ConfigureDefaults();
                    await this.SaveStateAsync(context, state, cancellationToken).ConfigureDefaults();
                }
            }

            return result;
        }

        private void TagTipSessions(ExperimentComponent component)
        {
            string tags = component.Parameters.GetValue<string>(StepParameters.NodeTag, string.Empty);
            List<string> tagList = tags.Split(',', ';').ToList();

            IEnumerable<EnvironmentEntity> tipSessions = this.EntityManager.EntitiesProvisioned.GetTipSessions()
                .OrderBy(t => t.Metadata[nameof(TipSession.GroupName)])
                .ThenBy(t => t.Metadata[nameof(TipSession.NodeId)]);
            IEnumerable<EnvironmentEntity> nodes = this.EntityManager.EntitiesProvisioned.GetNodes()
                .OrderBy(t => t.Metadata[nameof(TipSession.GroupName)])
                .ThenBy(t => t.Id);
            if ((tipSessions.Count() == nodes.Count()) && (nodes.Count() == tagList.Count))
            {
                for (int index = 0; index < tipSessions.Count(); index++)
                {
                    tipSessions.ElementAt(index).Metadata[nameof(TipSession.NodeTag)] = tagList[index];
                    nodes.ElementAt(index).Metadata[nameof(TipSession.NodeTag)] = tagList[index];
                }
            }
        }

        private static void ThrowOnAllOptionsExpended(NodeAffinity nodeAffinity)
        {
            throw new ProviderException(
                $"There are no nodes existing or remaining in the entity pool that match the affinity requirements '{nodeAffinity}' " +
                $"for the experiment workflow step.",
                ErrorReason.ExpectedEnvironmentEntitiesNotFound);
        }

        private static void ThrowOnTimeout(State state)
        {
            if (state.IsTimeoutExpired)
            {
                throw new TimeoutException(
                    $"TiP session creation attempt timed out. The time allowed for which to create the TiP session expired (timeout = '{state.Timeout}')");
            }
        }

        private async Task CheckTipSessionStatusesAsync(
            ExperimentContext context, IEnumerable<EnvironmentEntity> tipNodesRequested, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            foreach (EnvironmentEntity tipNode in tipNodesRequested)
            {
                // If there are in progress tip request, check their progress first.
                TipSessionStatus status = await this.GetTipSessionStatusAsync(context, tipNode, telemetryContext, cancellationToken)
                    .ConfigureDefaults();

                if (status == TipSessionStatus.Failed)
                {
                    // Mark the node entities in both the entity pool as discarded. They cannot be used again for
                    // node selection.
                    this.EntityManager.EntityPool.Discard(tipNode.Id);
                }
            }
        }

        private async Task DeleteTipSessionsAsync(IEnumerable<EnvironmentEntity> tipNodes, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            if (tipNodes?.Any() == true)
            {
                foreach (EnvironmentEntity tipNode in tipNodes)
                {
                    await this.DeleteTipSessionAsync(tipNode, telemetryContext, cancellationToken).ConfigureDefaults();
                }
            }
        }

        private async Task DeleteTipSessionAsync(EnvironmentEntity tipNode, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            EventContext relatedContext = telemetryContext.Clone()
                .AddContext("experimentGroup", tipNode.EnvironmentGroup)
                .AddContext("tipSession", tipNode);

            await this.Logger.LogTelemetryAsync($"{this.TelemetryEventNamePrefix}.RequestTipSessionDelete", relatedContext, async () =>
            {
                TipNodeSessionChange tipResponse = await this.TipClient.DeleteTipSessionAsync(tipNode.TipSessionId(), cancellationToken)
                    .ConfigureDefaults();

                tipNode.TipSessionStatus(TipSessionStatus.Deleting.ToString());
                tipNode.TipSessionDeleteRequestChangeId(tipResponse.TipNodeSessionChangeId);
                relatedContext.AddContext("tipResponse", tipResponse);
            }).ConfigureDefaults();
        }

        private IEnumerable<EnvironmentEntity> GetTipNodeSessions(params TipSessionStatus[] tipSesstionStatus)
        {
            return this.EntityManager.EntityPool.GetNodes()
                ?.Where(node => tipSesstionStatus.Select(s => s.ToString()).Contains(node.TipSessionStatus(), StringComparer.OrdinalIgnoreCase));
        }

        private IEnumerable<EnvironmentEntity> GetTipNodeSessionsCreatedButDiscarded()
        {
            return this.EntityManager.EntityPool.GetNodes()
                ?.Where(node => node.Discarded())
                ?.Where(node => node.TipSessionStatus() == TipSessionStatus.Created.ToString());
        }

        private async Task<TipSessionStatus> GetTipSessionStatusAsync(ExperimentContext context, EnvironmentEntity node, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            TipSessionStatus status = TipSessionStatus.Creating;
            string tipSessionId = node.TipSessionId();
            string tipSessionChangeId = node.TipSessionRequestChangeId();

            EventContext relatedContext = telemetryContext.Clone()
                .AddContext("experimentId", context.Experiment.Id)
                .AddContext("experimentGroup", node.EnvironmentGroup)
                .AddContext("tipSessionId", tipSessionId)
                .AddContext("tipSessionChangeId", tipSessionChangeId)
                .AddContext("tipSessionRegion", node.Region())
                .AddContext("tipSessionCluster", node.ClusterName())
                .AddContext("tipSessionNodeId", node.Id);

            bool isFailed = await this.TipClient.IsTipSessionChangeFailedAsync(tipSessionId, tipSessionChangeId, cancellationToken)
                .ConfigureDefaults();

            if (!isFailed)
            {
                TipNodeSession tipNode = await this.TipClient.GetTipSessionAsync(tipSessionId, cancellationToken)
                    .ConfigureDefaults();

                if (tipNode.Status == TipNodeSessionStatus.Created)
                {
                    status = TipSessionStatus.Created;
                    node.TipSessionCreatedTime(tipNode.CreatedTimeUtc);
                    node.TipSessionDeletedTime(tipNode.DeletedTimeUtc ?? DateTime.MaxValue);
                    node.TipSessionExpirationTime(tipNode.ExpirationTimeUtc);

                    // *** For backwards compatibility ***
                    // We need to update the TipSession class to integrate with the metadata properties used by the
                    // provider. Then we do not need to duplicate these values in order to accommodate different property
                    // names.
                    node.Metadata[nameof(TipSession.ChangeIdList)] = string.Join(";", new List<string> { tipSessionChangeId });
                    node.Metadata[nameof(TipSession.CreatedTimeUtc)] = tipNode.CreatedTimeUtc;
                    node.Metadata[nameof(TipSession.DeletedTimeUtc)] = DateTime.MaxValue;
                    node.Metadata[nameof(TipSession.ExpirationTimeUtc)] = tipNode.ExpirationTimeUtc;

                    relatedContext.AddContext("tipSessionStatus", status.ToString())
                        .AddContext("tipSessionExpirationTime", tipNode?.ExpirationTimeUtc)
                        .AddContext("tipNode", tipNode);

                    await this.Logger.LogTelemetryAsync($"{this.TelemetryEventNamePrefix}.TipSessionCreated", LogLevel.Information, relatedContext)
                        .ConfigureDefaults();
                }
            }
            else
            {
                status = TipSessionStatus.Failed;
                relatedContext.AddContext("tipSessionStatus", status.ToString());

                await this.Logger.LogTelemetryAsync($"{this.TelemetryEventNamePrefix}.TipSessionCreationFailed", LogLevel.Warning, relatedContext)
                    .ConfigureDefaults();
            }

            node.TipSessionStatus(status.ToString());

            // *** For backwards compatibility ***
            // We need to update the TipSession class to integrate with the metadata properties used by the
            // provider. Then we do not need to duplicate these values in order to accommodate different property
            // names.
            node.Metadata[nameof(TipSession.Status)] = status.ToString();

            return status;
        }

        private async Task RequestTipSessionAsync(EnvironmentEntity node, EventContext telemetryContext, CancellationToken cancellationToken, bool isAmberNode = false)
        {
            EventContext relatedContext = telemetryContext.Clone()
                .AddContext("node", node)
                .AddContext("tipSessionRegion", node.Region())
                .AddContext("tipSessionCluster", node.ClusterName())
                .AddContext("tipSessionRack", node.RackLocation())
                .AddContext("tipSessionNodeId", node.Id)
                .AddContext("experimentGroup", node.EnvironmentGroup)
                .AddContext("isAmberNode", isAmberNode);

            await this.Logger.LogTelemetryAsync($"{this.TelemetryEventNamePrefix}.RequestTipSession", relatedContext, async () =>
            {
                TipParameters tipParameters = new TipParameters()
                {
                    CandidateNodesId = new List<string>() { node.Id },
                    NodeCount = 1,
                    ClusterName = node.ClusterName(),
                    Region = node.Region(),
                    MachinePoolNames = new List<string>() { node.MachinePoolName() },
                    IsAmberNodeRequest = isAmberNode
                };

                relatedContext.AddContext(nameof(tipParameters), tipParameters);

                TipNodeSessionChange tipResponse = await this.TipClient.CreateTipSessionAsync(tipParameters, cancellationToken)
                    .ConfigureAwait(false);

                relatedContext.AddContext("tipSessionId", tipResponse.TipNodeSessionId);
                relatedContext.AddContext("tipSessionChangeId", tipResponse.TipNodeSessionChangeId);
                relatedContext.AddContext("tipResponse", tipResponse);

                node.AgentId(new AgentIdentification(node.ClusterName(), node.Id, context: tipResponse.TipNodeSessionId).ToString());
                node.TipSessionId(tipResponse.TipNodeSessionId);
                node.TipSessionRequestChangeId(tipResponse.TipNodeSessionChangeId);
                node.TipSessionStatus(TipSessionStatus.Creating.ToString());
            }).ConfigureDefaults();
        }

        private async Task RequestTipSessionsAsync(ExperimentComponent component, NodeAffinity nodeAffinity, State state, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            IEnumerable<EnvironmentEntity> tipNodesAcquired = this.EntityManager.EntityPool.GetNodes()
                ?.Where(node => !node.Discarded())
                ?.Where(node => node.TipSessionStatus() == TipSessionStatus.Created.ToString());

            IEnumerable<EnvironmentEntity> candidateNodes = this.SelectCandidateNodes(nodeAffinity, state.CountPerGroup, tipNodesAcquired?.ToArray());

            if (candidateNodes?.Any() != true)
            {
                // If we cannot find any candidate nodes that have an affinity to existing nodes associated with
                // successfully acquired TiP sessions, then we will delete the existing sessions.
                if (tipNodesAcquired?.Any() == true)
                {
                    tipNodesAcquired.ToList().ForEach(async tipNode =>
                    {
                        tipNode.Discarded(true);
                        EventContext nodeContext = telemetryContext.Clone()
                            .AddContext("node", tipNode);

                        await this.Logger.LogTelemetryAsync($"{this.TelemetryEventNamePrefix}.NodeDiscarded", LogLevel.Information, nodeContext)
                            .ConfigureDefaults();
                    });
                }

                // Once we've marked the nodes in the pool that were successfully established but
                // that cannot be used because of the inability to meet the requirements of the node affinity for all
                // environment groups, we will attempt to select a new group of candidate nodes.
                candidateNodes = this.SelectCandidateNodes(nodeAffinity, state.CountPerGroup);
            }

            if (candidateNodes?.Any() != true)
            {
                // We cannot meet the requirements of the experiment.
                TipCreationProvider2.ThrowOnAllOptionsExpended(nodeAffinity);
            }

            EventContext relatedContext = telemetryContext.Clone()
                .AddContext("nodes", this.EntityManager.EntityPool);

            await this.Logger.LogTelemetryAsync($"{this.TelemetryEventNamePrefix}.CandidateNodes", LogLevel.Information, relatedContext)
                .ConfigureDefaults();

            bool isAmberNode = component.Parameters.GetValue<bool>(StepParameters.IsAmberNodeRequest, false);
            foreach (EnvironmentEntity potentialNode in candidateNodes)
            {
                state.Attempts++;
                await this.RequestTipSessionAsync(potentialNode, telemetryContext, cancellationToken, isAmberNode)
                    .ConfigureDefaults();
            }
        }

        private IEnumerable<EnvironmentEntity> SelectCandidateNodes(NodeAffinity nodeAffinity, int countPerGroup, params EnvironmentEntity[] withAffinityToNodes)
        {
            // If there are no TiP session creation requests in progress, select nodes from the whole pool and
            // start the requests.
            IEnumerable<string> environmentGroups = this.EntityManager.EntityPool.Select(entity => entity.EnvironmentGroup)
                .Distinct()
                .OrderBy(group => group);

            Dictionary<string, int> remainingNodesList = new Dictionary<string, int>();
            
            foreach (string group in environmentGroups)
            {
                if (withAffinityToNodes?.Any() == true)
                {
                    if (countPerGroup > withAffinityToNodes.Where(n => n.EnvironmentGroup == group).Count())
                    {
                        remainingNodesList.Add(group, countPerGroup - withAffinityToNodes.Where(n => n.EnvironmentGroup == group).Count());
                    }
                }
                else
                {
                    remainingNodesList.Add(group, countPerGroup);
                }
            }

            List<EnvironmentEntity> candidateNodes = new List<EnvironmentEntity>();

            foreach (KeyValuePair<string, int> group in remainingNodesList)
            {
                for (int count = 0; count < group.Value; count++)
                {
                    EnvironmentEntity candidateNode = this.EntityManager.EntityPool.GetNode(
                        nodeAffinity,
                        group.Key,
                        countPerGroup,
                        candidateNodes.Union(withAffinityToNodes).ToArray());

                    if (candidateNode != null)
                    {
                        candidateNodes.Add(candidateNode);
                    }
                }
            }

            return candidateNodes;
        }

        internal class State
        {
            [JsonIgnore]
            public bool IsTimeoutExpired => DateTime.UtcNow > this.StepTimeout;

            public int CountPerGroup { get; set; }

            public int Attempts { get; set; }

            public DateTime StepTimeout { get; set; }

            public TimeSpan Timeout { get; set; }
        }
    }
}
