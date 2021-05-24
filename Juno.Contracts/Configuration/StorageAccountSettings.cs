namespace Juno.Contracts.Configuration
{
    using System;

    /// <summary>
    /// Represents settings for an Azure Storage account.
    /// </summary>
    public class StorageAccountSettings : SettingsBase
    {
        /// <summary>
        /// Gets or sets the Storage Account key.
        /// </summary>
        public string AccountKey { get; set; }

        /// <summary>
        /// Gets or sets the Storage Account queue endpoint URI.
        /// </summary>
        public Uri Uri { get; set; }
    }
}
