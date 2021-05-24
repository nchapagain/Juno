namespace Juno.Contracts.Configuration
{
    using System;

    /// <summary>
    /// Represents strongly-typed configuration settings for installers/bootstrappers
    /// used to install agents.
    /// </summary>
    public class InstallerSettings : SettingsBase
    {
        /// <summary>
        /// Gets or sets the URI to the installer resource.
        /// </summary>
        public Uri Uri { get; set; }
    }
}
