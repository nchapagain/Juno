namespace Juno.Execution.ArmIntegration
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides methods for managing resources in Azure subscriptions via the
    /// Azure Resource Manager (ARM) service.
    /// </summary>
    public interface IArmResourceManager : IDisposable
    {
        /// <summary>
        /// Requests bootstrap resource deployment (e.g. installation of Juno Guest Agent) on the
        /// target virtual machine(s).
        /// </summary>
        /// <param name="resourceGroup">Defines the specifics of the virtual machine resource group.</param>
        /// <param name="installerUri">URI to the Guest Agent installer.</param>
        /// <param name="cancellationToken">A token that can be used to request cancellation of this operations.</param>
        /// <param name="packageVersion">Guest agent package version</param>
        /// <returns>
        /// Updated <see cref="VmResourceGroupDefinition"/> with new state and deployment information for the target 
        /// virtual machine(s).
        /// </returns>
        Task<VmResourceGroupDefinition> BootstrapVirtualMachinesAsync(VmResourceGroupDefinition resourceGroup, Uri installerUri, CancellationToken cancellationToken, string packageVersion = null);

        /// <summary>
        /// Deletes the resource group (and all resources within) for virtual machine(s) defined.
        /// </summary>
        /// <param name="resourceGroup">Defines the specifics of the virtual machine resource group.</param>
        /// <param name="cancellationToken">A token that can be used to request cancellation of this operations.</param>
        /// <param name="forceDelete">Should we force a delete even if previous delete has not completed?</param>
        /// <returns>Deployment state</returns>
        Task DeleteResourceGroupAsync(VmResourceGroupDefinition resourceGroup, CancellationToken cancellationToken, bool forceDelete = false);

        /// <summary>
        /// Deletes the virtual machines in the resource group, but not the shared resources.
        /// </summary>
        /// <param name="resourceGroup">Defines the specifics of the virtual machine resource group.</param>
        /// <param name="cancellationToken">A token that can be used to request cancellation of this operations.</param>
        /// <returns>Deployment state</returns>
        Task DeleteVirtualMachinesAsync(VmResourceGroupDefinition resourceGroup, CancellationToken cancellationToken);

        /// <summary>
        /// Refresh the state of the virtual machines in the resource group.
        /// </summary>
        /// <param name="resourceGroup">Resource group definition</param>
        /// <param name="cancellationToken">A token that can be used to request cancellation of this operations.</param>
        /// <param name="refreshAll">Whether to force refresh all the virtual machines in the resource group. Will skip terminal state VM if set to false.</param>
        /// <returns>Updated <see cref="VmResourceGroupDefinition"/> with new state and deployment id for each shared resources and virtual machines</returns>
        Task<VmResourceGroupDefinition> RefreshVirtualMachinesStateAsync(VmResourceGroupDefinition resourceGroup, CancellationToken cancellationToken, bool refreshAll = true);

        /// <summary>
        /// Refresh the state of the resource group.
        /// </summary>
        /// <param name="resourceGroup">Resource group definition</param>
        /// <param name="cancellationToken">A token that can be used to request cancellation of this operations.</param>
        /// <returns>Updated <see cref="VmResourceGroupDefinition"/> with updated state for resource group.</returns>
        Task<VmResourceGroupDefinition> RefreshResourceGroupStateAsync(VmResourceGroupDefinition resourceGroup, CancellationToken cancellationToken);

        /// <summary>
        /// Requests the deployment of Virtual Machine(s) into a resource group along with required supporting resources
        /// (e.g. subnet, virtual network, key vault, network security group).
        /// </summary>
        /// <param name="resourceGroup">Defines the specifics of the virtual machine(s) to create/deploy.</param>
        /// <param name="cancellationToken">A token that can be used to request cancellation of this operations.</param>
        /// <returns>
        /// Updated <see cref="VmResourceGroupDefinition"/> with new state and deployment id for each shared resources and virtual machines.
        /// </returns>
        Task<VmResourceGroupDefinition> DeployResourceGroupAndVirtualMachinesAsync(VmResourceGroupDefinition resourceGroup, CancellationToken cancellationToken);
    }
}
