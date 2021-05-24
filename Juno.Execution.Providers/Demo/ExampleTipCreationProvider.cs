namespace Juno.Execution.Providers.Demo
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Execution.TipIntegration;
    using Juno.Extensions.Telemetry;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Example provider to demo the TiP session creation behavior.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.EnvironmentSetup, SupportedStepTarget.ExecuteRemotely)]
    public class ExampleTipCreationProvider : ExperimentProvider
    {
        private Random random;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExampleTipCreationProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public ExampleTipCreationProvider(IServiceCollection services)
            : base(services)
        {
            this.random = new Random();
        }

        /// <inheritdoc />
        protected override async Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            State state = await this.GetStateAsync<State>(context, cancellationToken, sharedState: false).ConfigureDefaults()
              ?? new State
              {
                  MaxAttempts = this.random.Next(5, 10)
              };

            ExecutionResult result = new ExecutionResult(ExecutionStatus.InProgress);

            if (!state.RequestMade)
            {
                // 1) Get the entity pool. In a typical experiment, one or more environment criteria providers must
                //    have selected a set of entities (e.g. clusters) that meet the criteria of the experiment.
                IEnumerable<EnvironmentEntity> entityPool = await this.GetEntityPoolAsync(context, cancellationToken).ConfigureDefaults();
                telemetryContext.AddContext(nameof(entityPool), entityPool);

                if (entityPool?.Any() != true)
                {
                    throw new ProviderException(
                        "The experiment does not have any clusters selected. This provider cannot determine the " +
                        "targets on which to acquire a TiP session.",
                        ErrorReason.DataNotFound);
                }

                IEnumerable<EnvironmentEntity> availableEntities = entityPool.GetEntities(EntityType.Node);

                if (availableEntities?.Any() != true)
                {
                    throw new ProviderException(
                        $"The experiment entity pool does not have matching clusters for the target group '{context.ExperimentStep.ExperimentGroup}'",
                        ErrorReason.DataNotFound);
                }

                IEnumerable<EnvironmentEntity> entitiesProvisioned = await this.GetEntitiesProvisionedAsync(context, cancellationToken).ConfigureDefaults();

                // 2) Mimic the attempt to establish a TiP session on a node in the target cluster.
                IEnumerable<EnvironmentEntity> entitiesAcquired = this.AcquireTiPSession(entityPool);

                List<EnvironmentEntity> updatedEntitiesProvisioned = new List<EnvironmentEntity>();
                if (entitiesProvisioned != null)
                {
                    updatedEntitiesProvisioned.AddRange(entitiesProvisioned);
                }

                foreach (EnvironmentEntity entity in entitiesAcquired)
                {
                    if (!updatedEntitiesProvisioned.Contains(entity))
                    {
                        updatedEntitiesProvisioned.Add(entity);
                    }
                }

                telemetryContext.AddContext(nameof(entitiesProvisioned), updatedEntitiesProvisioned);

                await this.SaveEntitiesProvisionedAsync(context, updatedEntitiesProvisioned, cancellationToken).ConfigureDefaults();
                state.RequestMade = true;

                await this.SaveStateAsync(context, state, cancellationToken, sharedState: false).ConfigureDefaults();
            }
            else if (context.ExperimentStep.Attempts > state.MaxAttempts)
            {
                result = new ExecutionResult(ExecutionStatus.Succeeded);
            }

            return result;
        }

        private IEnumerable<EnvironmentEntity> AcquireTiPSession(IEnumerable<EnvironmentEntity> entityPool)
        {
            List<EnvironmentEntity> entitiesAcquired = new List<EnvironmentEntity>();
            IEnumerable<EnvironmentEntity> nodes = entityPool.GetEntities(EntityType.Node);

            if (nodes?.Any() == true)
            {
                IEnumerable<string> environmentGroups = nodes.Select(node => node.EnvironmentGroup).Distinct();
                foreach (string group in environmentGroups)
                {
                    EnvironmentEntity relatedNode = nodes.GetEntities(EntityType.Node, group)
                        .FirstOrDefault();

                    if (relatedNode != null)
                    {
                        EnvironmentEntity tipSession = EnvironmentEntity.TipSession(Guid.NewGuid().ToString(), relatedNode.EnvironmentGroup, relatedNode.Metadata);
                        tipSession.Metadata["TipSessionId"] = tipSession.Id;
                        tipSession.Metadata["TipSessionNodeId"] = relatedNode.Id;
                        tipSession.Metadata["TipSessionRequestChangeId"] = Guid.NewGuid().ToString();
                        tipSession.Metadata["TipSessionStatus"] = "Created";
                        tipSession.Metadata["TipSessionRequestedTime"] = DateTime.UtcNow;
                        tipSession.Metadata["TipSessionCreatedTime"] = DateTime.UtcNow;
                        tipSession.Metadata["TipSessionDeletedTime"] = DateTime.MaxValue;
                        tipSession.Metadata["TipSessionExpirationTime"] = DateTime.UtcNow.AddDays(2);

                        entitiesAcquired.Add(relatedNode);
                        entitiesAcquired.Add(tipSession);
                    }
                }
            }

            return entitiesAcquired;
        }

        internal class State
        {
            public int MaxAttempts { get; set; }

            public bool RequestMade { get; set; }
        }
    }
}
