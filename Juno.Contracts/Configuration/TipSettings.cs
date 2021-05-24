namespace Juno.Contracts.Configuration
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents strongly-typed configuration settings for Tip client.
    /// </summary>
    public class TipSettings : SettingsBase
    {
        /// <summary>
        /// Gets the Azure Active Directory (AAD) principals used to authenticate
        /// and authorize with the tip service.
        /// </summary>
        public IEnumerable<AadPrincipalSettings> AadPrincipals { get; set; }

        /// <summary>
        /// Path of Pilot fish that boot strap host agent
        /// </summary>
        public string HostAgentBootstrapPilotfishPath { get; set; }

        /// <summary>
        /// Pilot fish that boot strap host agent
        /// </summary>
        public string HostAgentBootstrapPilotfishName { get; set; }
    }
}
