namespace Juno.Execution.ArmIntegration
{
    using AutoFixture;
    using Microsoft.Azure.CRC;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ResourceStateTests
    {
        private Fixture mockFixture;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
        }

        [Test]
        public void ResourceStateStateIsSetToPendingOnStart()
        {
            ResourceState resource = new ResourceState();
            Assert.IsTrue(resource.State == ProvisioningState.Pending);
        }

        [Test]
        public void ResourceStateCanBeSerialized()
        {
            ResourceState resource = this.mockFixture.Create<ResourceState>();
            SerializationAssert.IsJsonSerializable<ResourceState>(resource);
        }
    }
}