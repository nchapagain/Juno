namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ExperimentSummaryTests
    {
        private readonly string experimentNameValid = "something";
        private readonly string revisionValid = "something";
        private readonly string friendlyNameValid = "something";
        private readonly string shortNameValid = "something";
        private readonly string descriptionValid = "something";
        private readonly int countAValid = 50;
        private readonly int countBValid = 50;
        private readonly string overallSignalValid = "Red";
        private readonly DateTime experimentDateUtcValid = new DateTime(2020, 07, 20);
        private readonly int progressValid = 90;

        private BusinessSignalKPI businessSignalKPI1;
        private BusinessSignalKPI businessSignalKPI2;
        private BusinessSignalKPI businessSignalKPI3;

        private ExperimentSummary experimentSummary;

        [SetUp]
        public void SetupTest()
        {
            this.businessSignalKPI1 = new BusinessSignalKPI(
                this.friendlyNameValid,
                this.shortNameValid,
                this.descriptionValid,
                this.countAValid,
                this.countBValid,
                this.overallSignalValid);

            this.businessSignalKPI2 = new BusinessSignalKPI(
                this.friendlyNameValid,
                this.shortNameValid,
                this.descriptionValid,
                this.countAValid + 100,
                this.countBValid + 100,
                this.overallSignalValid);

            this.businessSignalKPI3 = new BusinessSignalKPI(
                this.friendlyNameValid,
                this.shortNameValid,
                this.descriptionValid,
                this.countAValid + 1000,
                this.countBValid + 1000,
                this.overallSignalValid);

            this.experimentSummary = new ExperimentSummary(
                this.experimentNameValid,
                this.revisionValid,
                this.progressValid,
                this.experimentDateUtcValid,
                new List<BusinessSignalKPI>() { this.businessSignalKPI1, this.businessSignalKPI2 });
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void ExperimentSummaryConstructorsValidateRequiredParameters(string invalidParameter)
        {
            // Invalid ExperimentName
            Assert.Throws<ArgumentException>(() => new ExperimentSummary(
                invalidParameter,
                this.revisionValid,
                this.progressValid,
                this.experimentDateUtcValid,
                new List<BusinessSignalKPI>() { this.businessSignalKPI1, this.businessSignalKPI2 }));

            // Invalid Revision
            Assert.Throws<ArgumentException>(() => new ExperimentSummary(
                this.experimentNameValid,
                invalidParameter,
                this.progressValid,
                this.experimentDateUtcValid,
                new List<BusinessSignalKPI>() { this.businessSignalKPI1, this.businessSignalKPI2 }));

            // Invalid Progress
            Assert.Throws<ArgumentException>(() => new ExperimentSummary(
                this.experimentNameValid,
                this.revisionValid,
                -50,
                this.experimentDateUtcValid,
                new List<BusinessSignalKPI>() { this.businessSignalKPI1, this.businessSignalKPI2 }));

            // Invalid Progress
            Assert.Throws<ArgumentException>(() => new ExperimentSummary(
                this.experimentNameValid,
                this.revisionValid,
                150,
                this.experimentDateUtcValid,
                new List<BusinessSignalKPI>() { this.businessSignalKPI1, this.businessSignalKPI2 }));
        }

        [Test]
        public void ExperimentSummaryIsJsonSerializableByDefault()
        {
            SerializationAssert.IsJsonSerializable(this.experimentSummary);
        }

        [Test]
        public void ExperimentSummaryIsJsonSerializableUsingExpectedSerializerSettings()
        {
            SerializationAssert.IsJsonSerializable(
                this.experimentSummary,
                ContractSerialization.DefaultJsonSerializationSettings);
        }

        [Test]
        public void ExperimentSummaryEqualityTest()
        {
            ExperimentSummary instance = this.experimentSummary;
            ExperimentSummary instanceWithSameReference = this.experimentSummary;

            Assert.IsTrue(instance.Equals(instanceWithSameReference));
            Assert.IsTrue(instance == instanceWithSameReference);
            Assert.IsFalse(instance != instanceWithSameReference);

            ExperimentSummary instanceWithSameValues = new ExperimentSummary(instance);

            Assert.IsTrue(instance.Equals(instanceWithSameValues));
            Assert.IsTrue(instance == instanceWithSameValues);
            Assert.IsFalse(instance != instanceWithSameValues);

            ExperimentSummary instanceWithDifferentValues = new ExperimentSummary(
                instance.ExperimentName,
                instance.Revision,
                instance.Progress + 5,
                DateTime.Parse(instance.ExperimentDateUtc),
                instance.BusinessSignalKPIs);

            Assert.IsFalse(instance.Equals(instanceWithDifferentValues));
            Assert.IsFalse(instance == instanceWithDifferentValues);
            Assert.IsTrue(instance != instanceWithDifferentValues);

            EqualityAssert.CorrectlyImplementsEqualitySemantics<ExperimentSummary>(() => instance, () => instanceWithDifferentValues);
        }

        [Test]
        public void ExperimentSummaryEqualityTestWithDeepObjects()
        {
            ExperimentSummary instance = this.experimentSummary;

            ExperimentSummary instanceWithDifferentDeepObject1 = new ExperimentSummary(
                instance.ExperimentName,
                instance.Revision,
                instance.Progress,
                DateTime.Parse(instance.ExperimentDateUtc),
                new List<BusinessSignalKPI>() { this.businessSignalKPI2 });

            Assert.IsFalse(instance.Equals(instanceWithDifferentDeepObject1));
            Assert.IsFalse(instance == instanceWithDifferentDeepObject1);
            Assert.IsTrue(instance != instanceWithDifferentDeepObject1);

            EqualityAssert.CorrectlyImplementsEqualitySemantics<ExperimentSummary>(() => instance, () => instanceWithDifferentDeepObject1);

            ExperimentSummary instanceWithDifferentDeepObject2 = new ExperimentSummary(
                instance.ExperimentName,
                instance.Revision,
                instance.Progress,
                DateTime.Parse(instance.ExperimentDateUtc),
                new List<BusinessSignalKPI>() { this.businessSignalKPI1, this.businessSignalKPI3 });

            Assert.IsFalse(instance.Equals(instanceWithDifferentDeepObject2));
            Assert.IsFalse(instance == instanceWithDifferentDeepObject2);
            Assert.IsTrue(instance != instanceWithDifferentDeepObject2);

            EqualityAssert.CorrectlyImplementsEqualitySemantics<ExperimentSummary>(() => instance, () => instanceWithDifferentDeepObject2);
        }

        [Test]
        public void ExperimentSummaryHashCodesAreNotCaseSensitive()
        {
            ExperimentSummary instance1 = new ExperimentSummary(
                this.experimentSummary.ExperimentName.ToLowerInvariant(),
                this.experimentSummary.Revision.ToLowerInvariant(),
                this.experimentSummary.Progress,
                DateTime.Parse(this.experimentSummary.ExperimentDateUtc),
                this.experimentSummary.BusinessSignalKPIs);

            ExperimentSummary instance2 = new ExperimentSummary(
                this.experimentSummary.ExperimentName.ToUpperInvariant(),
                this.experimentSummary.Revision.ToUpperInvariant(),
                this.experimentSummary.Progress,
                DateTime.Parse(this.experimentSummary.ExperimentDateUtc),
                this.experimentSummary.BusinessSignalKPIs);

            Assert.AreEqual(instance1.GetHashCode(), instance2.GetHashCode());
        }
    }
}