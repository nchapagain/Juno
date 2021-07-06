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

        // Notes:
        // We do not have a consensus on where the recommendation ID should be created or the format of it. We are
        // leaving this here for reference as we come back to this so that we do not lose track of the work we did
        // to integrate it and can simply refactor that to match the requisite semantics in the future.
        ////[Test]
        ////public void ExperimentGeneratesTheExpectedIdentifierHash()
        ////{
        ////    string experimentName = "Experiment1";
        ////    string revision = "AnyRevision";
        ////    string tenantId = "AnyTenant";

        ////    Guid identifierHash = Experiment.GenerateHash(experimentName, revision, tenantId);
        ////    Assert.AreEqual("b0879415-56d4-b27f-2456-3737722e4a90", identifierHash.ToString());
        ////}

        ////[Test]
        ////public void ExperimentGeneratesTheSameIdentifierHashGivenASetOfIdentifiersEveryTime()
        ////{
        ////    string experimentName = "Experiment1";
        ////    string revision = "AnyRevision";
        ////    string tenantId = "AnyTenant";

        ////    Guid identifierHash1 = Experiment.GenerateHash(experimentName, revision, tenantId);
        ////    Guid identifierHash2 = Experiment.GenerateHash(experimentName, revision, tenantId);
        ////    Assert.AreEqual(identifierHash1.ToString(), identifierHash2.ToString());
        ////}

        ////[Test]
        ////public void ExperimentAddsTheExpectedRecommendationIdToMetadata()
        ////{
        ////    string revision = "AnyRevision";
        ////    string tenantId = "AnyTenant";

        ////    Experiment experiment = this.mockFixture.Create<Experiment>();
        ////    experiment.Metadata[MetadataProperty.Revision] = revision;
        ////    experiment.Metadata[MetadataProperty.TenantId] = tenantId;

        ////    experiment.AddRecommendationId();
        ////    Assert.AreEqual("466e0a07-f4bf-1311-cb89-f8458d68930f", experiment.Metadata[MetadataProperty.RecommendationId].ToString());
        ////}

        ////[Test]
        ////public void ExperimentDoesNotReplaceTheRecommendationIdByDefault()
        ////{
        ////    Experiment experiment = this.mockFixture.Create<Experiment>();
        ////    experiment.AddRecommendationId();
        ////    string originalRecommendationId = experiment.Metadata[MetadataProperty.RecommendationId].ToString();

        ////    experiment.Metadata[MetadataProperty.Revision] = Guid.NewGuid().ToString();
        ////    experiment.AddRecommendationId();
        ////    string actualRecommendationId = experiment.Metadata[MetadataProperty.RecommendationId].ToString();

        ////    Assert.AreEqual(originalRecommendationId, actualRecommendationId);
        ////}

        ////[Test]
        ////public void ExperimentReplacesTheRecommendationIdWhenTheFlagToIndicateThisIntentIsProvided()
        ////{
        ////    Experiment experiment = this.mockFixture.Create<Experiment>();
        ////    experiment.AddRecommendationId();
        ////    string originalRecommendationId = experiment.Metadata[MetadataProperty.RecommendationId].ToString();

        ////    experiment.Metadata[MetadataProperty.Revision] = Guid.NewGuid().ToString();
        ////    experiment.AddRecommendationId(replace: true);
        ////    string actualRecommendationId = experiment.Metadata[MetadataProperty.RecommendationId].ToString();

        ////    Assert.AreNotEqual(originalRecommendationId, actualRecommendationId);
        ////}

        ////[Test]
        ////public void ExperimentThrowsIfARevisionIsNotDefinedWhenAddingARecommendationId()
        ////{
        ////    Experiment experiment = this.mockFixture.Create<Experiment>();
        ////    experiment.Metadata.Remove(MetadataProperty.Revision);

        ////    Assert.Throws<SchemaException>(() => experiment.AddRecommendationId());
        ////}

        ////[Test]
        ////public void ExperimentThrowsIfATenantIdIsNotDefinedWhenAddingARecommendationId()
        ////{
        ////    Experiment experiment = this.mockFixture.Create<Experiment>();
        ////    experiment.Metadata.Remove(MetadataProperty.TenantId);

        ////    Assert.Throws<SchemaException>(() => experiment.AddRecommendationId());
        ////}
    }
}
