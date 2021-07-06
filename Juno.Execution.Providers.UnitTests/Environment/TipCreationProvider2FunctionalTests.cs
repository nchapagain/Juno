namespace Juno.Execution.Providers.Environment
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Execution.TipIntegration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;
    using TipGateway.Entities;

    [TestFixture]
    [Category("Functional")]
    [SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1025:Code should not contain multiple whitespace in a row", Justification = "Comments in unit tests are better aligned with spaces.")]
    public class TipCreationProvider2FunctionalTests
    {
        private ProviderFixture mockFixture;
        private Mock<ITipClient> mockTipClient;
        private List<EnvironmentEntity> mockEntityPool;
        private List<EnvironmentEntity> mockEntitiesProvisioned;
        private TipCreationProvider2 provider;
        private TipCreationProvider2.State mockState;

        [Test]
        public async Task ProviderSelectsTheExpectedTipSessionsForTheNodeAffinityDefined_AnyNodeScenario_1()
        {
            this.SetupMockDefaults(ExperimentType.AB);
            this.mockFixture.Component.Parameters["nodeAffinity"] = NodeAffinity.Any;

            // This scenario includes a pool of entities includes entities that are in different clusters, different racks etc...
            // This should not prevent the correct selection of nodes from the pool.
            this.mockEntityPool.AddRange(new List<EnvironmentEntity>
            {
                this.CreateNode("Cluster01-Rack01-Node01", "Group A", "Cluster01", "Rack01", "Region01"),
                this.CreateNode("Cluster02-Rack02-Node02", "Group B", "Cluster02", "Rack02", "Region02")
            });

            await this.ExecuteProviderAsync(stopOnStatus: ExecutionStatus.Succeeded);

            IEnumerable<EnvironmentEntity> selectedTipNodes = this.mockFixture.EntityManager.EntitiesProvisioned.GetNodes();
            IEnumerable<EnvironmentEntity> selectedTipSessions = this.mockFixture.EntityManager.EntitiesProvisioned.GetTipSessions();

            Assert.IsNotNull(selectedTipNodes);
            Assert.AreEqual(2, selectedTipNodes.Count());
            CollectionAssert.AllItemsAreUnique(selectedTipNodes);
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.Id));
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.EnvironmentGroup));
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.TipSessionId()));
            CollectionAssert.AreEquivalent(this.mockEntityPool, selectedTipNodes);

            Assert.IsNotNull(selectedTipSessions);
            Assert.AreEqual(2, selectedTipSessions.Count());
            CollectionAssert.AllItemsAreUnique(selectedTipSessions);
            CollectionAssert.AllItemsAreUnique(selectedTipSessions.Select(tipSession => tipSession.Id));
            CollectionAssert.AllItemsAreUnique(selectedTipSessions.Select(tipSession => tipSession.EnvironmentGroup));
            CollectionAssert.AreEquivalent(selectedTipNodes.Select(tipNode => tipNode.Id), selectedTipSessions.Select(tipSession => tipSession.NodeId()));
            CollectionAssert.AreEquivalent(selectedTipNodes.Select(tipNode => tipNode.TipSessionId()), selectedTipSessions.Select(tipSession => tipSession.Id));
        }

        [Test]
        public async Task ProviderSelectsTheExpectedTipSessionsForTheNodeAffinityDefined_AnyNodeScenario_2()
        {
            this.SetupMockDefaults(ExperimentType.AB);
            this.mockFixture.Component.Parameters["nodeAffinity"] = NodeAffinity.Any;

            // This scenario includes a pool of entities includes entities that are in different clusters, different racks etc...
            // The initial 2 TiP session creation attempts fail.
            this.mockEntityPool.AddRange(new List<EnvironmentEntity>
            {
                this.CreateNode("Cluster01-Rack01-Node01", "Group A", "Cluster01", "Rack01", "Region01"),
                this.CreateNode("Cluster02-Rack02-Node02", "Group B", "Cluster02", "Rack02", "Region02"),
                this.CreateNode("Cluster03-Rack03-Node03", "Group A", "Cluster03", "Rack03", "Region03"),
                this.CreateNode("Cluster04-Rack04-Node04", "Group B", "Cluster04", "Rack03", "Region04")
            });

            this.mockTipClient
                .SetupSequence(c => c.IsTipSessionChangeFailedAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)   // TiP creation failed
                .ReturnsAsync(true)   // TiP creation failed
                .ReturnsAsync(false)  // TiP creation succeeded
                .ReturnsAsync(false); // TiP creation succeeded

            await this.ExecuteProviderAsync(stopOnStatus: ExecutionStatus.Succeeded);

            IEnumerable<EnvironmentEntity> selectedTipNodes = this.mockFixture.EntityManager.EntitiesProvisioned.GetNodes();
            IEnumerable<EnvironmentEntity> selectedTipSessions = this.mockFixture.EntityManager.EntitiesProvisioned.GetTipSessions();

            Assert.IsNotNull(selectedTipNodes);
            Assert.IsTrue(selectedTipNodes.Count() == 2);
            CollectionAssert.AllItemsAreUnique(selectedTipNodes);
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.Id));
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.EnvironmentGroup));
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.TipSessionId()));

            Assert.IsNotNull(selectedTipSessions);
            Assert.IsTrue(selectedTipSessions.Count() == 2);
            CollectionAssert.AllItemsAreUnique(selectedTipSessions);
            CollectionAssert.AllItemsAreUnique(selectedTipSessions.Select(tipSession => tipSession.Id));
            CollectionAssert.AllItemsAreUnique(selectedTipSessions.Select(tipSession => tipSession.EnvironmentGroup));
            CollectionAssert.AreEquivalent(selectedTipNodes.Select(tipNode => tipNode.Id), selectedTipSessions.Select(tipSession => tipSession.NodeId()));
            CollectionAssert.AreEquivalent(selectedTipNodes.Select(tipNode => tipNode.TipSessionId()), selectedTipSessions.Select(tipSession => tipSession.Id));
        }

        [Test]
        public async Task ProviderSelectsTheExpectedTipSessionsForTheNodeAffinityDefined_AnyNodeScenario_3()
        {
            this.SetupMockDefaults(ExperimentType.AB);
            this.mockFixture.Component.Parameters["nodeAffinity"] = NodeAffinity.Any;

            // This scenario includes a pool of entities includes entities that are in different clusters, different racks etc...
            // Of the initial 2 TiP sessions, 1 succeeds and 1 fails. There are additional nodes that meet the node affinity
            // requirements to pair with the first successful TiP session creation.
            this.mockEntityPool.AddRange(new List<EnvironmentEntity>
            {
                this.CreateNode("Cluster01-Rack01-Node01", "Group A", "Cluster01", "Rack01", "Region01"),
                this.CreateNode("Cluster02-Rack02-Node02", "Group B", "Cluster02", "Rack02", "Region02"),
                this.CreateNode("Cluster03-Rack03-Node03", "Group A", "Cluster03", "Rack03", "Region03"),
                this.CreateNode("Cluster04-Rack04-Node04", "Group B", "Cluster04", "Rack03", "Region04")
            });

            this.mockTipClient
                .SetupSequence(c => c.IsTipSessionChangeFailedAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false)   // TiP creation failed
                .ReturnsAsync(true)    // TiP creation failed
                .ReturnsAsync(false);  // TiP creation succeeded

            await this.ExecuteProviderAsync(stopOnStatus: ExecutionStatus.Succeeded);

            IEnumerable<EnvironmentEntity> selectedTipNodes = this.mockFixture.EntityManager.EntitiesProvisioned.GetNodes();
            IEnumerable<EnvironmentEntity> selectedTipSessions = this.mockFixture.EntityManager.EntitiesProvisioned.GetTipSessions();

            Assert.IsNotNull(selectedTipNodes);
            Assert.IsTrue(selectedTipNodes.Count() == 2);
            CollectionAssert.AllItemsAreUnique(selectedTipNodes);
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.Id));
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.EnvironmentGroup));
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.TipSessionId()));

            Assert.IsNotNull(selectedTipSessions);
            Assert.IsTrue(selectedTipSessions.Count() == 2);
            CollectionAssert.AllItemsAreUnique(selectedTipSessions);
            CollectionAssert.AllItemsAreUnique(selectedTipSessions.Select(tipSession => tipSession.Id));
            CollectionAssert.AllItemsAreUnique(selectedTipSessions.Select(tipSession => tipSession.EnvironmentGroup));
            CollectionAssert.AreEquivalent(selectedTipNodes.Select(tipNode => tipNode.Id), selectedTipSessions.Select(tipSession => tipSession.NodeId()));
            CollectionAssert.AreEquivalent(selectedTipNodes.Select(tipNode => tipNode.TipSessionId()), selectedTipSessions.Select(tipSession => tipSession.Id));
        }

        [Test]
        public async Task ProviderSelectsTheExpectedTipSessionsForTheNodeAffinityDefined_AnyNodeScenario_4()
        {
            // This scenario includes more than 2 environment groups (e.g. A/B/C)
            this.SetupMockDefaults(ExperimentType.ABC);
            this.mockFixture.Component.Parameters["nodeAffinity"] = NodeAffinity.Any;

            this.mockEntityPool.AddRange(new List<EnvironmentEntity>
            {
                this.CreateNode("Cluster01-Rack01-Node01", "Group A", "Cluster01", "Rack01", "Region01"),
                this.CreateNode("Cluster02-Rack02-Node02", "Group B", "Cluster02", "Rack02", "Region02"),
                this.CreateNode("Cluster03-Rack03-Node03", "Group C", "Cluster03", "Rack03", "Region03")
            });

            await this.ExecuteProviderAsync(stopOnStatus: ExecutionStatus.Succeeded);

            IEnumerable<EnvironmentEntity> selectedTipNodes = this.mockFixture.EntityManager.EntitiesProvisioned.GetNodes();
            IEnumerable<EnvironmentEntity> selectedTipSessions = this.mockFixture.EntityManager.EntitiesProvisioned.GetTipSessions();

            Assert.IsNotNull(selectedTipNodes);
            Assert.IsTrue(selectedTipNodes.Count() == 3);
            CollectionAssert.AllItemsAreUnique(selectedTipNodes);
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.Id));
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.EnvironmentGroup));
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.TipSessionId()));

            Assert.IsNotNull(selectedTipSessions);
            Assert.IsTrue(selectedTipSessions.Count() == 3);
            CollectionAssert.AllItemsAreUnique(selectedTipSessions);
            CollectionAssert.AllItemsAreUnique(selectedTipSessions.Select(tipSession => tipSession.Id));
            CollectionAssert.AllItemsAreUnique(selectedTipSessions.Select(tipSession => tipSession.EnvironmentGroup));
            CollectionAssert.AreEquivalent(selectedTipNodes.Select(tipNode => tipNode.Id), selectedTipSessions.Select(tipSession => tipSession.NodeId()));
            CollectionAssert.AreEquivalent(selectedTipNodes.Select(tipNode => tipNode.TipSessionId()), selectedTipSessions.Select(tipSession => tipSession.Id));
        }

        [Test]
        public async Task ProviderSelectsTheExpectedTipSessionsForTheNodeAffinityDefined_DifferentClusterScenario_1()
        {
            this.SetupMockDefaults(ExperimentType.AB);
            this.mockFixture.Component.Parameters["nodeAffinity"] = NodeAffinity.DifferentCluster;

            this.mockEntityPool.AddRange(new List<EnvironmentEntity>
            {
                this.CreateNode("Cluster01-Rack01-Node01", "Group A", "Cluster01", "Rack01", "Region01"),
                this.CreateNode("Cluster02-Rack02-Node02", "Group B", "Cluster02", "Rack02", "Region02")
            });

            await this.ExecuteProviderAsync(stopOnStatus: ExecutionStatus.Succeeded);

            IEnumerable<EnvironmentEntity> selectedTipNodes = this.mockFixture.EntityManager.EntitiesProvisioned.GetNodes();
            IEnumerable<EnvironmentEntity> selectedTipSessions = this.mockFixture.EntityManager.EntitiesProvisioned.GetTipSessions();

            Assert.IsNotNull(selectedTipNodes);
            Assert.IsTrue(selectedTipNodes.Count() == 2);
            CollectionAssert.AllItemsAreUnique(selectedTipNodes);
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.Id));
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.EnvironmentGroup));
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.TipSessionId()));
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.ClusterName()));

            Assert.IsNotNull(selectedTipSessions);
            Assert.IsTrue(selectedTipSessions.Count() == 2);
            CollectionAssert.AllItemsAreUnique(selectedTipSessions);
            CollectionAssert.AllItemsAreUnique(selectedTipSessions.Select(tipSession => tipSession.Id));
            CollectionAssert.AllItemsAreUnique(selectedTipSessions.Select(tipSession => tipSession.EnvironmentGroup));
            CollectionAssert.AllItemsAreUnique(selectedTipSessions.Select(tipSession => tipSession.ClusterName()));
            CollectionAssert.AreEquivalent(selectedTipNodes.Select(tipNode => tipNode.Id), selectedTipSessions.Select(tipSession => tipSession.NodeId()));
            CollectionAssert.AreEquivalent(selectedTipNodes.Select(tipNode => tipNode.TipSessionId()), selectedTipSessions.Select(tipSession => tipSession.Id));
        }

        [Test]
        public async Task ProviderSelectsTheExpectedTipSessionsForTheNodeAffinityDefined_DifferentClusterScenario_2()
        {
            this.SetupMockDefaults(ExperimentType.AB);
            this.mockFixture.Component.Parameters["nodeAffinity"] = NodeAffinity.DifferentCluster;

            // This scenario includes a pool of entities includes entities that are in different clusters, different racks etc...
            // The initial 2 TiP session creation attempts fail.
            this.mockEntityPool.AddRange(new List<EnvironmentEntity>
            {
                this.CreateNode("Cluster01-Rack01-Node01", "Group A", "Cluster01", "Rack01", "Region01"),
                this.CreateNode("Cluster02-Rack02-Node02", "Group B", "Cluster02", "Rack02", "Region02"),
                this.CreateNode("Cluster03-Rack03-Node03", "Group A", "Cluster03", "Rack03", "Region03"),
                this.CreateNode("Cluster04-Rack04-Node04", "Group B", "Cluster04", "Rack03", "Region04")
            });

            this.mockTipClient
                .SetupSequence(c => c.IsTipSessionChangeFailedAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)   // TiP creation failed
                .ReturnsAsync(true)   // TiP creation failed
                .ReturnsAsync(false)  // TiP creation succeeded
                .ReturnsAsync(false); // TiP creation succeeded

            await this.ExecuteProviderAsync(stopOnStatus: ExecutionStatus.Succeeded);

            IEnumerable<EnvironmentEntity> selectedTipNodes = this.mockFixture.EntityManager.EntitiesProvisioned.GetNodes();
            IEnumerable<EnvironmentEntity> selectedTipSessions = this.mockFixture.EntityManager.EntitiesProvisioned.GetTipSessions();

            Assert.IsNotNull(selectedTipNodes);
            Assert.IsTrue(selectedTipNodes.Count() == 2);
            CollectionAssert.AllItemsAreUnique(selectedTipNodes);
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.Id));
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.EnvironmentGroup));
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.TipSessionId()));
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.ClusterName()));

            Assert.IsNotNull(selectedTipSessions);
            Assert.IsTrue(selectedTipSessions.Count() == 2);
            CollectionAssert.AllItemsAreUnique(selectedTipSessions);
            CollectionAssert.AllItemsAreUnique(selectedTipSessions.Select(tipSession => tipSession.Id));
            CollectionAssert.AllItemsAreUnique(selectedTipSessions.Select(tipSession => tipSession.EnvironmentGroup));
            CollectionAssert.AllItemsAreUnique(selectedTipSessions.Select(tipSession => tipSession.ClusterName()));
            CollectionAssert.AreEquivalent(selectedTipNodes.Select(tipNode => tipNode.Id), selectedTipSessions.Select(tipSession => tipSession.NodeId()));
            CollectionAssert.AreEquivalent(selectedTipNodes.Select(tipNode => tipNode.TipSessionId()), selectedTipSessions.Select(tipSession => tipSession.Id));
        }

        [Test]
        public async Task ProviderSelectsTheExpectedTipSessionsForTheNodeAffinityDefined_DifferentClusterScenario_3()
        {
            this.SetupMockDefaults(ExperimentType.AB);
            this.mockFixture.Component.Parameters["nodeAffinity"] = NodeAffinity.DifferentCluster;

            // This scenario includes a pool of entities includes entities that are in different clusters, different racks etc...
            // Of the initial 2 TiP sessions, 1 succeeds and 1 fails. There are additional nodes that meet the node affinity
            // requirements to pair with the first successful TiP session creation.
            this.mockEntityPool.AddRange(new List<EnvironmentEntity>
            {
                this.CreateNode("Cluster01-Rack01-Node01", "Group A", "Cluster01", "Rack01", "Region01"),
                this.CreateNode("Cluster02-Rack02-Node02", "Group B", "Cluster02", "Rack02", "Region02"),
                this.CreateNode("Cluster03-Rack03-Node03", "Group A", "Cluster03", "Rack03", "Region03"),
                this.CreateNode("Cluster04-Rack04-Node04", "Group B", "Cluster04", "Rack03", "Region04")
            });

            this.mockTipClient
                .SetupSequence(c => c.IsTipSessionChangeFailedAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false)   // TiP creation failed
                .ReturnsAsync(true)    // TiP creation failed
                .ReturnsAsync(false);  // TiP creation succeeded

            await this.ExecuteProviderAsync(stopOnStatus: ExecutionStatus.Succeeded);

            IEnumerable<EnvironmentEntity> selectedTipNodes = this.mockFixture.EntityManager.EntitiesProvisioned.GetNodes();
            IEnumerable<EnvironmentEntity> selectedTipSessions = this.mockFixture.EntityManager.EntitiesProvisioned.GetTipSessions();

            Assert.IsNotNull(selectedTipNodes);
            Assert.IsTrue(selectedTipNodes.Count() == 2);
            CollectionAssert.AllItemsAreUnique(selectedTipNodes);
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.Id));
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.EnvironmentGroup));
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.TipSessionId()));

            Assert.IsNotNull(selectedTipSessions);
            Assert.IsTrue(selectedTipSessions.Count() == 2);
            CollectionAssert.AllItemsAreUnique(selectedTipSessions);
            CollectionAssert.AllItemsAreUnique(selectedTipSessions.Select(tipSession => tipSession.Id));
            CollectionAssert.AllItemsAreUnique(selectedTipSessions.Select(tipSession => tipSession.EnvironmentGroup));
            CollectionAssert.AreEquivalent(selectedTipNodes.Select(tipNode => tipNode.Id), selectedTipSessions.Select(tipSession => tipSession.NodeId()));
            CollectionAssert.AreEquivalent(selectedTipNodes.Select(tipNode => tipNode.TipSessionId()), selectedTipSessions.Select(tipSession => tipSession.Id));
        }

        [Test]
        public async Task ProviderSelectsTheExpectedTipSessionsForTheNodeAffinityDefined_DifferentClusterScenario_4()
        {
            // This scenario includes more than 2 environment groups (e.g. A/B/C)
            this.SetupMockDefaults(ExperimentType.ABC);
            this.mockFixture.Component.Parameters["nodeAffinity"] = NodeAffinity.DifferentCluster;

            this.mockEntityPool.AddRange(new List<EnvironmentEntity>
            {
                this.CreateNode("Cluster01-Rack01-Node01", "Group A", "Cluster01", "Rack01", "Region01"),
                this.CreateNode("Cluster02-Rack02-Node02", "Group B", "Cluster02", "Rack02", "Region02"),
                this.CreateNode("Cluster03-Rack03-Node03", "Group C", "Cluster03", "Rack03", "Region03")
            });

            await this.ExecuteProviderAsync(stopOnStatus: ExecutionStatus.Succeeded);

            IEnumerable<EnvironmentEntity> selectedTipNodes = this.mockFixture.EntityManager.EntitiesProvisioned.GetNodes();
            IEnumerable<EnvironmentEntity> selectedTipSessions = this.mockFixture.EntityManager.EntitiesProvisioned.GetTipSessions();

            Assert.IsNotNull(selectedTipNodes);
            Assert.IsTrue(selectedTipNodes.Count() == 3);
            CollectionAssert.AllItemsAreUnique(selectedTipNodes);
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.Id));
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.EnvironmentGroup));
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.TipSessionId()));

            Assert.IsNotNull(selectedTipSessions);
            Assert.IsTrue(selectedTipSessions.Count() == 3);
            CollectionAssert.AllItemsAreUnique(selectedTipSessions);
            CollectionAssert.AllItemsAreUnique(selectedTipSessions.Select(tipSession => tipSession.Id));
            CollectionAssert.AllItemsAreUnique(selectedTipSessions.Select(tipSession => tipSession.EnvironmentGroup));
            CollectionAssert.AreEquivalent(selectedTipNodes.Select(tipNode => tipNode.Id), selectedTipSessions.Select(tipSession => tipSession.NodeId()));
            CollectionAssert.AreEquivalent(selectedTipNodes.Select(tipNode => tipNode.TipSessionId()), selectedTipSessions.Select(tipSession => tipSession.Id));
        }

        [Test]
        public async Task ProviderSelectsTheExpectedTipSessionsForTheNodeAffinityDefined_SameClusterScenario_1()
        {
            this.SetupMockDefaults(ExperimentType.AB);
            this.mockFixture.Component.Parameters["nodeAffinity"] = NodeAffinity.SameCluster;

            this.mockEntityPool.AddRange(new List<EnvironmentEntity>
            {
                this.CreateNode("Cluster01-Rack01-Node01", "Group A", "Cluster01", "Rack01", "Region01"),
                this.CreateNode("Cluster01-Rack02-Node02", "Group B", "Cluster01", "Rack02", "Region01")
            });

            await this.ExecuteProviderAsync(stopOnStatus: ExecutionStatus.Succeeded);

            IEnumerable<EnvironmentEntity> selectedTipNodes = this.mockFixture.EntityManager.EntitiesProvisioned.GetNodes();
            IEnumerable<EnvironmentEntity> selectedTipSessions = this.mockFixture.EntityManager.EntitiesProvisioned.GetTipSessions();

            Assert.IsNotNull(selectedTipNodes);
            Assert.IsTrue(selectedTipNodes.Count() == 2);
            CollectionAssert.AllItemsAreUnique(selectedTipNodes);
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.Id));
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.EnvironmentGroup));
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.TipSessionId()));
            Assert.IsTrue(selectedTipNodes.Select(tipNode => tipNode.ClusterName()).Distinct().Count() == 1);

            Assert.IsNotNull(selectedTipSessions);
            Assert.IsTrue(selectedTipSessions.Count() == 2);
            CollectionAssert.AllItemsAreUnique(selectedTipSessions);
            CollectionAssert.AllItemsAreUnique(selectedTipSessions.Select(tipSession => tipSession.Id));
            CollectionAssert.AllItemsAreUnique(selectedTipSessions.Select(tipSession => tipSession.EnvironmentGroup));
            CollectionAssert.AreEquivalent(selectedTipNodes.Select(tipNode => tipNode.Id), selectedTipSessions.Select(tipSession => tipSession.NodeId()));
            CollectionAssert.AreEquivalent(selectedTipNodes.Select(tipNode => tipNode.TipSessionId()), selectedTipSessions.Select(tipSession => tipSession.Id));
        }

        [Test]
        public async Task ProviderSelectsTheExpectedTipSessionsForTheNodeAffinityDefined_SameClusterScenario_2()
        {
            this.SetupMockDefaults(ExperimentType.AB);
            this.mockFixture.Component.Parameters["nodeAffinity"] = NodeAffinity.SameCluster;

            // This scenario includes a pool of entities includes entities that are in different clusters, different racks etc...
            // The initial 2 TiP session creation attempts fail.
            this.mockEntityPool.AddRange(new List<EnvironmentEntity>
            {
                this.CreateNode("Cluster01-Rack01-Node01", "Group A", "Cluster01", "Rack01", "Region01"),
                this.CreateNode("Cluster01-Rack02-Node02", "Group B", "Cluster01", "Rack02", "Region01"),
                this.CreateNode("Cluster02-Rack03-Node03", "Group A", "Cluster02", "Rack03", "Region01"),
                this.CreateNode("Cluster02-Rack04-Node04", "Group B", "Cluster02", "Rack04", "Region01")
            });

            this.mockTipClient
                .SetupSequence(c => c.IsTipSessionChangeFailedAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)   // TiP creation failed
                .ReturnsAsync(true)   // TiP creation failed
                .ReturnsAsync(false)  // TiP creation succeeded
                .ReturnsAsync(false); // TiP creation succeeded

            await this.ExecuteProviderAsync(stopOnStatus: ExecutionStatus.Succeeded);

            IEnumerable<EnvironmentEntity> selectedTipNodes = this.mockFixture.EntityManager.EntitiesProvisioned.GetNodes();
            IEnumerable<EnvironmentEntity> selectedTipSessions = this.mockFixture.EntityManager.EntitiesProvisioned.GetTipSessions();

            Assert.IsNotNull(selectedTipNodes);
            Assert.IsTrue(selectedTipNodes.Count() == 2);
            CollectionAssert.AllItemsAreUnique(selectedTipNodes);
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.Id));
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.EnvironmentGroup));
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.TipSessionId()));
            Assert.IsTrue(selectedTipNodes.Select(tipNode => tipNode.ClusterName()).Distinct().Count() == 1);

            Assert.IsNotNull(selectedTipSessions);
            Assert.IsTrue(selectedTipSessions.Count() == 2);
            CollectionAssert.AllItemsAreUnique(selectedTipSessions);
            CollectionAssert.AllItemsAreUnique(selectedTipSessions.Select(tipSession => tipSession.Id));
            CollectionAssert.AllItemsAreUnique(selectedTipSessions.Select(tipSession => tipSession.EnvironmentGroup));
            Assert.IsTrue(selectedTipSessions.Select(tipSession => tipSession.ClusterName()).Distinct().Count() == 1);
            CollectionAssert.AreEquivalent(selectedTipNodes.Select(tipNode => tipNode.Id), selectedTipSessions.Select(tipSession => tipSession.NodeId()));
            CollectionAssert.AreEquivalent(selectedTipNodes.Select(tipNode => tipNode.TipSessionId()), selectedTipSessions.Select(tipSession => tipSession.Id));
        }

        [Test]
        public async Task ProviderSelectsTheExpectedTipSessionsForTheNodeAffinityDefined_SameClusterScenario_3()
        {
            this.SetupMockDefaults(ExperimentType.AB);
            this.mockFixture.Component.Parameters["nodeAffinity"] = NodeAffinity.SameCluster;

            // This scenario includes a pool of entities includes entities that are in different clusters, different racks etc...
            // Of the initial 2 TiP sessions, 1 succeeds and 1 fails. There are additional nodes that meet the node affinity
            // requirements to pair with the first successful TiP session creation.
            this.mockEntityPool.AddRange(new List<EnvironmentEntity>
            {
                this.CreateNode("Cluster01-Rack01-Node01", "Group A", "Cluster01", "Rack01", "Region01"),
                this.CreateNode("Cluster01-Rack02-Node02", "Group B", "Cluster01", "Rack02", "Region01"),
                this.CreateNode("Cluster01-Rack03-Node03", "Group A", "Cluster01", "Rack03", "Region01"),
                this.CreateNode("Cluster01-Rack04-Node04", "Group B", "Cluster01", "Rack04", "Region01")
            });

            this.mockTipClient
                .SetupSequence(c => c.IsTipSessionChangeFailedAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false)   // TiP creation failed
                .ReturnsAsync(true)    // TiP creation failed
                .ReturnsAsync(false);  // TiP creation succeeded

            await this.ExecuteProviderAsync(stopOnStatus: ExecutionStatus.Succeeded);

            IEnumerable<EnvironmentEntity> selectedTipNodes = this.mockFixture.EntityManager.EntitiesProvisioned.GetNodes();
            IEnumerable<EnvironmentEntity> selectedTipSessions = this.mockFixture.EntityManager.EntitiesProvisioned.GetTipSessions();

            Assert.IsNotNull(selectedTipNodes);
            Assert.IsTrue(selectedTipNodes.Count() == 2);
            CollectionAssert.AllItemsAreUnique(selectedTipNodes);
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.Id));
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.EnvironmentGroup));
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.TipSessionId()));

            Assert.IsNotNull(selectedTipSessions);
            Assert.IsTrue(selectedTipSessions.Count() == 2);
            CollectionAssert.AllItemsAreUnique(selectedTipSessions);
            CollectionAssert.AllItemsAreUnique(selectedTipSessions.Select(tipSession => tipSession.Id));
            CollectionAssert.AllItemsAreUnique(selectedTipSessions.Select(tipSession => tipSession.EnvironmentGroup));
            CollectionAssert.AreEquivalent(selectedTipNodes.Select(tipNode => tipNode.Id), selectedTipSessions.Select(tipSession => tipSession.NodeId()));
            CollectionAssert.AreEquivalent(selectedTipNodes.Select(tipNode => tipNode.TipSessionId()), selectedTipSessions.Select(tipSession => tipSession.Id));
        }

        [Test]
        public async Task ProviderSelectsTheExpectedTipSessionsForTheNodeAffinityDefined_SameClusterScenario_4()
        {
            // This scenario includes more than 2 environment groups (e.g. A/B/C)
            this.SetupMockDefaults(ExperimentType.ABC);
            this.mockFixture.Component.Parameters["nodeAffinity"] = NodeAffinity.SameCluster;

            this.mockEntityPool.AddRange(new List<EnvironmentEntity>
            {
                this.CreateNode("Cluster01-Rack01-Node01", "Group A", "Cluster01", "Rack01", "Region01"),
                this.CreateNode("Cluster01-Rack02-Node02", "Group B", "Cluster01", "Rack02", "Region01"),
                this.CreateNode("Cluster01-Rack03-Node03", "Group C", "Cluster01", "Rack03", "Region01")
            });

            await this.ExecuteProviderAsync(stopOnStatus: ExecutionStatus.Succeeded);

            IEnumerable<EnvironmentEntity> selectedTipNodes = this.mockFixture.EntityManager.EntitiesProvisioned.GetNodes();
            IEnumerable<EnvironmentEntity> selectedTipSessions = this.mockFixture.EntityManager.EntitiesProvisioned.GetTipSessions();

            Assert.IsNotNull(selectedTipNodes);
            Assert.IsTrue(selectedTipNodes.Count() == 3);
            CollectionAssert.AllItemsAreUnique(selectedTipNodes);
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.Id));
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.EnvironmentGroup));
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.TipSessionId()));

            Assert.IsNotNull(selectedTipSessions);
            Assert.IsTrue(selectedTipSessions.Count() == 3);
            CollectionAssert.AllItemsAreUnique(selectedTipSessions);
            CollectionAssert.AllItemsAreUnique(selectedTipSessions.Select(tipSession => tipSession.Id));
            CollectionAssert.AllItemsAreUnique(selectedTipSessions.Select(tipSession => tipSession.EnvironmentGroup));
            CollectionAssert.AreEquivalent(selectedTipNodes.Select(tipNode => tipNode.Id), selectedTipSessions.Select(tipSession => tipSession.NodeId()));
            CollectionAssert.AreEquivalent(selectedTipNodes.Select(tipNode => tipNode.TipSessionId()), selectedTipSessions.Select(tipSession => tipSession.Id));
        }

        [Test]
        public async Task ProviderSelectsTheExpectedTipSessionsForTheNodeAffinityDefined_SameRackScenario_1()
        {
            this.SetupMockDefaults(ExperimentType.AB);
            this.mockFixture.Component.Parameters["nodeAffinity"] = NodeAffinity.SameRack;

            this.mockEntityPool.AddRange(new List<EnvironmentEntity>
            {
                this.CreateNode("Cluster01-Rack01-Node01", "Group A", "Cluster01", "Rack01", "Region01"),
                this.CreateNode("Cluster01-Rack01-Node02", "Group B", "Cluster01", "Rack01", "Region02")
            });

            await this.ExecuteProviderAsync(stopOnStatus: ExecutionStatus.Succeeded);

            IEnumerable<EnvironmentEntity> selectedTipNodes = this.mockFixture.EntityManager.EntitiesProvisioned.GetNodes();
            IEnumerable<EnvironmentEntity> selectedTipSessions = this.mockFixture.EntityManager.EntitiesProvisioned.GetTipSessions();

            Assert.IsNotNull(selectedTipNodes);
            Assert.IsTrue(selectedTipNodes.Count() == 2);
            CollectionAssert.AllItemsAreUnique(selectedTipNodes);
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.Id));
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.EnvironmentGroup));
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.TipSessionId()));
            Assert.IsTrue(selectedTipNodes.Select(tipNode => tipNode.ClusterName()).Distinct().Count() == 1);
            Assert.IsTrue(selectedTipNodes.Select(tipNode => tipNode.RackLocation()).Distinct().Count() == 1);

            Assert.IsNotNull(selectedTipSessions);
            Assert.IsTrue(selectedTipSessions.Count() == 2);
            CollectionAssert.AllItemsAreUnique(selectedTipSessions);
            CollectionAssert.AllItemsAreUnique(selectedTipSessions.Select(tipSession => tipSession.Id));
            CollectionAssert.AllItemsAreUnique(selectedTipSessions.Select(tipSession => tipSession.EnvironmentGroup));
            Assert.IsTrue(selectedTipSessions.Select(tipSession => tipSession.ClusterName()).Distinct().Count() == 1);
            Assert.IsTrue(selectedTipSessions.Select(tipSession => tipSession.RackLocation()).Distinct().Count() == 1);
            CollectionAssert.AreEquivalent(selectedTipNodes.Select(tipNode => tipNode.Id), selectedTipSessions.Select(tipSession => tipSession.NodeId()));
            CollectionAssert.AreEquivalent(selectedTipNodes.Select(tipNode => tipNode.TipSessionId()), selectedTipSessions.Select(tipSession => tipSession.Id));
        }

        [Test]
        public async Task ProviderSelectsTheExpectedTipSessionsForTheNodeAffinityDefined_SameRackScenario_2()
        {
            this.SetupMockDefaults(ExperimentType.AB);
            this.mockFixture.Component.Parameters["nodeAffinity"] = NodeAffinity.SameRack;

            // This scenario includes a pool of entities includes entities that are in different clusters, different racks etc...
            // The initial 2 TiP session creation attempts fail.
            this.mockEntityPool.AddRange(new List<EnvironmentEntity>
            {
                this.CreateNode("Cluster01-Rack01-Node01", "Group A", "Cluster01", "Rack01", "Region01"),
                this.CreateNode("Cluster01-Rack01-Node02", "Group B", "Cluster01", "Rack01", "Region02"),
                this.CreateNode("Cluster02-Rack02-Node03", "Group A", "Cluster02", "Rack02", "Region03"),
                this.CreateNode("Cluster02-Rack02-Node04", "Group B", "Cluster02", "Rack02", "Region04")
            });

            this.mockTipClient
                .SetupSequence(c => c.IsTipSessionChangeFailedAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)   // TiP creation failed
                .ReturnsAsync(true)   // TiP creation failed
                .ReturnsAsync(false)  // TiP creation succeeded
                .ReturnsAsync(false); // TiP creation succeeded

            await this.ExecuteProviderAsync(stopOnStatus: ExecutionStatus.Succeeded);

            IEnumerable<EnvironmentEntity> selectedTipNodes = this.mockFixture.EntityManager.EntitiesProvisioned.GetNodes();
            IEnumerable<EnvironmentEntity> selectedTipSessions = this.mockFixture.EntityManager.EntitiesProvisioned.GetTipSessions();

            Assert.IsNotNull(selectedTipNodes);
            Assert.IsTrue(selectedTipNodes.Count() == 2);
            CollectionAssert.AllItemsAreUnique(selectedTipNodes);
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.Id));
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.EnvironmentGroup));
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.TipSessionId()));
            Assert.IsTrue(selectedTipNodes.Select(tipNode => tipNode.ClusterName()).Distinct().Count() == 1);
            Assert.IsTrue(selectedTipNodes.Select(tipNode => tipNode.RackLocation()).Distinct().Count() == 1);

            Assert.IsNotNull(selectedTipSessions);
            Assert.IsTrue(selectedTipSessions.Count() == 2);
            CollectionAssert.AllItemsAreUnique(selectedTipSessions);
            CollectionAssert.AllItemsAreUnique(selectedTipSessions.Select(tipSession => tipSession.Id));
            CollectionAssert.AllItemsAreUnique(selectedTipSessions.Select(tipSession => tipSession.EnvironmentGroup));
            Assert.IsTrue(selectedTipSessions.Select(tipSession => tipSession.ClusterName()).Distinct().Count() == 1);
            Assert.IsTrue(selectedTipSessions.Select(tipSession => tipSession.RackLocation()).Distinct().Count() == 1);
            CollectionAssert.AreEquivalent(selectedTipNodes.Select(tipNode => tipNode.Id), selectedTipSessions.Select(tipSession => tipSession.NodeId()));
            CollectionAssert.AreEquivalent(selectedTipNodes.Select(tipNode => tipNode.TipSessionId()), selectedTipSessions.Select(tipSession => tipSession.Id));
        }

        [Test]
        public async Task ProviderSelectsTheExpectedTipSessionsForTheNodeAffinityDefined_SameRackScenario_3()
        {
            this.SetupMockDefaults(ExperimentType.AB);
            this.mockFixture.Component.Parameters["nodeAffinity"] = NodeAffinity.SameRack;

            // This scenario includes a pool of entities includes entities that are in different clusters, different racks etc...
            // Of the initial 2 TiP sessions, 1 succeeds and 1 fails. There are additional nodes that meet the node affinity
            // requirements to pair with the first successful TiP session creation.
            this.mockEntityPool.AddRange(new List<EnvironmentEntity>
            {
                this.CreateNode("Cluster01-Rack01-Node01", "Group A", "Cluster01", "Rack01", "Region01"),
                this.CreateNode("Cluster01-Rack01-Node02", "Group B", "Cluster01", "Rack01", "Region01"),
                this.CreateNode("Cluster01-Rack01-Node03", "Group A", "Cluster01", "Rack01", "Region01"),
                this.CreateNode("Cluster01-Rack01-Node04", "Group B", "Cluster01", "Rack01", "Region01")
            });

            this.mockTipClient
                .SetupSequence(c => c.IsTipSessionChangeFailedAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false)   // TiP creation failed
                .ReturnsAsync(true)    // TiP creation failed
                .ReturnsAsync(false);  // TiP creation succeeded

            await this.ExecuteProviderAsync(stopOnStatus: ExecutionStatus.Succeeded);

            IEnumerable<EnvironmentEntity> selectedTipNodes = this.mockFixture.EntityManager.EntitiesProvisioned.GetNodes();
            IEnumerable<EnvironmentEntity> selectedTipSessions = this.mockFixture.EntityManager.EntitiesProvisioned.GetTipSessions();

            Assert.IsNotNull(selectedTipNodes);
            Assert.IsTrue(selectedTipNodes.Count() == 2);
            CollectionAssert.AllItemsAreUnique(selectedTipNodes);
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.Id));
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.EnvironmentGroup));
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.TipSessionId()));

            Assert.IsNotNull(selectedTipSessions);
            Assert.IsTrue(selectedTipSessions.Count() == 2);
            CollectionAssert.AllItemsAreUnique(selectedTipSessions);
            CollectionAssert.AllItemsAreUnique(selectedTipSessions.Select(tipSession => tipSession.Id));
            CollectionAssert.AllItemsAreUnique(selectedTipSessions.Select(tipSession => tipSession.EnvironmentGroup));
            CollectionAssert.AreEquivalent(selectedTipNodes.Select(tipNode => tipNode.Id), selectedTipSessions.Select(tipSession => tipSession.NodeId()));
            CollectionAssert.AreEquivalent(selectedTipNodes.Select(tipNode => tipNode.TipSessionId()), selectedTipSessions.Select(tipSession => tipSession.Id));
        }

        [Test]
        public async Task ProviderSelectsTheExpectedTipSessionsForTheNodeAffinityDefined_SameRackScenario_4()
        {
            // This scenario includes more than 2 environment groups (e.g. A/B/C)
            this.SetupMockDefaults(ExperimentType.ABC);
            this.mockFixture.Component.Parameters["nodeAffinity"] = NodeAffinity.SameRack;

            this.mockEntityPool.AddRange(new List<EnvironmentEntity>
            {
                this.CreateNode("Cluster01-Rack01-Node01", "Group A", "Cluster01", "Rack01", "Region01"),
                this.CreateNode("Cluster01-Rack01-Node02", "Group B", "Cluster01", "Rack01", "Region01"),
                this.CreateNode("Cluster01-Rack01-Node03", "Group C", "Cluster01", "Rack01", "Region01")
            });

            await this.ExecuteProviderAsync(stopOnStatus: ExecutionStatus.Succeeded);

            IEnumerable<EnvironmentEntity> selectedTipNodes = this.mockFixture.EntityManager.EntitiesProvisioned.GetNodes();
            IEnumerable<EnvironmentEntity> selectedTipSessions = this.mockFixture.EntityManager.EntitiesProvisioned.GetTipSessions();

            Assert.IsNotNull(selectedTipNodes);
            Assert.IsTrue(selectedTipNodes.Count() == 3);
            CollectionAssert.AllItemsAreUnique(selectedTipNodes);
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.Id));
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.EnvironmentGroup));
            CollectionAssert.AllItemsAreUnique(selectedTipNodes.Select(tipNode => tipNode.TipSessionId()));

            Assert.IsNotNull(selectedTipSessions);
            Assert.IsTrue(selectedTipSessions.Count() == 3);
            CollectionAssert.AllItemsAreUnique(selectedTipSessions);
            CollectionAssert.AllItemsAreUnique(selectedTipSessions.Select(tipSession => tipSession.Id));
            CollectionAssert.AllItemsAreUnique(selectedTipSessions.Select(tipSession => tipSession.EnvironmentGroup));
            CollectionAssert.AreEquivalent(selectedTipNodes.Select(tipNode => tipNode.Id), selectedTipSessions.Select(tipSession => tipSession.NodeId()));
            CollectionAssert.AreEquivalent(selectedTipNodes.Select(tipNode => tipNode.TipSessionId()), selectedTipSessions.Select(tipSession => tipSession.Id));
        }

        private static EnvironmentEntity CreateTipSessionEntityFrom(EnvironmentEntity node)
        {
            node.TipSessionId(Guid.NewGuid().ToString());
            node.TipSessionRequestChangeId(Guid.NewGuid().ToString());
            node.TipSessionStatus(TipSessionStatus.Created.ToString());

            EnvironmentEntity tipSession = EnvironmentEntity.TipSession(node.TipSessionId(), node.EnvironmentGroup, node.Metadata);
            tipSession.NodeId(node.Id);

            return tipSession;
        }

        private static TipNodeSession CreateTipNodeSessionFrom(EnvironmentEntity tipNodeEntity, TipNodeSessionStatus status = TipNodeSessionStatus.Created)
        {
            return new TipNodeSession
            {
                Cluster = tipNodeEntity.ClusterName(),
                CreatedTimeUtc = DateTime.UtcNow.AddMinutes(-1),
                NodeCount = 1,
                Id = tipNodeEntity.Id,
                Region = tipNodeEntity.Region(),
                Status = status
            };
        }

        private static TipNodeSessionChange CreateTipNodeSessionChange(Guid? tipSessionId = null, Guid? tipSessionChangeId = null, TipNodeSessionChangeStatus? status = null)
        {
            return new TipNodeSessionChange()
            {
                TipNodeSessionId = tipSessionId?.ToString() ?? Guid.NewGuid().ToString(),
                TipNodeSessionChangeId = tipSessionChangeId?.ToString() ?? Guid.NewGuid().ToString(),
                Status = status ?? TipNodeSessionChangeStatus.Queued
            };
        }

        private static TipNodeSessionChangeDetails CreateTipNodeSessionChangeDetail(Guid? tipSessionId = null, Guid? tipSessionChangeId = null)
        {
            return new TipNodeSessionChangeDetails()
            {
                TipNodeSessionId = tipSessionId?.ToString() ?? Guid.NewGuid().ToString(),
                TipNodeSessionChangeId = tipSessionChangeId?.ToString() ?? Guid.NewGuid().ToString(),
                Status = TipNodeSessionChangeStatus.Queued
            };
        }

        private async Task ExecuteProviderAsync(ExecutionStatus stopOnStatus)
        {
            int executionAttempts = 0;
            ExecutionResult result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);
            while (result.Status != stopOnStatus && executionAttempts < 10)
            {
                executionAttempts++;
                result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);
            }
        }

        private void SetupMockDefaults(ExperimentType experimentType)
        {
            this.mockFixture = new ProviderFixture(typeof(TipCreationProvider2), experimentType: experimentType);
            this.mockFixture.SetupExperimentMocks(experimentType);
            this.mockTipClient = new Mock<ITipClient>();

            this.mockFixture.Services.AddSingleton(NullLogger.Instance);
            this.mockFixture.Services.AddSingleton(this.mockTipClient.Object);
            this.mockFixture.Services.AddSingleton(this.mockFixture.EntityManager);

            this.mockState = new TipCreationProvider2.State
            {
                StepTimeout = DateTime.UtcNow.AddMinutes(10),
                Timeout = TimeSpan.FromMinutes(10),
                CountPerGroup = 1
            };

            this.mockEntityPool = new List<EnvironmentEntity>();
            this.mockEntitiesProvisioned = new List<EnvironmentEntity>();
            this.provider = new TipCreationProvider2(this.mockFixture.Services);

            // The EntityManager will make calls to the API through the IProviderDataClient to manage
            // entities (e.g. entityPool, entitiesProvisioned).
            this.mockFixture.DataClient.OnGetState<TipCreationProvider2.State>().ReturnsAsync(this.mockState);
            this.mockFixture.DataClient.OnGetEntityPool().ReturnsAsync(this.mockEntityPool);
            this.mockFixture.DataClient.OnGetEntitiesProvisioned().ReturnsAsync(this.mockEntitiesProvisioned);

            // Mimic the addition of entities to the entities provisioned that will be saved to a backing store
            // on subsequent provider executions.
            this.mockFixture.DataClient.OnSaveEntityPool()
                .Callback<string, string, IEnumerable<EnvironmentEntity>, CancellationToken, string>((experimentId, key, state, token, stateId) =>
                {
                    state.ToList().ForEach(entity =>
                    {
                        if (!this.mockEntityPool.Contains(entity))
                        {
                            this.mockEntityPool.Add(entity);
                        }
                    });
                })
                .Returns(Task.CompletedTask);

            // Mimic the addition of entities to the entities provisioned that will be saved to a backing store
            // on subsequent provider executions.
            this.mockFixture.DataClient.OnSaveEntitiesProvisioned()
                .Callback<string, string, IEnumerable<EnvironmentEntity>, CancellationToken, string>((experimentId, key, state, token, stateId) =>
                {
                    state.ToList().ForEach(entity =>
                    {
                        if (!this.mockEntitiesProvisioned.Contains(entity))
                        {
                            this.mockEntitiesProvisioned.Add(entity);
                        }
                    });
                })
                .Returns(Task.CompletedTask);

            // TiP session creation request default
            this.mockTipClient.OnCreateTipSession()
                .ReturnsAsync(() => TipCreationProvider2FunctionalTests.CreateTipNodeSessionChange());

            this.mockTipClient.OnDeleteTipSession()
                .ReturnsAsync(TipCreationProvider2FunctionalTests.CreateTipNodeSessionChange());

            // TiP session creation requests are confirmed succeeded by default.
            this.mockTipClient
                .Setup(c => c.IsTipSessionChangeFailedAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            TipNodeSession confirmedTipSession = null;
            this.mockTipClient.OnGetTipSession()
                .Callback<string, CancellationToken>((tipSessionId, token) =>
                {
                    EnvironmentEntity expectedTipNode = this.mockFixture.EntityManager.EntityPool.GetNodes()
                        ?.FirstOrDefault(e => e.TipSessionId() == tipSessionId);

                    confirmedTipSession = TipCreationProvider2FunctionalTests.CreateTipNodeSessionFrom(expectedTipNode);
                })
                .ReturnsAsync(() => confirmedTipSession);
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
    }
}
