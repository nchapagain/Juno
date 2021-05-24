using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using AutoFixture;
using Juno.Contracts;
using Juno.Execution.Providers.Watchdog;
using Juno.Providers;
using Microsoft.Azure.CRC.Repository.KeyVault;
using Microsoft.Azure.CRC.Rest;
using Microsoft.Azure.CRC.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace Juno.Execution.Providers
{
    [TestFixture]
    [Category("Integration/Live")]
    public class GenevaConfigProviderTests
    {
        private Fixture mockFixture;
        private Mock<IRestClient> mockRestClient;
        private ExperimentMetadataInstance mockExperimentMetadataInstance;
        private IConfiguration mockConfiguration;
        private ExperimentComponent mockExperimentComponent;
        private ServiceCollection providerServices;
        private ExperimentContext mockExperimentContext;
        private Mock<IProviderDataClient> mockDataClient;

        [SetUp]
        public void SetupTest()
        {
            TestDependencies.Initialize();

            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks(ExperimentType.AB);
            this.mockFixture.SetupAgentMocks();
            this.mockExperimentMetadataInstance = this.mockFixture.Create<ExperimentMetadataInstance>();
            this.mockRestClient = new Mock<IRestClient>();
            this.mockDataClient = new Mock<IProviderDataClient>();
            this.mockDataClient.SetReturnsDefault(new HttpResponseMessage(HttpStatusCode.OK));

            this.providerServices = new ServiceCollection();
            this.providerServices.AddSingleton(NullLogger.Instance);

            this.mockConfiguration = new ConfigurationBuilder()
                  .SetBasePath(Path.Combine(
                              Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                              @"Configuration"))
                          .AddJsonFile($"juno-dev01.environmentsettings.json")
                          .Build();

            // Mock experiment context
            this.mockExperimentContext = new ExperimentContext(
                this.mockFixture.Create<ExperimentInstance>(),
                this.mockFixture.Create<ExperimentStepInstance>(),
                this.mockConfiguration);

            this.mockExperimentComponent = this.mockFixture.Create<ExperimentComponent>();
            this.mockExperimentComponent.Parameters.Add("certificateThumbprint", Guid.NewGuid().ToString());
            this.mockExperimentComponent.Parameters.Add("genevaTenantName", "crcteamtenant");
            this.mockExperimentComponent.Parameters.Add("genevaAccountName", "crcteamaccount");
            this.mockExperimentComponent.Parameters.Add("genevaNamespace", "crcteamnamespace");
            this.mockExperimentComponent.Parameters.Add("genevaRegion", "wus2");
            this.mockExperimentComponent.Parameters.Add("genevaConfigVersion", "2");
            this.mockExperimentComponent.Parameters.Add("genevaRoleName", "foo");

            this.providerServices.AddSingleton(this.mockDataClient.Object);

            this.providerServices.AddSingleton(TestDependencies.KeyVaultClient);
        }

        /// <summary>
        /// Tests that the configuration file is properly written if the excepted directory structure
        /// for MA is present. That is c:\windowsazure\*\__json
        /// </summary>
        [Test]
        public void ProviderBuildsAndWritesConfigurationWithValidData()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks(ExperimentType.AB);
            var component = this.mockFixture.Create<ExperimentComponent>();
            component.Parameters.Add("genevaEndpoint", "foo");
            component.Parameters.Add("certificateThumbprint", Guid.NewGuid().ToString());
            component.Parameters.Add("genevaTenantName", "crcteamtenant");
            component.Parameters.Add("genevaAccountName", "crcteamaccount");
            component.Parameters.Add("genevaNamespace", "crcteamnamespace");
            component.Parameters.Add("genevaRegion", "wus2");
            component.Parameters.Add("genevaConfigVersion", "2");
            component.Parameters.Add("genevaRoleName", "foo");

            var genevaConfigProvider = new GenevaConfigProvider(this.providerServices);
            bool configApplied = genevaConfigProvider.ApplyConfigAsync(component, EventContext.Persisted(), CancellationToken.None)
                .GetAwaiter().GetResult();
            Assert.IsTrue(configApplied);
        }

        /// <summary>
        /// Tests that the keyvault certificate is successfully installed
        /// </summary>
        [Test]
        public void ProviderInstallsKeyvaultCertificateGivenCorrectData()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks(ExperimentType.AB);
            var component = this.mockFixture.Create<ExperimentComponent>();
            component.Parameters.Add("genevaEndpoint", "foo");
            component.Parameters.Add("certificateThumbprint", Guid.NewGuid().ToString());
            component.Parameters.Add("genevaTenantName", "crcteamtenant");
            component.Parameters.Add("genevaAccountName", "crcteamaccount");
            component.Parameters.Add("genevaNamespace", "crcteamnamespace");
            component.Parameters.Add("genevaRegion", "wus2");
            component.Parameters.Add("genevaConfigVersion", "2");
            component.Parameters.Add("genevaRoleName", "foo");
            component.Parameters.Add("certificateKey", "juno-dev01-monagent");

            var genevaConfigProvider = new GenevaConfigProvider(this.providerServices);
            AzureKeyVault keyVault = new AzureKeyVault(TestDependencies.KeyVaultClient, TestDependencies.KeyVaultUri);
            Assert.DoesNotThrow(() => genevaConfigProvider.InstallMonitoringAgentCertificateAsync(component, keyVault, EventContext.Persisted(), CancellationToken.None)
                .GetAwaiter().GetResult());
        }
    }
}
