namespace Juno.Contracts
{
    using System;
    using AutoFixture;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ExperimentTests
    {
        private Fixture mockFixture;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks(ExperimentType.AB);
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void ExperimentConstructorsValidateRequiredParameters(string invalidParameter)
        {
            Experiment validExperiment = this.mockFixture.Create<Experiment>();

            Assert.Throws<ArgumentException>(() => new Experiment(
                invalidParameter,
                validExperiment.Description,
                validExperiment.ContentVersion,
                validExperiment.Metadata,
                validExperiment.Parameters,
                validExperiment.Workflow));

            Assert.Throws<ArgumentException>(() => new Experiment(
               validExperiment.Name,
               validExperiment.Description,
               invalidParameter,
               validExperiment.Metadata,
               validExperiment.Parameters,
               validExperiment.Workflow));

            Assert.Throws<ArgumentException>(() => new Experiment(
               validExperiment.Name,
               validExperiment.Description,
               validExperiment.ContentVersion,
               validExperiment.Metadata,
               validExperiment.Parameters,
               null));
        }

        [Test]
        public void ExperimentIsJsonSerializableByDefault()
        {
            SerializationAssert.IsJsonSerializable(this.mockFixture.Create<Experiment>());
        }

        [Test]
        public void ExperimentIsJsonSerializableUsingExpectedSerializerSettings()
        {
            SerializationAssert.IsJsonSerializable(
                this.mockFixture.Create<Experiment>(),
                ContractSerialization.DefaultJsonSerializationSettings);
        }

        [Test]
        public void ExperimentCorrectlyImplementsEqualitySemantics()
        {
            Experiment instance1 = this.mockFixture.Create<Experiment>();
            Experiment instance2 = this.mockFixture.Create<Experiment>();

            // Each individual contract class implements an override of GetHashCode. The
            // ExperimentComponent class uses this method to determine equality.
            EqualityAssert.CorrectlyImplementsEqualitySemantics<Experiment>(() => instance1, () => instance2);
        }

        [Test]
        public void ExperimentCorrectlyImplementsHashcodeSemantics()
        {
            Experiment instance1 = this.mockFixture.Create<Experiment>();
            Experiment instance2 = this.mockFixture.Create<Experiment>();

            EqualityAssert.CorrectlyImplementsHashcodeSemantics(() => instance1, () => instance2);
        }

        [Test]
        public void ExperimentHashCodesHandlePropertiesWithNullValues()
        {
            // The Description, Metadata, Parameters, Payload, and Workload properties are all null here.
            Experiment template = this.mockFixture.Create<Experiment>();
            Experiment instance = new Experiment(
                template.Name.ToLowerInvariant(),
                null,
                template.ContentVersion.ToLowerInvariant(),
                null,
                null,
                template.Workflow);

            Assert.DoesNotThrow(() => instance.GetHashCode());
        }

        [Test]
        public void ExperimentHashCodesAreNotCaseSensitive()
        {
            Experiment template = this.mockFixture.Create<Experiment>();
            Experiment instance1 = new Experiment(
                template.Name.ToLowerInvariant(),
                template.Description.ToLowerInvariant(),
                template.ContentVersion.ToLowerInvariant(),
                template.Metadata,
                template.Parameters,
                template.Workflow);

            Experiment instance2 = new Experiment(
                template.Name.ToUpperInvariant(),
                template.Description.ToUpperInvariant(),
                template.ContentVersion.ToUpperInvariant(),
                template.Metadata,
                template.Parameters,
                template.Workflow);

            Assert.AreEqual(instance1.GetHashCode(), instance2.GetHashCode());
        }
    }
}
