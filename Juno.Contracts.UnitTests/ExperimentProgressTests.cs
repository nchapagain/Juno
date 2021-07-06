namespace Juno.Contracts
{
    using System;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ExperimentProgressTests
    {
        private readonly string experimentNameValid = "something";
        private readonly string revisionValid = "something";
        private readonly int progressValid = 90;

        private ExperimentProgress experimentProgress;

        [SetUp]
        public void SetupTest()
        {
            this.experimentProgress = new ExperimentProgress(
                this.experimentNameValid,
                this.revisionValid,
                this.progressValid);
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void ExperimentProgressConstructorsValidateRequiredParameters(string invalidParameter)
        {
            // Invalid ExperimentName
            Assert.Throws<ArgumentException>(() => new ExperimentProgress(
                invalidParameter,
                this.revisionValid,
                this.progressValid));

            // Invalid Revision
            Assert.Throws<ArgumentException>(() => new ExperimentProgress(
                this.experimentNameValid,
                invalidParameter,
                this.progressValid));

            // Invalid Progress
            Assert.Throws<ArgumentException>(() => new ExperimentProgress(
                this.experimentNameValid,
                this.revisionValid,
                -50));

            // Invalid Progress
            Assert.Throws<ArgumentException>(() => new ExperimentProgress(
                this.experimentNameValid,
                this.revisionValid,
                150));
        }

        [Test]
        public void ExperimentProgressIsJsonSerializableByDefault()
        {
            SerializationAssert.IsJsonSerializable(this.experimentProgress);
        }

        [Test]
        public void ExperimentProgressIsJsonSerializableUsingExpectedSerializerSettings()
        {
            SerializationAssert.IsJsonSerializable(
                this.experimentProgress,
                ContractSerialization.DefaultJsonSerializationSettings);
        }

        [Test]
        public void ExperimentProgressEqualityTest()
        {
            ExperimentProgress instance = this.experimentProgress;
            ExperimentProgress instanceWithSameReference = this.experimentProgress;

            Assert.IsTrue(instance.Equals(instanceWithSameReference));
            Assert.IsTrue(instance == instanceWithSameReference);
            Assert.IsFalse(instance != instanceWithSameReference);

            ExperimentProgress instanceWithSameValues = new ExperimentProgress(instance);

            Assert.IsTrue(instance.Equals(instanceWithSameValues));
            Assert.IsTrue(instance == instanceWithSameValues);
            Assert.IsFalse(instance != instanceWithSameValues);

            ExperimentProgress instanceWithDifferentValues = new ExperimentProgress(
                instance.ExperimentName,
                instance.Revision,
                instance.Progress + 5);

            Assert.IsFalse(instance.Equals(instanceWithDifferentValues));
            Assert.IsFalse(instance == instanceWithDifferentValues);
            Assert.IsTrue(instance != instanceWithDifferentValues);

            EqualityAssert.CorrectlyImplementsEqualitySemantics<ExperimentProgress>(() => instance, () => instanceWithDifferentValues);
        }

        [Test]
        public void ExperimentProgressHashCodesAreNotCaseSensitive()
        {
            ExperimentProgress instance1 = new ExperimentProgress(
                this.experimentProgress.ExperimentName.ToLowerInvariant(),
                this.experimentProgress.Revision.ToLowerInvariant(),
                this.experimentProgress.Progress);

            ExperimentProgress instance2 = new ExperimentProgress(
                this.experimentProgress.ExperimentName.ToUpperInvariant(),
                this.experimentProgress.Revision.ToUpperInvariant(),
                this.experimentProgress.Progress);

            Assert.AreEqual(instance1.GetHashCode(), instance2.GetHashCode());
        }
    }
}