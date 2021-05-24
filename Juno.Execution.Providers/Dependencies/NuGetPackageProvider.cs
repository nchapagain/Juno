namespace Juno.Execution.Providers.Dependencies
{
    using System;
    using System.Security;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Execution.NuGetIntegration;
    using Juno.Extensions.Telemetry;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Repository.KeyVault;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Provider is used to download a NuGet package dependency to a target
    /// location.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.Dependency, SupportedStepTarget.ExecuteOnVirtualMachine)]
    [SupportedParameter(Name = Parameters.FeedUri, Type = typeof(Uri), Required = true)]
    [SupportedParameter(Name = Parameters.PackageName, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.PackageVersion, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.PersonalAccessToken, Type = typeof(string))]
    [ProviderInfo(Name = "Download NuGet Package", Description = "Downloads a NuGet package dependency to the physical nodes/blades or VMs in the experiment group", FullDescription = "Step to install a NuGet package as a dependency of an existing experiment workflow step. Whereas, this step can exist in the experiment workflow, it is generally intended to be used as a direct dependency of an experiment workflow step vs. a standalone step in the workflow itself. This step can ONLY be run in the Juno Guest agent process on virtual machines in the environment that are part of an experiment group.")]
    public class NuGetPackageProvider : ExperimentProvider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NuGetPackageProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public NuGetPackageProvider(IServiceCollection services)
            : base(services)
        {
        }

        /// <inheritdoc />
        public override Task ConfigureServicesAsync(ExperimentContext context, ExperimentComponent component)
        {
            INuGetPackageInstaller packageInstaller;
            if (!this.Services.TryGetService<INuGetPackageInstaller>(out packageInstaller))
            {
                this.Services.AddSingleton<INuGetPackageInstaller>(new NuGetPackageInstaller());
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        protected override async Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));

            ExecutionResult result = new ExecutionResult(ExecutionStatus.Succeeded);

            if (!cancellationToken.IsCancellationRequested)
            {
                Uri packageFeedUri = new Uri(component.Parameters.GetValue<string>(Parameters.FeedUri));
                string packageVersion = component.Parameters.GetValue<string>(Parameters.PackageVersion);
                string packageName = component.Parameters.GetValue<string>(Parameters.PackageName);
                string personalAccessToken = component.Parameters.GetValue<string>(Parameters.PersonalAccessToken, string.Empty);
                string installationPath = DependencyPaths.NuGetPackages;

                telemetryContext.AddContext(nameof(packageFeedUri), packageFeedUri)
                    .AddContext(nameof(packageName), packageName)
                    .AddContext(nameof(packageVersion), packageVersion)
                    .AddContext(nameof(installationPath), installationPath);

                if (!string.IsNullOrWhiteSpace(personalAccessToken))
                {
                    IAzureKeyVault keyVaultClient = this.Services.GetService<IAzureKeyVault>();
                    if (keyVaultClient.IsSecretReference(personalAccessToken))
                    {
                        using (SecureString secureString = await keyVaultClient.ResolveSecretAsync(personalAccessToken, cancellationToken).ConfigureDefaults())
                        {
                            personalAccessToken = secureString.ToOriginalString();
                        }
                    }
                }

                if (!cancellationToken.IsCancellationRequested)
                {
                    INuGetPackageInstaller packageInstaller = this.Services.GetService<INuGetPackageInstaller>();

                    var packageInfo = new NuGetPackageInfo(
                        packageFeedUri,
                        packageName,
                        packageVersion,
                        installationPath,
                        "juno",
                        personalAccessToken);

                    // Install nuget package under working directory
                    // The install location will be working directory/packagename/packageversion
                    await packageInstaller.InstallPackageAsync(packageInfo, cancellationToken).ConfigureAwait(false);
                }
            }

            return result;
        }

        private class Parameters
        {
            internal const string FeedUri = "feedUri";
            internal const string PackageName = "packageName";
            internal const string PackageVersion = "packageVersion";
            internal const string PersonalAccessToken = "personalAccessToken";
        }
    }
}
