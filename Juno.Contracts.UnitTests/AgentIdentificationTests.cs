namespace Juno.Contracts
{
    using AutoFixture;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class AgentIdentificationTests
    {
        private Fixture mockFixture;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupAgentMocks();
        }

        [Test]
        public void AgentIdentificationConstructorParsesTheExpectedAgentIdParts()
        {
            AgentIdentification instance = new AgentIdentification("Cluster01,Node01");
            Assert.AreEqual("Cluster01", instance.ClusterName);
            Assert.AreEqual("Node01", instance.NodeName);
            Assert.IsNull(instance.VirtualMachineName);
            Assert.IsNull(instance.Context);

            instance = new AgentIdentification("Cluster01,Node01,VM01");
            Assert.AreEqual("Cluster01", instance.ClusterName);
            Assert.AreEqual("Node01", instance.NodeName);
            Assert.AreEqual("VM01", instance.VirtualMachineName);
            Assert.IsNull(instance.Context);

            instance = new AgentIdentification("Cluster01,Node01,VM01,TiPSession01");
            Assert.AreEqual("Cluster01", instance.ClusterName);
            Assert.AreEqual("Node01", instance.NodeName);
            Assert.AreEqual("VM01", instance.VirtualMachineName);
            Assert.AreEqual("TiPSession01", instance.Context);
        }

        [Test]
        public void AgentIdentificationIsJsonSerializableByDefault()
        {
            SerializationAssert.IsJsonSerializable(this.mockFixture.Create<AgentIdentification>());
        }

        [Test]
        public void AgentIdentificationIsJsonSerializableUsingExpectedSerializerSettings()
        {
            SerializationAssert.IsJsonSerializable(
                this.mockFixture.Create<AgentIdentification>(),
                ContractSerialization.DefaultJsonSerializationSettings);
        }

        [Test]
        public void AgentIdentificationCorrectlyImplementsHashcodeSemantics()
        {
            var instance1 = new AgentIdentification("Cluster01", "Node01", "VM01", "TiPSession01");
            var instance2 = new AgentIdentification("Cluster02", "Node02", "VM02", "TiPSesson02");

            EqualityAssert.CorrectlyImplementsHashcodeSemantics(() => instance1, () => instance2);
        }

        [Test]
        public void AgentIdentificationCorrectlyImplementsEqualitySemantics()
        {
            var instance1 = new AgentIdentification("Cluster01", "Node01", "VM01", "TiPSession01");
            var instance2 = new AgentIdentification("Cluster02", "Node02", "VM02", "TiPSesson02");

            int h1 = instance1.GetHashCode();
            int h2 = instance2.GetHashCode();

            EqualityAssert.CorrectlyImplementsEqualitySemantics(() => instance1, () => instance2);
        }

        [Test]
        public void AgentIdentificationToStringOverrideProducesTheExpectedAgentId()
        {
            AgentIdentification instance = new AgentIdentification("Cluster01", "Node01");
            Assert.AreEqual("Cluster01,Node01".ToLowerInvariant(), instance.ToString());

            instance = new AgentIdentification("Cluster01", "Node01", "VM01");
            Assert.AreEqual("Cluster01,Node01,VM01".ToLowerInvariant(), instance.ToString());

            instance = new AgentIdentification("Cluster01", "Node01", "VM01", "TiPSession01");
            Assert.AreEqual("Cluster01,Node01,VM01,TiPSession01".ToLowerInvariant(), instance.ToString());
        }

        [Test]
        public void AgentIdentificationCreatesTheExpectedIdentifierForAnAgentThatWillRunOnANodeWhenNotAllIdentifiersAreSupplied()
        {
            Assert.AreEqual(
                "Cluster01,unknown".ToLowerInvariant(),
                AgentIdentification.CreateNodeId(clusterName: "Cluster01").ToString());

            Assert.AreEqual(
                "Cluster01,unknown,TipSession01".ToLowerInvariant(),
                AgentIdentification.CreateNodeId(clusterName: "Cluster01", contextId: "TipSession01").ToString());

            Assert.AreEqual(
                "unknown,Node01".ToLowerInvariant(),
                AgentIdentification.CreateNodeId(nodeId: "Node01").ToString());

            Assert.AreEqual(
                "unknown,Node01,TipSession01".ToLowerInvariant(),
                AgentIdentification.CreateNodeId(nodeId: "Node01", contextId: "TipSession01").ToString());
        }

        [Test]
        public void AgentIdentificationCreatesTheExpectedIdentifierForAnAgentThatWillRunOnAVirtualMachineWhenNotAllIdentifiersAreSupplied()
        {
            Assert.AreEqual(
                "unknown,unknown,VM01".ToLowerInvariant(),
                AgentIdentification.CreateVirtualMachineId(vmName: "VM01").ToString());

            Assert.AreEqual(
                "Cluster01,unknown,unknown".ToLowerInvariant(),
                AgentIdentification.CreateVirtualMachineId(clusterName: "Cluster01").ToString());

            Assert.AreEqual(
                "Cluster01,Node01,unknown,TipSession01".ToLowerInvariant(),
                AgentIdentification.CreateVirtualMachineId(clusterName: "Cluster01", nodeId: "Node01", contextId: "TipSession01").ToString());

            Assert.AreEqual(
                "unknown,Node01,unknown".ToLowerInvariant(),
                AgentIdentification.CreateVirtualMachineId(nodeId: "Node01").ToString());

            Assert.AreEqual(
                "unknown,Node01,unknown,TipSession01".ToLowerInvariant(),
                AgentIdentification.CreateVirtualMachineId(nodeId: "Node01", contextId: "TipSession01").ToString());
        }
    }
}
