namespace Juno.Contracts
{
    using System;
    using AutoFixture;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class PortalUserSettingTests
    {
        private Fixture mockFixture;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks(ExperimentType.AB);
        }

        [Test]
        public void PortalUserSettingIsJsonSerializableByDefault()
        {
            SerializationAssert.IsJsonSerializable(this.mockFixture.Create<PortalUserSetting>());
        }

        [Test]
        public void PortalUserSettingIsJsonSerializableUsingExpectedSerializerSettings()
        {
            SerializationAssert.IsJsonSerializable(
                this.mockFixture.Create<PortalUserSetting>(),
                ContractSerialization.DefaultJsonSerializationSettings);
        }

        [Test]
        public void PortalUserSettingCorrectlyImplementsEqualitySemantics()
        {
            PortalUserSetting instance1 = this.mockFixture.Create<PortalUserSetting>();
            PortalUserSetting instance2 = this.mockFixture.Create<PortalUserSetting>();

            EqualityAssert.CorrectlyImplementsEqualitySemantics<PortalUserSetting>(() => instance1, () => instance2);
        }

        [Test]
        public void PortalUserSettingCorrectlyImplementsHashcodeSemantics()
        {
            PortalUserSetting instance1 = this.mockFixture.Create<PortalUserSetting>();
            PortalUserSetting instance2 = this.mockFixture.Create<PortalUserSetting>();

            EqualityAssert.CorrectlyImplementsHashcodeSemantics(() => instance1, () => instance2);
        }
    }
}
