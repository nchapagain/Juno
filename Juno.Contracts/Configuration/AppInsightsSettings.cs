namespace Juno.Contracts.Configuration
{
    /// <summary>
    /// Represents strongly-typed configuration settings for an Azure
    /// Application Insights resource.
    /// </summary>
    public class AppInsightsSettings : SettingsBase
    {
        /// <summary>
        /// Gets or sets the Azure Application Insights instrumentation
        /// key setting.
        /// </summary>
        public string InstrumentationKey { get; set; }
    }
}
