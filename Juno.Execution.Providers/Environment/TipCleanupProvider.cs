namespace Juno.Execution.Providers.Environment
{
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
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
    using Kusto.Cloud.Platform.Utils;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using TipGateway.Entities;

    /// <summary>
    /// Provider to cleanup TipSessions.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.EnvironmentCleanup, SupportedStepTarget.ExecuteRemotely)]
    [SupportedParameter(Name = Parameters.TipSessionId, Type = typeof(string), Required = false)]
    [ProviderInfo(Name = "Release TiP Sessions/Nodes", Description = "Release nodes associated with TiP sessions back to the Azure production fleet for the experiment group", FullDescription = "Step to cleanup TiP sessions associated with an experiment. After the experiment completes (i.e. payload is applied, workloads run their course), the environment must be cleaned up. To ensure the physical nodes used as part of the experiment are returned to the Azure production pool, we need to delete the TiP sessions.")]
    public class TipCleanupProvider : ExperimentProvider
    {
        private ITipClient tipClient;
        private IEnvironmentClient environmentClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="TipCleanupProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public TipCleanupProvider(IServiceCollection services)
            : base(services)
        {
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

        /// <inheritdoc/>
        protected override async Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            ExecutionResult result = new ExecutionResult(ExecutionStatus.Pending);

            if (!cancellationToken.IsCancellationRequested)
            {
                result = new ExecutionResult(ExecutionStatus.InProgressContinue);

                this.tipClient = this.Services.GetService<ITipClient>();
                this.environmentClient = this.Services.GetService<IEnvironmentClient>();

                DeleteTipSessionState state = await this.GetStateAsync<DeleteTipSessionState>(context, cancellationToken).ConfigureDefaults()
                    ?? new DeleteTipSessionState();

                if (!cancellationToken.IsCancellationRequested)
                {
                    IList<TipSession> tipSessions = null;

                    if (component.Parameters.ContainsKey(Parameters.TipSessionId))
                    {
                        string tipSessionId = component.Parameters.GetValue<string>(Parameters.TipSessionId, string.Empty);

                        tipSessions = new List<TipSession>
                        {
                            new TipSession() { TipSessionId = tipSessionId, ChangeIdList = new List<string>() }
                        };

                        telemetryContext.AddContext("explicitTipSession", true);
                        telemetryContext.AddContext(nameof(tipSessionId), tipSessionId);
                    }
                    else
                    {
                        tipSessions = await this.GetTipSessionsAsync(context, component, cancellationToken).ConfigureDefaults();
                    }

                    // if there are no TiP sessions to delete, completed the 
                    // provider execution with success.
                    if (tipSessions?.Any() != true)
                    {
                        result = new ExecutionResult(ExecutionStatus.Succeeded);
                    }
                    else
                    {
                        if (!state.TipDeletionStarted)
                        {
                            await this.RequestTipSessionDeletionAsync(context, component, state, tipSessions, telemetryContext, cancellationToken)
                                .ConfigureDefaults();

                            await this.SaveStateAsync<DeleteTipSessionState>(context, state, cancellationToken).ConfigureDefaults();

                            IEnumerable<EnvironmentCandidate> tipNodeIds = tipSessions.Select(t => new EnvironmentCandidate(node: t.NodeId, cluster: t.ClusterName));
                            await this.environmentClient.DeleteReservedNodesAsync(new ReservedNodes(tipNodeIds), cancellationToken)
                                .ConfigureDefaults();
                        }
                        else if (state.TipDeletionStarted)
                        {
                            result = await this.VerifyTipSessionDeletionAsync(context, state, telemetryContext, cancellationToken)
                                .ConfigureDefaults();
                        }
                    }
                }
            }

            return result;
        }

        private Task SaveTipSessionsAsync(IList<TipSession> tipSessions, ExperimentContext context, ExperimentComponent component, CancellationToken cancellationToken)
        {
            IEnumerable<EnvironmentEntity> tipEntities = TipSession.ToEnvironmentEntities(tipSessions, component.Group);
            return this.UpdateEntitiesProvisionedAsync(context, tipEntities, cancellationToken);
        }

        private async Task<IList<TipSession>> GetTipSessionsAsync(ExperimentContext context, ExperimentComponent component, CancellationToken cancellationToken)
        {
            IList<TipSession> tipSessions = new List<TipSession>();
            IEnumerable<EnvironmentEntity> entitiesProvisioned = await this.GetEntitiesProvisionedAsync(context, cancellationToken)
                .ConfigureDefaults();

            if (entitiesProvisioned?.Any() == true)
            {
                IEnumerable<EnvironmentEntity> tipEntities = entitiesProvisioned.GetEntities(EntityType.TipSession, component.Group).ToList();
                if (tipEntities?.Any() == true)
                {
                    tipSessions = new List<TipSession>(tipEntities.Select(entity => TipSession.FromEnvironmentEntity(entity)));
                }
            }

            return tipSessions;
        }

        private async Task RequestTipSessionDeletionAsync(ExperimentContext context, ExperimentComponent component, DeleteTipSessionState state, IList<TipSession> tipSessions, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            state.TipChangeIdSet = new Dictionary<string, string>();
            foreach (TipSession session in tipSessions)
            {
                EventContext relatedContext = telemetryContext.Clone()
                    .AddContext("experimentGroup", context.ExperimentStep.ExperimentGroup)
                    .AddContext(session);

                await this.Logger.LogTelemetryAsync($"{nameof(TipCleanupProvider)}.RequestTipSessionDeletion", relatedContext, async () =>
                {
                    TipNodeSessionChange updatedSession = await this.tipClient.DeleteTipSessionAsync(session.TipSessionId, cancellationToken)
                        .ConfigureAwait(false);

                    if (updatedSession.TipNodeSessionChangeId.IsNullOrEmpty())
                    {
                        throw new Contracts.ProviderException($"Tip Node Session Change Id value is null or empty. Tip Session: {session.TipSessionId}");
                    }

                    session.ChangeIdList.Add(updatedSession.TipNodeSessionChangeId);
                    state.TipChangeIdSet.Add(session.TipSessionId, updatedSession.TipNodeSessionChangeId);
                }).ConfigureDefaults();
            }

            await this.SaveTipSessionsAsync(tipSessions, context, component, cancellationToken).ConfigureDefaults();
            state.TipDeletionStarted = true;
        }

        [SuppressMessage("Naming", "AZCA1004:AsyncResultProhibited Rule", Justification = "False positive. TiP node session change details object has a property named 'Result'.")]
        private async Task<ExecutionResult> VerifyTipSessionDeletionAsync(ExperimentContext context, DeleteTipSessionState state, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            IList<string> tipChangeResults = new List<string>();
            ExecutionResult executionResult = new ExecutionResult(ExecutionStatus.InProgress);

            foreach (string tipSessionId in state.TipChangeIdSet.Keys)
            {
                EventContext relatedContext = telemetryContext.Clone()
                    .AddContext("experimentGroup", context.ExperimentStep.ExperimentGroup)
                    .AddContext(nameof(tipSessionId), tipSessionId);

                await this.Logger.LogTelemetryAsync($"{nameof(TipCleanupProvider)}.VerifyTipSessionDeleted", relatedContext, async () =>
                {
                    TipNodeSessionChangeDetails updatedSessionDetails = await this.tipClient.GetTipSessionChangeAsync(tipSessionId, state.TipChangeIdSet[tipSessionId], cancellationToken)
                        .ConfigureDefaults();

                    tipChangeResults.Add(updatedSessionDetails.Result.ToString());
                    relatedContext.AddContext(updatedSessionDetails);
                }).ConfigureDefaults();
            }

            if (tipChangeResults.Any(r => r.ToString() == "Failed"))
            {
                executionResult = new ExecutionResult(ExecutionStatus.Failed);
            }
            else if (tipChangeResults.All(r => r.ToString() == "Succeeded"))
            {
                executionResult = new ExecutionResult(ExecutionStatus.Succeeded);
            }

            return executionResult;
        }

        internal class DeleteTipSessionState
        {
            /// <summary>
            /// denotes delete tipsession is triggered.
            /// </summary>
            public bool TipDeletionStarted { get; set; }

            /// <summary>
            /// denotes pair of Tipsessionid,Tipsessionchangeid.
            /// </summary>
            public Dictionary<string, string> TipChangeIdSet { get; set; }
        }

        /// <summary>
        /// Parameters class defines the keys that are expected from the user
        /// </summary>
        internal class Parameters
        {
            internal const string TipSessionId = "tipSessionId";
        }
    }
}