namespace Juno.Execution.NuGetIntegration
{
    using System.Threading;
    using System.Threading.Tasks;
    using NuGet.Versioning;

    /// <summary>
    /// Provides functionality for downloading NuGet packages.
    /// </summary>
    public interface INuGetPackageInstaller
    {
        /// <summary>
        /// Installs the NuGet package 
        /// </summary>
        /// <param name="packageInfo">Provides information about the target NuGet package and source feed.</param>
        /// <param name="cancellationToken">Allows the package download/installation to be cancelled.</param>
        /// <returns>
        /// NuGet version that's been installed.
        /// </returns>
        Task<NuGetVersion> InstallPackageAsync(NuGetPackageInfo packageInfo, CancellationToken cancellationToken);
    }
}
