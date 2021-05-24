namespace Juno.Execution.ArmIntegration
{
    using System;
    using System.Collections.Generic;
    using AutoFixture;
    using Juno.Contracts;
    using Juno.Execution.ArmIntegration;
    using Microsoft.Azure.CRC;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class VmResourceGroupDefinitionTests
    {
        private VmResourceGroupDefinition resourceGroup;
        private Fixture mockFixture;
        private string experimentId;
        private string stepId;
        private string nodeId;
        private string clusterId;
        private string tipSessionId;
        private string subId;

        [SetUp]
        public void SetUp()
        {
            this.mockFixture = new Fixture();
            var azureVmSpec = this.mockFixture.Create<AzureVmSpecification>();
            var azureVmSpec2 = this.mockFixture.Create<AzureVmSpecification>();

            var specs = new List<AzureVmSpecification>() { azureVmSpec, azureVmSpec2 };
            this.subId = Guid.NewGuid().ToString();
            this.experimentId = Guid.NewGuid().ToString();
            this.stepId = Guid.NewGuid().ToString();
            this.nodeId = Guid.NewGuid().ToString();
            this.clusterId = Guid.NewGuid().ToString();
            this.tipSessionId = Guid.NewGuid().ToString();

            this.resourceGroup = new VmResourceGroupDefinition(
                "AnyEnvironment",
                this.subId,
                this.experimentId,
                this.stepId,
                specs,
                "west2",
                this.clusterId,
                this.tipSessionId);
        }

        [Test]
        public void ResourceGroupDefinitionStateIsSetToPendingOnStart()
        {
            Assert.IsTrue(this.resourceGroup.State == ProvisioningState.Pending);
        }

        [Test]
        public void ResourceGroupDefinitionCreateNamesBasedOnConvention()
        {
            string prefix = this.stepId.Replace("-", string.Empty).Substring(0, 11);
            Assert.AreEqual(this.resourceGroup.Name, $"rg-{prefix}");
            Assert.AreEqual(this.resourceGroup.NetworkSecurityGroupName, $"nsg-{prefix}");
            Assert.AreEqual(this.resourceGroup.VirtualNetworkName, $"vnet-{prefix}");
            Assert.AreEqual(this.resourceGroup.KeyVaultName, $"kv-{prefix}");

            Assert.AreEqual(this.resourceGroup.VirtualMachines[0].Name, $"{prefix}-0");
            Assert.AreEqual(this.resourceGroup.VirtualMachines[0].AdminUserName, "junovmadmin");
            Assert.AreEqual(this.resourceGroup.VirtualMachines[0].AdminPasswordSecretName, $"{prefix}-0-pw");

            Assert.AreEqual(this.resourceGroup.VirtualMachines[1].Name, $"{prefix}-1");
            Assert.AreEqual(this.resourceGroup.VirtualMachines[1].AdminUserName, "junovmadmin");
            Assert.AreEqual(this.resourceGroup.VirtualMachines[1].AdminPasswordSecretName, $"{prefix}-1-pw");
        }

        [Test]
        public void ResourceGroupDefinitionCanBeSerialized()
        {
            SerializationAssert.IsJsonSerializable<VmResourceGroupDefinition>(this.resourceGroup);
        }
    }
}