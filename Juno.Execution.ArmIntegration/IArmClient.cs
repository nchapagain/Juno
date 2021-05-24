namespace Juno.Execution.ArmIntegration
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Execution.ArmIntegration.Parameters;

    /// <summary>
    /// A client with methods for managing resources in Azure subscriptions via the
    /// Azure Resource Manager (ARM) service.
    /// </summary>
    public interface IArmClient
    {
        /// <summary>
        /// Create or update resource group
        /// </summary>
        /// <param name="subscriptionId">Subscription Id</param>
        /// <param name="resourceGroupName">Resource group name</param>
        /// <param name="location">Location .aka. region where to create resource group</param>
        /// <param name="cancellationToken">A token that can be used to request cancellation of this operations.</param>
        /// <param name="tags">Tags for the resource group</param>
        /// <returns>Http response message</returns>
        /// <returns></returns>
        Task<HttpResponseMessage> CreateResourceGroupAsync(string subscriptionId, string resourceGroupName, string location, CancellationToken cancellationToken, IDictionary<string, string> tags = null);

        /// <summary>
        /// Delete resource group
        /// https://docs.microsoft.com/en-us/rest/api/resources/resourcegroups/delete
        /// </summary>
        /// <param name="subscriptionId">Subscription Id</param>
        /// <param name="resourceGroupName">Resource group name</param>
        /// <param name="cancellationToken">A token that can be used to request cancellation of this operations.</param>
        /// <returns>Http response message</returns>
        Task<HttpResponseMessage> DeleteResourceGroupAsync(string subscriptionId, string resourceGroupName, CancellationToken cancellationToken);

        /// <summary>
        /// Delete a Virtual Machine.
        /// https://docs.microsoft.com/en-us/rest/api/resources/resources/delete
        /// </summary>
        /// <param name="subscriptionId">Subscription Id</param>
        /// <param name="resourceGroupName">Resource group name</param>
        /// <param name="virtualMachineName">Name of the virtual machine to delete.</param>
        /// <param name="cancellationToken">A token that can be used to request cancellation of this operations.</param>
        /// <returns>Http response message</returns>
        Task<HttpResponseMessage> DeleteVirtualMachineAsync(string subscriptionId, string resourceGroupName, string virtualMachineName, CancellationToken cancellationToken);

        /// <summary>
        /// Deploys resources at subscription scope. This mainly useful when there is no resource group
        /// so that we can create resource group. It is also possible to create resources inside the resource
        /// group as part of single deployment. The template need to be nested though.
        /// https://docs.microsoft.com/en-us/rest/api/resources/deployments/createorupdateatsubscriptionscope
        /// </summary>
        /// <param name="subscriptionId">Subscription id/target subscriptionId</param>
        /// <param name="deploymentName">Deployment name. It will part of by deployment Id if deployment is extended</param>
        /// <param name="templateJson">Template json string</param>
        /// <param name="parameters">Resource group template parameters</param>
        /// <param name="cancellationToken">A token that can be used to request cancellation of this operations.</param>
        /// <returns>Http response message</returns>
        Task<HttpResponseMessage> DeployAtSubscriptionScopeAsync(string subscriptionId, string deploymentName, string templateJson, VmResourceGroupTemplateParameters parameters, CancellationToken cancellationToken);

        /// <summary>
        /// Deploys resources to a resource group.
        /// https://docs.microsoft.com/en-us/rest/api/resources/deployments/createorupdate
        /// </summary>
        /// <param name="subscriptionId">Subscription id/target subscription</param>
        /// <param name="resourceGroupName">Resource group name</param>
        /// <param name="deploymentName">Deployment name. It will part of by deployment Id if deployment is extended</param>
        /// <param name="templateJson">Template json string</param>
        /// <param name="parameters">Template parameters</param>
        /// <param name="cancellationToken">A token that can be used to request cancellation of this operations.</param>
        /// <returns>Http response message</returns>
        Task<HttpResponseMessage> DeployAtResourceGroupScopeAsync(string subscriptionId, string resourceGroupName, string deploymentName, string templateJson, TemplateParameters parameters, CancellationToken cancellationToken);

        /// <summary>
        /// Returns the subscription-level activity logs applying the filter provided.
        /// </summary>
        /// <param name="subscriptionId">The subscription in which to get the activity logs.</param>
        /// <param name="filter">
        /// The filter to apply (e.g. eventTimestamp ge '2020-06-01T00:00:00.000Z' and resourceGroupName eq 'resourceGroupName')
        /// </param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="fields">Optional string parameter of comma separated values allows specific fields to be returned by the API (default is null).</param>
        /// <returns>
        /// A response containing one or more subscription-level activity logs matching the filter provided.
        /// </returns>
        Task<HttpResponseMessage> GetSubscriptionActivityLogsAsync(string subscriptionId, string filter, CancellationToken cancellationToken, IEnumerable<string> fields = null);

        /// <summary>
        /// Get deployment state at subscription scope
        /// </summary>
        /// <param name="deploymentId">Deployment id</param>
        /// <param name="cancellationToken">A token that can be used to request cancellation of this operations.</param>
        /// <returns>Http response message</returns>
        Task<HttpResponseMessage> GetSubscriptionScopeDeploymentStateAsync(string deploymentId, CancellationToken cancellationToken);

        /// <summary>
        /// Get deployment state at resource group scope.
        /// https://docs.microsoft.com/en-us/rest/api/resources/deployments/get
        /// </summary>
        /// <param name="deploymentId">Deployment id</param>
        /// <param name="cancellationToken">A token that can be used to request cancellation of this operations.</param>
        /// <returns>Http response message</returns>
        Task<HttpResponseMessage> GetResourceGroupScopeDeploymentStateAsync(string deploymentId, CancellationToken cancellationToken);

        /// <summary>
        /// Get a Virtual Machine information.
        /// https://docs.microsoft.com/en-us/rest/api/resources/resources/get
        /// </summary>
        /// <param name="subscriptionId">Subscription Id</param>
        /// <param name="resourceGroupName">Resource group name</param>
        /// <param name="virtualMachineName">Name of the virtual machine to delete.</param>
        /// <param name="cancellationToken">A token that can be used to request cancellation of this operations.</param>
        /// <returns>Http response message</returns>
        Task<HttpResponseMessage> GetVirtualMachineAsync(string subscriptionId, string resourceGroupName, string virtualMachineName, CancellationToken cancellationToken);

        /// <summary>
        /// Get a resource group information
        /// https://docs.microsoft.com/en-us/rest/api/resources/resourcegroups/get
        /// </summary>
        /// <param name="subscriptionId">Subscription Id</param>
        /// <param name="resourceGroupName">Resource group name</param>
        /// <param name="cancellationToken">A token that can be used to request cancellation of this operations.</param>
        /// <returns>Http response message</returns>
        Task<HttpResponseMessage> GetResourceGroupAsync(string subscriptionId, string resourceGroupName, CancellationToken cancellationToken);

        /// <summary>
        /// Head request to check if resource group exist
        /// https://docs.microsoft.com/en-us/rest/api/resources/resourcegroups/checkexistence
        /// </summary>
        /// <param name="subscriptionId">Subscription Id</param>
        /// <param name="resourceGroupName">Resource group name</param>
        /// <param name="cancellationToken">A token that can be used to request cancellation of this operations.</param>
        /// <returns>Http response message</returns>
        Task<HttpResponseMessage> HeadResourceGroupAsync(string subscriptionId, string resourceGroupName, CancellationToken cancellationToken);
    }
}