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
    public class FpgaConfigTests
    {
        private Fixture mockFixture;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
        }

        [Test]
        public void FPGAConfigIsJsonSerializableByDefault()
        {
            SerializationAssert.IsJsonSerializable(this.mockFixture.Create<FpgaConfig>());
        }

        [Test]
        public void FPGAConfigIsJsonSerializableUsingExpectedSerializerSettings()
        {
            SerializationAssert.IsJsonSerializable(
                this.mockFixture.Create<FpgaHealth>(),
                ContractSerialization.DefaultJsonSerializationSettings);
        }

        [Test]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase(null)]
        public void FPGAConfigConstructorValidatesRequiredStringParameters(string invalidParameter)
        {
            var validComponent = this.mockFixture.Create<FpgaConfig>();

            Assert.Throws<ArgumentException>(() => new FpgaConfig(
                validComponent.IsStatusOK,
                invalidParameter,
                validComponent.RoleID,
                validComponent.RoleVersion,
                validComponent.ShellID,
                validComponent.ShellVersion,
                validComponent.IsGolden));

            Assert.Throws<ArgumentException>(() => new FpgaConfig(
                validComponent.IsStatusOK,
                validComponent.BoardName,
                invalidParameter,
                validComponent.RoleVersion,
                validComponent.ShellID,
                validComponent.ShellVersion,
                validComponent.IsGolden));
        }
    }
}