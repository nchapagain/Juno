namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using AutoFixture;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using Newtonsoft.Json;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class EnvironmentEntityTests
    {
        private Fixture mockFixture;

        private static JsonSerializerSettings DefaultJsonSerializationSettings { get; } = new JsonSerializerSettings
        {
            // Format: 2012-03-21T05:40:12.340Z
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore,

            // We tried using PreserveReferenceHandling.All and Object, but ran into issues
            // when deserializing string arrays and read only dictionaries
            ReferenceLoopHandling = ReferenceLoopHandling.Error,

            // This is the default setting, but to avoid remote code execution bugs do NOT change
            // this to any other setting.
            TypeNameHandling = TypeNameHandling.All
        };

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks();
        }

        [Test]
        public void EnvironmentEntityIsJsonSerializableByDefault()
        {
            SerializationAssert.IsJsonSerializable(this.mockFixture.Create<EnvironmentEntity>());
        }

        [Test]
        public void EnvironmentEntityIsJsonSerializableUsingExpectedSerializerSettings()
        {
            SerializationAssert.IsJsonSerializable(
                this.mockFixture.Create<EnvironmentEntity>(),
                ContractSerialization.DefaultJsonSerializationSettings);
        }

        [Test]
        public void EnvironmentEntityCorrectlyImplementsEqualitySemantics()
        {
            EnvironmentEntity instance1 = this.mockFixture.Create<EnvironmentEntity>();
            EnvironmentEntity instance2 = this.mockFixture.Create<EnvironmentEntity>();

            EqualityAssert.CorrectlyImplementsEqualitySemantics(() => instance1, () => instance2);
        }

        [Test]
        public void EnvironmentEntityCorrectlyImplementsHashcodeSemantics()
        {
            EnvironmentEntity instance1 = this.mockFixture.Create<EnvironmentEntity>();
            EnvironmentEntity instance2 = this.mockFixture.Create<EnvironmentEntity>();

            EqualityAssert.CorrectlyImplementsHashcodeSemantics(() => instance1, () => instance2);
        }

        [Test]
        public void EnvironmentEntityHashCodesAreNotCaseSensitive()
        {
            EnvironmentEntity template = this.mockFixture.Create<EnvironmentEntity>();
            EnvironmentEntity instance1 = new EnvironmentEntity(
                template.EntityType,
                template.Id.ToLowerInvariant(),
                template.ParentId?.ToLowerInvariant(),
                template.EnvironmentGroup.ToLowerInvariant(),
                template.Metadata);

            EnvironmentEntity instance2 = new EnvironmentEntity(
                template.EntityType,
                template.Id.ToUpperInvariant(),
                template.ParentId?.ToUpperInvariant(),
                template.EnvironmentGroup.ToUpperInvariant(),
                template.Metadata);

            Assert.AreEqual(instance1.GetHashCode(), instance2.GetHashCode());
        }

        [Test]
        public void EnvironmentEntityClusterInstancesAreCreatedAsExpected()
        {
            string expectedId = "Any ID";
            string expectedParentId = "Any Parent";
            string expectedGroup = "Any Group";
            IDictionary<string, IConvertible> expectedMetadata = new Dictionary<string, IConvertible>
            {
                ["Property1"] = "Any Value",
                ["Property2"] = 123,
                ["Property3"] = true,
            };

            EnvironmentEntity cluster = EnvironmentEntity.Cluster(expectedId, expectedGroup);
            Assert.AreEqual(EntityType.Cluster, cluster.EntityType);
            Assert.AreEqual(expectedId, cluster.Id);
            Assert.AreEqual(expectedGroup, cluster.EnvironmentGroup);
            Assert.IsNull(cluster.ParentId);
            Assert.IsEmpty(cluster.Metadata);

            cluster = EnvironmentEntity.Cluster(expectedId, expectedParentId, expectedGroup);
            Assert.AreEqual(EntityType.Cluster, cluster.EntityType);
            Assert.AreEqual(expectedId, cluster.Id);
            Assert.AreEqual(expectedParentId, cluster.ParentId);
            Assert.AreEqual(expectedGroup, cluster.EnvironmentGroup);
            Assert.IsEmpty(cluster.Metadata);

            cluster = EnvironmentEntity.Cluster(expectedId, expectedParentId, expectedGroup, expectedMetadata);
            Assert.AreEqual(EntityType.Cluster, cluster.EntityType);
            Assert.AreEqual(expectedId, cluster.Id);
            Assert.AreEqual(expectedParentId, cluster.ParentId);
            Assert.AreEqual(expectedGroup, cluster.EnvironmentGroup);
            CollectionAssert.AreEquivalent(expectedMetadata.Keys, cluster.Metadata.Keys);
            CollectionAssert.AreEquivalent(expectedMetadata.Values, cluster.Metadata.Values);
        }

        [Test]
        public void EnvironmentEntityNodeInstancesAreCreatedAsExpected()
        {
            string expectedId = "Any ID";
            string expectedParentId = "Any Parent";
            string expectedGroup = "Any Group";
            IDictionary<string, IConvertible> expectedMetadata = new Dictionary<string, IConvertible>
            {
                ["Property1"] = "Any Value",
                ["Property2"] = 123,
                ["Property3"] = true,
            };

            EnvironmentEntity node = EnvironmentEntity.Node(expectedId, expectedGroup);
            Assert.AreEqual(EntityType.Node, node.EntityType);
            Assert.AreEqual(expectedId, node.Id);
            Assert.AreEqual(expectedGroup, node.EnvironmentGroup);
            Assert.IsNull(node.ParentId);
            Assert.IsEmpty(node.Metadata);

            node = EnvironmentEntity.Node(expectedId, expectedParentId, expectedGroup);
            Assert.AreEqual(EntityType.Node, node.EntityType);
            Assert.AreEqual(expectedId, node.Id);
            Assert.AreEqual(expectedParentId, node.ParentId);
            Assert.AreEqual(expectedGroup, node.EnvironmentGroup);
            Assert.IsEmpty(node.Metadata);

            node = EnvironmentEntity.Node(expectedId, expectedParentId, expectedGroup, expectedMetadata);
            Assert.AreEqual(EntityType.Node, node.EntityType);
            Assert.AreEqual(expectedId, node.Id);
            Assert.AreEqual(expectedParentId, node.ParentId);
            Assert.AreEqual(expectedGroup, node.EnvironmentGroup);
            CollectionAssert.AreEquivalent(expectedMetadata.Keys, node.Metadata.Keys);
            CollectionAssert.AreEquivalent(expectedMetadata.Values, node.Metadata.Values);
        }

        [Test]
        public void EnvironmentEntityTipSessionInstancesAreCreatedAsExpected()
        {
            string expectedId = "Any ID";
            string expectedParentId = "Any Parent";
            string expectedGroup = "Any Group";
            IDictionary<string, IConvertible> expectedMetadata = new Dictionary<string, IConvertible>
            {
                ["Property1"] = "Any Value",
                ["Property2"] = 123,
                ["Property3"] = true,
            };

            EnvironmentEntity node = EnvironmentEntity.TipSession(expectedId, expectedGroup);
            Assert.AreEqual(EntityType.TipSession, node.EntityType);
            Assert.AreEqual(expectedId, node.Id);
            Assert.AreEqual(expectedGroup, node.EnvironmentGroup);
            Assert.IsNull(node.ParentId);
            Assert.IsEmpty(node.Metadata);

            node = EnvironmentEntity.TipSession(expectedId, expectedParentId, expectedGroup);
            Assert.AreEqual(EntityType.TipSession, node.EntityType);
            Assert.AreEqual(expectedId, node.Id);
            Assert.AreEqual(expectedParentId, node.ParentId);
            Assert.AreEqual(expectedGroup, node.EnvironmentGroup);
            Assert.IsEmpty(node.Metadata);

            node = EnvironmentEntity.TipSession(expectedId, expectedParentId, expectedGroup, expectedMetadata);
            Assert.AreEqual(EntityType.TipSession, node.EntityType);
            Assert.AreEqual(expectedId, node.Id);
            Assert.AreEqual(expectedParentId, node.ParentId);
            Assert.AreEqual(expectedGroup, node.EnvironmentGroup);
            CollectionAssert.AreEquivalent(expectedMetadata.Keys, node.Metadata.Keys);
            CollectionAssert.AreEquivalent(expectedMetadata.Values, node.Metadata.Values);
        }

        [Test]
        public void EnvironmentEntityVirtualMachineInstancesAreCreatedAsExpected()
        {
            string expectedId = "Any ID";
            string expectedParentId = "Any Parent";
            string expectedGroup = "Any Group";
            IDictionary<string, IConvertible> expectedMetadata = new Dictionary<string, IConvertible>
            {
                ["Property1"] = "Any Value",
                ["Property2"] = 123,
                ["Property3"] = true,
            };

            EnvironmentEntity virtualMachine = EnvironmentEntity.VirtualMachine(expectedId, expectedGroup);
            Assert.AreEqual(EntityType.VirtualMachine, virtualMachine.EntityType);
            Assert.AreEqual(expectedId, virtualMachine.Id);
            Assert.AreEqual(expectedGroup, virtualMachine.EnvironmentGroup);
            Assert.IsNull(virtualMachine.ParentId);
            Assert.IsEmpty(virtualMachine.Metadata);

            virtualMachine = EnvironmentEntity.VirtualMachine(expectedId, expectedParentId, expectedGroup);
            Assert.AreEqual(EntityType.VirtualMachine, virtualMachine.EntityType);
            Assert.AreEqual(expectedId, virtualMachine.Id);
            Assert.AreEqual(expectedParentId, virtualMachine.ParentId);
            Assert.AreEqual(expectedGroup, virtualMachine.EnvironmentGroup);
            Assert.IsEmpty(virtualMachine.Metadata);

            virtualMachine = EnvironmentEntity.VirtualMachine(expectedId, expectedParentId, expectedGroup, expectedMetadata);
            Assert.AreEqual(EntityType.VirtualMachine, virtualMachine.EntityType);
            Assert.AreEqual(expectedId, virtualMachine.Id);
            Assert.AreEqual(expectedParentId, virtualMachine.ParentId);
            Assert.AreEqual(expectedGroup, virtualMachine.EnvironmentGroup);
            CollectionAssert.AreEquivalent(expectedMetadata.Keys, virtualMachine.Metadata.Keys);
            CollectionAssert.AreEquivalent(expectedMetadata.Values, virtualMachine.Metadata.Values);
        }
    }
}
