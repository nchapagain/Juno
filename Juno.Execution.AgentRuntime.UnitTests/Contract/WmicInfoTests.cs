namespace Juno.Execution.AgentRuntime.Contract
{
    using System;
    using AutoFixture;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class WmicInfoTests : RuntimeContractsTests<SsdWmicInfo>
    {
        [Test]
        [TestCase(null)]
        public void ConstructorValidatesStringParameters(string invalidParameter)
        {
            SsdWmicInfo validComponent = this.MockFixture.Create<SsdWmicInfo>();
            Assert.Throws<ArgumentException>(() => new SsdWmicInfo(invalidParameter, validComponent.ModelName, validComponent.SerialNumber));
            Assert.Throws<ArgumentException>(() => new SsdWmicInfo(validComponent.FirmwareVersion, invalidParameter, validComponent.SerialNumber));
            Assert.Throws<ArgumentException>(() => new SsdWmicInfo(validComponent.FirmwareVersion, validComponent.ModelName, invalidParameter));
        }
    }
}
