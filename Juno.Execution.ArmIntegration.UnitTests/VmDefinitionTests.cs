namespace Juno.Execution.ArmIntegration
{
    using System;
    using AutoFixture;
    using Microsoft.Azure.CRC;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class VmDefinitionTests
    {
        private Fixture mockFixture;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
        }

        [Test]
        public void VirtualMachineDefinitionStateIsSetToPendingOnStart()
        {
            VmDefinition vm = new VmDefinition();
            Assert.IsTrue(vm.State == ProvisioningState.Pending);
        }

        [Test]
        public void VirtualMachineDefinitionCanBeSerialized()
        {
            VmDefinition vm = this.mockFixture.Create<VmDefinition>();
            SerializationAssert.IsJsonSerializable<VmDefinition>(vm);
        }

        [Test]
        public void VmDefinitionDeploymentRequestIsTimedOutReturnsTheExpectedResultWhenTheRequestIsNotExpired()
        {
            VmDefinition vm = new VmDefinition();
            Assert.IsFalse(vm.IsDeploymentRequestTimedOut);

            vm.DeploymentRequestStartTime = DateTime.UtcNow;
            Assert.IsFalse(vm.IsDeploymentRequestTimedOut);
        }

        [Test]
        public void VmDefinitionDeploymentRequestIsTimedOutReturnsTheExpectedResultWhenTheRequestIsExpired()
        {
            VmDefinition vm = new VmDefinition();

            vm.DeploymentRequestStartTime = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(61));
            vm.DeploymentRequestTimeout = TimeSpan.FromMinutes(60);
            Assert.IsTrue(vm.IsDeploymentRequestTimedOut);
        }
    }
}