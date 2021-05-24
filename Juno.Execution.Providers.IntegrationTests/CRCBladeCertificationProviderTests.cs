using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using AutoFixture;
using Juno.Contracts;
using Juno.Execution.Providers.Certification;
using Juno.Execution.Providers.Environment;
using Juno.Execution.Providers.Payloads;
using Juno.Execution.TipIntegration;
using Juno.Providers;
using Microsoft.Azure.CRC.Rest;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace Juno.Execution.Providers
{
    /// <summary>
    /// Integration tests to check that provider can talk to Kusto
    /// Validating response etc is done in the UT
    /// </summary>
    [TestFixture]
    [Category("Integration/Live")]
    public class CRCBladeCertificationProviderTests
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
            this.providerServices.AddSingleton(this.mockDataClient.Object);

            this.providerServices.AddSingleton(TestDependencies.KeyVaultClient);
        }

        /// <summary>
        /// Live integration test that interacts with the certification agent package in c:\\app
        /// </summary>
        [Test]
        public void ProviderCanDecideCertificationResult()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks(ExperimentType.AB);

            var selectionProvider = new CRCBladeCertificationProvider(this.providerServices);
            selectionProvider.ConfigureServicesAsync(this.mockExperimentContext, this.mockExperimentComponent).GetAwaiter().GetResult();
            var result = selectionProvider.ExecuteAsync(this.mockExperimentContext, this.mockExperimentComponent, CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(result.Status, ExecutionStatus.Succeeded);           
        }
    }
}
