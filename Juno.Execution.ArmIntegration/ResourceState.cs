namespace Juno.Execution.ArmIntegration
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Class that represents the state of an Azure resource.
    /// </summary>
    public class ResourceState
    {
        /// <summary>
        /// Provisioning state of the resource.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public ProvisioningState State { get; set; }

        /// <summary>
        /// Name of resource.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Any error associated with the state
        /// </summary>
        public ErrorResponse Error { get; set; }

        /// <summary>
        /// Name of the deployment action
        /// </summary>
        public string DeploymentName { get; set; }

        /// <summary>
        /// ID of the deployment action
        /// </summary>
        public string DeploymentId { get; set; }

        /// <summary>
        /// CorrelationId for the deployment request that created this VM
        /// This is used to debug the deployment internally in Azure
        /// </summary>
        public string CorrelationId { get; set; }

        /// <summary>
        /// Checks whether the resource was deployment successfully
        /// </summary>
        /// <returns>True if resource deployment was successful</returns>
        public bool IsSuccessful()
        {
            return this.State == ProvisioningState.Succeeded;
        }

        /// <summary>
        /// Checks whether the resource deployment is in terminal state
        /// </summary>
        /// <returns>Returns true if resource is successful, failed or deleted</returns>
        public bool IsDeploymentFinished()
        {
            return this.State == ProvisioningState.Succeeded || this.State == ProvisioningState.Failed || this.State == ProvisioningState.Deleted;
        }

        /// <summary>
        /// Checks whether the resource deployment in progress.
        /// </summary>
        /// <returns>Returns true if resource is accepted or creating</returns>
        public bool IsDeploymentInProgress()
        {
            return this.State == ProvisioningState.Accepted || this.State == ProvisioningState.Creating;
        }

        /// <summary>
        /// Checks whether the resource deployment is in terminal state
        /// </summary>
        /// <returns>Returns true if resource is successful, failed or deleted</returns>
        public bool FailedOrDeleted()
        {
            return this.State == ProvisioningState.Failed || this.State == ProvisioningState.Deleted;
        }
    }
}