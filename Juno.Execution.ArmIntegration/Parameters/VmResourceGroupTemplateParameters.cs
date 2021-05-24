namespace Juno.Execution.ArmIntegration.Parameters
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    /// <summary>
    /// Encapsulate resource group template parameters
    /// </summary>
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class VmResourceGroupTemplateParameters : TemplateParameters
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="VmResourceGroupTemplateParameters"/> class.
        /// </summary>
        /// <param name="location">Location aka. region </param>
        /// <param name="resourceGroupName">Resource group name</param>
        /// <param name="keyVaultName"> Key vault name</param>
        /// <param name="subnetName">Subnet name</param>
        /// <param name="networkSecurityGroupName">Network security group name</param>
        /// <param name="virtualNetworkName">Virtual network name</param>
        /// <param name="executionServicePrincipalObjectId">Specifies the object ID of the Juno EOS to access the keyvault.
        /// The object ID must be unique for the list of access policies. 
        /// Note: servicePrincipalObjectId!= servicePrincipalId. This value can obtained in portal by navigating to Enterprise Application level of service principal </param>
        /// <param name="guestAgentPrincipalObjectId">Specifies the object ID of the Juno Guest Agent to access the keyvault.</param>
        public VmResourceGroupTemplateParameters(
            ParameterValue<string> location,
            ParameterValue<string> resourceGroupName,
            ParameterValue<string> keyVaultName,
            ParameterValue<string> subnetName,
            ParameterValue<string> networkSecurityGroupName,
            ParameterValue<string> virtualNetworkName,
            ParameterValue<string> executionServicePrincipalObjectId,
            ParameterValue<string> guestAgentPrincipalObjectId)
            : base(location)
        {
            this.ResourceGroupName = resourceGroupName;
            this.KeyVaultName = keyVaultName;
            this.SubnetName = subnetName;
            this.NetworkSecurityGroupName = networkSecurityGroupName;
            this.VirtualNetworkName = virtualNetworkName;
            this.ExecutionServicePrincipalObjectId = executionServicePrincipalObjectId;
            this.GuestAgentPrincipalObjectId = guestAgentPrincipalObjectId;
        }

        /// <summary>
        /// Get resource group name
        /// </summary>
        public ParameterValue<string> ResourceGroupName { get; private set; }

        /// <summary>
        /// Get key vault name
        /// </summary>
        public ParameterValue<string> KeyVaultName { get; private set; }

        /// <summary>
        ///  Get subnet name
        /// </summary>
        public ParameterValue<string> SubnetName { get; private set; }

        /// <summary>
        /// Get network security group name
        /// </summary>
        public ParameterValue<string> NetworkSecurityGroupName { get; private set; }

        /// <summary>
        /// Get virtual network name
        /// </summary>
        public ParameterValue<string> VirtualNetworkName { get; private set; }

        /// <summary>
        /// Get objectid 
        /// </summary>
        [JsonProperty(PropertyName = "eosPrincipalObjectId")]
        public ParameterValue<string> ExecutionServicePrincipalObjectId { get; private set; }

        /// <summary>
        /// Get objectid 
        /// </summary>
        [JsonProperty(PropertyName = "gaPrincipalObjectId")]
        public ParameterValue<string> GuestAgentPrincipalObjectId { get; private set; }
    }
}