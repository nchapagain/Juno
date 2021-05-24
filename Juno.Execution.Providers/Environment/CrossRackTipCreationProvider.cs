namespace Juno.Execution.Providers.Environment
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.Execution.TipIntegration;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using TipGateway.Entities;

    /// <summary>
    /// Tip Creation Provider to temporarily work around the same rack constraint. 
    /// </summary>
    [ExecutionConstraints(SupportedStepType.EnvironmentSetup, SupportedStepTarget.ExecuteRemotely)]
    [SupportedParameter(Name = Constants.TreatmentCluster, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Constants.TreatmentGroup, Type = typeof(string), Required = true)]
    public class CrossRackTipCreationProvider : TipCreationProvider
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromHours(1);
        private ITipClient tipClient;
        private TipSettings tipSettings;

        /// <summary>
        /// Initializes an instance of <see cref="CrossRackTipCreationProvider"/>
        /// </summary>
        /// <param name="services"></param>
        public CrossRackTipCreationProvider(IServiceCollection services)
            : base(services)
        {   
        }

        /// <inheritdoc/>
        protected async override Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            ExecutionResult result = new ExecutionResult(ExecutionStatus.InProgress);
            if (cancellationToken.IsCancellationRequested)
            {
                return new ExecutionResult(ExecutionStatus.Cancelled);
            }

            State state = await this.GetStateAsync<State>(context, cancellationToken).ConfigureDefaults()
                ?? new State();

            EnvironmentSettings settings = EnvironmentSettings.Initialize(context.Configuration);
            this.tipSettings = settings.TipSettings;
            this.tipClient = this.Services.GetService<ITipClient>();

            if (cancellationToken.IsCancellationRequested)
            {
                return new ExecutionResult(ExecutionStatus.Cancelled);
            }

            IEnumerable<EnvironmentEntity> provisionedEntities = await this.GetEntitiesProvisionedAsync(context, cancellationToken)
                .ConfigureDefaults();

            IList<EnvironmentEntity> tipSessionRequestsInProgress = provisionedEntities?.GetEntities(EntityType.TipSession).ToList();

            // No rack has been chosen and no tip sessions have been requested
            // this provider does not retry.
            try
            {
                if (state.TargetRacks?.Any() != true)
                {
                    List<EnvironmentEntity> tipSessionRequests = new List<EnvironmentEntity>();

                    // The Treatment cluster experimentation is where the provider should attempt to launch tip sessions
                    // on two different clusters, as of right now there should only be two rack entities, this is the only way
                    // that we can gurantee that we will land one tipsession on OVL and another on a non-OVL cluster
                    IEnumerable<string> experimentGroups = context.GetExperimentGroups();

                    if (experimentGroups?.Count() != 2)
                    {
                        throw new ProviderException($"For Overlake experimentation there should only be two experiment groups. There are {experimentGroups?.Count()}");
                    }

                    string treatmentCluster = component.Parameters.GetValue<string>(Constants.TreatmentCluster);
                    string treatmentGroup = component.Parameters.GetValue<string>(Constants.TreatmentGroup);
                    // Grab the rack entities from the entity pool and make sure that there are only TWO and one of them is OVL and the other is not.
                    IEnumerable<EnvironmentEntity> entityPool = await this.GetEntityPoolAsync(context, cancellationToken).ConfigureDefaults();
                    IEnumerable<EnvironmentEntity> candidateRacks = entityPool.GetEntities(EntityType.Rack, ExperimentComponent.AllGroups);
                    if (candidateRacks?.Count() != 2
                        || candidateRacks.All(r => r.ClusterName().Equals(treatmentCluster, StringComparison.OrdinalIgnoreCase))
                        || candidateRacks.All(r => !r.ClusterName().Equals(treatmentCluster, StringComparison.OrdinalIgnoreCase)))
                    {
                        throw new ProviderException($"{nameof(CrossRackTipCreationProvider)} only supports 2 racks. One that is the OVL Cluster, and one that is not. " +
                            $"The clusters provided were {string.Join(",", candidateRacks.Select(r => r.ClusterName()))}");
                    }

                    state.TargetRacks = candidateRacks;
                    foreach (string group in experimentGroups)
                    {
                        EnvironmentEntity rack = group.Equals(treatmentGroup, StringComparison.OrdinalIgnoreCase)
                            ? candidateRacks.First(r => r.ClusterName().Equals(treatmentCluster, StringComparison.OrdinalIgnoreCase))
                            : candidateRacks.First(r => !r.ClusterName().Equals(treatmentCluster, StringComparison.OrdinalIgnoreCase));

                        state.Timeout = component.Parameters.GetTimeSpanValue(StepParameters.Timeout, CrossRackTipCreationProvider.DefaultTimeout);
                        state.StepTimeout = DateTime.UtcNow.Add(state.Timeout);

                        tipSessionRequests.Add(await this.CreateTipSessionAsync(rack, component, group, state, telemetryContext, cancellationToken).ConfigureDefaults());
                    }

                    await this.UpdateEntitiesProvisionedAsync(context, tipSessionRequests, cancellationToken)
                        .ConfigureDefaults();

                }
                else if (state.TargetRacks?.Any() == true)
                {
                    IEnumerable<Task<TipSessionStatus>> statusTasks = tipSessionRequestsInProgress.Select((tipSession) =>
                    {
                        return this.GetTipSessionStatusAsync(tipSession, telemetryContext, cancellationToken);
                    });

                    IEnumerable<TipSessionStatus> status = await Task.WhenAll(statusTasks).ConfigureDefaults();

                    // Both tip requests have succeeded
                    if (status.All(s => s == TipSessionStatus.Created))
                    {
                        List<EnvironmentEntity> tipSessionEntitiesToUpdate = new List<EnvironmentEntity>();
                        tipSessionEntitiesToUpdate.AddRange(tipSessionRequestsInProgress);
                        tipSessionEntitiesToUpdate.AddRange(tipSessionRequestsInProgress.Select(session =>
                        {
                            AgentIdentification nodeAgentId = new AgentIdentification(
                                           session.ClusterName(),
                                           session.NodeId(),
                                           null,
                                           session.TipSessionId());

                            EnvironmentEntity nodeEntity = EnvironmentEntity.Node(nodeAgentId.ToString(), session.EnvironmentGroup, session.Metadata);
                            nodeEntity.AgentId(nodeAgentId.ToString());

                            return nodeEntity;
                        }));

                        await this.UpdateEntitiesProvisionedAsync(context, tipSessionEntitiesToUpdate, cancellationToken).ConfigureDefaults();

                        result = new ExecutionResult(ExecutionStatus.Succeeded);
                    }

                    // If either group a or group b fail, fail the whole thing, clean up and exit.
                    if (status.Any(s => s == TipSessionStatus.Failed))
                    {
                        List<EnvironmentEntity> tipSessionEntitiesToRemove = new List<EnvironmentEntity>();
                        // Delete all tip sessions and fail step.
                        tipSessionEntitiesToRemove.AddRange(tipSessionRequestsInProgress);

                        IEnumerable<EnvironmentEntity> tipSessionSessionsToDelete = tipSessionRequestsInProgress
                                                .Where(session => TipSession.FromEnvironmentEntity(session).Status != TipSessionStatus.Failed);

                        IEnumerable<Task<TipNodeSessionChange>> deletionTasks = tipSessionSessionsToDelete.Select((entity) =>
                        {
                            TipSession tipSession = TipSession.FromEnvironmentEntity(entity);
                            entity.Metadata[nameof(TipSession.Status)] = TipSessionStatus.Deleting.ToString();
                            return this.tipClient.DeleteTipSessionAsync(tipSession.TipSessionId, cancellationToken);
                        });

                        IEnumerable<TipNodeSessionChange> deletionResponses = await Task.WhenAll(deletionTasks).ConfigureDefaults();
                        telemetryContext.AddContext(nameof(deletionResponses), deletionResponses);

                        await this.RemoveFromEntitiesProvisionedAsync(context, tipSessionEntitiesToRemove, cancellationToken).ConfigureDefaults();

                        result = new ExecutionResult(ExecutionStatus.Failed);
                    }

                    this.ThrowOnTimeout(state);
                }
            }
            finally
            {
                await this.SaveStateAsync(context, state, cancellationToken).ConfigureDefaults();
            }

            return result;
        }

        private async Task<EnvironmentEntity> CreateTipSessionAsync(EnvironmentEntity entity, ExperimentComponent component, string experimentGroup, State state, EventContext telemetryContext, CancellationToken token)
        {
            TipRack rack = TipRack.FromEnvironmentEntity(entity);
            EnvironmentEntity tipSessionEntity = null;

            bool isAmberNode = component.Parameters.GetValue<bool>(StepParameters.IsAmberNodeRequest, false);

            EventContext relatedContext = telemetryContext.Clone()
                .AddContext("rack", rack)
                .AddContext("experimentGroup", experimentGroup)
                .AddContext("isAmberNode", isAmberNode);

            await this.Logger.LogTelemetryAsync($"{nameof(CrossRackTipCreationProvider)}.RequestTipSession", relatedContext, async () =>
            {
                string nodeId = entity.NodeList().First();
                TipParameters tipParameters = new TipParameters()
                {
                    CandidateNodesId = new List<string>() { nodeId },
                    NodeCount = 1,
                    ClusterName = rack.ClusterName,
                    Region = rack.Region,
                    // We can get away with this for now because ESS compares against machine pool for node parity.
                    MachinePoolNames = new List<string>() { rack.MachinePoolName },
                    IsAmberNodeRequest = isAmberNode
                };

                relatedContext.AddContext(nameof(tipParameters), tipParameters);
                TipNodeSessionChange tipResponse = await this.tipClient.CreateTipSessionAsync(tipParameters, token).ConfigureDefaults();
                relatedContext.AddContext("tipResponse", tipResponse);

                TipSession session = new TipSession()
                {
                    TipSessionId = tipResponse.TipNodeSessionId,
                    ClusterName = rack.ClusterName,
                    Region = rack.Region,
                    GroupName = experimentGroup,
                    NodeId = nodeId,
                    ChangeIdList = new List<string>() { tipResponse.TipNodeSessionChangeId },
                    Status = TipSessionStatus.Creating,
                    CreatedTimeUtc = DateTime.MaxValue,
                    ExpirationTimeUtc = DateTime.MaxValue,
                    SupportedVmSkus = rack.SupportedVmSkus,

                    // Select a preferred VM SKU for experiments that require the VM SKU match across
                    // all experiment groups (e.g. a typical A/B experiment).
                    PreferredVmSku = state.TargetRacks.Shuffle().First().SupportedVmSkus().Shuffle().First()
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
                TipNodeSession tipNode = await this.tipClient.GetTipSessionAsync(tipSession.TipSessionId, cancellationToken).ConfigureDefaults();
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

        internal new class State
        {
            [JsonIgnore]
            public bool IsTimeoutExpired => DateTime.UtcNow > this.StepTimeout;

            public int Attempts { get; set; }

            public DateTime StepTimeout { get; set; }

            public TimeSpan Timeout { get; set; }

            public IEnumerable<EnvironmentEntity> TargetRacks { get; set; }
        }

        internal class Constants
        {
            internal const string TreatmentCluster = "treatmentCluster";
            internal const string TreatmentGroup = "treatmentGroup";
        }
    }
}
