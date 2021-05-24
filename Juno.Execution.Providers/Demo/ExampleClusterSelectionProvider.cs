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
    /// Example provider to illustrate the cluster selection behavior. Note that this provider is for
    /// example only and does not actually query for live Azure cluster data. It does however place
    /// fake/mock environment entity (e.g. clusters, racks) in the context/state data for the experiment.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.EnvironmentCriteria, SupportedStepTarget.ExecuteRemotely)]
    [SupportedParameter(Name = Parameters.CpuId, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.VmSkus, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.Regions, Type = typeof(string), Required = false)]
    public class ExampleClusterSelectionProvider : ExperimentProvider
    {
        private Random random;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExampleClusterSelectionProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public ExampleClusterSelectionProvider(IServiceCollection services)
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
                    MaxAttempts = this.random.Next(2, 5)
                };

            ExecutionResult result = new ExecutionResult(ExecutionStatus.InProgress);

            if (!state.SelectionMade)
            {
                // 1) Mimic a search to find matching clusters.
                IEnumerable<EnvironmentEntity> entityPool = await this.ExecuteSearchQueryAsync(context).ConfigureDefaults();
                telemetryContext.AddContext(nameof(entityPool), entityPool);

                // 2) Create the pool of entities/clusters from that matching set. We call this the "entity pool". The
                //    entity pool is a set of entities that are available and that meet the criteria of the experiment.
                //    Not all of them will be used. In fact, only a small set of them will be used. When entities from 
                //    the entity pool are then used in the experiment, they are placed in the 'entities provisioned' set.
                //    Other providers can reference them from the 'entities provisioned' set to know which nodes, VMs
                //    etc.. for which the experiment is targeting.
                await this.SaveEntityPoolAsync(context, entityPool, cancellationToken).ConfigureDefaults();
                state.SelectionMade = true;

                await this.SaveStateAsync(context, state, cancellationToken, sharedState: false).ConfigureDefaults();
            }
            else if (context.ExperimentStep.Attempts > state.MaxAttempts)
            {
                result = new ExecutionResult(ExecutionStatus.Succeeded);
            }

            return result;
        }

        private async Task<IEnumerable<EnvironmentEntity>> ExecuteSearchQueryAsync(ExperimentContext context)
        {
            // Mimic the execution of the search query (e.g. to Kusto).
            await Task.Delay(5000).ConfigureDefaults();

            List<EnvironmentEntity> entities = new List<EnvironmentEntity>();
            IEnumerable<string> targetGroups = null;

            if (context.ExperimentStep.ExperimentGroup == ExperimentComponent.AllGroups)
            {
                targetGroups = context.GetExperimentGroups();
            }
            else
            {
                targetGroups = new List<string> { context.ExperimentStep.ExperimentGroup };
            }

            // Imagine the provider makes a call to a Kusto Cluster API. This provides a response that is tabular in format
            // (e.g. a DataTable) with results that indicate clusters with racks having nodes/blades that can meet the criteria
            // of the experiment environment/infrastructure and that are additionally available.
            targetGroups.ToList().ForEach(group =>
            {
                // Format:
                // Cluster01,Node01-A
                // Cluster01,Node01-A
                AgentIdentification agentId = new AgentIdentification("Cluster01", Guid.NewGuid().ToString());
                
                EnvironmentEntity node = node = EnvironmentEntity.Node(agentId.NodeName, group);
                node.Metadata["RackLocation"] = $"{agentId.ClusterName}-Rack01";
                node.Metadata["MachinePoolName"] = $"{agentId.ClusterName}-Rack01-MP01";
                node.Metadata["Region"] = "East US 2";
                node.Metadata["ClusterName"] = agentId.ClusterName;
                node.Metadata["SupportedVmSkus"] = "Standard_VM_v2,Standard_VM_v3";
                node.Metadata["PreferredVmSku"] = "Standard_VM_v3";
                node.AgentId(agentId.ToString());

                entities.Add(node);
            });

            return entities;
        }

        private class Parameters
        {
            internal const string CpuId = "cpuId";
            internal const string VmSkus = "vmSkus";
            internal const string Regions = "regions";
        }

        internal class State
        {
            public int MaxAttempts { get; set; }

            public bool SelectionMade { get; set; }
        }
    }
}
