namespace Juno.Contracts.Tests
{
    using System;
    using AutoFixture;
    using Juno.Execution.AgentRuntime.Contract;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class FpgaHealthTests
    {
        private Fixture mockFixture;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
        }

        /*Tests for FPGAHealth Class*/
        [Test]
        public void FPGAHealthIsJsonSerializableByDefault()
        {
            SerializationAssert.IsJsonSerializable(this.mockFixture.Create<FpgaHealth>());
        }

        [Test]
        public void FPGAHealthIsJsonSerializableUsingExpectedSerializerSettings()
        {
            SerializationAssert.IsJsonSerializable(
                this.mockFixture.Create<FpgaHealth>(),
                ContractSerialization.DefaultJsonSerializationSettings);
        }

        [Test]
        [TestCase(null)]
        public void FPGAHealthConstructorValidatesRequiredParameters(object invalidParameter)
        {
            Assert.Throws<ArgumentException>(() => new FpgaHealth(
                (FpgaConfig)invalidParameter,
                this.mockFixture.Create<FpgaTemperature>(),
                this.mockFixture.Create<FpgaNetwork>(),
                this.mockFixture.Create<FpgaID>(),
                this.mockFixture.Create<FpgaClockReset>(),
                this.mockFixture.Create<FpgaPcie>(),
                this.mockFixture.Create<FpgaDram>(),
                this.mockFixture.Create<FpgaCables>()));

            Assert.Throws<ArgumentException>(() => new FpgaHealth(
                this.mockFixture.Create<FpgaConfig>(),
                (FpgaTemperature)invalidParameter,                
                this.mockFixture.Create<FpgaNetwork>(),
                this.mockFixture.Create<FpgaID>(),
                this.mockFixture.Create<FpgaClockReset>(),
                this.mockFixture.Create<FpgaPcie>(),
                this.mockFixture.Create<FpgaDram>(),
                this.mockFixture.Create<FpgaCables>()));

            Assert.Throws<ArgumentException>(() => new FpgaHealth(
                this.mockFixture.Create<FpgaConfig>(),                
                this.mockFixture.Create<FpgaTemperature>(),
                (FpgaNetwork)invalidParameter,
                this.mockFixture.Create<FpgaID>(),
                this.mockFixture.Create<FpgaClockReset>(),
                this.mockFixture.Create<FpgaPcie>(),
                this.mockFixture.Create<FpgaDram>(),
                this.mockFixture.Create<FpgaCables>()));

            Assert.Throws<ArgumentException>(() => new FpgaHealth(
                this.mockFixture.Create<FpgaConfig>(),
                this.mockFixture.Create<FpgaTemperature>(),
                this.mockFixture.Create<FpgaNetwork>(),
                (FpgaID)invalidParameter,
                this.mockFixture.Create<FpgaClockReset>(),
                this.mockFixture.Create<FpgaPcie>(),
                this.mockFixture.Create<FpgaDram>(),
                this.mockFixture.Create<FpgaCables>()));

            Assert.Throws<ArgumentException>(() => new FpgaHealth(
                this.mockFixture.Create<FpgaConfig>(),
                this.mockFixture.Create<FpgaTemperature>(),
                this.mockFixture.Create<FpgaNetwork>(),
                this.mockFixture.Create<FpgaID>(),
                (FpgaClockReset)invalidParameter,
                this.mockFixture.Create<FpgaPcie>(),
                this.mockFixture.Create<FpgaDram>(),
                this.mockFixture.Create<FpgaCables>()));

            Assert.Throws<ArgumentException>(() => new FpgaHealth(
                this.mockFixture.Create<FpgaConfig>(),
                this.mockFixture.Create<FpgaTemperature>(),
                this.mockFixture.Create<FpgaNetwork>(),
                this.mockFixture.Create<FpgaID>(),
                this.mockFixture.Create<FpgaClockReset>(),
                (FpgaPcie)invalidParameter,
                this.mockFixture.Create<FpgaDram>(),
                this.mockFixture.Create<FpgaCables>()));

            Assert.Throws<ArgumentException>(() => new FpgaHealth(
                this.mockFixture.Create<FpgaConfig>(),
                this.mockFixture.Create<FpgaTemperature>(),
                this.mockFixture.Create<FpgaNetwork>(),
                this.mockFixture.Create<FpgaID>(),
                this.mockFixture.Create<FpgaClockReset>(),                
                this.mockFixture.Create<FpgaPcie>(),
                (FpgaDram)invalidParameter,
                this.mockFixture.Create<FpgaCables>()));

            Assert.Throws<ArgumentException>(() => new FpgaHealth(
                this.mockFixture.Create<FpgaConfig>(),
                this.mockFixture.Create<FpgaTemperature>(),
                this.mockFixture.Create<FpgaNetwork>(),
                this.mockFixture.Create<FpgaID>(),
                this.mockFixture.Create<FpgaClockReset>(),
                this.mockFixture.Create<FpgaPcie>(),
                this.mockFixture.Create<FpgaDram>(),
                (FpgaCables)invalidParameter));
        }

        [Test]
        public void FPGAHealthCopyContructorAndEqualityComparatorWorksAsExpected()
        {
            var fpgaHealth1 = this.mockFixture.Create<FpgaHealth>();
            var fpgaHealth2 = new FpgaHealth(fpgaHealth1);
            var fpgaHealth3 = this.mockFixture.Create<FpgaHealth>();

            Assert.IsTrue(fpgaHealth1 == fpgaHealth2);
            Assert.IsTrue(fpgaHealth1 != fpgaHealth3);
        }
    }
}