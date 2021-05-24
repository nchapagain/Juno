namespace Juno.Execution.NuGetIntegration
{
    using System;
    using Microsoft.Azure.CRC.Extensions;

    /// <summary>
    /// Encapsulate NuGet package information including access token and location to install. This not a contract 
    /// and intended to be used for runtime only 
    /// </summary>
    public class NuGetPackageInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NuGetPackageInfo"/> class.
        /// </summary>
        /// <param name="feedUri">feed uri</param>
        /// <param name="packageName">package name</param>
        /// <param name="packageVersion">package version, support "latest"</param>
        /// <param name="userName">user name</param>
        /// <param name="password">password</param>
        /// <param name="downloadPath">Location to install the NuGet package</param>
        public NuGetPackageInfo(Uri feedUri, string packageName, string packageVersion, string downloadPath, string userName = null, string password = null)
        {
            feedUri.ThrowIfNull(nameof(feedUri));
            packageName.ThrowIfNullOrWhiteSpace(nameof(packageName));
            packageVersion.ThrowIfNullOrWhiteSpace(nameof(packageVersion));
            downloadPath.ThrowIfNullOrWhiteSpace(nameof(downloadPath));
            this.FeedUri = feedUri;
            this.PackageName = packageName;
            this.PackageVersion = packageVersion;
            this.UserName = userName;
            this.Password = password;
            this.DownloadPath = downloadPath;
        }

        /// <summary>
        /// Get feed uri
        /// </summary>
        public Uri FeedUri { get; private set; }

        /// <summary>
        /// Get package name
        /// </summary>
        public string PackageName { get; private set; }

        /// <summary>
        /// Get package version
        /// </summary>
        public string PackageVersion { get; private set; }

        /// <summary>
        /// Get user name
        /// </summary>
        public string UserName { get; private set; }

        /// <summary>
        /// Get password
        /// </summary>
        public string Password { get; private set; }

        /// <summary>
        /// Get download Path
        /// </summary>
        public string DownloadPath { get; private set; }
    }
}
