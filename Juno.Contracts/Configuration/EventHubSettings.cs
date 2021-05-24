namespace Juno.Contracts.Configuration
{
    /// <summary>
    /// Represents strongly-typed configuration settings for an Azure Event Hub resource.
    /// </summary>
    public class EventHubSettings : SettingsBase
    {
        /// <summary>
        /// Gets or sets the Azure Event Hub connection string setting.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Gets or sets the name of the Event Hub to which events/messages
        /// should be written.
        /// </summary>
        public string EventHub { get; set; }
    }
}
