namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class EnvironmentEntityExtensionsTests
    {
        private IEnumerable<EnvironmentEntity> mockEntities;

        [SetUp]
        public void SetupTest()
        {
            this.mockEntities = new List<EnvironmentEntity>
            {
                // Note:
                // For validation of node selection scenarios, the entity definitions below ensure that we
                // have enough nodes and the right variations to support the required node affinity/selection
                // scenarios: SameRack, SameCluster, DifferentCluster, Any.
                //
                // SameRack
                EnvironmentEntity.Node("Node01", "Group A", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster01",
                    ["RackLocation"] = "Rack01"
                }),
                EnvironmentEntity.Node("Node02", "Group A", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster01",
                    ["RackLocation"] = "Rack01"
                }),
                EnvironmentEntity.Node("Node03", "Group A", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster01",
                    ["RackLocation"] = "Rack01"
                }),
                EnvironmentEntity.Node("Node04", "Group B", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster01",
                    ["RackLocation"] = "Rack01"
                }),
                EnvironmentEntity.Node("Node05", "Group B", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster01",
                    ["RackLocation"] = "Rack01"
                }),

                EnvironmentEntity.Node("Node06", "Group C", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster01",
                    ["RackLocation"] = "Rack01"
                }),
                EnvironmentEntity.Node("Node07", "Group C", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster01",
                    ["RackLocation"] = "Rack01"
                }),

                // SameCluster...but different rack
                EnvironmentEntity.Node("Node07", "Group A", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster01",
                    ["RackLocation"] = "Rack02"
                }),
                EnvironmentEntity.Node("Node08", "Group B", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster01",
                    ["RackLocation"] = "Rack02"
                }),
                EnvironmentEntity.Node("Node09", "Group C", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster01",
                    ["RackLocation"] = "Rack02"
                }),

                 // DifferentCluster
                EnvironmentEntity.Node("Node10", "Group A", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster02",
                    ["RackLocation"] = "Rack03"
                }),
                EnvironmentEntity.Node("Node11", "Group B", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster02",
                    ["RackLocation"] = "Rack03"
                }),
                EnvironmentEntity.Node("Node12", "Group C", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster02",
                    ["RackLocation"] = "Rack03"
                }),

                EnvironmentEntity.Node("Node13", "Group A", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster03",
                    ["RackLocation"] = "Rack04"
                }),
                EnvironmentEntity.Node("Node14", "Group B", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster03",
                    ["RackLocation"] = "Rack04"
                }),
                EnvironmentEntity.Node("Node15", "Group C", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster03",
                    ["RackLocation"] = "Rack04"
                }),

                EnvironmentEntity.VirtualMachine("VM01", "Node01", "Group A"),
                EnvironmentEntity.VirtualMachine("VM02", "Node01", "Group A"),
                EnvironmentEntity.VirtualMachine("VM03", "Node02", "Group A"),
                EnvironmentEntity.VirtualMachine("VM04", "Node02", "Group A"),
                EnvironmentEntity.VirtualMachine("VM05", "Node03", "Group A"),
                EnvironmentEntity.VirtualMachine("VM06", "Node03", "Group A"),
                EnvironmentEntity.VirtualMachine("VM07", "Node04", "Group A"),
                EnvironmentEntity.VirtualMachine("VM08", "Node04", "Group A"),

                EnvironmentEntity.VirtualMachine("VM09", "Node01", "Group B"),
                EnvironmentEntity.VirtualMachine("VM10", "Node01", "Group B"),
                EnvironmentEntity.VirtualMachine("VM11", "Node02", "Group B"),
                EnvironmentEntity.VirtualMachine("VM12", "Node02", "Group B"),
                EnvironmentEntity.VirtualMachine("VM13", "Node03", "Group B"),
                EnvironmentEntity.VirtualMachine("VM14", "Node03", "Group B"),
                EnvironmentEntity.VirtualMachine("VM15", "Node04", "Group B"),
                EnvironmentEntity.VirtualMachine("VM16", "Node04", "Group B")
            };
        }

        [Test]
        public void AgentIdPropertyExtensionWorksAsExpected()
        {
            string expectedValue = "cluster1,node1,tip1";
            EnvironmentEntity entity = EnvironmentEntity.Node("AnyId", "AnyGroup");

            // Should not throw an exception when it is not defined.
            Assert.DoesNotThrow(() => entity.AgentId());
            Assert.AreEqual(string.Empty, entity.AgentId());
            Assert.IsFalse(entity.Metadata.ContainsKey("AgentId"));

            // Set the metadata value
            entity.AgentId(expectedValue);
            Assert.DoesNotThrow(() => entity.AgentId());
            Assert.AreEqual(expectedValue, entity.AgentId());
            Assert.IsTrue(entity.Metadata.ContainsKey("AgentId"));
        }

        [Test]
        public void ClusterNamePropertyExtensionWorksAsExpected()
        {
            string expectedValue = "cluster1";
            EnvironmentEntity entity = EnvironmentEntity.Node("AnyId", "AnyGroup");

            // Should not throw an exception when it is not defined.
            Assert.DoesNotThrow(() => entity.ClusterName());
            Assert.AreEqual(string.Empty, entity.ClusterName());
            Assert.IsFalse(entity.Metadata.ContainsKey("ClusterName"));

            // Set the metadata value
            entity.ClusterName(expectedValue);
            Assert.DoesNotThrow(() => entity.ClusterName());
            Assert.AreEqual(expectedValue, entity.ClusterName());
            Assert.IsTrue(entity.Metadata.ContainsKey("ClusterName"));
        }

        [Test]
        public void DataDisksPropertyExtensionWorksAsExpected()
        {
            string expectedValue = "storageAccountType=Premium_LRS,sku=Standard_LRS,lun=1,sizeInGB=32";
            EnvironmentEntity entity = EnvironmentEntity.Node("AnyId", "AnyGroup");

            // Should not throw an exception when it is not defined.
            Assert.DoesNotThrow(() => entity.DataDisks());
            Assert.AreEqual(string.Empty, entity.DataDisks());
            Assert.IsFalse(entity.Metadata.ContainsKey("DataDisks"));

            // Set the metadata value
            entity.DataDisks(expectedValue);
            Assert.DoesNotThrow(() => entity.DataDisks());
            Assert.AreEqual(expectedValue, entity.DataDisks());
            Assert.IsTrue(entity.Metadata.ContainsKey("DataDisks"));
        }

        [Test]
        public void DiscardedPropertyExtensionWorksAsExpected()
        {
            EnvironmentEntity entity = EnvironmentEntity.Node("AnyId", "AnyGroup");

            // Should not throw an exception when it is not defined.
            Assert.DoesNotThrow(() => entity.Discarded());
            Assert.IsFalse(entity.Discarded());
            Assert.IsFalse(entity.Metadata.ContainsKey("Discarded"));

            // Set the metadata value
            entity.Discarded(true);
            Assert.DoesNotThrow(() => entity.Discarded());
            Assert.IsTrue(entity.Discarded());
            Assert.IsTrue(entity.Metadata.ContainsKey("Discarded"));

            // Remove the metadata value
            entity.Discarded(false);
            Assert.DoesNotThrow(() => entity.Discarded());
            Assert.IsFalse(entity.Discarded());
            Assert.IsFalse(entity.Metadata.ContainsKey("Discarded"));
        }

        [Test]
        public void GroupNamePropertyExtensionWorksAsExpected()
        {
            string expectedValue = "Group A";
            EnvironmentEntity entity = EnvironmentEntity.Node("AnyId", "AnyGroup");

            // Should not throw an exception when it is not defined.
            Assert.DoesNotThrow(() => entity.GroupName());
            Assert.AreEqual(string.Empty, entity.GroupName());
            Assert.IsFalse(entity.Metadata.ContainsKey("GroupName"));

            // Set the metadata value
            entity.GroupName(expectedValue);
            Assert.DoesNotThrow(() => entity.GroupName());
            Assert.AreEqual(expectedValue, entity.GroupName());
            Assert.IsTrue(entity.Metadata.ContainsKey("GroupName"));
        }

        [Test]
        public void MachinePoolNamePropertyExtensionWorksAsExpected()
        {
            string expectedValue = "mp01";
            EnvironmentEntity entity = EnvironmentEntity.Node("AnyId", "AnyGroup");

            // Should not throw an exception when it is not defined.
            Assert.DoesNotThrow(() => entity.MachinePoolName());
            Assert.AreEqual(string.Empty, entity.MachinePoolName());
            Assert.IsFalse(entity.Metadata.ContainsKey("MachinePoolName"));

            // Set the metadata value
            entity.MachinePoolName(expectedValue);
            Assert.DoesNotThrow(() => entity.MachinePoolName());
            Assert.AreEqual(expectedValue, entity.MachinePoolName());
            Assert.IsTrue(entity.Metadata.ContainsKey("MachinePoolName"));
        }

        [Test]
        public void NodeIdPropertyExtensionWorksAsExpected()
        {
            string expectedValue = Guid.NewGuid().ToString();
            EnvironmentEntity entity = EnvironmentEntity.Node("AnyId", "AnyGroup");

            // Should not throw an exception when it is not defined.
            Assert.DoesNotThrow(() => entity.NodeId());
            Assert.AreEqual(string.Empty, entity.NodeId());
            Assert.IsFalse(entity.Metadata.ContainsKey("NodeId"));

            // Set the metadata value
            entity.NodeId(expectedValue);
            Assert.DoesNotThrow(() => entity.NodeId());
            Assert.AreEqual(expectedValue, entity.NodeId());
            Assert.IsTrue(entity.Metadata.ContainsKey("NodeId"));
        }

        [Test]
        public void NodeListPropertyExtensionWorksAsExpected()
        {
            List<string> expectedValues = new List<string> { Guid.NewGuid().ToString(), Guid.NewGuid().ToString() };
            EnvironmentEntity entity = EnvironmentEntity.Node("AnyId", "AnyGroup");

            // Should not throw an exception when it is not defined.
            Assert.DoesNotThrow(() => entity.NodeList());
            Assert.AreEqual(string.Empty, entity.NodeList());
            Assert.IsFalse(entity.Metadata.ContainsKey("NodeList"));

            // Set the metadata value
            entity.NodeList(expectedValues.ToArray());
            Assert.DoesNotThrow(() => entity.NodeList());
            CollectionAssert.AreEquivalent(expectedValues, entity.NodeList());
            Assert.IsTrue(entity.Metadata.ContainsKey("NodeList"));
        }

        [Test]
        public void OsDiskSkuPropertyExtensionWorksAsExpected()
        {
            string expectedValue = "Standard_LRS";
            EnvironmentEntity entity = EnvironmentEntity.Node("AnyId", "AnyGroup");

            // Should not throw an exception when it is not defined.
            Assert.DoesNotThrow(() => entity.OsDiskSku());
            Assert.AreEqual(string.Empty, entity.OsDiskSku());
            Assert.IsFalse(entity.Metadata.ContainsKey("OsDiskSku"));

            // Set the metadata value
            entity.OsDiskSku(expectedValue);
            Assert.DoesNotThrow(() => entity.OsDiskSku());
            Assert.AreEqual(expectedValue, entity.OsDiskSku());
            Assert.IsTrue(entity.Metadata.ContainsKey("OsDiskSku"));
        }

        [Test]
        public void PreferredVmSkuPropertyExtensionWorksAsExpected()
        {
            string expectedValue = "Standard_D2s_v3";
            EnvironmentEntity entity = EnvironmentEntity.Node("AnyId", "AnyGroup");

            // Should not throw an exception when it is not defined.
            Assert.DoesNotThrow(() => entity.PreferredVmSku());
            Assert.AreEqual(string.Empty, entity.PreferredVmSku());
            Assert.IsFalse(entity.Metadata.ContainsKey("PreferredVmSku"));

            // Set the metadata value
            entity.PreferredVmSku(expectedValue);
            Assert.DoesNotThrow(() => entity.PreferredVmSku());
            Assert.AreEqual(expectedValue, entity.PreferredVmSku());
            Assert.IsTrue(entity.Metadata.ContainsKey("PreferredVmSku"));
        }

        [Test]
        public void RackLocationPropertyExtensionWorksAsExpected()
        {
            string expectedValue = "Rack01_D12";
            EnvironmentEntity entity = EnvironmentEntity.Node("AnyId", "AnyGroup");

            // Should not throw an exception when it is not defined.
            Assert.DoesNotThrow(() => entity.RackLocation());
            Assert.AreEqual(string.Empty, entity.RackLocation());
            Assert.IsFalse(entity.Metadata.ContainsKey("RackLocation"));

            // Set the metadata value
            entity.RackLocation(expectedValue);
            Assert.DoesNotThrow(() => entity.RackLocation());
            Assert.AreEqual(expectedValue, entity.RackLocation());
            Assert.IsTrue(entity.Metadata.ContainsKey("RackLocation"));
        }

        [Test]
        public void RegionPropertyExtensionWorksAsExpected()
        {
            string expectedValue = "East US 2";
            EnvironmentEntity entity = EnvironmentEntity.Node("AnyId", "AnyGroup");

            // Should not throw an exception when it is not defined.
            Assert.DoesNotThrow(() => entity.Region());
            Assert.AreEqual(string.Empty, entity.Region());
            Assert.IsFalse(entity.Metadata.ContainsKey("Region"));

            // Set the metadata value
            entity.Region(expectedValue);
            Assert.DoesNotThrow(() => entity.Region());
            Assert.AreEqual(expectedValue, entity.Region());
            Assert.IsTrue(entity.Metadata.ContainsKey("Region"));
        }

        [Test]
        public void SupportedVmSkusPropertyExtensionWorksAsExpected()
        {
            List<string> expectedValues = new List<string> { "Standard_D2_v2", "Standard_D2_v3" };
            EnvironmentEntity entity = EnvironmentEntity.Node("AnyId", "AnyGroup");

            // Should not throw an exception when it is not defined.
            Assert.DoesNotThrow(() => entity.SupportedVmSkus());
            Assert.AreEqual(string.Empty, entity.SupportedVmSkus());
            Assert.IsFalse(entity.Metadata.ContainsKey("SupportedVmSkus"));

            // Set the metadata value
            entity.SupportedVmSkus(expectedValues.ToArray());
            Assert.DoesNotThrow(() => entity.SupportedVmSkus());
            CollectionAssert.AreEquivalent(expectedValues, entity.SupportedVmSkus());
            Assert.IsTrue(entity.Metadata.ContainsKey("SupportedVmSkus"));
        }

        [Test]
        public void TipSessionIdPropertyExtensionWorksAsExpected()
        {
            string expectedValue = Guid.NewGuid().ToString();
            EnvironmentEntity entity = EnvironmentEntity.Node("AnyId", "AnyGroup");

            // Should not throw an exception when it is not defined.
            Assert.DoesNotThrow(() => entity.TipSessionId());
            Assert.AreEqual(string.Empty, entity.TipSessionId());
            Assert.IsFalse(entity.Metadata.ContainsKey("TipSessionId"));

            // Set the metadata value
            entity.TipSessionId(expectedValue);
            Assert.DoesNotThrow(() => entity.TipSessionId());
            Assert.AreEqual(expectedValue, entity.TipSessionId());
            Assert.IsTrue(entity.Metadata.ContainsKey("TipSessionId"));
        }

        [Test]
        public void TipSessionDeleteRequestChangeIdPropertyExtensionWorksAsExpected()
        {
            string expectedValue = Guid.NewGuid().ToString();
            EnvironmentEntity entity = EnvironmentEntity.Node("AnyId", "AnyGroup");

            // Should not throw an exception when it is not defined.
            Assert.DoesNotThrow(() => entity.TipSessionDeleteRequestChangeId());
            Assert.AreEqual(string.Empty, entity.TipSessionDeleteRequestChangeId());
            Assert.IsFalse(entity.Metadata.ContainsKey("TipSessionDeleteRequestChangeId"));

            // Set the metadata value
            entity.TipSessionDeleteRequestChangeId(expectedValue);
            Assert.DoesNotThrow(() => entity.TipSessionDeleteRequestChangeId());
            Assert.AreEqual(expectedValue, entity.TipSessionDeleteRequestChangeId());
            Assert.IsTrue(entity.Metadata.ContainsKey("TipSessionDeleteRequestChangeId"));
        }

        [Test]
        public void TipSessionRequestChangeIdPropertyExtensionWorksAsExpected()
        {
            string expectedValue = Guid.NewGuid().ToString();
            EnvironmentEntity entity = EnvironmentEntity.Node("AnyId", "AnyGroup");

            // Should not throw an exception when it is not defined.
            Assert.DoesNotThrow(() => entity.TipSessionRequestChangeId());
            Assert.AreEqual(string.Empty, entity.TipSessionRequestChangeId());
            Assert.IsFalse(entity.Metadata.ContainsKey("TipSessionRequestChangeId"));

            // Set the metadata value
            entity.TipSessionRequestChangeId(expectedValue);
            Assert.DoesNotThrow(() => entity.TipSessionRequestChangeId());
            Assert.AreEqual(expectedValue, entity.TipSessionRequestChangeId());
            Assert.IsTrue(entity.Metadata.ContainsKey("TipSessionRequestChangeId"));
        }

        [Test]
        public void TipSessionCreatedTimePropertyExtensionWorksAsExpected()
        {
            DateTime expectedValue = DateTime.UtcNow;
            EnvironmentEntity entity = EnvironmentEntity.Node("AnyId", "AnyGroup");

            // Should not throw an exception when it is not defined.
            Assert.DoesNotThrow(() => entity.TipSessionCreatedTime());
            Assert.AreEqual(DateTime.MinValue, entity.TipSessionCreatedTime());
            Assert.IsFalse(entity.Metadata.ContainsKey("TipSessionCreatedTime"));

            // Set the metadata value
            entity.TipSessionCreatedTime(expectedValue);
            Assert.DoesNotThrow(() => entity.TipSessionCreatedTime());
            Assert.AreEqual(expectedValue, entity.TipSessionCreatedTime());
            Assert.IsTrue(entity.Metadata.ContainsKey("TipSessionCreatedTime"));
        }

        [Test]
        public void TipSessionDeletedTimePropertyExtensionWorksAsExpected()
        {
            DateTime expectedValue = DateTime.UtcNow;
            EnvironmentEntity entity = EnvironmentEntity.Node("AnyId", "AnyGroup");

            // Should not throw an exception when it is not defined.
            Assert.DoesNotThrow(() => entity.TipSessionDeletedTime());
            Assert.AreEqual(DateTime.MinValue, entity.TipSessionDeletedTime());
            Assert.IsFalse(entity.Metadata.ContainsKey("TipSessionDeletedTime"));

            // Set the metadata value
            entity.TipSessionDeletedTime(expectedValue);
            Assert.DoesNotThrow(() => entity.TipSessionDeletedTime());
            Assert.AreEqual(expectedValue, entity.TipSessionDeletedTime());
            Assert.IsTrue(entity.Metadata.ContainsKey("TipSessionDeletedTime"));
        }

        [Test]
        public void TipSessionExpirationTimePropertyExtensionWorksAsExpected()
        {
            DateTime expectedValue = DateTime.UtcNow;
            EnvironmentEntity entity = EnvironmentEntity.Node("AnyId", "AnyGroup");

            // Should not throw an exception when it is not defined.
            Assert.DoesNotThrow(() => entity.TipSessionExpirationTime());
            Assert.AreEqual(DateTime.MinValue, entity.TipSessionExpirationTime());
            Assert.IsFalse(entity.Metadata.ContainsKey("TipSessionExpirationTime"));

            // Set the metadata value
            entity.TipSessionExpirationTime(expectedValue);
            Assert.DoesNotThrow(() => entity.TipSessionExpirationTime());
            Assert.AreEqual(expectedValue, entity.TipSessionExpirationTime());
            Assert.IsTrue(entity.Metadata.ContainsKey("TipSessionExpirationTime"));
        }

        [Test]
        public void TipSessionRequestedTimePropertyExtensionWorksAsExpected()
        {
            DateTime expectedValue = DateTime.UtcNow;
            EnvironmentEntity entity = EnvironmentEntity.Node("AnyId", "AnyGroup");

            // Should not throw an exception when it is not defined.
            Assert.DoesNotThrow(() => entity.TipSessionRequestedTime());
            Assert.AreEqual(DateTime.MinValue, entity.TipSessionRequestedTime());
            Assert.IsFalse(entity.Metadata.ContainsKey("TipSessionRequestedTime"));

            // Set the metadata value
            entity.TipSessionRequestedTime(expectedValue);
            Assert.DoesNotThrow(() => entity.TipSessionRequestedTime());
            Assert.AreEqual(expectedValue, entity.TipSessionRequestedTime());
            Assert.IsTrue(entity.Metadata.ContainsKey("TipSessionRequestedTime"));
        }

        [Test]
        public void TipSessionStatusPropertyExtensionWorksAsExpected()
        {
            string expectedValue = "Creating";
            EnvironmentEntity entity = EnvironmentEntity.Node("AnyId", "AnyGroup");

            // Should not throw an exception when it is not defined.
            Assert.DoesNotThrow(() => entity.TipSessionStatus());
            Assert.AreEqual(string.Empty, entity.TipSessionStatus());
            Assert.IsFalse(entity.Metadata.ContainsKey("TipSessionStatus"));

            // Set the metadata value
            entity.TipSessionStatus(expectedValue);
            Assert.DoesNotThrow(() => entity.TipSessionStatus());
            Assert.AreEqual(expectedValue, entity.TipSessionStatus());
            Assert.IsTrue(entity.Metadata.ContainsKey("TipSessionStatus"));
        }

        [Test]
        public void GetEntitiesExtensionReturnsTheExpectedEntitiesFromAGivenSet()
        {
            // 1) Node entities
            IEnumerable<EnvironmentEntity> nodeEntities = this.mockEntities.GetEntities(EntityType.Node);

            Assert.IsNotNull(nodeEntities);
            CollectionAssert.AreEquivalent(
                this.mockEntities.Where(node => node.EntityType == EntityType.Node).Select(node => node.GetHashCode()),
                nodeEntities.Select(node => node.GetHashCode()));

            // 2) Virtual Machine entities
            IEnumerable<EnvironmentEntity> vmEntities = this.mockEntities.GetEntities(EntityType.VirtualMachine);

            Assert.IsNotNull(vmEntities);
            CollectionAssert.AreEquivalent(
                this.mockEntities.Where(vm => vm.EntityType == EntityType.VirtualMachine).Select(vm => vm.GetHashCode()),
                vmEntities.Select(vm => vm.GetHashCode()));
        }

        [Test]
        public void GetEntitiesExtensionReturnsTheExpectedEntitiesFromAGivenSetForASpecificEnvironmentGroup()
        {
            string environmentGroup = "Group B";

            // 2) Node entities
            IEnumerable<EnvironmentEntity> nodeEntities = this.mockEntities.GetEntities(EntityType.Node, environmentGroup);

            Assert.IsNotNull(nodeEntities);
            CollectionAssert.AreEquivalent(
                this.mockEntities.Where(node => node.EntityType == EntityType.Node && node.EnvironmentGroup == environmentGroup).Select(node => node.GetHashCode()),
                nodeEntities.Select(node => node.GetHashCode()));

            // 3) Virtual Machine entities
            IEnumerable<EnvironmentEntity> vmEntities = this.mockEntities.GetEntities(EntityType.VirtualMachine, environmentGroup);

            Assert.IsNotNull(vmEntities);
            CollectionAssert.AreEquivalent(
                this.mockEntities.Where(vm => vm.EntityType == EntityType.VirtualMachine && vm.EnvironmentGroup == environmentGroup).Select(vm => vm.GetHashCode()),
                vmEntities.Select(vm => vm.GetHashCode()));
        }

        [Test]
        public void GetNodeExtensionHandlesEmptyEntitySets()
        {
            EnvironmentEntity selectedNode = null;
            Assert.DoesNotThrow(() => selectedNode = new List<EnvironmentEntity>().GetNode(NodeAffinity.Any, "Group B"));
            Assert.IsNull(selectedNode);
        }

        [Test]
        public void GetNodeExtensionIgnoresAllNodesMarkedAsDiscarded()
        {
            this.mockEntities.Where(node => node.EntityType == EntityType.Node).ToList().ForEach(node => node.Discarded(true));

            EnvironmentEntity selectedNode = this.mockEntities.GetNode(NodeAffinity.Any, "Group A");
            Assert.IsNull(selectedNode);

            selectedNode = this.mockEntities.GetNode(NodeAffinity.DifferentCluster, "Group B");
            Assert.IsNull(selectedNode);

            selectedNode = this.mockEntities.GetNode(NodeAffinity.SameCluster, "Group C");
            Assert.IsNull(selectedNode);

            selectedNode = this.mockEntities.GetNode(NodeAffinity.SameRack, "Group A");
            Assert.IsNull(selectedNode);
        }

        [Test]
        public void GetNodeExtensionSelectsTheExpectedNodes_AnyNodeScenario()
        {
            IEnumerable<string> experimentGroups = this.mockEntities.Select(e => e.EnvironmentGroup).Distinct();
            List<EnvironmentEntity> selectedNodes = new List<EnvironmentEntity>();

            for (int i = 0; i < experimentGroups.Count(); i++)
            {
                string group = experimentGroups.ElementAt(i);
                EnvironmentEntity selectedNode = this.mockEntities.GetNode(NodeAffinity.Any, group, countPerGroup: 1, selectedNodes.ToArray());
                selectedNodes.Add(selectedNode);

                Assert.IsNotNull(selectedNode);
                Assert.IsTrue(selectedNodes.All(node => node.EntityType == EntityType.Node));
                Assert.IsFalse(selectedNodes.Any(node => node.Discarded()));
                CollectionAssert.AllItemsAreUnique(selectedNodes);
            }

            CollectionAssert.AreEquivalent(experimentGroups, selectedNodes.Select(node => node.EnvironmentGroup));
        }

        [Test]
        public void GetNodeExtensionSelectsTheExpectedNodes_AnyMultipleNodesScenario()
        {
            int countPerGroup = 3;

            IEnumerable<string> experimentGroups = this.mockEntities.Select(e => e.EnvironmentGroup).Distinct();
            IEnumerable<string> expectedExperimentGroups = new List<string>();
            for (int j = 0; j < countPerGroup; j++)
            {
                expectedExperimentGroups = expectedExperimentGroups.Concat(experimentGroups);
            }

            List<EnvironmentEntity> selectedNodes = new List<EnvironmentEntity>();

            for (int i = 0; i < experimentGroups.Count(); i++)
            {
                string group = experimentGroups.ElementAt(i);
                for (int j = 0; j < countPerGroup; j++)
                {
                    EnvironmentEntity selectedNode = this.mockEntities.GetNode(NodeAffinity.Any, group, countPerGroup: countPerGroup, selectedNodes.ToArray());
                    selectedNodes.Add(selectedNode);

                    Assert.IsNotNull(selectedNode);
                    Assert.IsTrue(selectedNodes.All(node => node.EntityType == EntityType.Node));
                    Assert.IsFalse(selectedNodes.Any(node => node.Discarded()));
                    CollectionAssert.AllItemsAreUnique(selectedNodes);
                }
            }

            CollectionAssert.AreEquivalent(expectedExperimentGroups, selectedNodes.Select(node => node.EnvironmentGroup));
        }

        [Test]
        public void GetNodeExtensionThrowsWhenTryingToGetMoreThanDefinedNodes_AnyMultipleNodesScenario()
        {
            int countPerGroup = 3;

            IEnumerable<string> experimentGroups = this.mockEntities.Select(e => e.EnvironmentGroup).Distinct();
            IEnumerable<string> expectedExperimentGroups = new List<string>();
            for (int j = 0; j < countPerGroup; j++)
            {
                expectedExperimentGroups = expectedExperimentGroups.Concat(experimentGroups);
            }

            List<EnvironmentEntity> selectedNodes = new List<EnvironmentEntity>();

            ArgumentException exc = Assert.Throws<ArgumentException>(() =>
            {
                for (int i = 0; i < experimentGroups.Count(); i++)
                {
                    string group = experimentGroups.ElementAt(i);
                    for (int j = 0; j < countPerGroup + 1; j++)
                    {
                        EnvironmentEntity selectedNode = this.mockEntities.GetNode(NodeAffinity.Any, group, countPerGroup: countPerGroup, selectedNodes.ToArray());
                        selectedNodes.Add(selectedNode);

                        Assert.IsNotNull(selectedNode);
                        Assert.IsTrue(selectedNodes.All(node => node.EntityType == EntityType.Node));
                        Assert.IsFalse(selectedNodes.Any(node => node.Discarded()));
                        CollectionAssert.AllItemsAreUnique(selectedNodes);
                    }
                }
            });

            Assert.IsTrue(exc.Message.Contains($"The affinity nodes provided already have {countPerGroup} node in the environment group"));
        }

        [Test]
        public void GetNodeExtensionSelectsTheExpectedNodesWhenDiscardedNodesExist_AnyNodeScenario()
        {
            // Ensure nodes have been marked as "discarded"
            this.mockEntities.First().Discarded(true);

            IEnumerable<string> experimentGroups = this.mockEntities.Select(e => e.EnvironmentGroup).Distinct();
            List<EnvironmentEntity> selectedNodes = new List<EnvironmentEntity>();

            for (int i = 0; i < experimentGroups.Count(); i++)
            {
                string group = experimentGroups.ElementAt(i);
                EnvironmentEntity selectedNode = this.mockEntities.GetNode(NodeAffinity.Any, group, countPerGroup: 1, selectedNodes.ToArray());
                selectedNodes.Add(selectedNode);

                Assert.IsNotNull(selectedNode);
                Assert.IsTrue(!object.ReferenceEquals(selectedNode, this.mockEntities.First()));
                Assert.IsTrue(selectedNodes.All(node => node.EntityType == EntityType.Node));
                Assert.IsFalse(selectedNodes.Any(node => node.Discarded()));
                CollectionAssert.AllItemsAreUnique(selectedNodes);
            }

            CollectionAssert.AreEquivalent(experimentGroups, selectedNodes.Select(node => node.EnvironmentGroup));
        }

        [Test]
        public void GetNodeExtensionSelectsTheExpectedNodesWhenThereIsOnlyASingleExperimentGroup_AnyNodeScenario()
        {
            EnvironmentEntity selectedNode = this.mockEntities.Where(group => group.EnvironmentGroup == this.mockEntities.First().EnvironmentGroup)
                .GetNode(NodeAffinity.Any, this.mockEntities.First().EnvironmentGroup);

            Assert.IsNotNull(selectedNode);
            Assert.IsTrue(selectedNode.EntityType == EntityType.Node);
            Assert.IsFalse(selectedNode.Discarded());
        }

        [Test]
        public void GetNodeExtensionDoesNotReturnMatchesIfTheyDoNotExist_AnyNodeScenario()
        {
            // Setup a scenario where there are no nodes that match
            string group = this.mockEntities.Select(e => e.EnvironmentGroup).Distinct().First();
            IEnumerable<EnvironmentEntity> groupEntities = this.mockEntities.Where(node => node.EnvironmentGroup == group);

            foreach (string otherGroup in this.mockEntities.Where(e => e.EnvironmentGroup != group).Select(e => e.EnvironmentGroup).Distinct())
            {
                EnvironmentEntity selectedNode = groupEntities.GetNode(NodeAffinity.Any, otherGroup, countPerGroup: 1, groupEntities.First());
                Assert.IsNull(selectedNode);
            }
        }

        [Test]
        public void GetNodeExtensionHandlesScenariosWhereASingleNodeExistsInMultipleEnvironmentGroups_AnyNodeScenario_1()
        {
            // In the scenario below, the node reference exists in both environment groups (Group A and Group B).
            // However, for the sake of the selection, we would not want to return this node for both A and B.
            // By the definition of an experiment, we cannot have the exact same node as Group A and Group B
            // at the same time. Thus, it should be selected for either Group A or Group B but not both.
            List<EnvironmentEntity> entities = new List<EnvironmentEntity>
            {
                EnvironmentEntity.Node("Node01", "Group A", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster01",
                    ["RackLocation"] = "Rack01"
                }),
                EnvironmentEntity.Node("Node01", "Group B", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster01",
                    ["RackLocation"] = "Rack01"
                }),
                EnvironmentEntity.Node("Node01", "Group C", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster01",
                    ["RackLocation"] = "Rack01"
                })
            };

            EnvironmentEntity selectedNode = entities.GetNode(NodeAffinity.Any, "Group B");

            // If we cannot match an entity, then we return only what we could match. This SHOULD be
            // just 1 of the entities.
            Assert.IsNotNull(selectedNode);
            Assert.IsTrue(selectedNode.Id == "Node01");
            Assert.IsTrue(selectedNode.EnvironmentGroup == "Group B");

            // Given a reference node, we would expect that we find no matches at all because the
            // ID of the other nodes is the same and thus they are the same node differing only by the
            // experiment group.
            selectedNode = entities.GetNode(NodeAffinity.Any, "Group B", withAffinityToNodes: entities.First());
            Assert.IsNull(selectedNode);
        }

        [Test]
        public void GetNodeExtensionHandlesScenariosWhereASingleNodeExistsInMultipleEnvironmentGroups_AnyNodeScenario_2()
        {
            // In the scenario below, the node reference exists in both environment groups (Group A and Group B).
            // However, for the sake of the selection, we would not want to return this node for both A and B.
            // By the definition of an experiment, we cannot have the exact same node as Group A and Group B
            // at the same time. Thus, it should be selected for either Group A or Group B but not both.
            List<EnvironmentEntity> entities = new List<EnvironmentEntity>
            {
                // This is a highly unlikely scenario in the real Azure fleet. Each node in the fleet will
                // have a unique ID and cannot physically be in 2 clusters at the same time. For the sake of
                // consistency with other affinities/selection strategies, the behavior should be the same nonetheless.
                EnvironmentEntity.Node("Node01", "Group A", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster01",
                    ["RackLocation"] = "Rack01"
                }),
                EnvironmentEntity.Node("Node01", "Group B", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster02",
                    ["RackLocation"] = "Rack02"
                }),
                EnvironmentEntity.Node("Node01", "Group C", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster02",
                    ["RackLocation"] = "Rack02"
                })
            };

            EnvironmentEntity selectedNode = entities.GetNode(NodeAffinity.Any, "Group B");

            // If we cannot match an entity, then we return only what we could match. This SHOULD be
            // just 1 of the entities.
            Assert.IsNotNull(selectedNode);
            Assert.IsTrue(selectedNode.Id == "Node01");
            Assert.IsTrue(selectedNode.EnvironmentGroup == "Group B");
            Assert.IsTrue(selectedNode.RackLocation() == "Rack02");

            // Given a reference node, we would expect that we find no matches at all because the
            // ID of the other nodes is the same and thus they are the same node differing only by the
            // experiment group.
            selectedNode = entities.GetNode(NodeAffinity.Any, "Group C", withAffinityToNodes: entities.ElementAt(1));
            Assert.IsNull(selectedNode);
        }

        [Test]
        public void GetNodeExtensionSelectsTheExpectedNodes_SameRackScenario()
        {
            IEnumerable<string> experimentGroups = this.mockEntities.Select(e => e.EnvironmentGroup).Distinct();
            List<EnvironmentEntity> selectedNodes = new List<EnvironmentEntity>();

            for (int i = 0; i < experimentGroups.Count(); i++)
            {
                string group = experimentGroups.ElementAt(i);
                EnvironmentEntity selectedNode = this.mockEntities.GetNode(NodeAffinity.SameRack, group, countPerGroup: 1, selectedNodes.ToArray());
                selectedNodes.Add(selectedNode);

                Assert.IsNotNull(selectedNode);
                Assert.IsTrue(selectedNodes.All(node => node.EntityType == EntityType.Node));
                Assert.IsFalse(selectedNodes.Any(node => node.Discarded()));
                Assert.IsTrue(selectedNodes.Select(node => node.ClusterName()).Distinct().Count() == 1);
                Assert.IsTrue(selectedNodes.Select(node => node.RackLocation()).Distinct().Count() == 1);
                CollectionAssert.AllItemsAreUnique(selectedNodes);
            }

            CollectionAssert.AreEquivalent(experimentGroups, selectedNodes.Select(node => node.EnvironmentGroup));
        }

        [Test]
        public void GetNodeExtensionSelectsTheExpectedNodes_SameRackMultipleNodesScenario()
        {
            int countPerGroup = 2;

            IEnumerable<string> experimentGroups = this.mockEntities.Select(e => e.EnvironmentGroup).Distinct();
            IEnumerable<string> expectedExperimentGroups = new List<string>();
            for (int j = 0; j < countPerGroup; j++)
            {
                expectedExperimentGroups = expectedExperimentGroups.Concat(experimentGroups);
            }

            List<EnvironmentEntity> selectedNodes = new List<EnvironmentEntity>();

            for (int i = 0; i < experimentGroups.Count(); i++)
            {
                string group = experimentGroups.ElementAt(i);
                for (int j = 0; j < countPerGroup; j++)
                {
                    EnvironmentEntity selectedNode = this.mockEntities.GetNode(NodeAffinity.SameRack, group, countPerGroup: countPerGroup, selectedNodes.ToArray());
                    selectedNodes.Add(selectedNode);

                    Assert.IsNotNull(selectedNode);
                    Assert.IsTrue(selectedNodes.All(node => node.EntityType == EntityType.Node));
                    Assert.IsFalse(selectedNodes.Any(node => node.Discarded()));
                    CollectionAssert.AllItemsAreUnique(selectedNodes);
                }

                Assert.IsTrue(selectedNodes.Select(node => node.ClusterName()).Distinct().Count() == 1);
                Assert.IsTrue(selectedNodes.Select(node => node.RackLocation()).Distinct().Count() == 1);
            }

            CollectionAssert.AreEquivalent(expectedExperimentGroups, selectedNodes.Select(node => node.EnvironmentGroup));
        }

        [Test]
        public void GetNodeExtensionSelectsTheExpectedNodesWhenDiscardedNodesExist_SameRackScenario()
        {
            // Ensure nodes have been marked as "discarded"
            this.mockEntities.First().Discarded(true);

            IEnumerable<string> experimentGroups = this.mockEntities.Select(e => e.EnvironmentGroup).Distinct();
            List<EnvironmentEntity> selectedNodes = new List<EnvironmentEntity>();

            for (int i = 0; i < experimentGroups.Count(); i++)
            {
                string group = experimentGroups.ElementAt(i);
                EnvironmentEntity selectedNode = this.mockEntities.GetNode(NodeAffinity.SameRack, group, countPerGroup: 1, selectedNodes.ToArray());
                selectedNodes.Add(selectedNode);

                Assert.IsNotNull(selectedNode);
                Assert.IsTrue(!object.ReferenceEquals(selectedNode, this.mockEntities.First()));
                Assert.IsTrue(selectedNodes.All(node => node.EntityType == EntityType.Node));
                Assert.IsFalse(selectedNodes.Any(node => node.Discarded()));
                Assert.IsTrue(selectedNodes.Select(node => node.ClusterName()).Distinct().Count() == 1);
                Assert.IsTrue(selectedNodes.Select(node => node.RackLocation()).Distinct().Count() == 1);
                CollectionAssert.AllItemsAreUnique(selectedNodes);
            }

            CollectionAssert.AreEquivalent(experimentGroups, selectedNodes.Select(node => node.EnvironmentGroup));
        }

        [Test]
        public void GetNodeExtensionSelectsTheExpectedNodesWhenDiscardedNodesExist_SameRackMultipleNodesScenario()
        {
            // Ensure nodes have been marked as "discarded"
            this.mockEntities.First().Discarded(true);
            int countPerGroup = 2;

            IEnumerable<string> experimentGroups = this.mockEntities.Select(e => e.EnvironmentGroup).Distinct();
            IEnumerable<string> expectedExperimentGroups = new List<string>();
            for (int j = 0; j < countPerGroup; j++)
            {
                expectedExperimentGroups = expectedExperimentGroups.Concat(experimentGroups);
            }

            List<EnvironmentEntity> selectedNodes = new List<EnvironmentEntity>();

            for (int i = 0; i < experimentGroups.Count(); i++)
            {
                string group = experimentGroups.ElementAt(i);
                for (int j = 0; j < countPerGroup; j++)
                {
                    EnvironmentEntity selectedNode = this.mockEntities.GetNode(NodeAffinity.SameRack, group, countPerGroup: countPerGroup, selectedNodes.ToArray());
                    selectedNodes.Add(selectedNode);

                    Assert.IsNotNull(selectedNode);
                    Assert.IsTrue(!object.ReferenceEquals(selectedNode, this.mockEntities.First()));
                    Assert.IsTrue(selectedNodes.All(node => node.EntityType == EntityType.Node));
                    Assert.IsFalse(selectedNodes.Any(node => node.Discarded()));
                    CollectionAssert.AllItemsAreUnique(selectedNodes);
                }

                Assert.IsTrue(selectedNodes.Select(node => node.ClusterName()).Distinct().Count() == 1);
                Assert.IsTrue(selectedNodes.Select(node => node.RackLocation()).Distinct().Count() == 1);
            }

            CollectionAssert.AreEquivalent(expectedExperimentGroups, selectedNodes.Select(node => node.EnvironmentGroup));
        }

        [Test]
        public void GetNodeExtensionSelectsTheExpectedNodesWhenThereIsOnlyASingleExperimentGroup_SameRackScenario()
        {
            EnvironmentEntity selectedNode = this.mockEntities.Where(group => group.EnvironmentGroup == this.mockEntities.First().EnvironmentGroup)
                .GetNode(NodeAffinity.SameRack, this.mockEntities.First().EnvironmentGroup);

            Assert.IsNotNull(selectedNode);
            Assert.IsTrue(selectedNode.EntityType == EntityType.Node);
            Assert.IsFalse(selectedNode.Discarded());
        }

        [Test]
        public void GetNodeExtensionDoesNotReturnMatchesIfTheyDoNotExist_SameRackScenario()
        {
            // Setup a scenario where there are no nodes that match
            string group = this.mockEntities.First().EnvironmentGroup;
            IEnumerable<EnvironmentEntity> groupEntities = this.mockEntities.Where(node => node.EntityType == EntityType.Node && node.EnvironmentGroup == group);
            groupEntities.ToList().ForEach(node => node.RackLocation("AnyRackThatDoesNotMatch"));

            foreach (string experimentGroup in this.mockEntities.Where(e => e.EnvironmentGroup != group).Select(e => e.EnvironmentGroup).Distinct())
            {
                EnvironmentEntity selectedNode = this.mockEntities.GetNode(NodeAffinity.SameRack, experimentGroup, countPerGroup: 1, groupEntities.Last());
                Assert.IsNull(selectedNode);
            }
        }

        [Test]
        public void GetNodeExtensionHandlesScenariosWhereASingleNodeExistsInMultipleEnvironmentGroups_SameRackScenario_1()
        {
            // In the scenario below, the node reference exists in both environment groups (Group A and Group B).
            // However, for the sake of the selection, we would not want to return this node for both A and B.
            // By the definition of an experiment, we cannot have the exact same node as Group A and Group B
            // at the same time. Thus, it should be selected for either Group A or Group B but not both.
            List<EnvironmentEntity> entities = new List<EnvironmentEntity>
            {
                EnvironmentEntity.Node("Node01", "Group A", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster01",
                    ["RackLocation"] = "Rack01"
                }),
                EnvironmentEntity.Node("Node01", "Group B", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster01",
                    ["RackLocation"] = "Rack01"
                }),
                EnvironmentEntity.Node("Node01", "Group C", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster01",
                    ["RackLocation"] = "Rack01"
                })
            };

            EnvironmentEntity selectedNode = entities.GetNode(NodeAffinity.SameRack, "Group B");

            // If we cannot match an entity, then we return only what we could match. This SHOULD be
            // just 1 of the entities.
            Assert.IsNotNull(selectedNode);
            Assert.IsTrue(selectedNode.Id == "Node01");
            Assert.IsTrue(selectedNode.EnvironmentGroup == "Group B");

            // Given a reference node, we would expect that we find no matches at all because the
            // ID of the other nodes is the same and thus they are the same node differing only by the
            // experiment group.
            selectedNode = entities.GetNode(NodeAffinity.SameRack, "Group B", withAffinityToNodes: entities.First());
            Assert.IsNull(selectedNode);
        }

        [Test]
        public void GetNodeExtensionHandlesScenariosWhereASingleNodeExistsInMultipleEnvironmentGroups_SameRackScenario_2()
        {
            // In the scenario below, the node reference exists in both environment groups (Group A and Group B).
            // However, for the sake of the selection, we would not want to return this node for both A and B.
            // By the definition of an experiment, we cannot have the exact same node as Group A and Group B
            // at the same time. Thus, it should be selected for either Group A or Group B but not both.
            List<EnvironmentEntity> entities = new List<EnvironmentEntity>
            {
                // This is a highly unlikely scenario in the real Azure fleet. Each node in the fleet will
                // have a unique ID and cannot physically be in 2 clusters at the same time. For the sake of
                // consistency with other affinities/selection strategies, the behavior should be the same nonetheless.
                EnvironmentEntity.Node("Node01", "Group A", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster01",
                    ["RackLocation"] = "Rack01"
                }),
                EnvironmentEntity.Node("Node01", "Group B", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster02",
                    ["RackLocation"] = "Rack02"
                }),
                EnvironmentEntity.Node("Node01", "Group C", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster02",
                    ["RackLocation"] = "Rack02"
                })
            };

            EnvironmentEntity selectedNode = entities.GetNode(NodeAffinity.SameRack, "Group B");

            // If we cannot match an entity, then we return only what we could match. This SHOULD be
            // just 1 of the entities.
            Assert.IsNotNull(selectedNode);
            Assert.IsTrue(selectedNode.Id == "Node01");
            Assert.IsTrue(selectedNode.EnvironmentGroup == "Group B");
            Assert.IsTrue(selectedNode.RackLocation() == "Rack02");

            // Given a reference node, we would expect that we find no matches at all because the
            // ID of the other nodes is the same and thus they are the same node differing only by the
            // experiment group.
            selectedNode = entities.GetNode(NodeAffinity.SameRack, "Group C", withAffinityToNodes: entities.ElementAt(1));
            Assert.IsNull(selectedNode);
        }

        [Test]
        public void GetNodeExtensionSelectsTheExpectedNodes_SameClusterScenario()
        {
            IEnumerable<string> experimentGroups = this.mockEntities.Select(e => e.EnvironmentGroup).Distinct();
            List<EnvironmentEntity> selectedNodes = new List<EnvironmentEntity>();

            for (int i = 0; i < experimentGroups.Count(); i++)
            {
                string group = experimentGroups.ElementAt(i);
                EnvironmentEntity selectedNode = this.mockEntities.GetNode(NodeAffinity.SameCluster, group, countPerGroup: 1, selectedNodes.ToArray());
                selectedNodes.Add(selectedNode);

                Assert.IsNotNull(selectedNode);
                Assert.IsTrue(selectedNodes.All(node => node.EntityType == EntityType.Node));
                Assert.IsFalse(selectedNodes.Any(node => node.Discarded()));
                Assert.IsTrue(selectedNodes.Select(node => node.ClusterName()).Distinct().Count() == 1);
                CollectionAssert.AllItemsAreUnique(selectedNodes);
            }

            CollectionAssert.AreEquivalent(experimentGroups, selectedNodes.Select(node => node.EnvironmentGroup));
        }

        [Test]
        public void GetNodeExtensionSelectsTheExpectedNodesWhenDiscardedNodesExist_SameClusterScenario()
        {
            // Ensure nodes have been marked as "discarded"
            this.mockEntities.First().Discarded(true);

            IEnumerable<string> experimentGroups = this.mockEntities.Select(e => e.EnvironmentGroup).Distinct();
            List<EnvironmentEntity> selectedNodes = new List<EnvironmentEntity>();

            for (int i = 0; i < experimentGroups.Count(); i++)
            {
                string group = experimentGroups.ElementAt(i);
                EnvironmentEntity selectedNode = this.mockEntities.GetNode(NodeAffinity.SameCluster, group, countPerGroup: 1, selectedNodes.ToArray());
                selectedNodes.Add(selectedNode);

                Assert.IsNotNull(selectedNode);
                Assert.IsTrue(!object.ReferenceEquals(selectedNode, this.mockEntities.First()));
                Assert.IsTrue(selectedNodes.All(node => node.EntityType == EntityType.Node));
                Assert.IsFalse(selectedNodes.Any(node => node.Discarded()));
                Assert.IsTrue(selectedNodes.Select(node => node.ClusterName()).Distinct().Count() == 1);
                CollectionAssert.AllItemsAreUnique(selectedNodes);
            }

            CollectionAssert.AreEquivalent(experimentGroups, selectedNodes.Select(node => node.EnvironmentGroup));
        }

        [Test]
        public void GetNodeExtensionSelectsTheExpectedNodesWhenThereIsOnlyASingleExperimentGroup_SameClusterScenario()
        {
            EnvironmentEntity selectedNode = this.mockEntities.Where(group => group.EnvironmentGroup == this.mockEntities.First().EnvironmentGroup)
                .GetNode(NodeAffinity.SameCluster, this.mockEntities.First().EnvironmentGroup);

            Assert.IsNotNull(selectedNode);
            Assert.IsTrue(selectedNode.EntityType == EntityType.Node);
            Assert.IsFalse(selectedNode.Discarded());
        }

        [Test]
        public void GetNodeExtensionDoesNotReturnMatchesIfTheyDoNotExist_SameClusterScenario()
        {
            // Setup a scenario where there are no nodes that match
            string group = this.mockEntities.First().EnvironmentGroup;
            IEnumerable<EnvironmentEntity> groupEntities = this.mockEntities.Where(node => node.EntityType == EntityType.Node && node.EnvironmentGroup == group);
            groupEntities.ToList().ForEach(node => node.ClusterName("AnyClusterThatDoesNotMatch"));

            foreach (string experimentGroup in this.mockEntities.Where(e => e.EnvironmentGroup != group).Select(e => e.EnvironmentGroup).Distinct())
            {
                EnvironmentEntity selectedNode = this.mockEntities.GetNode(NodeAffinity.SameCluster, experimentGroup, countPerGroup: 1, groupEntities.Last());
                Assert.IsNull(selectedNode);
            }
        }

        [Test]
        public void GetNodeExtensionHandlesScenariosWhereASingleNodeExistsInMultipleEnvironmentGroups_SameClusterScenario_1()
        {
            // In the scenario below, the node reference exists in both environment groups (Group A and Group B).
            // However, for the sake of the selection, we would not want to return this node for both A and B.
            // By the definition of an experiment, we cannot have the exact same node as Group A and Group B
            // at the same time. Thus, it should be selected for either Group A or Group B but not both.
            List<EnvironmentEntity> entities = new List<EnvironmentEntity>
            {
                EnvironmentEntity.Node("Node01", "Group A", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster01",
                    ["RackLocation"] = "Rack01"
                }),
                EnvironmentEntity.Node("Node01", "Group B", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster01",
                    ["RackLocation"] = "Rack01"
                }),
                EnvironmentEntity.Node("Node01", "Group C", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster01",
                    ["RackLocation"] = "Rack01"
                })
            };

            EnvironmentEntity selectedNode = entities.GetNode(NodeAffinity.SameCluster, "Group B");

            // If we cannot match an entity, then we return only what we could match. This SHOULD be
            // just 1 of the entities.
            Assert.IsNotNull(selectedNode);
            Assert.IsTrue(selectedNode.Id == "Node01");
            Assert.IsTrue(selectedNode.EnvironmentGroup == "Group B");

            // Given a reference node, we would expect that we find no matches at all because the
            // ID of the other nodes is the same and thus they are the same node differing only by the
            // experiment group.
            selectedNode = entities.GetNode(NodeAffinity.SameCluster, "Group B", withAffinityToNodes: entities.First());
            Assert.IsNull(selectedNode);
        }

        [Test]
        public void GetNodeExtensionHandlesScenariosWhereASingleNodeExistsInMultipleEnvironmentGroups_SameClusterScenario_2()
        {
            // In the scenario below, the node reference exists in both environment groups (Group A and Group B).
            // However, for the sake of the selection, we would not want to return this node for both A and B.
            // By the definition of an experiment, we cannot have the exact same node as Group A and Group B
            // at the same time. Thus, it should be selected for either Group A or Group B but not both.
            List<EnvironmentEntity> entities = new List<EnvironmentEntity>
            {
                // This is a highly unlikely scenario in the real Azure fleet. Each node in the fleet will
                // have a unique ID and cannot physically be in 2 clusters at the same time. For the sake of
                // consistency with other affinities/selection strategies, the behavior should be the same nonetheless.
                EnvironmentEntity.Node("Node01", "Group A", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster01",
                    ["RackLocation"] = "Rack01"
                }),
                EnvironmentEntity.Node("Node01", "Group B", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster02",
                    ["RackLocation"] = "Rack02"
                }),
                EnvironmentEntity.Node("Node01", "Group C", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster02",
                    ["RackLocation"] = "Rack02"
                })
            };

            EnvironmentEntity selectedNode = entities.GetNode(NodeAffinity.SameCluster, "Group B");

            // If we cannot match an entity, then we return only what we could match. This SHOULD be
            // just 1 of the entities.
            Assert.IsNotNull(selectedNode);
            Assert.IsTrue(selectedNode.Id == "Node01");
            Assert.IsTrue(selectedNode.EnvironmentGroup == "Group B");
            Assert.IsTrue(selectedNode.ClusterName() == "Cluster02");

            // Given a reference node, we would expect that we find no matches at all because the
            // ID of the other nodes is the same and thus they are the same node differing only by the
            // experiment group.
            selectedNode = entities.GetNode(NodeAffinity.SameCluster, "Group C", withAffinityToNodes: entities.ElementAt(1));
            Assert.IsNull(selectedNode);
        }

        [Test]
        public void GetNodeExtensionSelectsTheExpectedNodes_DifferentClusterScenario()
        {
            IEnumerable<string> experimentGroups = this.mockEntities.Select(e => e.EnvironmentGroup).Distinct();
            List<EnvironmentEntity> selectedNodes = new List<EnvironmentEntity>();

            for (int i = 0; i < experimentGroups.Count(); i++)
            {
                string group = experimentGroups.ElementAt(i);
                EnvironmentEntity selectedNode = this.mockEntities.GetNode(NodeAffinity.DifferentCluster, group, countPerGroup: 1, selectedNodes.ToArray());
                selectedNodes.Add(selectedNode);

                Assert.IsNotNull(selectedNode);
                Assert.IsTrue(selectedNodes.All(node => node.EntityType == EntityType.Node));
                Assert.IsFalse(selectedNodes.Any(node => node.Discarded()));
                CollectionAssert.AllItemsAreUnique(selectedNodes.Select(node => node.ClusterName()));
                CollectionAssert.AllItemsAreUnique(selectedNodes);
            }

            CollectionAssert.AreEquivalent(experimentGroups, selectedNodes.Select(node => node.EnvironmentGroup));
        }

        [Test]
        public void GetNodeExtensionSelectsTheExpectedNodesWhenDiscardedNodesExist_DifferentClusterScenario()
        {
            // Ensure nodes have been marked as "discarded"
            this.mockEntities.First().Discarded(true);

            IEnumerable<string> experimentGroups = this.mockEntities.Select(e => e.EnvironmentGroup).Distinct();
            List<EnvironmentEntity> selectedNodes = new List<EnvironmentEntity>();

            for (int i = 0; i < experimentGroups.Count(); i++)
            {
                string group = experimentGroups.ElementAt(i);
                EnvironmentEntity selectedNode = this.mockEntities.GetNode(NodeAffinity.DifferentCluster, group, countPerGroup: 1, selectedNodes.ToArray());
                selectedNodes.Add(selectedNode);

                Assert.IsNotNull(selectedNode);
                Assert.IsTrue(!object.ReferenceEquals(selectedNode, this.mockEntities.First()));
                Assert.IsTrue(selectedNodes.All(node => node.EntityType == EntityType.Node));
                Assert.IsFalse(selectedNodes.Any(node => node.Discarded()));
                CollectionAssert.AllItemsAreUnique(selectedNodes.Select(node => node.ClusterName()));
                CollectionAssert.AllItemsAreUnique(selectedNodes);
            }

            CollectionAssert.AreEquivalent(experimentGroups, selectedNodes.Select(node => node.EnvironmentGroup));
        }

        [Test]
        public void GetNodeExtensionSelectsTheExpectedNodesWhenThereIsOnlyASingleExperimentGroup_DifferentClusterScenario()
        {
            EnvironmentEntity selectedNode = this.mockEntities.Where(group => group.EnvironmentGroup == this.mockEntities.First().EnvironmentGroup)
                .GetNode(NodeAffinity.DifferentCluster, this.mockEntities.First().EnvironmentGroup);

            Assert.IsNotNull(selectedNode);
            Assert.IsTrue(selectedNode.EntityType == EntityType.Node);
            Assert.IsFalse(selectedNode.Discarded());
        }

        [Test]
        public void GetNodeExtensionDoesNotReturnMatchesIfTheyDoNotExist_DifferentClusterScenario()
        {
            // Setup a scenario where there are no nodes that match
            string group = this.mockEntities.First().EnvironmentGroup;
            IEnumerable<EnvironmentEntity> groupEntities = this.mockEntities.Where(node => node.EntityType == EntityType.Node);
            groupEntities.ToList().ForEach(node => node.ClusterName("AllMatchingClusters"));

            foreach (string experimentGroup in this.mockEntities.Where(e => e.EnvironmentGroup != group).Select(e => e.EnvironmentGroup).Distinct())
            {
                EnvironmentEntity selectedNode = this.mockEntities.GetNode(NodeAffinity.DifferentCluster, experimentGroup, countPerGroup: 1, groupEntities.First());
                Assert.IsNull(selectedNode);
            }
        }

        [Test]
        public void GetNodeExtensionHandlesScenariosWhereASingleNodeExistsInMultipleEnvironmentGroups_DifferentClusterScenario_1()
        {
            // In the scenario below, the node reference exists in both environment groups (Group A and Group B).
            // However, for the sake of the selection, we would not want to return this node for both A and B.
            // By the definition of an experiment, we cannot have the exact same node as Group A and Group B
            // at the same time. Thus, it should be selected for either Group A or Group B but not both.
            List<EnvironmentEntity> entities = new List<EnvironmentEntity>
            {
                EnvironmentEntity.Node("Node01", "Group A", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster01",
                    ["RackLocation"] = "Rack01"
                }),
                EnvironmentEntity.Node("Node01", "Group B", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster02",
                    ["RackLocation"] = "Rack02"
                }),
                EnvironmentEntity.Node("Node01", "Group C", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster03",
                    ["RackLocation"] = "Rack03"
                })
            };

            EnvironmentEntity selectedNode = entities.GetNode(NodeAffinity.DifferentCluster, "Group B");

            // If we cannot match an entity, then we return only what we could match. This SHOULD be
            // just 1 of the entities.
            Assert.IsNotNull(selectedNode);
            Assert.IsTrue(selectedNode.Id == "Node01");
            Assert.IsTrue(selectedNode.EnvironmentGroup == "Group B");

            // Given a reference node, we would expect that we find no matches at all because the
            // ID of the other nodes is the same and thus they are the same node differing only by the
            // experiment group.
            selectedNode = entities.GetNode(NodeAffinity.DifferentCluster, "Group B", withAffinityToNodes: entities.First());
            Assert.IsNull(selectedNode);
        }

        [Test]
        public void GetNodeExtensionHandlesScenariosWhereASingleNodeExistsInMultipleEnvironmentGroups_DifferentClusterScenario_2()
        {
            // In the scenario below, the node reference exists in both environment groups (Group A and Group B).
            // However, for the sake of the selection, we would not want to return this node for both A and B.
            // By the definition of an experiment, we cannot have the exact same node as Group A and Group B
            // at the same time. Thus, it should be selected for either Group A or Group B but not both.
            List<EnvironmentEntity> entities = new List<EnvironmentEntity>
            {
                // This is a highly unlikely scenario in the real Azure fleet. Each node in the fleet will
                // have a unique ID and cannot physically be in 2 clusters at the same time. For the sake of
                // consistency with other affinities/selection strategies, the behavior should be the same nonetheless.
                EnvironmentEntity.Node("Node01", "Group A", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster01",
                    ["RackLocation"] = "Rack01"
                }),
                EnvironmentEntity.Node("Node01", "Group B", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster02",
                    ["RackLocation"] = "Rack02"
                }),
                EnvironmentEntity.Node("Node01", "Group C", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster03",
                    ["RackLocation"] = "Rack03"
                })
            };

            EnvironmentEntity selectedNode = entities.GetNode(NodeAffinity.DifferentCluster, "Group B");

            // If we cannot match an entity, then we return only what we could match. This SHOULD be
            // just 1 of the entities.
            Assert.IsNotNull(selectedNode);
            Assert.IsTrue(selectedNode.Id == "Node01");
            Assert.IsTrue(selectedNode.EnvironmentGroup == "Group B");
            Assert.IsTrue(selectedNode.ClusterName() == "Cluster02");

            // Given a reference node, we would expect that we find no matches at all because the
            // ID of the other nodes is the same and thus they are the same node differing only by the
            // experiment group.
            selectedNode = entities.GetNode(NodeAffinity.DifferentCluster, "Group C", withAffinityToNodes: entities.ElementAt(1));
            Assert.IsNull(selectedNode);
        }
    }
}
