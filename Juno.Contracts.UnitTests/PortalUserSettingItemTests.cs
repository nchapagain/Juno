namespace Juno.Contracts
{
    using System.Collections.Generic;
    using AutoFixture;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class PortalUserSettingItemTests
    {
        private Fixture mockFixture;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks();
        }

        [Test]
        public void PortalUserSettingItemIsJsonSerializableByDefault()
        {
            SerializationAssert.IsJsonSerializable(this.mockFixture.Create<PortalUserSettingItem>());
        }

        [Test]
        public void PortalUserSettingItemIsJsonSerializableUsingExpectedSerializerSettings()
        {
            SerializationAssert.IsJsonSerializable(
                this.mockFixture.Create<PortalUserSettingItem>(),
                ContractSerialization.DefaultJsonSerializationSettings);
        }

        [Test]
        public void PortalUserSettingItemCorrectlyImplementsEqualitySemantics()
        {
            // Because the IEquality<T> interface is on the Item<T> base, we have to use
            // that base to do the comparison.
            Item<PortalUserSetting> instance1 = this.mockFixture.Create<PortalUserSettingItem>();
            Item<PortalUserSetting> instance2 = this.mockFixture.Create<PortalUserSettingItem>();

            EqualityAssert.CorrectlyImplementsEqualitySemantics(() => instance1, () => instance2);
        }

        [Test]
        public void PortalUserSettingItemCorrectlyImplementsHashcodeSemantics()
        {
            PortalUserSettingItem instance1 = this.mockFixture.Create<PortalUserSettingItem>();
            PortalUserSettingItem instance2 = this.mockFixture.Create<PortalUserSettingItem>();

            EqualityAssert.CorrectlyImplementsHashcodeSemantics(() => instance1, () => instance2);
        }

        [Test]
        public void PortalUserSettingItemHashCodesAreNotCaseSensitive()
        {
            PortalUserSettingItem portalUserSettingItem = this.mockFixture.Create<PortalUserSettingItem>();
            PortalUserSettingItem portalUserSettingItem1 = new PortalUserSettingItem(
                portalUserSettingItem.Id.ToLowerInvariant(),
                portalUserSettingItem.Definition);

            portalUserSettingItem1.Extensions.Add("Any".ToLowerInvariant(), JToken.Parse(new Dictionary<int, string>
            {
                [1] = "Any Value".ToLowerInvariant()
            }.ToJson()));

            PortalUserSettingItem portalUserSettingItem2 = new PortalUserSettingItem(
                portalUserSettingItem.Id.ToUpperInvariant(),
                portalUserSettingItem.Definition);

            portalUserSettingItem2.Extensions.Add("Any".ToUpperInvariant(), JToken.Parse(new Dictionary<int, string>
            {
                [1] = "Any Value".ToUpperInvariant()
            }.ToJson()));

            Assert.AreEqual(portalUserSettingItem1.GetHashCode(), portalUserSettingItem2.GetHashCode());
        }
    }
}
