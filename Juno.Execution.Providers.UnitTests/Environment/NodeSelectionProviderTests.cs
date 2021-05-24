namespace Juno.Execution.Providers.Environment
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Kusto;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    [SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Disposed in [TearDown].")]
    public class NodeSelectionProviderTests
    {
        private ProviderFixture mockFixture;
        private Mock<IKustoQueryIssuer> mockKustoClient;
        private DataTable mockDataTable;
        private TestNodeSelectionProvider provider;
        private List<EnvironmentEntity> mockEntityPool;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new ProviderFixture(typeof(NodeSelectionProvider));
            this.mockFixture.Component.Parameters.Add("nodes", "foo");

            this.mockEntityPool = new List<EnvironmentEntity>();
            this.mockKustoClient = new Mock<IKustoQueryIssuer>();
            this.mockFixture.Services.AddSingleton(NullLogger.Instance);
            this.mockFixture.Services.AddSingleton(this.mockKustoClient.Object);
            this.mockFixture.Services.AddSingleton(this.mockFixture.EntityManager);
            this.provider = new TestNodeSelectionProvider(this.mockFixture.Services);

            // Setup the default Kusto datatable query results.
            this.mockDataTable = new DataTable();
            this.mockDataTable.Columns.Add(Constants.MachinePoolName);
            this.mockDataTable.Columns.Add(Constants.RackLocation);
            this.mockDataTable.Columns.Add(Constants.Region);
            this.mockDataTable.Columns.Add(Constants.NodeId);
            this.mockDataTable.Columns.Add(Constants.ClusterName);
            this.mockDataTable.Columns.Add(Constants.SupportedVmSkus);

            this.AddEntityRow("Cluster01-Rack01-Node01", "Cluster01", "Rack01", "Region01", "MP01");
            this.AddEntityRow("Cluster01-Rack01-Node02", "Cluster01", "Rack01", "Region01", "MP01");
            this.AddEntityRow("Cluster01-Rack02-Node04", "Cluster01", "Rack02", "Region01", "MP02");
            this.AddEntityRow("Cluster01-Rack02-Node05", "Cluster01", "Rack02", "Region01", "MP02");
            this.AddEntityRow("Cluster02-Rack03-Node07", "Cluster02", "Rack03", "Region02", "MP03");
            this.AddEntityRow("Cluster02-Rack03-Node08", "Cluster02", "Rack03", "Region02", "MP03");

            this.SetupMockDefaultBehaviors();
        }

        [TearDown]
        public void CleanupTest()
        {
            this.mockDataTable.Dispose();
        }

        [Test]
        public void ProviderValidatesRequiredParameters()
        {
            Assert.ThrowsAsync<ArgumentException>(() => this.provider.ExecuteAsync(null, this.mockFixture.Component, CancellationToken.None));
            Assert.ThrowsAsync<ArgumentException>(() => this.provider.ExecuteAsync(this.mockFixture.Context, null, CancellationToken.None));
        }

        [Test]
        public void ProviderReturnsTheExpectedResultWhenTheKustoResponseWhenItSuccessfullyQueriesKusto()
        {
            this.mockKustoClient.Setup(i => i.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult<DataTable>(NodeSelectionProviderTests.GetValidDataTable()));

            var result = this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None).Result;

            Assert.IsTrue(result.Status == ExecutionStatus.Succeeded);
        }

        [Test]
        public void ProviderReturnsTheExpectedResultWhenTheKustoResponseIsEmpty()
        {
            this.mockKustoClient.Setup(i => i.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult<DataTable>(new DataTable()));

            var result = this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None).Result;
            Assert.IsTrue(result.Status == ExecutionStatus.Failed);
        }

        [Test]
        public void ProviderReturnsTheExpectedResultWhenTheKustoResponseContainsInvalidSupportedVmSkuInformation()
        {
            // set the supported VM SKUs to be null
            this.mockDataTable.Rows[0][5] = null;

            this.mockKustoClient.Setup(i => i.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult<DataTable>(NodeSelectionProviderTests.GetValidDataTable(true)));

            var result = this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None).Result;
            Assert.IsTrue(result.Status == ExecutionStatus.Failed);
        }

        [Test]
        public void ProviderReturnsTheExpectedResultWhenTheKustoResponseContainsInvalidMachinePoolInformation()
        {
            // set the machine pool to be null
            this.mockDataTable.Rows[0][0] = null;

            var result = this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None).Result;

            Assert.IsTrue(result.Status == ExecutionStatus.Failed);
            Assert.IsTrue(result.Error.Message.Contains("DBNull"), "Null machine pool name should have been caught");
        }

        [Test]
        public void ProviderReturnsTheExpectedResultWhenRequiredParametersAreMissing()
        {
            var component = this.mockFixture.Create<ExperimentComponent>();
            var result = this.provider.ExecuteAsync(this.mockFixture.Context, component, CancellationToken.None).Result;

            Assert.IsTrue(result.Status == ExecutionStatus.Failed);
        }

        [Test]
        public async Task ProviderAssignsTheExpectedGroupToNodeEntitiesWhenTheGlobalWildcardIsDefinedForTheStep()
        {
            ExperimentStepInstance step = this.mockFixture.Context.ExperimentStep;
            this.mockFixture.Context = new ExperimentContext(
                this.mockFixture.Context.Experiment, 
                new ExperimentStepInstance(
                    step.Id,
                    step.ExperimentId,
                    ExperimentComponent.AllGroups, // the global wildcard (group = *)
                    step.StepType,
                    step.Status,
                    step.Sequence,
                    step.Attempts,
                    step.Definition),
                this.mockFixture.Configuration);

            bool entityPoolSaved = false;
            this.mockFixture.DataClient.OnSaveEntityPool()
                .Callback<string, string, IEnumerable<EnvironmentEntity>, CancellationToken, string>((experimentId, key, state, token, stateId) =>
                {
                    entityPoolSaved = true;
                    CollectionAssert.AreEquivalent(state.Select(node => node.EnvironmentGroup).Distinct(), this.mockFixture.Context.GetExperimentGroups());
                })
                .Returns(Task.CompletedTask);

            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            Assert.IsTrue(entityPoolSaved);
        }

        [Test]
        public async Task ProviderAssignsTheExpectedGroupToNodeEntitiesWhenASpecificGroupIsDefinedForTheStep()
        {
            ExperimentStepInstance step = this.mockFixture.Context.ExperimentStep;
            this.mockFixture.Context = new ExperimentContext(
                this.mockFixture.Context.Experiment,
                new ExperimentStepInstance(
                    step.Id,
                    step.ExperimentId,
                    "Group A", // an explicit group definition
                    step.StepType,
                    step.Status,
                    step.Sequence,
                    step.Attempts,
                    step.Definition),
                this.mockFixture.Configuration);

            bool entityPoolSaved = false;
            this.mockFixture.DataClient.OnSaveEntityPool()
                .Callback<string, string, IEnumerable<EnvironmentEntity>, CancellationToken, string>((experimentId, key, state, token, stateId) =>
                {
                    entityPoolSaved = true;
                    Assert.IsTrue(state.Select(node => node.EnvironmentGroup).Distinct().Count() == 1);
                    Assert.IsTrue(state.Select(node => node.EnvironmentGroup).First() == "Group A");
                })
                .Returns(Task.CompletedTask);

            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            Assert.IsTrue(entityPoolSaved);
        }

        [Test]
        public void ProviderConvertsKustoResultsIntoTheExpectedNodeEntities_Scenario_1()
        {
            // This scenario has a single group in the experiment.
            this.mockDataTable.Clear();
            this.AddEntityRow("Cluster01-Rack01-Node01", "Cluster01", "Rack01", "Region01", "MP01");
            this.AddEntityRow("Cluster01-Rack01-Node02", "Cluster01", "Rack01", "Region01", "MP01");
            this.AddEntityRow("Cluster01-Rack02-Node03", "Cluster01", "Rack02", "Region01", "MP02");
            this.AddEntityRow("Cluster01-Rack02-Node04", "Cluster01", "Rack02", "Region01", "MP02");
            this.AddEntityRow("Cluster01-Rack03-Node05", "Cluster01", "Rack03", "Region01", "MP03");
            this.AddEntityRow("Cluster01-Rack03-Node06", "Cluster01", "Rack03", "Region01", "MP03");

            IEnumerable<EnvironmentEntity> entities = TestNodeSelectionProvider.ConvertToEntities(this.mockDataTable, new string[] { "Group A" });

            Assert.IsNotNull(entities);
            CollectionAssert.IsNotEmpty(entities);
            CollectionAssert.AllItemsAreUnique(entities);
            Assert.IsTrue(entities.Count() == this.mockDataTable.Rows.Count);
            Assert.IsTrue(entities.Select(entity => entity.EntityType).Distinct().Count() == 1);
            Assert.IsTrue(entities.Select(entity => entity.EntityType).Distinct().First() == EntityType.Node);
            Assert.IsTrue(entities.Select(entity => entity.EnvironmentGroup).Distinct().Count() == 1);

            // Group A nodes
            CollectionAssert.AreEquivalent(entities, entities.Where(entity => entity.EnvironmentGroup == "Group A"));
        }

        [Test]
        public void ProviderConvertsKustoResultsIntoTheExpectedNodeEntities_Scenario_2()
        {
            // This scenario has an even number of nodes for each cluster + rack combination
            // (i.e. 2 node subsets).
            this.mockDataTable.Clear();
            this.AddEntityRow("Cluster01-Rack01-Node01", "Cluster01", "Rack01", "Region01", "MP01"); // Group A
            this.AddEntityRow("Cluster01-Rack01-Node02", "Cluster01", "Rack01", "Region01", "MP01"); // Group B
            this.AddEntityRow("Cluster01-Rack02-Node03", "Cluster01", "Rack02", "Region01", "MP02"); // Group A
            this.AddEntityRow("Cluster01-Rack02-Node04", "Cluster01", "Rack02", "Region01", "MP02"); // Group B
            this.AddEntityRow("Cluster01-Rack03-Node05", "Cluster01", "Rack03", "Region01", "MP03"); // Group A
            this.AddEntityRow("Cluster01-Rack03-Node06", "Cluster01", "Rack03", "Region01", "MP03"); // Group B

            IEnumerable<EnvironmentEntity> entities = TestNodeSelectionProvider.ConvertToEntities(this.mockDataTable, new string[] { "Group A", "Group B" });

            Assert.IsNotNull(entities);
            CollectionAssert.IsNotEmpty(entities);
            CollectionAssert.AllItemsAreUnique(entities);
            Assert.IsTrue(entities.Count() == this.mockDataTable.Rows.Count);
            Assert.IsTrue(entities.Select(entity => entity.EntityType).Distinct().Count() == 1);
            Assert.IsTrue(entities.Select(entity => entity.EntityType).Distinct().First() == EntityType.Node);
            Assert.IsTrue(entities.Select(entity => entity.EnvironmentGroup).Distinct().Count() == 2);

            // Group A nodes
            CollectionAssert.AreEquivalent(
                new List<EnvironmentEntity>
                {
                    entities.ElementAt(0),
                    entities.ElementAt(2),
                    entities.ElementAt(4)
                },
                entities.Where(entity => entity.EnvironmentGroup == "Group A"));

            // Group B nodes
            CollectionAssert.AreEquivalent(
                new List<EnvironmentEntity>
                {
                    entities.ElementAt(1),
                    entities.ElementAt(3),
                    entities.ElementAt(5)
                },
                entities.Where(entity => entity.EnvironmentGroup == "Group B"));
        }

        [Test]
        public void ProviderConvertsKustoResultsIntoTheExpectedNodeEntities_Scenario_3()
        {
            // This scenario has an uneven number of nodes for each cluster + rack combination.
            this.mockDataTable.Clear();
            this.AddEntityRow("Cluster01-Rack01-Node01", "Cluster01", "Rack01", "Region01", "MP01"); // Group A
            this.AddEntityRow("Cluster01-Rack01-Node02", "Cluster01", "Rack01", "Region01", "MP01"); // Group B
            this.AddEntityRow("Cluster01-Rack01-Node03", "Cluster01", "Rack01", "Region01", "MP01"); // Group A
            this.AddEntityRow("Cluster01-Rack01-Node04", "Cluster01", "Rack01", "Region01", "MP01"); // Group B
            this.AddEntityRow("Cluster01-Rack02-Node05", "Cluster01", "Rack02", "Region01", "MP02"); // Group A
            this.AddEntityRow("Cluster01-Rack02-Node06", "Cluster01", "Rack02", "Region01", "MP02"); // Group B
            this.AddEntityRow("Cluster01-Rack02-Node07", "Cluster01", "Rack02", "Region01", "MP02"); // Group A
            this.AddEntityRow("Cluster01-Rack03-Node08", "Cluster02", "Rack03", "Region02", "MP03"); // Group B
            this.AddEntityRow("Cluster01-Rack03-Node09", "Cluster02", "Rack03", "Region02", "MP03"); // Group A

            IEnumerable<EnvironmentEntity> entities = TestNodeSelectionProvider.ConvertToEntities(this.mockDataTable, new string[] { "Group A", "Group B" });

            Assert.IsNotNull(entities);
            CollectionAssert.IsNotEmpty(entities);
            CollectionAssert.AllItemsAreUnique(entities);
            Assert.IsTrue(entities.Count() == this.mockDataTable.Rows.Count);
            Assert.IsTrue(entities.Select(entity => entity.EntityType).Distinct().Count() == 1);
            Assert.IsTrue(entities.Select(entity => entity.EntityType).Distinct().First() == EntityType.Node);
            Assert.IsTrue(entities.Select(entity => entity.EnvironmentGroup).Distinct().Count() == 2);

            // Group A nodes
            CollectionAssert.AreEquivalent(
                new List<EnvironmentEntity>
                {
                    entities.ElementAt(0),
                    entities.ElementAt(2),
                    entities.ElementAt(4),
                    entities.ElementAt(6),
                    entities.ElementAt(8)
                },
                entities.Where(entity => entity.EnvironmentGroup == "Group A"));

            // Group B nodes
            CollectionAssert.AreEquivalent(
                new List<EnvironmentEntity>
                {
                    entities.ElementAt(1),
                    entities.ElementAt(3),
                    entities.ElementAt(5),
                    entities.ElementAt(7)
                },
                entities.Where(entity => entity.EnvironmentGroup == "Group B"));
        }

        [Test]
        public void ProviderConvertsKustoResultsIntoTheExpectedNodeEntities_Scenario_5()
        {
            // This scenario includes 3 groups having an uneven number of nodes for each cluster + rack combination
            // and some subsets that will not even have nodes in one of the groups.
            this.mockDataTable.Clear();
            this.AddEntityRow("Cluster01-Rack01-Node01", "Cluster01", "Rack01", "Region01", "MP01"); // Group A
            this.AddEntityRow("Cluster01-Rack01-Node02", "Cluster01", "Rack01", "Region01", "MP01"); // Group B
            this.AddEntityRow("Cluster01-Rack01-Node03", "Cluster01", "Rack01", "Region01", "MP01"); // Group C
            this.AddEntityRow("Cluster01-Rack01-Node04", "Cluster01", "Rack01", "Region01", "MP01"); // Group A
            this.AddEntityRow("Cluster01-Rack02-Node05", "Cluster01", "Rack02", "Region01", "MP02"); // Group B
            this.AddEntityRow("Cluster01-Rack02-Node06", "Cluster01", "Rack02", "Region01", "MP02"); // Group C
            this.AddEntityRow("Cluster01-Rack02-Node07", "Cluster01", "Rack02", "Region01", "MP02"); // Group A
            this.AddEntityRow("Cluster01-Rack03-Node08", "Cluster02", "Rack03", "Region02", "MP03"); // Group B

            IEnumerable<EnvironmentEntity> entities = TestNodeSelectionProvider.ConvertToEntities(this.mockDataTable, new string[] { "Group A", "Group B", "Group C", });

            Assert.IsNotNull(entities);
            CollectionAssert.IsNotEmpty(entities);
            CollectionAssert.AllItemsAreUnique(entities);
            Assert.IsTrue(entities.Count() == this.mockDataTable.Rows.Count);
            Assert.IsTrue(entities.Select(entity => entity.EntityType).Distinct().Count() == 1);
            Assert.IsTrue(entities.Select(entity => entity.EntityType).Distinct().First() == EntityType.Node);
            Assert.IsTrue(entities.Select(entity => entity.EnvironmentGroup).Distinct().Count() == 3);

            // Group A nodes
            CollectionAssert.AreEquivalent(
                new List<EnvironmentEntity>
                {
                    entities.ElementAt(0),
                    entities.ElementAt(3),
                    entities.ElementAt(6)
                },
                entities.Where(entity => entity.EnvironmentGroup == "Group A"));

            // Group B nodes
            CollectionAssert.AreEquivalent(
                new List<EnvironmentEntity>
                {
                    entities.ElementAt(1),
                    entities.ElementAt(4),
                    entities.ElementAt(7)
                },
                entities.Where(entity => entity.EnvironmentGroup == "Group B"));

            // Group C nodes
            CollectionAssert.AreEquivalent(
                new List<EnvironmentEntity>
                {
                    entities.ElementAt(2),
                    entities.ElementAt(5)
                },
                entities.Where(entity => entity.EnvironmentGroup == "Group C"));
        }

        [Test]
        public void ProviderConvertsKustoResultsIntoTheExpectedNodeEntities_Scenario_6()
        {
            // This scenario has nodes, none of which are in the same cluster or rack. This is a special scenario
            // (e.g. DifferentCluster) where the logic cannot rely upon the normal grouping semantics.
            this.mockDataTable.Clear();
            this.AddEntityRow("Cluster01-Rack01-Node01", "Cluster01", "Rack01", "Region01", "MP01"); // Group A
            this.AddEntityRow("Cluster01-Rack02-Node02", "Cluster01", "Rack02", "Region02", "MP02"); // Group B
            this.AddEntityRow("Cluster01-Rack03-Node03", "Cluster01", "Rack03", "Region03", "MP03"); // Group A
            this.AddEntityRow("Cluster02-Rack04-Node04", "Cluster02", "Rack04", "Region04", "MP04"); // Group B
            this.AddEntityRow("Cluster02-Rack05-Node05", "Cluster02", "Rack05", "Region05", "MP05"); // Group A
            this.AddEntityRow("Cluster02-Rack06-Node06", "Cluster02", "Rack06", "Region06", "MP06"); // Group B

            IEnumerable<EnvironmentEntity> entities = TestNodeSelectionProvider.ConvertToEntities(this.mockDataTable, new string[] { "Group A", "Group B" });

            Assert.IsNotNull(entities);
            CollectionAssert.IsNotEmpty(entities);
            CollectionAssert.AllItemsAreUnique(entities);
            Assert.IsTrue(entities.Count() == this.mockDataTable.Rows.Count);
            Assert.IsTrue(entities.Select(entity => entity.EntityType).Distinct().Count() == 1);
            Assert.IsTrue(entities.Select(entity => entity.EntityType).Distinct().First() == EntityType.Node);
            Assert.IsTrue(entities.Select(entity => entity.EnvironmentGroup).Distinct().Count() == 2);

            // Group A nodes
            CollectionAssert.AreEquivalent(
                new List<EnvironmentEntity>
                {
                    entities.ElementAt(0),
                    entities.ElementAt(2),
                    entities.ElementAt(4)
                },
                entities.Where(entity => entity.EnvironmentGroup == "Group A"));

            // Group B nodes
            CollectionAssert.AreEquivalent(
                new List<EnvironmentEntity>
                {
                    entities.ElementAt(1),
                    entities.ElementAt(3),
                    entities.ElementAt(5)
                },
                entities.Where(entity => entity.EnvironmentGroup == "Group B"));
        }

        [Test]
        public void ProviderConvertsKustoResultsIntoTheExpectedNodeEntities_Scenario_7()
        {
            // This scenario has nodes, none of which are in the same cluster or rack. This is a special scenario
            // (e.g. DifferentCluster) where the logic cannot rely upon the normal grouping semantics.
            this.mockDataTable.Clear();
            this.AddEntityRow("Cluster01-Rack01-Node01", "Cluster01", "Rack01", "Region01", "MP01"); // Group A
            this.AddEntityRow("Cluster02-Rack02-Node02", "Cluster02", "Rack02", "Region02", "MP02"); // Group B
            this.AddEntityRow("Cluster03-Rack03-Node03", "Cluster03", "Rack03", "Region03", "MP03"); // Group A
            this.AddEntityRow("Cluster04-Rack04-Node04", "Cluster04", "Rack04", "Region04", "MP04"); // Group B
            this.AddEntityRow("Cluster05-Rack05-Node05", "Cluster05", "Rack05", "Region05", "MP05"); // Group A
            this.AddEntityRow("Cluster06-Rack06-Node06", "Cluster06", "Rack06", "Region06", "MP06"); // Group B

            IEnumerable<EnvironmentEntity> entities = TestNodeSelectionProvider.ConvertToEntities(this.mockDataTable, new string[] { "Group A", "Group B" });

            Assert.IsNotNull(entities);
            CollectionAssert.IsNotEmpty(entities);
            CollectionAssert.AllItemsAreUnique(entities);
            Assert.IsTrue(entities.Count() == this.mockDataTable.Rows.Count);
            Assert.IsTrue(entities.Select(entity => entity.EntityType).Distinct().Count() == 1);
            Assert.IsTrue(entities.Select(entity => entity.EntityType).Distinct().First() == EntityType.Node);
            Assert.IsTrue(entities.Select(entity => entity.EnvironmentGroup).Distinct().Count() == 2);

            // Group A nodes
            CollectionAssert.AreEquivalent(
                new List<EnvironmentEntity>
                {
                    entities.ElementAt(0),
                    entities.ElementAt(2),
                    entities.ElementAt(4)
                },
                entities.Where(entity => entity.EnvironmentGroup == "Group A"));

            // Group B nodes
            CollectionAssert.AreEquivalent(
                new List<EnvironmentEntity>
                {
                    entities.ElementAt(1),
                    entities.ElementAt(3),
                    entities.ElementAt(5)
                },
                entities.Where(entity => entity.EnvironmentGroup == "Group B"));
        }

        [Test]
        public async Task ProviderSavesTheExpectedNodeEntitiesToTheEntityPool()
        {
            bool entityPoolSaved = false;
            this.mockFixture.DataClient.OnSaveEntityPool()
                .Callback<string, string, IEnumerable<EnvironmentEntity>, CancellationToken, string>((experimentId, key, state, token, stateId) =>
                {
                    entityPoolSaved = true;
                    CollectionAssert.AreEquivalent(this.mockFixture.EntityManager.EntityPool, state);
                })
                .Returns(Task.CompletedTask);

            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            Assert.IsTrue(entityPoolSaved);
        }

        private static DataTable GetValidDataTable(bool malformedResponse = false)
        {
            // Here we create a DataTable with four columns.
            DataTable table = new DataTable();

            table.Columns.Add(Constants.MachinePoolName);
            table.Columns.Add(Constants.RackLocation);
            table.Columns.Add(Constants.Region);
            table.Columns.Add(Constants.NodeId);
            table.Columns.Add(Constants.ClusterName);
            table.Columns.Add(Constants.SupportedVmSkus);

            table.Rows.Add(
                "MP01",
                "Rack01",
                "Region01",
                "23610dfa-8a74-40f9-874e-23ae45219e5b",
                "Cluster01mp1",
                malformedResponse ? null : "[  \"Standard_E64s_v3\",  \"Standard_E64s_v3\"]");
            return table;
        }

        private void AddEntityRow(string nodeId, string clusterName, string rackLocation, string region = null, string machinePool = null)
        {
            this.mockDataTable.Rows.Add(
                machinePool ?? $"{clusterName}-MP01",
                rackLocation,
                region ?? "Region01",
                nodeId ?? Guid.NewGuid().ToString(),
                clusterName,
                "[ \"Standard_E64s_v3\", \"Standard_E64s_v3\" ]"); // JSON-formatted
        }

        private EnvironmentEntity CreateNode(string id, string environmentGroup, string clusterName, string rackLocation, string region = null, string machinePool = null)
        {
            return EnvironmentEntity.Node(id, environmentGroup, new Dictionary<string, IConvertible>
            {
                [nameof(EnvironmentEntityExtensions.ClusterName)] = clusterName,
                [nameof(EnvironmentEntityExtensions.RackLocation)] = rackLocation,
                [nameof(EnvironmentEntityExtensions.Region)] = region ?? "Region01",
                [nameof(EnvironmentEntityExtensions.MachinePoolName)] = machinePool ?? $"{clusterName}-MP01"
            });
        }

        private void SetupMockDefaultBehaviors()
        {
            this.mockFixture.DataClient.SetReturnsDefault(new HttpResponseMessage(HttpStatusCode.OK));

            // Get the entity pool from the backing store.
            this.mockFixture.DataClient.OnGetEntityPool()
                .ReturnsAsync(this.mockEntityPool);

            // Save the entity pool to the backing store.
            this.mockFixture.DataClient.OnSaveEntityPool()
                .Returns(Task.CompletedTask);

            // Get node information from the Kusto source.
            this.mockKustoClient
                .Setup(client => client.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult(this.mockDataTable));
        }

        private class TestNodeSelectionProvider : NodeSelectionProvider
        {
            public TestNodeSelectionProvider(IServiceCollection services)
                : base(services)
            {
            }

            public static new IList<EnvironmentEntity> ConvertToEntities(DataTable dataTable, params string[] environmentGroups)
            {
                return NodeSelectionProvider.ConvertToEntities(dataTable, environmentGroups);
            }
        }

        private class Constants
        {
            internal const string NodeId = nameof(Constants.NodeId);
            internal const string RackLocation = nameof(Constants.RackLocation);
            internal const string ClusterName = nameof(Constants.ClusterName);
            internal const string MachinePoolName = nameof(Constants.MachinePoolName);
            internal const string CpuId = nameof(Constants.CpuId);
            internal const string Region = nameof(Constants.Region);
            internal const string SupportedVmSkus = nameof(Constants.SupportedVmSkus);
        }
    }
}
