namespace Juno.Execution.Api.Validation
{
    using Juno.Contracts;
    using Juno.Contracts.Validation;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class MetadataValidationTests
    {
        private ExperimentFixture mockFixture;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new ExperimentFixture();
            this.mockFixture.Setup();
        }

        [Test]
        public void ValidationCorrectlyIdentifiesExperimentsHavingAllRequiredMetadataDefined()
        {
            // The mock extensions define a valid experiment (including having required metadata).
            Experiment experiment = this.mockFixture.Experiment;
            ValidationResult result = MetadataValidation.Instance.Validate(experiment);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid);
            Assert.IsEmpty(result.ValidationErrors);
        }

        [Test]
        public void ValidationCorrectlyIdentifiesExperimentsThatAreMissingRequiredMetadata()
        {
            // The mock extensions define a valid experiment (including having required metadata).
            Experiment experiment = this.mockFixture.Experiment;
            experiment.Metadata.Clear();

            foreach (string requiredMetadataProperty in MetadataValidation.RequiredMetadata)
            {
                ValidationResult result = MetadataValidation.Instance.Validate(experiment);

                Assert.IsNotNull(result);
                Assert.IsFalse(result.IsValid);
                Assert.IsNotEmpty(result.ValidationErrors);

                experiment.Metadata.Add(requiredMetadataProperty, "AnyValue");
            }
        }
    }
}
