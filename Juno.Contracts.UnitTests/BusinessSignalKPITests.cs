namespace Juno.Contracts
{
    using System;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class BusinessSignalKPITests
    {
        private readonly string friendlyNameValid = "something";
        private readonly string shortNameValid = "something";
        private readonly string descriptionValid = "something";
        private readonly int countAValid = 50;
        private readonly int countBValid = 50;
        private readonly string overallSignalValid = "Red";

        private BusinessSignalKPI businessSignalKPI;

        [SetUp]
        public void SetupTest()
        {
            this.businessSignalKPI = new BusinessSignalKPI(
                this.friendlyNameValid,
                this.shortNameValid,
                this.descriptionValid,
                this.countAValid,
                this.countBValid,
                this.overallSignalValid);
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void BusinessSignalKPIConstructorsValidateRequiredParameters(string invalidParameter)
        {
            // Invalid FriendlyName
            Assert.Throws<ArgumentException>(() => new BusinessSignalKPI(
                invalidParameter,
                this.shortNameValid,
                this.descriptionValid,
                this.countAValid,
                this.countBValid,
                this.overallSignalValid));

            // Invalid ShortName
            Assert.Throws<ArgumentException>(() => new BusinessSignalKPI(
                this.friendlyNameValid,
                invalidParameter,
                this.descriptionValid,
                this.countAValid,
                this.countBValid,
                this.overallSignalValid));

            // Invalid Description
            Assert.Throws<ArgumentException>(() => new BusinessSignalKPI(
                this.friendlyNameValid,
                this.shortNameValid,
                invalidParameter,
                this.countAValid,
                this.countBValid,
                this.overallSignalValid));

            // Invalid CountA
            Assert.Throws<ArgumentException>(() => new BusinessSignalKPI(
                this.friendlyNameValid,
                this.shortNameValid,
                this.descriptionValid,
                -10,
                this.countBValid,
                this.overallSignalValid));

            // Invalid CountB
            Assert.Throws<ArgumentException>(() => new BusinessSignalKPI(
                this.friendlyNameValid,
                this.shortNameValid,
                this.descriptionValid,
                this.countAValid,
                -10,
                this.overallSignalValid));

            // Invalid OverallSignal
            Assert.Throws<ArgumentException>(() => new BusinessSignalKPI(
                this.friendlyNameValid,
                this.shortNameValid,
                this.descriptionValid,
                this.countAValid,
                this.countBValid,
                invalidParameter));
        }

        [Test]
        public void BusinessSignalKPIIsJsonSerializableByDefault()
        {
            SerializationAssert.IsJsonSerializable(this.businessSignalKPI);
        }

        [Test]
        public void BusinessSignalKPIIsJsonSerializableUsingExpectedSerializerSettings()
        {
            SerializationAssert.IsJsonSerializable(
                this.businessSignalKPI,
                ContractSerialization.DefaultJsonSerializationSettings);
        }

        [Test]
        public void BusinessSignalKPIEqualityTest()
        {
            BusinessSignalKPI instance = this.businessSignalKPI;
            BusinessSignalKPI instanceWithSameReference = this.businessSignalKPI;

            Assert.IsTrue(instance.Equals(instanceWithSameReference));
            Assert.IsTrue(instance == instanceWithSameReference);
            Assert.IsFalse(instance != instanceWithSameReference);

            BusinessSignalKPI instanceWithSameValues = new BusinessSignalKPI(instance);

            Assert.IsTrue(instance.Equals(instanceWithSameValues));
            Assert.IsTrue(instance == instanceWithSameValues);
            Assert.IsFalse(instance != instanceWithSameValues);

            BusinessSignalKPI instanceWithDifferentValues = new BusinessSignalKPI(
                instance.FriendlyName,
                instance.ShortName,
                instance.Description,
                instance.CountA + 1000,
                instance.CountB + 1000,
                instance.OverallSignal);

            Assert.IsFalse(instance.Equals(instanceWithDifferentValues));
            Assert.IsFalse(instance == instanceWithDifferentValues);
            Assert.IsTrue(instance != instanceWithDifferentValues);

            EqualityAssert.CorrectlyImplementsEqualitySemantics<BusinessSignalKPI>(() => instance, () => instanceWithDifferentValues);
        }

        [Test]
        public void BusinessSignalKPIHashCodesAreNotCaseSensitive()
        {
            BusinessSignalKPI instance1 = new BusinessSignalKPI(
                this.businessSignalKPI.FriendlyName.ToLowerInvariant(),
                this.businessSignalKPI.ShortName.ToLowerInvariant(),
                this.businessSignalKPI.Description.ToLowerInvariant(),
                this.businessSignalKPI.CountA,
                this.businessSignalKPI.CountB,
                this.businessSignalKPI.OverallSignal.ToLowerInvariant());

            BusinessSignalKPI instance2 = new BusinessSignalKPI(
                this.businessSignalKPI.FriendlyName.ToUpperInvariant(),
                this.businessSignalKPI.ShortName.ToUpperInvariant(),
                this.businessSignalKPI.Description.ToUpperInvariant(),
                this.businessSignalKPI.CountA,
                this.businessSignalKPI.CountB,
                this.businessSignalKPI.OverallSignal.ToUpperInvariant());

            Assert.AreEqual(instance1.GetHashCode(), instance2.GetHashCode());
        }
    }
}