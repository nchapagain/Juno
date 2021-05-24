namespace Juno.Contracts.Configuration
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents strongly-typed configuration settings for an Azure
    /// Kusto/Data Explorer (ADX) resource.
    /// </summary>
    public class KustoSettings : SettingsBase
    {
        /// <summary>
        /// Gets or sets the URI to the Kusto/ADX cluster.
        /// </summary>
        public Uri ClusterUri { get; set; }

        /// <summary>
        /// Gets or sets the Kusto/ADX cluster database name.
        /// </summary>
        public string ClusterDatabase { get; set; }

        /// <summary>
        /// Gets the Azure Active Directory (AAD) principals used to authenticate
        /// and authorize with the Kusto cluster.
        /// </summary>
        public IEnumerable<AadPrincipalSettings> AadPrincipals { get; set; }
    }
}
