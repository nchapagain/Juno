namespace Juno.Execution.Providers.Environment
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Contracts;
    using Juno.Execution.Providers.Payloads;
    using Juno.Providers;
    using Kusto.Data.Exceptions;
    using Microsoft.Azure.CRC.Kusto;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;
    using Polly;

    [TestFixture]
    [Category("Unit")]
    public class ClusterSelectionProviderTests
    {
        private Fixture mockFixture;
        private Mock<IProviderDataClient> mockDataClient;
        private Mock<IKustoQueryIssuer> mockKustoClient = new Mock<IKustoQueryIssuer>();
        private IConfiguration mockConfiguration;
        private ExperimentComponent mockExperimentComponent;
        private ServiceCollection providerServices;
        private ExperimentContext mockExperimentContext;
        private DataTable mockDataTable;
        private ClusterSelectionProvider provider;
        private ClusterSelectionProvider.State providerState;

        /// <summary>
        /// Local list used to mock the save for entity pool
        /// </summary>
        private List<EnvironmentEntity> mockList;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks(ExperimentType.AB);
            this.mockDataClient = new Mock<IProviderDataClient>();
            this.mockDataClient.SetReturnsDefault(new HttpResponseMessage(HttpStatusCode.OK));

            // This mocks the save for entitypool so we can validate it later
            this.mockDataClient
                .Setup(s => s.UpdateStateItemsAsync<EnvironmentEntity>(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<EnvironmentEntity>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .Returns((string a, string b, IEnumerable<EnvironmentEntity> list, CancellationToken d, string stateId) => Task.FromResult(this.mockList = list.ToList()));

            // This mocks the get for entitypool so we can validate it
            this.mockDataClient
                .Setup(s => s.GetOrCreateStateAsync<IEnumerable<EnvironmentEntity>>(
                   It.IsAny<string>(),
                   It.IsAny<string>(),
                   It.IsAny<CancellationToken>(),
                   It.IsAny<string>()))
               .ReturnsAsync(() => this.mockList.AsEnumerable());

            this.mockExperimentComponent = this.mockFixture.Create<ExperimentComponent>();
            this.mockExperimentComponent.Parameters.Add("cpuId", "11");
            this.mockExperimentComponent.Parameters.Add("vmSkus", "Standard_D2");

            this.mockConfiguration = new ConfigurationBuilder()
                  .SetBasePath(Path.Combine(
                              Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                              @"Configuration"))
                          .AddJsonFile($"juno-dev01.environmentsettings.json")
                          .Build();

            this.mockExperimentContext = new ExperimentContext(
                this.mockFixture.Create<ExperimentInstance>(),
                this.mockFixture.Create<ExperimentStepInstance>(),
                this.mockConfiguration);

            this.mockKustoClient = new Mock<IKustoQueryIssuer>();
            this.mockDataTable = ClusterSelectionProviderTests.GetValidDataTable();

            this.mockKustoClient
                .Setup(client => client.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult(this.mockDataTable));

            this.providerServices = new ServiceCollection();
            this.providerServices.AddSingleton(NullLogger.Instance);
            this.providerServices.AddSingleton(this.mockDataClient.Object);
            this.providerServices.AddSingleton(this.mockKustoClient.Object);

            this.provider = new ClusterSelectionProvider(this.providerServices);
            this.providerState = new ClusterSelectionProvider.State();
            this.mockDataClient.OnGetState<ClusterSelectionProvider.State>().Returns(Task.FromResult(this.providerState));
        }

        [Test]
        public void ProviderValidatesRequiredParameters()
        {
            Assert.ThrowsAsync<ArgumentException>(() => this.provider.ExecuteAsync(null, this.mockExperimentComponent, CancellationToken.None));
            Assert.ThrowsAsync<ArgumentException>(() => this.provider.ExecuteAsync(this.mockExperimentContext, null, CancellationToken.None));
        }

        [Test]
        public void ProviderReturnSucceededResultOnValidData()
        {
            this.mockKustoClient.Setup(i => i.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult<DataTable>(ClusterSelectionProviderTests.GetValidDataTable()));

            var result = this.provider.ExecuteAsync(this.mockExperimentContext, this.mockExperimentComponent, CancellationToken.None).Result;

            Assert.IsTrue(result.Status == ExecutionStatus.Succeeded);
        }

        [Test]
        public void ProviderRetriesOnKustoResponsesThatDoNotHaveAnyResults()
        {
            this.providerState.MaxSearchAttempts = 2;

            this.mockKustoClient.Setup(i => i.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult<DataTable>(new DataTable()));

            // Search Attempt #1
            var result = this.provider.ExecuteAsync(this.mockExperimentContext, this.mockExperimentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsTrue(result.Status == ExecutionStatus.InProgress);

            // Search Attempt #2...will not find any results and the max search attempts will be exhausted.
            result = this.provider.ExecuteAsync(this.mockExperimentContext, this.mockExperimentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsTrue(result.Status == ExecutionStatus.Failed);
        }

        [Test]
        public void ProviderReturnsFailedOnMalformedKustoResponse()
        {
            this.mockKustoClient.Setup(i => i.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult<DataTable>(ClusterSelectionProviderTests.GetValidDataTable(true)));

            var result = this.provider.ExecuteAsync(this.mockExperimentContext, this.mockExperimentComponent, CancellationToken.None).Result;
            Assert.IsTrue(result.Status == ExecutionStatus.Failed);
        }

        [Test]
        public void ProviderReturnsFailedOnMalformedKustoResponse_MachinePoolOnly()
        {
            // set the machine pool to be null
            this.mockDataTable.Rows[0][7] = null;

            var result = this.provider.ExecuteAsync(this.mockExperimentContext, this.mockExperimentComponent, CancellationToken.None).Result;

            Assert.IsTrue(result.Status == ExecutionStatus.Failed);
            Assert.IsTrue(result.Error.Message.Contains("DBNull"), "Null machine pool name should have been caught");
        }

        [Test]
        public void ProviderCreatesValidEntityPoolWithValidKustoResponse()
        {
            var result = this.provider.ExecuteAsync(this.mockExperimentContext, this.mockExperimentComponent, CancellationToken.None).Result;
            Assert.IsTrue(result.Status == ExecutionStatus.Succeeded);

            var entityPool = this.provider.GetEntityPoolAsync(this.mockExperimentContext, CancellationToken.None).Result.ToList();

            Assert.IsTrue(entityPool.Count == 1);
            Assert.IsTrue(entityPool[0].Metadata.ContainsKey("MachinePoolName"), "TipRack must contain machine pool information");
            Assert.IsFalse(string.IsNullOrEmpty(entityPool[0].Metadata["MachinePoolName"].ToString()), "MachinePool must be non empty string");
        }

        [Test]
        public void ProviderReturnsFailedOnFailedWhenFilterIsMissingFromExperimentComponent()
        {
            var component = this.mockFixture.Create<ExperimentComponent>();
            var result = this.provider.ExecuteAsync(this.mockExperimentContext, component, CancellationToken.None).Result;

            Assert.IsTrue(result.Status == ExecutionStatus.Failed);
        }

        [Test]
        public void ProviderDefaultKustoClientRetryPolicyHandlesExpectedErrorsWhenQueryingTheKustoCluster()
        {
            List<Exception> retryableErrors = new List<Exception>
            {
                new KustoServiceTimeoutException(),
                new KustoRequestThrottledException()
            };

            foreach (Exception error in retryableErrors)
            {
                int retries = 0;
                Assert.DoesNotThrow(() => this.provider.RetryPolicy.ExecuteAsync(() =>
                {
                    retries++;
                    if (retries <= 1)
                    {
                        throw new KustoServiceTimeoutException();
                    }

                    return Task.CompletedTask;
                }));
            }
        }

        [Test]
        public void ProviderAppliesTheRetryPolicyDefinedWhenQueryingTheKustoCluster()
        {
            this.mockKustoClient
                .Setup(client => client.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Throws(new KustoServiceTimeoutException());

            this.provider.RetryPolicy = Policy.Handle<KustoServiceTimeoutException>().RetryAsync(3);

            this.provider.ExecuteAsync(this.mockExperimentContext, this.mockExperimentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();

            this.mockKustoClient.Verify(client => client.IssueAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
                Times.Exactly(4));
        }

        [Test]
        public void ProviderUsesTheExpectedClusterNodeSelectionQueryByDefault()
        {
            this.mockKustoClient
                .Setup(client => client.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string, string>((cluster, database, query) =>
                {
                    Assert.IsNotNull(query);
                    Assert.IsTrue(query.Contains("// TipRackQuery"));
                })
                .Returns(Task.FromResult(this.mockDataTable));

            this.provider.ExecuteAsync(this.mockExperimentContext, this.mockExperimentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();
        }

        [Test]
        public void ProviderUsesTheExpectedClusterNodeSelectionQueryWhenASpecificQueryNameIsProvided()
        {
            string queryName = "MCU2019_2_RefreshQuery";

            this.mockKustoClient
                .Setup(client => client.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string, string>((cluster, database, query) =>
                {
                    Assert.IsNotNull(query);
                    Assert.IsTrue(query.Contains($"// {queryName}"));
                })
                .Returns(Task.FromResult(this.mockDataTable));

            this.mockExperimentComponent.Parameters.Add("queryName", queryName);
            this.provider.ExecuteAsync(this.mockExperimentContext, this.mockExperimentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();
        }

        [Test]
        public void ProviderFiltersOutPinnedClustersFromTheListOfPotentialTipRacks()
        {
            List<string> pinnedClusters = new List<string>
            {
                "bn9prdapp08",
                "bn9prdapp18",
                "cpq22prdapp04",
                "fra21prdapp09",
                "lon23prdapp20",
                "sat20prdapp09"
            };

            IEnumerable<string> expectedPinnedClusters = ClusterSelectionProvider.GetPinnedClusters();
            foreach (string cluster in pinnedClusters)
            {
                Assert.IsTrue(expectedPinnedClusters.Contains(cluster));
            }
        }

        private static DataTable GetValidDataTable(bool malformedResponse = false)
        {
            // Here we create a DataTable with four columns.
            DataTable table = new DataTable();

            for (int i = 0; i < 9; i++)
            {
                table.Columns.Add($"{i}thIndex", typeof(string));
            }

            // Here we add five DataRows.
            table.Rows.Add(
                "Rack01",
                "Cluster01",
                "Cpu01",
                "Region01",
                10,
                malformedResponse ? "  \"Standard_E64s_v3\",  \"Standard_E64s_v3\"]" : "[  \"Standard_E64s_v3\",  \"Standard_E64s_v3\"]",
                "[  \"23610dfa-8a74-40f9-874e-23ae45219e5b\",  \"299e3a68-411e-4115-a690-9ed452bd2b9b\"]",
                "Cluster01mp1");
            return table;
        }
    }
}
