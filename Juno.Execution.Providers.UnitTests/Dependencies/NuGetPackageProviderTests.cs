namespace Juno.Execution.Providers.Dependencies
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Execution.NuGetIntegration;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Repository.KeyVault;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using NuGet.Protocol.Plugins;
    using NuGet.Versioning;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class NuGetPackageProviderTests
    {
        private ProviderFixture mockFixture;
        private NuGetPackageProvider provider;

        private Mock<IAzureKeyVault> mockKeyVaultStore;
        private Mock<INuGetPackageInstaller> mockPackageInstaller;
        private IServiceCollection providerServices;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new ProviderFixture(typeof(NuGetPackageProvider));
            this.mockFixture.Component.Parameters.AddRange(new Dictionary<string, IConvertible>
            {
                { "feedUri", "https://any/nuget/feed" },
                { "packageName", "anyPackage" },
                { "packageVersion", "1.2.3" },
                { "personalAccessToken", "[secret:keyvault]=AnyNuGetAccessTokenSecret" }
            });

            this.mockKeyVaultStore = new Mock<IAzureKeyVault>();
            this.mockPackageInstaller = new Mock<INuGetPackageInstaller>();
            this.SetupMockDefaults();

            this.providerServices = new ServiceCollection();
            this.providerServices.AddSingleton<IAzureKeyVault>(this.mockKeyVaultStore.Object);
            this.providerServices.AddSingleton<INuGetPackageInstaller>(this.mockPackageInstaller.Object);

            this.provider = new NuGetPackageProvider(this.providerServices);
        }

        [Test]
        public async Task NuGetPackageProviderValidatesRequiredComponentParameters()
        {
            this.mockFixture.Component.Parameters.Clear();
            Dictionary<string, IConvertible> requiredParameters = new Dictionary<string, IConvertible>
            {
                { "feedUri", "https://any/nuget/feed" },
                { "packageName", "anyPackage" },
                { "packageVersion", "1.2.3" }
            };

            ExecutionResult result = null;
            foreach (var entry in requiredParameters)
            {
                result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

                Assert.IsNotNull(result);
                Assert.IsTrue(result.Status == ExecutionStatus.Failed);
                Assert.IsNotNull(result.Error);
                Assert.IsInstanceOf<SchemaException>(result.Error);

                this.mockFixture.Component.Parameters.Add(entry.Key, entry.Value);
            }

            result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status != ExecutionStatus.Failed);
        }

        [Test]
        public Task NuGetPackageProviderDownloadsTheExpectedPackage()
        {
            this.mockPackageInstaller.OnInstallPackage()
                .Callback<NuGetPackageInfo, CancellationToken>((packageInfo, token) =>
                {
                    Assert.AreEqual(packageInfo.FeedUri.AbsoluteUri, this.mockFixture.Component.Parameters.GetValue<string>("feedUri"));
                    Assert.AreEqual(packageInfo.PackageName, this.mockFixture.Component.Parameters.GetValue<string>("packageName"));
                    Assert.AreEqual(packageInfo.PackageVersion, this.mockFixture.Component.Parameters.GetValue<string>("packageVersion"));

                    Assert.IsNotNull(this.mockFixture.Component.Parameters.GetValue<string>("installationPath"));
                    Assert.IsNotNull(packageInfo.Password);
                })
                .ReturnsAsync(new NuGetVersion("0.0.0.1"));

            return this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);
        }

        [Test]
        public Task NuGetPackageProviderInstallsThePackageInTheExpectedLocation()
        {
            this.mockPackageInstaller.OnInstallPackage()
                .Callback<NuGetPackageInfo, CancellationToken>((packageInfo, token) =>
                {
                    Assert.AreEqual(packageInfo.DownloadPath, DependencyPaths.NuGetPackages);
                })
                .ReturnsAsync(new NuGetVersion("0.0.0.1"));

            return this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);
        }

        [Test]
        public Task NuGetPackageProviderResolvesAccessTokenSecrets()
        {
            string expectedSecret = "resolvedSecret";

            this.mockKeyVaultStore.OnGetSecret()
                .Returns(Task.FromResult(expectedSecret.ToSecureString()));

            this.mockPackageInstaller.OnInstallPackage()
                .Callback<NuGetPackageInfo, CancellationToken>((packageInfo, token) =>
                {
                    string personalAccessToken = this.mockFixture.Component.Parameters.GetValue<string>("personalAccessToken");
                    Assert.AreNotEqual(packageInfo.Password, personalAccessToken);
                    Assert.AreEqual(expectedSecret, packageInfo.Password);
                })
                .ReturnsAsync(new NuGetVersion("0.0.0.1"));

            return this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);
        }

        [Test]
        public async Task NuGetPackageProviderReturnsTheExpectedResultWhenThePackageIsDownloadedSuccessfully()
        {
            ExecutionResult result = await this.provider.ExecuteAsync(
                this.mockFixture.Context,
                this.mockFixture.Component,
                CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.Succeeded);
        }

        [Test]
        public async Task NuGetPackageProviderReturnsTheExpectedResultWhenThePackageFailsDownload()
        {
            this.mockPackageInstaller.OnInstallPackage()
                .Throws(new ProtocolException($"Unauthorized access."));

            ExecutionResult result = await this.provider.ExecuteAsync(
                this.mockFixture.Context,
                this.mockFixture.Component,
                CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.Failed);
            Assert.IsNotNull(result.Error);
            Assert.IsInstanceOf<ProtocolException>(result.Error);
        }

        private void SetupMockDefaults()
        {
            this.mockKeyVaultStore.OnGetSecret()
                .Returns(Task.FromResult("anySecretValue".ToSecureString()));

            this.mockPackageInstaller.OnInstallPackage()
                .ReturnsAsync(new NuGetVersion("0.0.0.1"));
        }
    }
}
