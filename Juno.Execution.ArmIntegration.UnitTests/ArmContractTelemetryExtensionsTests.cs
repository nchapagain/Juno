namespace Juno.Execution.ArmIntegration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AutoFixture;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Telemetry;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ArmContractTelemetryExtensionsTests
    {
        private Fixture mockFixture;
        private EventContext eventContext;
        private VmResourceGroupDefinition exampleVmResourceGroupDefinition;
        private AzureVmSpecification exampleVmSpecification1;
        private AzureVmSpecification exampleVmSpecification2;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks();
            this.eventContext = new EventContext(Guid.NewGuid());
            this.exampleVmSpecification1 = new AzureVmSpecification(
                osDiskStorageAccountType: "AnyOSDiskStorageAccountType",
                vmSize: "Standard_E3s_v3",
                osPublisher: "AnyPublisher",
                osOffer: "AnyOffer",
                osSku: "AnyOSSku",
                osVersion: "1.2.3.4",
                enableAcceleratedNetworking: true,
                clusterId: "Cluster01",
                tipSessionId: Guid.NewGuid().ToString(),
                nodeId: Guid.NewGuid().ToString());

            // With data disks
            this.exampleVmSpecification2 = new AzureVmSpecification(
                osDiskStorageAccountType: "AnyDiskStorageAccountType",
                vmSize: "Standard_E3s_v3",
                osPublisher: "AnyPublisher",
                osOffer: "AnyOffer",
                osSku: "AnyOSSku",
                osVersion: "1.2.3.4",
                dataDiskCount: 2,
                dataDiskSku: "AnyDiskSku",
                dataDiskSizeInGB: 32,
                dataDiskStorageAccountType: "AnyDiskStorageAccountType",
                enableAcceleratedNetworking: true,
                clusterId: "Cluster02",
                tipSessionId: Guid.NewGuid().ToString(),
                nodeId: Guid.NewGuid().ToString());

            this.exampleVmResourceGroupDefinition = new VmResourceGroupDefinition(
                environment: "AnyEnvironment",
                subscriptionId: Guid.NewGuid().ToString(),
                experimentId: Guid.NewGuid().ToString(),
                stepId: Guid.NewGuid().ToString(),
                vmSpecs: new List<AzureVmSpecification>
                {
                    this.exampleVmSpecification1,
                    this.exampleVmSpecification2
                },
                region: "AnyRegion");
        }

        [Test]
        public void AddContextExtensionAddsTheExpectedResourceGroupInformationToTheEventContext()
        {
            this.eventContext.AddContext(this.exampleVmResourceGroupDefinition);

            Assert.IsTrue(this.eventContext.Properties.ContainsKey("resourceGroup"));

            // It is easiest just to compare the properties as JSON
            SerializationAssert.JsonEquals(
                this.eventContext.Properties["resourceGroup"].ToJson(),
                new
                {
                    experimentId = this.exampleVmResourceGroupDefinition.ExperimentId,
                    stepId = this.exampleVmResourceGroupDefinition.StepId,
                    name = this.exampleVmResourceGroupDefinition.Name,
                    subscriptionId = this.exampleVmResourceGroupDefinition.SubscriptionId,
                    region = this.exampleVmResourceGroupDefinition.Region,
                    cluster = $"{this.exampleVmSpecification1.ClusterId},{this.exampleVmSpecification2.ClusterId}",
                    tipSessionId = $"{this.exampleVmSpecification1.TipSessionId},{this.exampleVmSpecification2.TipSessionId}",
                    deploymentName = this.exampleVmResourceGroupDefinition.DeploymentName,
                    subnetName = this.exampleVmResourceGroupDefinition.SubnetName,
                    vmNetworkName = this.exampleVmResourceGroupDefinition.VirtualNetworkName,
                    networkSecurityGroupName = this.exampleVmResourceGroupDefinition.NetworkSecurityGroupName,
                    state = this.exampleVmResourceGroupDefinition.State,
                    deletionState = this.exampleVmResourceGroupDefinition.DeletionState
                }.ToJson());

            Assert.IsTrue(this.eventContext.Properties.ContainsKey("virtualMachines"));
            SerializationAssert.JsonEquals(
                this.eventContext.Properties["virtualMachines"].ToJson(),
                this.exampleVmResourceGroupDefinition.VirtualMachines.Select(vm =>
                new
                {
                    vmName = vm.Name,
                    vmSize = vm.VirtualMachineSize,
                    vmOsDiskStorageAccountType = vm.OsDiskStorageAccountType,
                    vmOsSku = vm.ImageReference,
                    vmDisks = vm.VirtualDisks,
                    enableAcceleratedNetworking = vm.EnableAcceleratedNetworking,
                    deploymentCorrelationId = vm.CorrelationId,
                    deploymentId = vm.DeploymentId,
                    deploymentName = vm.DeploymentName,
                    deploymentState = vm.State
                }).ToJson());
        }

        [Test]
        public void AddContextExtensionAddsTheExpectedVirtualMachineSpecificationInformationToTheEventContext()
        {
            VmDefinition expectedVmDefinition = this.exampleVmResourceGroupDefinition.VirtualMachines.ElementAt(0);
            this.eventContext.AddContext(expectedVmDefinition);

            // It is easiest just to compare the properties as JSON
            Assert.IsTrue(this.eventContext.Properties.ContainsKey("virtualMachine"));
            SerializationAssert.JsonEquals(
                this.eventContext.Properties["virtualMachine"].ToJson(),
                new
                {
                    vmName = expectedVmDefinition.Name,
                    vmSize = expectedVmDefinition.VirtualMachineSize,
                    vmOsDiskStorageAccountType = expectedVmDefinition.OsDiskStorageAccountType,
                    vmOsSku = expectedVmDefinition.ImageReference,
                    vmDisks = expectedVmDefinition.VirtualDisks,
                    enableAcceleratedNetworking = expectedVmDefinition.EnableAcceleratedNetworking,
                    deploymentCorrelationId = expectedVmDefinition.CorrelationId,
                    deploymentId = expectedVmDefinition.DeploymentId,
                    deploymentName = expectedVmDefinition.DeploymentName,
                    deploymentState = expectedVmDefinition.State,
                    role = expectedVmDefinition.Role,
                    tipSessionId = expectedVmDefinition.TipSessionId,
                    nodeId = expectedVmDefinition.NodeId
                }.ToJson());

            // ...and where data disks are defined.
            expectedVmDefinition = this.exampleVmResourceGroupDefinition.VirtualMachines.ElementAt(1);
            this.eventContext.Properties.Clear();
            this.eventContext.AddContext(expectedVmDefinition);

            // It is easiest just to compare the properties as JSON
            Assert.IsTrue(this.eventContext.Properties.ContainsKey("virtualMachine"));
            SerializationAssert.JsonEquals(
                this.eventContext.Properties["virtualMachine"].ToJson(),
                new
                {
                    vmName = expectedVmDefinition.Name,
                    vmSize = expectedVmDefinition.VirtualMachineSize,
                    vmOsDiskStorageAccountType = expectedVmDefinition.OsDiskStorageAccountType,
                    vmOsSku = expectedVmDefinition.ImageReference,
                    vmDisks = expectedVmDefinition.VirtualDisks,
                    enableAcceleratedNetworking = expectedVmDefinition.EnableAcceleratedNetworking,
                    deploymentCorrelationId = expectedVmDefinition.CorrelationId,
                    deploymentId = expectedVmDefinition.DeploymentId,
                    deploymentName = expectedVmDefinition.DeploymentName,
                    deploymentState = expectedVmDefinition.State,
                    role = expectedVmDefinition.Role,
                    tipSessionId = expectedVmDefinition.TipSessionId,
                    nodeId = expectedVmDefinition.NodeId
                }.ToJson());
        }

        [Test]
        public void AddContextExtensionAddsTheExpectedVirtualMachineSpecificationsInformationToTheEventContext()
        {
            this.eventContext.AddContext(this.exampleVmResourceGroupDefinition.VirtualMachines);

            // It is easiest just to compare the properties as JSON
            Assert.IsTrue(this.eventContext.Properties.ContainsKey("virtualMachines"));
            SerializationAssert.JsonEquals(
                this.eventContext.Properties["virtualMachines"].ToJson(), this.exampleVmResourceGroupDefinition.VirtualMachines.Select(vmDefinition =>
                new
                {
                    vmName = vmDefinition.Name,
                    vmSize = vmDefinition.VirtualMachineSize,
                    vmOsDiskStorageAccountType = vmDefinition.OsDiskStorageAccountType,
                    vmOsSku = vmDefinition.ImageReference,
                    vmDisks = vmDefinition.VirtualDisks,
                    enableAcceleratedNetworking = vmDefinition.EnableAcceleratedNetworking,
                    deploymentCorrelationId = vmDefinition.CorrelationId,
                    deploymentId = vmDefinition.DeploymentId,
                    deploymentName = vmDefinition.DeploymentName,
                    deploymentState = vmDefinition.State
                }).ToJson());
        }
    }
}
