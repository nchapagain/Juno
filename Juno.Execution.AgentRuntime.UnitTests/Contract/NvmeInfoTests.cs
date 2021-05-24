namespace Juno.Execution.AgentRuntime.Contract
{
    using System;
    using System.Text;
    using AutoFixture;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class NvmeInfoTests : RuntimeContractsTests<NvmeInfo>
    {
        [Test]
        public void ConstructorValidatesNonStringParameters()
        {
            NvmeInfo validComponent = this.MockFixture.Create<NvmeInfo>();
            Assert.Throws<ArgumentException>(() => new NvmeInfo(null, validComponent.SmartHealth, validComponent.ModelName, validComponent.FirmwareVersion, validComponent.SerialNumber, validComponent.NvmeHealth));
            Assert.Throws<ArgumentException>(() => new NvmeInfo(validComponent.Device, null, validComponent.ModelName, validComponent.FirmwareVersion, validComponent.SerialNumber, validComponent.NvmeHealth));
            Assert.Throws<ArgumentException>(() => new NvmeInfo(validComponent.Device, validComponent.SmartHealth, validComponent.ModelName, validComponent.FirmwareVersion, validComponent.SerialNumber, null));
        }

        [Test]
        [TestCase(null)]
        public void ConstructorValidatesStringParameters(string invalidParameter)
        {
            NvmeInfo validComponent = this.MockFixture.Create<NvmeInfo>();
            Assert.Throws<ArgumentException>(() => new NvmeInfo(validComponent.Device, validComponent.SmartHealth, invalidParameter, validComponent.FirmwareVersion, validComponent.SerialNumber, validComponent.NvmeHealth));
            Assert.Throws<ArgumentException>(() => new NvmeInfo(validComponent.Device, validComponent.SmartHealth, validComponent.ModelName, invalidParameter, validComponent.SerialNumber, validComponent.NvmeHealth));
            Assert.Throws<ArgumentException>(() => new NvmeInfo(validComponent.Device, validComponent.SmartHealth, validComponent.ModelName, validComponent.FirmwareVersion, invalidParameter, validComponent.NvmeHealth));
        }
    }
}
