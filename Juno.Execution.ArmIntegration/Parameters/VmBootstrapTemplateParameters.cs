namespace Juno.Execution.ArmIntegration.Parameters
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    /// <summary>
    /// Encapsulate bootstrap template parameters
    /// </summary>
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class VmBootstrapTemplateParameters : TemplateParameters
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="VmBootstrapTemplateParameters"/> class.
        /// </summary>
        /// <param name="location">Location.aka. region</param>
        /// <param name="fileUri">The bootstap script uri</param>
        /// <param name="commandArguments">Command line argument to script</param>
        /// <param name="virtualMachineName">The virtual machine name in the group</param>
        public VmBootstrapTemplateParameters(
            ParameterValue<string> location,
            ParameterValue<string> fileUri,
            ParameterValue<string> commandArguments,
            ParameterValue<string> virtualMachineName)
            : base(location)
        {
            this.FileUri = fileUri;
            this.CommandArguments = commandArguments;
            this.VirtualMachineName = virtualMachineName;
        }

        /// <summary>
        /// Get bootstrap file uri
        /// </summary>
        public ParameterValue<string> FileUri { get; private set; }

        /// <summary>
        /// Get bootstrap command arguments
        /// </summary>
        [JsonProperty(PropertyName = "arguments")]
        public ParameterValue<string> CommandArguments { get; private set; }

        /// <summary>
        /// Get virtual machine name
        /// </summary>
        public ParameterValue<string> VirtualMachineName { get; private set; }
    }
}
