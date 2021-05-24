namespace Juno.Execution.Providers.Demo
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Example provider to illustrate the creation of virtual machines in the experiment environment
    /// group using the Azure Resource Manager (ARM) service. Note that this provider is for example only
    /// and does not actually interface with the ARM service nor does it create VMs.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.EnvironmentSetup, SupportedStepTarget.ExecuteRemotely)]
    [SupportedParameter(Name = Parameters.SubscriptionId, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.VmCount, Type = typeof(int), Required = true)]
    [SupportedParameter(Name = Parameters.VmSize, Type = typeof(string), Required = true)]
    public class ExampleArmVmProvider : ExperimentProvider
    {
        private Random random;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExampleArmVmProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public ExampleArmVmProvider(IServiceCollection services)
            : base(services)
        {
            this.random = new Random();
        }

        /// <inheritdoc />
        protected override async Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));

            ExecutionResult result = new ExecutionResult(ExecutionStatus.InProgress);

            ArmVmProviderState state = await this.GetStateAsync<ArmVmProviderState>(context, cancellationToken, sharedState: false).ConfigureDefaults()
                ?? new ArmVmProviderState
                {
                    MaxAttempts = this.random.Next(5, 10)
                };

            if (!state.VirtualMachineCreationRequested)
            {
                IEnumerable<EnvironmentEntity> entitiesProvisioned = await this.GetEntitiesProvisionedAsync(context, cancellationToken)
                    .ConfigureDefaults();

                if (entitiesProvisioned?.Any() != true)
                {
                    throw new ProviderException(
                        $"The experiment does does not have matching physical nodes/blades in the target group '{context.ExperimentStep.ExperimentGroup}' " +
                        $"to which VMs can be deployed.",
                        ErrorReason.DataNotFound);
                }

                // Imagine making a call to the ARM API service to request the creation of a resource group and a set of one or more
                // VMs inside of it. The process of creating a VM takes time to complete, so this is NOT an atomic operation. Below, you
                // can see that we have a step to verify the VM creation completed.
                IEnumerable<EnvironmentEntity> vmsRequested = await this.RequestVirtualMachineCreationAsync(context, component, entitiesProvisioned)
                    .ConfigureDefaults();

                state.VirtualMachines = vmsRequested;
                state.VirtualMachineCreationRequested = true;
                await this.SaveStateAsync(context, state, cancellationToken, sharedState: false).ConfigureDefaults();
            }
            else if (!state.VirtualMachineCreationVerified)
            {
                // Once a request to create VMs has been started, it will take time to actually create the VMs and get the up and running.
                // Imagine the provider continues to poll the ARM API service for the status of the VM creation. The ARM API service will
                // send statuses that indicate the creation is in progress until it either succeeds or fails at creating the VMs.
                if (await this.ConfirmVirtualMachineCreationAsync(context).ConfigureDefaults())
                {
                    state.VirtualMachineCreationVerified = true;
                    await this.SaveStateAsync(context, state, cancellationToken, sharedState: false).ConfigureDefaults();
                    await this.UpdateEntitiesProvisionedAsync(context, state.VirtualMachines, cancellationToken).ConfigureDefaults();
                }
            }
            else if (context.ExperimentStep.Attempts > state.MaxAttempts)
            {
                result = new ExecutionResult(ExecutionStatus.Succeeded);
            }

            result.Extension = TimeSpan.FromSeconds(30);
            return result;
        }

        private Task<bool> ConfirmVirtualMachineCreationAsync(ExperimentContext context)
        {
            return Task.FromResult(context.ExperimentStep.Attempts > 5);
        }

        private Task<IEnumerable<EnvironmentEntity>> RequestVirtualMachineCreationAsync(ExperimentContext context, ExperimentComponent component, IEnumerable<EnvironmentEntity> entitiesProvisioned)
        {
            string environmentGroup = context.ExperimentStep.ExperimentGroup;
            IEnumerable<EnvironmentEntity> tipSessions = entitiesProvisioned.GetEntities(EntityType.TipSession, environmentGroup);
            List<EnvironmentEntity> updatedEntitiesProvisioned = new List<EnvironmentEntity>(entitiesProvisioned);

            foreach (EnvironmentEntity tipSession in tipSessions)
            {
                for (int vmCount = 1; vmCount <= 2; vmCount++)
                {
                    string nodeId = tipSession.Metadata["NodeId"].ToString();

                    IDictionary<string, IConvertible> metadata = new Dictionary<string, IConvertible>(tipSession.Metadata);
                    metadata.Add("vmSku", component.Parameters.GetValue<string>(Parameters.VmSize));
                    metadata.Add("subscriptionId", component.Parameters.GetValue<string>(Parameters.SubscriptionId));

                    updatedEntitiesProvisioned.Add(EnvironmentEntity.VirtualMachine($"VM0{vmCount},{nodeId}", environmentGroup, metadata));
                }
            }

            return Task.FromResult(updatedEntitiesProvisioned as IEnumerable<EnvironmentEntity>);
        }

        internal class ArmVmProviderState
        {
            public int MaxAttempts { get; set; }

            public IEnumerable<EnvironmentEntity> VirtualMachines { get; set; }

            public bool VirtualMachineCreationRequested { get; set; }

            public bool VirtualMachineCreationVerified { get; set; }
        }

        private class Parameters
        {
            internal const string SubscriptionId = nameof(Parameters.SubscriptionId);
            internal const string VmCount = nameof(Parameters.VmCount);
            internal const string VmSize = nameof(Parameters.VmSize);
        }
    }
}
