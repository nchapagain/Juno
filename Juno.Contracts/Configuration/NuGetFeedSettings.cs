namespace Juno.Contracts.Configuration
{
    using System;

    /// <summary>
    /// Represents strongly-typed configuration settings for NuGet feeds.
    /// </summary>
    public class NuGetFeedSettings : SettingsBase
    {
        /// <summary>
        /// Gets or sets the URI to the NuGet feed.
        /// </summary>
        public Uri Uri { get; set; }

        /// <summary>
        /// Gets or sets an access token used to authenticate with the NuGet feed.
        /// </summary>
        public string AccessToken { get; set; }
    }
}
