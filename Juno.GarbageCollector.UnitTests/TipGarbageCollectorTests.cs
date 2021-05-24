namespace Juno.GarbageCollector
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using AutoFixture;
    using Castle.Core.Internal;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.DataManagement;
    using Juno.Execution.TipIntegration;
    using Kusto.Cloud.Platform.Utils;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Kusto;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Newtonsoft.Json;
    using NUnit.Framework;
    using TipGateway.Entities;

    [TestFixture]
    [Category("Unit")]
    public class TipGarbageCollectorTests
    {
        private Mock<IKustoQueryIssuer> kustoIssuer;
        private Mock<ITipClient> tipClient;
        private Mock<IConfiguration> configuration;
        private ILogger logger;
        private IServiceCollection services;
        private IList<LeakedResource> leakedResource;
        private KustoSettings kustoSetting;
        private Mock<IExperimentTemplateDataManager> experimentTemplateDataManager;
        private Mock<IExperimentClient> experimentClientManager;
        private string appId = "mockAppId";
        private ExperimentItem garbageCollectorExperimentItem;
        private Fixture mockFixtureGC;
        private TipGarbageCollector tipGarbageCollector;

        [SetUp]
        public void SetupTest()
        {
            this.services = new ServiceCollection();

            this.tipClient = new Mock<ITipClient>();
            this.kustoIssuer = new Mock<IKustoQueryIssuer>();
            this.logger = NullLogger.Instance;
            this.leakedResource = new List<LeakedResource>();
            this.leakedResource.Add(TipGarbageCollectorTests.CreateMockNonImpactfulExpiredLeakedResource());
            this.experimentTemplateDataManager = new Mock<IExperimentTemplateDataManager>();
            this.mockFixtureGC = new Fixture();
            this.experimentClientManager = new Mock<IExperimentClient>();

            this.configuration = new Mock<IConfiguration>();
            this.kustoSetting = new KustoSettings()
            {
                ClusterDatabase = "TestKustoDB",
                ClusterUri = new Uri("https://thisisamockURI.com")
            };

            this.services.AddSingleton<IConfiguration>(this.configuration.Object);
            this.services.AddSingleton<IKustoQueryIssuer>(this.kustoIssuer.Object);
            this.services.AddSingleton<ILogger>(this.logger);
            this.services.AddSingleton<ITipClient>(this.tipClient.Object);
            this.services.AddSingleton<IExperimentTemplateDataManager>(this.experimentTemplateDataManager.Object);
            this.services.AddSingleton<IExperimentClient>(this.experimentClientManager.Object);

            this.mockFixtureGC.SetupExperimentMocks();
            this.garbageCollectorExperimentItem = this.mockFixtureGC.Create<ExperimentItem>();

            this.tipGarbageCollector = new TipGarbageCollector(this.services, this.kustoSetting, this.appId);
        }

        [Test]
        public void ProviderValidatesRequiredParameters()
        {
            IGarbageCollector tipGarbageCollector;
            Assert.Throws<ArgumentNullException>(() => tipGarbageCollector = new TipGarbageCollector(null));
        }

        [Test]
        public void LeakedResourcesIsSerializable()
        {
            SerializationAssert.IsJsonSerializable(this.leakedResource);
        }

        [Test]
        [TestCase(5, 20)]
        [TestCase(5, 1)]
        [TestCase(5, 5)]
        [TestCase(1, 5)]
        [TestCase(1, 1)]
        [TestCase(0, 0)]
        public void TipGarbageCollectorReturnsExpectedResponseWhenRequiredServicesAreProvided(int kustoRowCount, int tipClientSessionsCount)
        {
            DataTable kustoDataTable = TipGarbageCollectorTests.CreateMockDataTable(kustoRowCount);
            IList<TipNodeSession> tipClientSession = TipGarbageCollectorTests.CreateMockTipNodeSessions(tipClientSessionsCount);

            this.kustoIssuer.Setup(x => x.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(kustoDataTable);
            this.tipClient.Setup(x => x.GetTipSessionsByAppIdAsync(this.appId, CancellationToken.None)).ReturnsAsync(tipClientSession);

            Dictionary<string, bool> tipSessionCreatedValidate = new Dictionary<string, bool>();
            tipClientSession.ForEach(x => tipSessionCreatedValidate.Add(x.Id, true));

            this.tipClient.Setup(x => x.IsTipSessionCreatedAsync(It.IsAny<IEnumerable<string>>(), CancellationToken.None)).ReturnsAsync(tipSessionCreatedValidate);

            var response = this.tipGarbageCollector.GetLeakedResourcesAsync(CancellationToken.None).Result;

            HashSet<string> expectedCount = new HashSet<string>();
            foreach (DataRow a in kustoDataTable.Rows)
            {              
                if (DateTime.Parse((string)a["createdTime"]) < DateTime.UtcNow.AddHours(-30))
                {
                    expectedCount.Add((string)a["tipNodeSessionId"]);
                }
            }

            expectedCount.AddRange(tipSessionCreatedValidate.Keys);

            Assert.AreEqual(response.Count, expectedCount.Count);
        }

        [Test]
        public void TipGarbageCollectorReturnsLeakedResourcesFromTipClientForOnlyTipSessionsCreated()
        {
            var tipClientSession = TipGarbageCollectorTests.CreateMockTipNodeSessions(15);

            this.kustoIssuer.Setup(x => x.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(new DataTable());
            this.tipClient.Setup(x => x.GetTipSessionsByAppIdAsync(this.appId, CancellationToken.None)).ReturnsAsync(tipClientSession);

            List<string> tipCreated = new List<string>() { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10" };
            List<string> tipNotCreated = new List<string>() { "11", "12", "13", "14", "15" };
            Dictionary<string, bool> tipCreatedResult = new Dictionary<string, bool>();

            tipCreated.ForEach(x => tipCreatedResult.Add(x, true));
            tipNotCreated.ForEach(x => tipCreatedResult.Add(x, false));
            this.tipClient.Setup(x => x.IsTipSessionCreatedAsync(It.IsAny<IEnumerable<string>>(), CancellationToken.None)).ReturnsAsync(tipCreatedResult);

            var response = this.tipGarbageCollector.GetLeakedResourcesAsync(CancellationToken.None).Result;

            Assert.AreEqual(response.Count, tipCreated.Count);
        }

        [Test]
        public void TipGarbageCollectorDoesNotReturnsLeakedResourcesWhenTipClientReturnsFalseForTipSessionsCreated()
        {
            var tipClientSession = TipGarbageCollectorTests.CreateMockTipNodeSessions(10);

            this.kustoIssuer.Setup(x => x.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(new DataTable());
            this.tipClient.Setup(x => x.GetTipSessionsByAppIdAsync(this.appId, CancellationToken.None)).ReturnsAsync(tipClientSession);

            List<string> tipCreated = new List<string>() { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10" };
            Dictionary<string, bool> tipCreatedResult = new Dictionary<string, bool>();

            tipCreated.ForEach(x => tipCreatedResult.Add(x, false));
            this.tipClient.Setup(x => x.IsTipSessionCreatedAsync(It.IsAny<IEnumerable<string>>(), CancellationToken.None)).ReturnsAsync(tipCreatedResult);

            var response = this.tipGarbageCollector.GetLeakedResourcesAsync(CancellationToken.None).Result;

            Assert.IsFalse(response.Any());
        }

        [Test]
        public void TipGarbageCollectorDoesNotReturnLeakedResourcesForTipNodeSessionWithStatusCreating()
        {
            IList<TipNodeSession> tipNodeSessions = new List<TipNodeSession>();
            for (int i = 0; i < 10; i++)
            {
                tipNodeSessions.Add(new TipNodeSession()
                {
                    Id = i.ToString(),
                    CreatedTimeUtc = DateTime.UtcNow.AddDays(-10),
                    Cluster = "Mock Cluster",
                    Region = "Mock Region",
                    Status = TipNodeSessionStatus.Creating // we ignore TipNodeSession with TipNodeSessionStatus == Creating
                });
            }

            this.kustoIssuer.Setup(x => x.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(new DataTable());
            this.tipClient.Setup(x => x.GetTipSessionsByAppIdAsync(this.appId, CancellationToken.None)).ReturnsAsync(tipNodeSessions);
            var response = this.tipGarbageCollector.GetLeakedResourcesAsync(CancellationToken.None).Result;

            this.tipClient.Verify(x => x.IsTipSessionCreatedAsync(It.IsAny<IEnumerable<string>>(), CancellationToken.None), Times.Never);
            Assert.IsTrue(response.IsNullOrEmpty());
        }

        [Test]
        public void TipGarbageCollectorReturnsLeakedResourcesFromTipClientAsImpactfulWhenKustoResponsesAreEmpty()
        {
            var tipClientSession = TipGarbageCollectorTests.CreateMockTipNodeSessions(15);

            this.kustoIssuer.Setup(x => x.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(new DataTable());
            this.tipClient.Setup(x => x.GetTipSessionsByAppIdAsync(this.appId, CancellationToken.None)).ReturnsAsync(tipClientSession);

            Dictionary<string, bool> tipSessionCreatedValidate = new Dictionary<string, bool>();
            tipClientSession.ForEach(x => tipSessionCreatedValidate.Add(x.Id, true));

            this.tipClient.Setup(x => x.IsTipSessionCreatedAsync(It.IsAny<IEnumerable<string>>(), CancellationToken.None)).ReturnsAsync(tipSessionCreatedValidate);

            var response = this.tipGarbageCollector.GetLeakedResourcesAsync(CancellationToken.None).Result;

            foreach (var leakedResource in response)
            {
                Assert.AreEqual(leakedResource.Value.ImpactType, ImpactType.Impactful);
            }
        }

        [Test]
        public void TipGarbageCollectorReturnsExpectedResponseWhenThereAreNoLeakedTipSessions()
        {
            // With this test, we are calling tip client API as our source of truth.
            DataTable kustoDataTable = TipGarbageCollectorTests.CreateMockDataTable(10);

            this.kustoIssuer.Setup(x => x.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(kustoDataTable);
            this.tipClient.Setup(x => x.GetTipSessionsByAppIdAsync(this.appId, CancellationToken.None)).ReturnsAsync(new List<TipNodeSession>());
            
            var response = this.tipGarbageCollector.GetLeakedResourcesAsync(CancellationToken.None).Result;

            this.tipClient.Verify(x => x.IsTipSessionCreatedAsync(It.IsAny<IEnumerable<string>>(), CancellationToken.None), Times.Never);

            int expectedCount = 0;
            foreach (DataRow row in kustoDataTable.Rows)
            {
                if (DateTime.Parse((string)row["createdTime"]) < DateTime.UtcNow.AddHours(-30))
                {
                    expectedCount++;
                }
            }

            Assert.AreEqual(response.Count, expectedCount);
        }

        [Test]
        public void ResourceGroupGarbageCollectorCleansOnlyExpiredResources()
        {
            Dictionary<string, LeakedResource> leakedResource = new Dictionary<string, LeakedResource>();
            leakedResource.Add("1", TipGarbageCollectorTests.CreateMockImpactfulUnExpiredLeakedResource());
            leakedResource.Add("2", TipGarbageCollectorTests.CreateMockImpactfulUnExpiredLeakedResource());
            leakedResource.Add("3", TipGarbageCollectorTests.CreateMockImpactfulUnExpiredLeakedResource());

            var output = this.tipGarbageCollector.CleanupLeakedResourcesAsync(leakedResource, CancellationToken.None).Result;

            Assert.IsTrue(output.IsNullOrEmpty());

            this.experimentClientManager.Verify(x => x.CreateExperimentFromTemplateAsync(It.IsAny<ExperimentTemplate>(), CancellationToken.None, It.IsAny<string>()), Times.Never);

            this.experimentTemplateDataManager.Verify(x => x.GetExperimentTemplateAsync(
                It.IsAny<string>(), It.IsAny<string>(), CancellationToken.None, It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void TipGarbageCollectorCleansExpiredLeakedResourcesAreProvided()
        {
            Dictionary<string, LeakedResource> leakedResource = new Dictionary<string, LeakedResource>();
            leakedResource.Add("1", TipGarbageCollectorTests.CreateMockNonImpactfulExpiredLeakedResource());
            leakedResource.Add("2", TipGarbageCollectorTests.CreateMockNonImpactfulExpiredLeakedResource());
            leakedResource.Add("3", TipGarbageCollectorTests.CreateMockNonImpactfulExpiredLeakedResource());

            this.experimentTemplateDataManager.Setup(x => x.GetExperimentTemplateAsync(It.IsAny<string>(), It.IsAny<string>(), CancellationToken.None, It.IsAny<string>()))
                .ReturnsAsync(this.garbageCollectorExperimentItem);

            this.experimentClientManager.Setup(x => x.CreateExperimentFromTemplateAsync(It.IsAny<ExperimentTemplate>(), CancellationToken.None, It.IsAny<string>()))
                .ReturnsAsync(this.CreateMockHttpResponse(HttpStatusCode.OK));

            var output = this.tipGarbageCollector.CleanupLeakedResourcesAsync(leakedResource, CancellationToken.None).Result;
            foreach (var a in output)
            {
                Assert.IsTrue(leakedResource.ContainsKey(a.Key));
            }

            this.experimentClientManager.Verify(x => x.CreateExperimentFromTemplateAsync(It.IsAny<ExperimentTemplate>(), CancellationToken.None, It.IsAny<string>()), Times.Exactly(3));

            this.experimentTemplateDataManager.Verify(x => x.GetExperimentTemplateAsync(
                It.IsAny<string>(), It.IsAny<string>(), CancellationToken.None, It.IsAny<string>()), Times.Once);
        }

        [Test]
        public void TipGarbageCollectorCleansOnlyNonImpactfulResources()
        {
            Dictionary<string, LeakedResource> leakedResource = new Dictionary<string, LeakedResource>();
            leakedResource.Add("1", TipGarbageCollectorTests.CreateMockNonImpactfulExpiredLeakedResource()); // Non Impactful
            leakedResource.Add("2", TipGarbageCollectorTests.CreateMockNonImpactfulExpiredLeakedResource()); // Non Impactful
            leakedResource.Add("3", TipGarbageCollectorTests.CreateMockImpactfulExpiredLeakedResource()); // Impactful

            this.experimentTemplateDataManager.Setup(x => x.GetExperimentTemplateAsync(It.IsAny<string>(), It.IsAny<string>(), CancellationToken.None, It.IsAny<string>()))
                .ReturnsAsync(this.garbageCollectorExperimentItem);

            this.experimentClientManager.Setup(x => x.CreateExperimentFromTemplateAsync(It.IsAny<ExperimentTemplate>(), CancellationToken.None, It.IsAny<string>()))
                .ReturnsAsync(this.CreateMockHttpResponse(HttpStatusCode.OK));

            var output = this.tipGarbageCollector.CleanupLeakedResourcesAsync(leakedResource, CancellationToken.None).Result;
            foreach (var a in output)
            {
                Assert.IsFalse(output.ContainsKey("3"));
            }
        }

        [Test]
        public void TipGarbageCollectorValidatesThatAValidSetOfResourcesAreProvided()
        {
            Dictionary<string, LeakedResource> leakedResource = new Dictionary<string, LeakedResource>();
            IDictionary<string, string> response;
            TipGarbageCollector tipGC = new TipGarbageCollector(this.services, this.kustoSetting, this.appId);
            Assert.Throws<ArgumentException>(() => response = tipGC.CleanupLeakedResourcesAsync(leakedResource, CancellationToken.None)
                .GetAwaiter().GetResult());
        }

        [Test]
        public void TipGarbageCollectorWillAttemptToCleanOtherResourcesEvenAfterFailingtoDeleteFirstResource()
        {
            Dictionary<string, LeakedResource> leakedResource = new Dictionary<string, LeakedResource>();
            leakedResource.Add("1", TipGarbageCollectorTests.CreateMockNonImpactfulExpiredLeakedResource());
            leakedResource.Add("2", TipGarbageCollectorTests.CreateMockNonImpactfulExpiredLeakedResource());
            leakedResource.Add("3", TipGarbageCollectorTests.CreateMockNonImpactfulExpiredLeakedResource());

            this.experimentTemplateDataManager.Setup(x => x.GetExperimentTemplateAsync(It.IsAny<string>(), It.IsAny<string>(), CancellationToken.None, It.IsAny<string>()))
                .ReturnsAsync(this.garbageCollectorExperimentItem);

            this.experimentClientManager.Setup(x => x.CreateExperimentFromTemplateAsync(It.IsAny<ExperimentTemplate>(), CancellationToken.None, It.IsAny<string>()))
                .ReturnsAsync(this.CreateMockHttpResponse(HttpStatusCode.BadRequest));

            var output = this.tipGarbageCollector.CleanupLeakedResourcesAsync(leakedResource, CancellationToken.None).Result;
            foreach (var a in output)
            {
                Assert.IsTrue(leakedResource.ContainsKey(a.Key));
                Assert.IsEmpty(a.Value);
            }

            this.experimentClientManager.Verify(x => x.CreateExperimentFromTemplateAsync(It.IsAny<ExperimentTemplate>(), CancellationToken.None, It.IsAny<string>()), Times.Exactly(3));

            this.experimentTemplateDataManager.Verify(x => x.GetExperimentTemplateAsync(
                It.IsAny<string>(), It.IsAny<string>(), CancellationToken.None, It.IsAny<string>()), Times.Once);
        }

        internal static LeakedResource CreateMockNonImpactfulExpiredLeakedResource()
        {
            return new LeakedResource(
                createdTime: DateTime.UtcNow.AddDays(-4),
                id: "Mock Resource ID" + Guid.NewGuid().ToString(),
                resourceType: "Mock Resource",
                tipNodeSessionId: " ",
                nodeId: " ",
                daysLeaked: 5,
                experimentId: "Mock Experiment ID",
                experimentName: "QoS",
                impactType: ImpactType.None,
                cluster: "None", 
                subscriptionId: "None",
                source: LeakedResourceSource.AzureCM);
        }

        internal static LeakedResource CreateMockImpactfulExpiredLeakedResource()
        {
            return new LeakedResource(
                createdTime: DateTime.UtcNow.AddDays(-4),
                id: "Mock Resource ID",
                resourceType: "Mock Resource",
                tipNodeSessionId: " ",
                nodeId: " ",
                daysLeaked: 5,
                experimentId: "Mock Experiment ID",
                experimentName: "QoS",
                impactType: ImpactType.Impactful,
                cluster: "None",
                subscriptionId: "None",
                source: LeakedResourceSource.TipClient);
        }

        internal static LeakedResource CreateMockImpactfulUnExpiredLeakedResource()
        {
            return new LeakedResource(
                createdTime: DateTime.UtcNow,
                id: "Mock Resource ID",
                resourceType: "Mock Resource",
                tipNodeSessionId: " ",
                nodeId: " ",
                daysLeaked: 5,
                experimentId: "Mock Experiment ID",
                experimentName: "QoS",
                impactType: ImpactType.Impactful,
                cluster: "None",
                subscriptionId: "None",
                source: LeakedResourceSource.TipClientAndAzureCM);
        }

        private static IList<TipNodeSession> CreateMockTipNodeSessions(int numberOfSession)
        {
            // Here we create a TipNodeSession with 4 properties and given numberOfSession
            // ID will match row number.

            var tipNodeSession = new List<TipNodeSession>();

            if (numberOfSession < 1)
            {
                return tipNodeSession;
            }

            for (int i = 0; i < numberOfSession; i++)
            {
                tipNodeSession.Add(new TipNodeSession()
                {
                    Id = i.ToString(),
                    CreatedTimeUtc = DateTime.UtcNow.AddDays(-10),
                    Cluster = "Mock Cluster",
                    Region = "Mock Region", 
                    Status = TipNodeSessionStatus.Created
                });
            }

            return tipNodeSession;
        }

        private static DataTable CreateMockDataTable(int numberOfLeakedSessions)
        {
            // Here we create a DataTable with 7 columns and given numberOfRows
            // tipNodeSessionID will match the row number.

            DataTable table = new DataTable();

            DataColumn createdTimeColumn = new DataColumn("createdTime", typeof(string));
            DataColumn tipNodeSessionIdColumn = new DataColumn("tipNodeSessionId", typeof(string));
            DataColumn nodeIdColumn = new DataColumn("nodeId", typeof(string));
            DataColumn daysLeakedColumn = new DataColumn("daysLeaked", typeof(string));
            DataColumn experimentIdColumn = new DataColumn("experimentId", typeof(string));
            DataColumn experimentNameColumn = new DataColumn("experimentName", typeof(string));
            DataColumn impactTypeColumn = new DataColumn("impactType", typeof(string));

            table.Columns.Add(createdTimeColumn);
            table.Columns.Add(tipNodeSessionIdColumn);
            table.Columns.Add(nodeIdColumn);
            table.Columns.Add(daysLeakedColumn);
            table.Columns.Add(experimentIdColumn);
            table.Columns.Add(experimentNameColumn);
            table.Columns.Add(impactTypeColumn);

            if (numberOfLeakedSessions < 1)
            {
                return table;
            }

            for (int i = 0; i < numberOfLeakedSessions; i++)
            {
                DataRow newRow = table.NewRow();
                newRow["createdTime"] = DateTime.UtcNow.AddDays(-i).ToString();
                newRow["tipNodeSessionId"] = i;
                newRow["nodeId"] = Guid.NewGuid();
                newRow["daysLeaked"] = i;
                newRow["experimentId"] = Guid.NewGuid();
                newRow["experimentName"] = "QoS";
                newRow["impactType"] = "None";
                table.Rows.Add(newRow);
            }

            return table;
        }

        private HttpResponseMessage CreateMockHttpResponse(HttpStatusCode statusCode)
        {
            return new HttpResponseMessage { StatusCode = statusCode, Content = new StringContent(JsonConvert.SerializeObject(this.garbageCollectorExperimentItem)) };
        }
    }
}
