namespace Juno.Contracts.Configuration
{
    using System;

    /// <summary>
    /// Represents an API/service endpoint.
    /// </summary>
    public class ApiSettings : SettingsBase
    {
        /// <summary>
        /// Gets or sets the URI to the API endpoint/service.
        /// </summary>
        public Uri Uri { get; set; }
    }
}
