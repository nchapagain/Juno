using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using AutoFixture;
using Juno.Contracts;
using Juno.Execution.Providers.Environment;
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
    public class PassThroughNodeSelectionProviderTests
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
            this.mockExperimentComponent.Parameters.Add("nodes", "fde834d0-ee6b-463c-a00d-a8490d5ad932,af3e487d-ec90-4a33-bc40-57238cd42bea,73d59467-03f3-486a-846b-370a9108f8fe,962b2161-d8fe-4025-9058-ab8512427a4c,7b2b1a1c-fff0-4792-ad4f-a482f11fcd31,65db2bd0-4e77-44c2-9d58-6f3fd84fc8fb,5b5d6c4c-259a-4b1a-a148-809eede1b210,e75af8be-b2f0-4e41-8d03-5fc21b4253b4,59f72813-dc2a-4cf9-b569-e5fd6b8b85f9,4041ee6a-50b5-4128-997b-7dc120589a30,41b6b22e-343c-4bc2-941e-c2c026094403,6c018f91-6ef4-42be-afb7-15f4cd9c5e20,89efd443-7c2d-4595-a390-5c525ca1728f,53d4050c-66cd-4152-86ea-118248463fc8,9bd79f0d-b5bf-4187-a2d6-42d01227a348,f1965321-4e86-400f-a60f-0421357973a9,495be6e1-4d48-4828-8116-9acac974361a,6f0b7d23-d826-4afd-a7fc-d1eba07e8de5,34a69ac2-3c73-4ae7-b0cc-643041c4d566,7d0ee84e-ca41-44d3-bea4-32884d8301ce,64aeb653-0dda-46d9-98ea-2c97729f0491,a08d1fff-1e53-4599-a168-6c62ffd28f60,27d351fe-0dfa-4cbb-bb04-bbd4538d36a7,5721a6ca-ea41-4039-83ff-7c94d203d8bf,abf29002-fefe-4a0f-96ad-83448470f8a2,e85de369-1954-4c60-9303-ce65d9c2bb11");

            this.providerServices.AddSingleton(this.mockDataClient.Object);

            this.providerServices.AddSingleton(TestDependencies.KeyVaultClient);
        }

        /// <summary>
        /// Live integration test that we can talk to kusto and get a valid response
        /// </summary>
        [Test]
        public void ProviderAbleToGetKustoResponse()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks(ExperimentType.AB);

            var selectionProvider = new PassThroughNodeSelectionProvider(this.providerServices);
            selectionProvider.ExecuteAsync(this.mockExperimentContext, this.mockExperimentComponent, CancellationToken.None).GetAwaiter().GetResult();
            var tableResponse = selectionProvider.GetKustoResponseAsync(this.mockExperimentContext, this.mockExperimentComponent)
                .GetAwaiter().GetResult();
            var result = PassThroughNodeSelectionProvider.ParseRack(tableResponse);
            var entities = TipRack.ToEnvironmentEntities(result);
            Assert.IsTrue(entities.Count > 0);
        }
    }
}
