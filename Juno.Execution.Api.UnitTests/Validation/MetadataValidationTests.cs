namespace Juno.Execution.Api.Validation
{
    using System;
    using System.Collections.Generic;
    using Juno.Contracts;
    using Juno.Contracts.Validation;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class MetadataValidationTests
    {
        private ExperimentFixture mockFixture;
        private Experiment mockExperiment;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new ExperimentFixture();
            this.mockFixture.Setup();
            Experiment template = this.mockFixture.Experiment;

            this.mockExperiment = new Experiment(
                template.Name,
                template.Description,
                template.ContentVersion,
                new Dictionary<string, IConvertible>
                {
                    // ALL experiments require the following metadata properties
                    ["experimentType"] = "Test",
                    ["generation"] = "Any",
                    ["nodeCpuId"] = "12345",
                    ["payload"] = "MCU3030.1",
                    ["workload"] = "PERF-CPU-V1",
                    ["revision"] = "MCU3030.1 Revision1",
                    ["tenantId"] = "CRC AIR",
                    ["impactType"] = "None"
                },
                template.Parameters,
                template.Workflow,
                template.Schema);

            // ["workloadType"] = "VirtualClient",
            // ["workloadVersion"] = "1.0.9876.11",
            // ["payloadType"] = "Microcode Update",
            // ["payloadVersion"] = "30b123",
            // ["payloadPFVersion"] = "10.20.1596.22",
        }

        [Test]
        public void ValidationCorrectlyIdentifiesExperimentsHavingAllRequiredMetadataDefined()
        {
            // The mock extensions define a valid experiment (including having required metadata).
            ValidationResult result = MetadataValidation.Instance.Validate(this.mockExperiment);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid);
            Assert.IsEmpty(result.ValidationErrors);
        }

        [Test]
        public void ValidationCorrectlyIdentifiesExperimentsThatAreMissingRequiredMetadata()
        {
            // The mock extensions define a valid experiment (including having required metadata).
            this.mockExperiment.Metadata.Clear();

            foreach (string requiredMetadataProperty in MetadataValidation.RequiredMetadata)
            {
                ValidationResult result = MetadataValidation.Instance.Validate(this.mockExperiment);

                Assert.IsNotNull(result);
                Assert.IsFalse(result.IsValid);
                Assert.IsNotEmpty(result.ValidationErrors);

                this.mockExperiment.Metadata.Add(requiredMetadataProperty, "AnyValue");
            }
        }

        [Test]
        public void ValidationRequiresCertainPayloadSpecificMetadataWhenAPayloadTypeIsDefined()
        {
            this.mockExperiment.Metadata.Add("payloadType", "AnyType");

            // The mock extensions define a valid experiment (including having required metadata).
            ValidationResult result = MetadataValidation.Instance.Validate(this.mockExperiment);

            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsNotEmpty(result.ValidationErrors);

            this.mockExperiment.Metadata.Add("payloadVersion", "1.2.3.4");
            this.mockExperiment.Metadata.Add("payloadPFVersion", "10.1002.32.45");

            result = MetadataValidation.Instance.Validate(this.mockExperiment);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid);
            Assert.IsEmpty(result.ValidationErrors);
        }

        [Test]
        public void ValidationRequiresCertainWorkloadSpecificMetadataWhenAWorkloadTypeIsDefined()
        {
            this.mockExperiment.Metadata.Add("workloadType", "AnyType");

            // The mock extensions define a valid experiment (including having required metadata).
            ValidationResult result = MetadataValidation.Instance.Validate(this.mockExperiment);

            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsNotEmpty(result.ValidationErrors);

            this.mockExperiment.Metadata.Add("workloadVersion", "1.2.3.4");

            result = MetadataValidation.Instance.Validate(this.mockExperiment);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid);
            Assert.IsEmpty(result.ValidationErrors);
        }
    }
}
