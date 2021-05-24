namespace Juno.Execution.ArmIntegration
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    /// <summary>
    /// Encapsulate Os Image reference properties.
    /// https://docs.microsoft.com/en-us/azure/templates/Microsoft.Compute/2018-10-01/virtualmachines#ImageReference
    /// </summary>
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class SigImageReference
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImageReference"/> class.
        /// </summary>
        /// <param name="id">The image resource Id.</param>
        [JsonConstructor]
        public SigImageReference(string id)
        {
            this.Id = id;
        }

        /// <summary>
        /// Get or set the resource Id
        /// </summary>
        [JsonProperty(PropertyName = "id", Required = Required.Always)]
        public string Id { get; private set; }
    }
}
