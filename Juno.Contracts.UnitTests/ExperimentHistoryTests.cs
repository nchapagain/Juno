namespace Juno.Contracts
{
    using System;
    using AutoFixture;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ExperimentHistoryTests
    {
        private Fixture mockFixture;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks(ExperimentType.AB);
        }

        [Test]
        public void ExperimentHistoryIsJsonSerializableByDefault()
        {
            SerializationAssert.IsJsonSerializable(this.mockFixture.Create<ExperimentHistory>());
        }

        [Test]
        public void ExperimentHistoryIsJsonSerializableUsingExpectedSerializerSettings()
        {
            SerializationAssert.IsJsonSerializable(
                this.mockFixture.Create<ExperimentHistory>(),
                ContractSerialization.DefaultJsonSerializationSettings);
        }

        [Test]
        public void ExperimentHistoryCorrectlyImplementsEqualitySemantics()
        {
            ExperimentHistory instance1 = this.mockFixture.Create<ExperimentHistory>();
            ExperimentHistory instance2 = this.mockFixture.Create<ExperimentHistory>();

            // Each individual contract class implements an override of GetHashCode. The
            // ExperimentComponent class uses this method to determine equality.
            EqualityAssert.CorrectlyImplementsEqualitySemantics<ExperimentHistory>(() => instance1, () => instance2);
        }

        [Test]
        public void ExperimentHistoryCorrectlyImplementsHashcodeSemantics()
        {
            ExperimentHistory instance1 = this.mockFixture.Create<ExperimentHistory>();
            ExperimentHistory instance2 = this.mockFixture.Create<ExperimentHistory>();

            EqualityAssert.CorrectlyImplementsHashcodeSemantics(() => instance1, () => instance2);
        }
    }
}
