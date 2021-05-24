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
    public class ExperimentMetadataTests
    {
        private Fixture mockFixture;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks();
        }

        [Test]
        public void ExperimentMetadataIsJsonSerializableByDefault()
        {
            SerializationAssert.IsJsonSerializable(this.mockFixture.Create<ExperimentMetadata>());
        }

        [Test]
        public void ExperimentMetadataIsJsonSerializableUsingExpectedSerializerSettings()
        {
            SerializationAssert.IsJsonSerializable(
                this.mockFixture.Create<ExperimentMetadata>(),
                ContractSerialization.DefaultJsonSerializationSettings);
        }

        [Test]
        public void ExperimentMetadataCorrectlyImplementsEqualitySemantics()
        {
            // Because the IEquality<T> interface is on the Item<T> base, we have to use
            // that base to do the comparison.
            ExperimentMetadata instance1 = this.mockFixture.Create<ExperimentMetadata>();
            ExperimentMetadata instance2 = this.mockFixture.Create<ExperimentMetadata>();

            EqualityAssert.CorrectlyImplementsEqualitySemantics(() => instance1, () => instance2);
        }

        [Test]
        public void ExperimentMetadataCorrectlyImplementsHashcodeSemantics()
        {
            ExperimentMetadata instance1 = this.mockFixture.Create<ExperimentMetadata>();
            ExperimentMetadata instance2 = this.mockFixture.Create<ExperimentMetadata>();

            EqualityAssert.CorrectlyImplementsHashcodeSemantics(() => instance1, () => instance2);
        }

        [Test]
        public void ExperimentMetadataHashCodesAreNotCaseSensitive()
        {
            ExperimentMetadata template = this.mockFixture.Create<ExperimentMetadata>();
            ExperimentMetadata instance1 = new ExperimentMetadata(
                template.ExperimentId.ToLowerInvariant(),
                template.Metadata);

            ExperimentMetadata instance2 = new ExperimentMetadata(
                template.ExperimentId.ToUpperInvariant(),
                template.Metadata);

            Assert.AreEqual(instance1.GetHashCode(), instance2.GetHashCode());
        }
    }
}
