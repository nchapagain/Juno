namespace Juno.Execution.NuGetIntegration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using NuGet.Configuration;
    using NuGet.Packaging.Core;
    using NuGet.Protocol.Core.Types;
    using NuGet.Versioning;
    using Polly;

    /// <summary>
    /// Provides methods for installing NuGet packages
    /// </summary>
    public class NuGetPackageInstaller : INuGetPackageInstaller
    {
        private static readonly IAsyncPolicy DefaultRetryPolicy = Policy.Handle<FatalProtocolException>()
            .WaitAndRetryAsync(10, (retries) => TimeSpan.FromSeconds(retries + 1));

        /// <summary>
        /// Initializes a new instance of the <see cref="NuGetPackageInstaller"/> class.
        /// </summary>
        public NuGetPackageInstaller(ILogger logger = null)
            : this(NuGetPackageInstaller.DefaultRetryPolicy)
        {
            this.Logger = logger ?? NullLogger.Instance;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NuGetPackageInstaller"/> class.
        /// </summary>
        /// <param name="retryPolicy">
        /// The policy to use for transient error retries when attempting to download the package from the
        /// NuGet feed.
        /// </param>
        public NuGetPackageInstaller(IAsyncPolicy retryPolicy)
        {
            retryPolicy.ThrowIfNull(nameof(retryPolicy));
            this.RetryPolicy = NuGetPackageInstaller.DefaultRetryPolicy;
        }

        /// <summary>
        /// Gets the policy to use for transient error retries when attempting to download the package from the
        /// NuGet feed.
        /// </summary>
        public IAsyncPolicy RetryPolicy { get; }

        /// <summary>
        /// Gets logger for the installer.
        /// </summary>
        public ILogger Logger { get; }

        /// <summary>
        /// Installs the NuGet package 
        /// </summary>
        /// <param name="packageInfo">Provides information about the target NuGet package and source feed.</param>
        /// <param name="cancellationToken">Allows the package download/installation to be cancelled.</param>
        /// <returns>
        /// A task that can be used to asynchronously install the package.
        /// </returns>
        public virtual async Task<NuGetVersion> InstallPackageAsync(NuGetPackageInfo packageInfo, CancellationToken cancellationToken)
        {
            packageInfo.ThrowIfNull(nameof(packageInfo));
            PackageSourceCredential credential = null;
            if (!string.IsNullOrWhiteSpace(packageInfo.UserName) && !string.IsNullOrWhiteSpace(packageInfo.Password))
            {
                credential = new PackageSourceCredential(packageInfo.FeedUri.AbsoluteUri, packageInfo.UserName, packageInfo.Password, true, null);
            }
  
            SourceRepository packageRepository = NuGetPackageInstaller.CreatePackageRepository(packageInfo.FeedUri, credential);

            NuGetVersion version = null;
            if (string.IsNullOrWhiteSpace(packageInfo.PackageVersion) || packageInfo.PackageVersion.Equals("latest", StringComparison.InvariantCultureIgnoreCase))
            {
                version = await NuGetPackageInstaller.GetLatestVersionAsync(packageInfo.PackageName, packageRepository, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                version = new NuGetVersion(packageInfo.PackageVersion);
            }

            PackageIdentity package = new PackageIdentity(packageInfo.PackageName, version);

            DownloadResourceResult result = await this.RetryPolicy.ExecuteAsync<DownloadResourceResult>(async () =>
            {
                return await NuGetPackageInstaller.DownloadPackageAsync(packageRepository, package, packageInfo.DownloadPath, cancellationToken)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false);

            if (result.Status == DownloadResourceResultStatus.NotFound)
            {
                throw new Exception($"The package '{packageInfo.PackageName},{packageInfo.PackageVersion}' does not exist on feed.");
            }

            return version;
        }

        private static SourceRepository CreatePackageRepository(Uri feedUri, PackageSourceCredential credential = null)
        {
            List<Lazy<INuGetResourceProvider>> providers = new List<Lazy<INuGetResourceProvider>>();
            providers.AddRange(Repository.Provider.GetCoreV3());

            PackageSource packageSource = new PackageSource(feedUri.AbsoluteUri);

            if (credential != null)
            {
                packageSource.Credentials = credential;
            }

            return new SourceRepository(packageSource, providers);
        }

        /// <summary>
        /// Downloads the NuGet package to the path provided.
        /// </summary>
        /// <param name="sourceRepository">The NuGet feed/source repository where the package exists.</param>
        /// <param name="package">The NuGet package identity (i.e. ID and version).</param>
        /// <param name="downloadPath">The path to which the NuGet package will be downloaded.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the package download operation.</param>
        /// <returns>
        /// A task that can be used to download the NuGet package.
        /// </returns>
        private static async Task<DownloadResourceResult> DownloadPackageAsync(SourceRepository sourceRepository, PackageIdentity package, string downloadPath, CancellationToken cancellationToken)
        {
            sourceRepository.ThrowIfNull(nameof(sourceRepository));
            using (var sourceCacheContext = new SourceCacheContext { DirectDownload = false, NoCache = false })
            {
                PackageDownloadContext downloadContext = new PackageDownloadContext(sourceCacheContext);
                DownloadResource downloadResource = await sourceRepository.GetResourceAsync<DownloadResource>().ConfigureAwait(false);

                return await downloadResource.GetDownloadResourceResultAsync(
                    package,
                    downloadContext,
                    downloadPath,
                    NuGet.Common.NullLogger.Instance,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        private static async Task<NuGetVersion> GetLatestVersionAsync(string packageName, SourceRepository sourceRepo, CancellationToken cancellationToken)
        {
            FindPackageByIdResource allPackages = await sourceRepo.GetResourceAsync<FindPackageByIdResource>(cancellationToken).ConfigureAwait(false);
            using (SourceCacheContext sourceCache = new SourceCacheContext())
            {
                IEnumerable<NuGetVersion> versions = await allPackages.GetAllVersionsAsync(
                    packageName,
                    sourceCache,
                    NuGet.Common.NullLogger.Instance,
                    cancellationToken).ConfigureAwait(false);

                return versions.Max();
            }
        }
    }
}
