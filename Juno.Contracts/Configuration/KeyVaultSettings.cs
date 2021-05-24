namespace Juno.Contracts.Configuration
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents strongly-typed configuration settings for an Azure
    /// Key Vault resource.
    /// </summary>
    public class KeyVaultSettings : SettingsBase
    {
        /// <summary>
        /// Gets or sets the URI to the Azure Key Vault.
        /// </summary>
        public Uri Uri { get; set; }

        /// <summary>
        /// Gets the Azure Active Directory (AAD) principals used to authenticate
        /// and authorize with the Key Vault.
        /// </summary>
        public IEnumerable<AadPrincipalSettings> AadPrincipals { get; set; }
    }
}
