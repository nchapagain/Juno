namespace Juno.Execution.ArmIntegration
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    /// <summary>
    /// Encapsulate Os Image reference properties.
    /// https://docs.microsoft.com/en-us/azure/templates/Microsoft.Compute/2018-10-01/virtualmachines#ImageReference
    /// </summary>
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class ImageReference
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImageReference"/> class.
        /// </summary>
        /// <param name="publisher">The image publisher.</param>
        /// <param name="offer">Specifies the offer of the platform image or marketplace image used to create the virtual machine.</param>
        /// <param name="sku">The image SKU.</param>
        /// <param name="version">Specifies the version of the platform image or marketplace image used to create the virtual machine.</param>
        public ImageReference(string publisher, string offer, string sku, string version = null)
        {
            this.Publisher = publisher;
            this.Offer = offer;
            this.Sku = sku;
            this.Version = version ?? "latest";
        }

        /// <summary>
        /// Get or set publisher
        /// </summary>
        public string Publisher { get; private set; }

        /// <summary>
        /// Get or set offer
        /// </summary>
        public string Offer { get; private set; }

        /// <summary>
        /// Get or set sku
        /// </summary>
        public string Sku { get; private set; }

        /// <summary>
        /// Get or set version
        /// </summary>
        public string Version { get; private set; }
    }
}
