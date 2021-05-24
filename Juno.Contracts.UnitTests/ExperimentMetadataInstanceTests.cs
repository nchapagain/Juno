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
    public class ExperimentMetadataInstanceTests
    {
        private Fixture mockFixture;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks();
        }

        [Test]
        public void ExperimentMetadataInstanceIsJsonSerializableByDefault()
        {
            SerializationAssert.IsJsonSerializable(this.mockFixture.Create<ExperimentMetadataInstance>());
        }

        [Test]
        public void ExperimentMetadataInstanceIsJsonSerializableUsingExpectedSerializerSettings()
        {
            SerializationAssert.IsJsonSerializable(
                this.mockFixture.Create<ExperimentMetadataInstance>(),
                ContractSerialization.DefaultJsonSerializationSettings);
        }

        [Test]
        public void ExperimentMetadataInstanceCorrectlyImplementsEqualitySemantics()
        {
            // Because the IEquality<T> interface is on the Item<T> base, we have to use
            // that base to do the comparison.
            Item<ExperimentMetadata> instance1 = this.mockFixture.Create<ExperimentMetadataInstance>();
            Item<ExperimentMetadata> instance2 = this.mockFixture.Create<ExperimentMetadataInstance>();

            EqualityAssert.CorrectlyImplementsEqualitySemantics(() => instance1, () => instance2);
        }

        [Test]
        public void ExperimentMetadataInstanceCorrectlyImplementsHashcodeSemantics()
        {
            ExperimentMetadataInstance instance1 = this.mockFixture.Create<ExperimentMetadataInstance>();
            ExperimentMetadataInstance instance2 = this.mockFixture.Create<ExperimentMetadataInstance>();

            EqualityAssert.CorrectlyImplementsHashcodeSemantics(() => instance1, () => instance2);
        }

        [Test]
        public void ExperimentMetadataInstanceHashCodesAreNotCaseSensitive()
        {
            ExperimentMetadataInstance template = this.mockFixture.Create<ExperimentMetadataInstance>();
            ExperimentMetadataInstance instance1 = new ExperimentMetadataInstance(
                template.Id.ToLowerInvariant(),
                template.Definition);

            instance1.Extensions.Add("Any".ToLowerInvariant(), JToken.Parse(new Dictionary<int, string>
            {
                [1] = "Any Value".ToLowerInvariant()
            }.ToJson()));

            ExperimentMetadataInstance instance2 = new ExperimentMetadataInstance(
                template.Id.ToUpperInvariant(),
                template.Definition);

            instance2.Extensions.Add("Any".ToUpperInvariant(), JToken.Parse(new Dictionary<int, string>
            {
                [1] = "Any Value".ToUpperInvariant()
            }.ToJson()));

            Assert.AreEqual(instance1.GetHashCode(), instance2.GetHashCode());
        }
    }
}
