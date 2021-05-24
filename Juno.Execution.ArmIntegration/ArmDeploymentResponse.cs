namespace Juno.Execution.ArmIntegration
{
    using System.Collections.Generic;
    using System.Linq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    /// <summary>
    /// Encapsulate deployment information for status code 200 or 201
    /// https://docs.microsoft.com/en-us/rest/api/resources/deployments/createorupdate#deploymentextended
    /// </summary>
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class ArmDeploymentResponse
    {
        /// <summary>
        /// Get or set the ID of the deployment.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Get or set the name of the deployment.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Get or set the location of the deployment.
        /// </summary>
        public string Location { get; set; }

        /// <summary>
        /// Get or set deployment properties.
        /// </summary>
        public DeploymentProperties Properties { get; set; }
    }

    /// <summary>
    /// Deployment properties with additional details.
    /// https://docs.microsoft.com/en-us/rest/api/resources/deployments/get#deploymentextended
    /// </summary>
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class DeploymentProperties
    {
        /// <summary>
        /// Get or set the hash produced for the template.
        /// </summary>
        public string TemplateHash { get; set; }

        /// <summary>
        /// Get or set the deployment mode. Possible values are Incremental and Complete.
        /// </summary>
        public string Mode { get; set; }

        /// <summary>
        /// Get or set the state of the provisioning.
        /// </summary>
        public string ProvisioningState { get; set; }

        /// <summary>
        /// Get or set the timestamp of the template deployment.
        /// </summary>
        public string Timestamp { get; set; }

        /// <summary>
        /// Get or set the correlation ID of the deployment.
        /// </summary>
        public string CorrelationId { get; set; }

        /// <summary>
        /// Get or set the duration of the template deployment.
        /// </summary>
        public string Duration { get; set; }

        /// <summary>
        /// Get or set the deployment error.
        /// </summary>
        public ErrorResponse Error { get; set; }
    }

    /// <summary>
    /// The resource management error response.
    /// https://docs.microsoft.com/en-us/rest/api/resources/deployments/get#erroradditionalinfo
    /// </summary>
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class ErrorResponse
    {
        /// <summary>
        /// Get or set the error code.
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// Get or set the error message.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Get or set the error message.
        /// </summary>
        public List<ArmErrorDetails> Details { get; set; }

        /// <summary>
        /// Get or set the error target.
        /// </summary>
        public string Target { get; set; }

        /// <summary>
        /// Converts the error response to string format
        /// </summary>
        /// <returns>Returns the error response</returns>
        public override string ToString()
        {
            return $"Code:{this.Code}, Message:{this.Message}, Details:{string.Join(",", this.Details)}";
        }
    }

    /// <summary>
    /// The resource management error response.
    /// https://docs.microsoft.com/en-us/rest/api/resources/deployments/get#erroradditionalinfo
    /// </summary>
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class ArmErrorDetails
    {
        /// <summary>
        /// Get or set the error code.
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// Get or set the error message.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Converts the error response to string format
        /// </summary>
        /// <returns>Returns the error response</returns>
        public override string ToString()
        {
            return $"Code:{this.Code}, Message:{this.Message}";
        }
    }

    /// <summary>
    /// Encapsulate cloud error if deployment is not extended.
    /// https://docs.microsoft.com/en-us/rest/api/resources/deployments/createorupdate#clouderror
    /// </summary>
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class CloudError
    {
        /// <summary>
        /// Get or set error. <see cref="ErrorResponse"/>
        /// </summary>
        public ErrorResponse Error { get; set; }
    }

    /// <summary>
    /// Encapsulate create or update resource group arm response
    /// https://docs.microsoft.com/en-us/rest/api/resources/resourcegroups/createorupdate#resourcegroup
    /// </summary>
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class CreateResourceGroupResponse
    {
        /// <summary>
        /// Get or the Id of the resource group.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Get or set the location of the resource group. 
        /// It cannot be changed after the resource group has been created. It must be one of the supported Azure locations.
        /// </summary>
        public string Location { get; set; }

        /// <summary>
        /// Get or set the ID of the resource that manages this resource group.
        /// </summary>
        public string ManagedBy { get; set; }

        /// <summary>
        /// Get or set the name of the resource group.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Get or set the type of the resource group.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Get or the resource group properties.
        /// </summary>
        public CreateResourceGroupProperties Properties { get; set; }
    }

    /// <summary>
    ///  Encapsulate create or update resource group response properties
    ///  https://docs.microsoft.com/en-us/rest/api/resources/resourcegroups/createorupdate#resourcegroupproperties
    /// </summary>
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class CreateResourceGroupProperties
    {
        /// <summary>
        /// Get or set the provisioning state.
        /// </summary>
        public string ProvisioningState { get; set; }
    }
}
