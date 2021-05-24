namespace Juno.Execution.ArmIntegration
{
    using AutoFixture;
    using Juno.Execution.ArmIntegration.Parameters;
    using Microsoft.Azure.CRC;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class TemplatesParametersTests
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
            var parameters = this.mockFixture.Create<VmTemplateParameters>();
            Assert.IsNotNull(parameters);
        }

        [Test]
        public void VmTemplateParametersCanBeSerialized()
        {
            var parameters = this.mockFixture.Create<VmTemplateParameters>();
            SerializationAssert.IsJsonSerializable<VmTemplateParameters>(parameters);
        }

        [Test]
        public void ResourceGroupTemplateParametersCanBeSerialized()
        {
            var parameters = this.mockFixture.Create<VmResourceGroupTemplateParameters>();
            SerializationAssert.IsJsonSerializable<VmResourceGroupTemplateParameters>(parameters);
        }

        [Test]
        public void VmBootstrapTemplateParametersCanBeSerialized()
        {
            var parameters = this.mockFixture.Create<VmBootstrapTemplateParameters>();
            SerializationAssert.IsJsonSerializable<VmBootstrapTemplateParameters>(parameters);
        }
    }
}