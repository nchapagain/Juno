namespace Juno.Contracts.Configuration
{
    using System;

    /// <summary>
    /// Represents settings for an Azure Cosmos DB resource.
    /// </summary>
    public class CosmosSettings : SettingsBase
    {
        /// <summary>
        /// Gets the Cosmos DB URI.
        /// </summary>
        public Uri Uri { get; set; }

        /// <summary>
        /// Gets the Cosmos DB account key.
        /// </summary>
        public string AccountKey { get; set; }
    }
}
