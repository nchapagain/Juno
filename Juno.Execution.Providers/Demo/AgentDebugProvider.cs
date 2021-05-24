namespace Juno.Execution.Providers.Demo
{
    using System;
    using System.Collections.Generic;
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
    /// Provider enables the author of the experiment to define the agent ID of nodes of VMs that will
    /// be created in the entity pool for the experiment aiding in local debug scenarios on the developer
    /// machine.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.EnvironmentCriteria, SupportedStepTarget.ExecuteRemotely)]
    [SupportedParameter(Name = Parameters.UseAgentId, Type = typeof(string), Required = true)]
    public class AgentDebugProvider : ExperimentProvider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AgentDebugProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public AgentDebugProvider(IServiceCollection services)
            : base(services)
        {
        }

        /// <inheritdoc />
        protected override async Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            if (context.ExperimentStep.ExperimentGroup == ExperimentComponent.AllGroups)
            {
                throw new ProviderException($"The workflow step must have an experiment group defined that is not the global wildcard '*'.");
            }

            AgentIdentification useAgentId = new AgentIdentification(component.Parameters.GetValue<string>(Parameters.UseAgentId));
            IEnumerable<EnvironmentEntity> entityPool = null;

            if (!string.IsNullOrWhiteSpace(useAgentId.VirtualMachineName))
            {
                entityPool = this.CreateVMEntityPoolReferences(context, useAgentId);
            }
            else
            {
                entityPool = this.CreateNodeEntityPoolReferences(context, useAgentId);
            }

            telemetryContext.AddContext(nameof(entityPool), entityPool);
            await this.SaveEntityPoolAsync(context, entityPool, cancellationToken).ConfigureDefaults();
            await this.SaveEntitiesProvisionedAsync(context, entityPool, cancellationToken).ConfigureDefaults();

            return new ExecutionResult(ExecutionStatus.Succeeded);
        }

        private IEnumerable<EnvironmentEntity> CreateNodeEntityPoolReferences(ExperimentContext context, AgentIdentification agentId)
        {
            List<EnvironmentEntity> entities = new List<EnvironmentEntity>();

            // Format:
            // Cluster01,Node01
            // Cluster01,Node01,Tip01
            EnvironmentEntity node = EnvironmentEntity.Node(agentId.NodeName, context.ExperimentStep.ExperimentGroup);
            node.Metadata["RackLocation"] = $"{agentId.ClusterName}-Rack01";
            node.Metadata["MachinePoolName"] = $"{agentId.ClusterName}-Rack01-MP01";
            node.Metadata["Region"] = "East US 2";
            node.Metadata["ClusterName"] = agentId.ClusterName;
            node.Metadata["SupportedVmSkus"] = "Standard_VM_v2,Standard_VM_v3";
            node.Metadata["PreferredVmSku"] = "Standard_VM_v3";
            node.AgentId(agentId.ToString());

            entities.Add(node);

            return entities;
        }

        private IEnumerable<EnvironmentEntity> CreateVMEntityPoolReferences(ExperimentContext context, AgentIdentification agentId)
        {
            List<EnvironmentEntity> entities = new List<EnvironmentEntity>();

            // Format:
            // Cluster01,Node01,VM01
            // Cluster01,Node01,VM01,Tip01
            EnvironmentEntity vm = EnvironmentEntity.VirtualMachine(agentId.VirtualMachineName, context.ExperimentStep.ExperimentGroup);

            vm.Metadata["VmName"] = agentId.VirtualMachineName;
            vm.Metadata["VmSku"] = "Standard_VM_v3";
            vm.Metadata["DeploymentId"] = Guid.NewGuid().ToString();
            vm.Metadata["TipSessionId"] = Guid.NewGuid().ToString();
            vm.Metadata["Region"] = "East US 2";
            vm.Metadata["ClusterName"] = agentId.ClusterName;
            vm.Metadata["OsDiskSku"] = "Standard_LRS";
            vm.Metadata["DataDiskSku"] = "Standard_LRS";
            vm.Metadata["DataDiskCount"] = 1;
            vm.Metadata["DataDisks"] = "sku=Standard_LRS,lun=0,sizeInGB=32";
            vm.AgentId(agentId.ToString());

            entities.Add(vm);

            return entities;
        }

        private class Parameters
        {
            internal const string UseAgentId = "useAgentId";
        }
    }
}
