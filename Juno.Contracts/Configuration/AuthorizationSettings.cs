namespace Juno.Contracts.Configuration
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents Authorization settings for Experiments API
    /// </summary>
    public class AuthorizationSettings : SettingsBase
    {
        /// <summary>
        /// Security Group Id.
        /// </summary>
        public string GroupId { get; set; }

        /// <summary>
        /// Security Group Name.
        /// </summary>
        public string Group { get; set; }

        /// <summary>
        /// Name of the authorization Policy.
        /// </summary>
        public string Policy { get; set; }
    }
}