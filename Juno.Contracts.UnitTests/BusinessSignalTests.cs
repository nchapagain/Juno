namespace Juno.Contracts
{
    using System;
    using AutoFixture;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class BusinessSignalTests
    {
        private readonly string experimentNameValid = "something";
        private readonly string revisionValid = "something";
        private readonly string friendlyNameValid = "something";
        private readonly string shortNameValid = "something";
        private readonly string descriptionValid = "something";
        private readonly int totalCountValid = 100;
        private readonly int countAValid = 50;
        private readonly int countBValid = 50;
        private readonly int redCountValid = 10;
        private readonly int greenCountValid = 50;
        private readonly int yellowCountValid = 20;
        private readonly int greyCountValid = 20;
        private readonly string overallSignalValid = "Red";
        private readonly DateTime experimentDateUtcValid = new DateTime(2020, 07, 20);

        private BusinessSignal businessSignal;

        private Fixture mockFixture;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();

            this.businessSignal = new BusinessSignal(
                this.experimentNameValid,
                this.revisionValid,
                this.friendlyNameValid,
                this.shortNameValid,
                this.descriptionValid,
                this.totalCountValid,
                this.countAValid,
                this.countBValid,
                this.redCountValid,
                this.greenCountValid,
                this.yellowCountValid,
                this.greyCountValid,
                this.overallSignalValid,
                this.experimentDateUtcValid);
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void BusinessSignalConstructorsValidateRequiredParameters(string invalidParameter)
        {
            // Invalid ExperimentName
            Assert.Throws<ArgumentException>(() => new BusinessSignal(
                invalidParameter,
                this.revisionValid,
                this.friendlyNameValid,
                this.shortNameValid,
                this.descriptionValid,
                this.totalCountValid,
                this.countAValid,
                this.countBValid,
                this.redCountValid,
                this.greenCountValid,
                this.yellowCountValid,
                this.greyCountValid,
                this.overallSignalValid,
                this.experimentDateUtcValid));

            // Invalid Revision
            Assert.Throws<ArgumentException>(() => new BusinessSignal(
                this.experimentNameValid,
                invalidParameter,
                this.friendlyNameValid,
                this.shortNameValid,
                this.descriptionValid,
                this.totalCountValid,
                this.countAValid,
                this.countBValid,
                this.redCountValid,
                this.greenCountValid,
                this.yellowCountValid,
                this.greyCountValid,
                this.overallSignalValid,
                this.experimentDateUtcValid));

            // Invalid FriendlyName
            Assert.Throws<ArgumentException>(() => new BusinessSignal(
                this.experimentNameValid,
                this.revisionValid,
                invalidParameter,
                this.shortNameValid,
                this.descriptionValid,
                this.totalCountValid,
                this.countAValid,
                this.countBValid,
                this.redCountValid,
                this.greenCountValid,
                this.yellowCountValid,
                this.greyCountValid,
                this.overallSignalValid,
                this.experimentDateUtcValid));

            // Invalid ShortName
            Assert.Throws<ArgumentException>(() => new BusinessSignal(
                this.experimentNameValid,
                this.revisionValid,
                this.friendlyNameValid,
                invalidParameter,
                this.descriptionValid,
                this.totalCountValid,
                this.countAValid,
                this.countBValid,
                this.redCountValid,
                this.greenCountValid,
                this.yellowCountValid,
                this.greyCountValid,
                this.overallSignalValid,
                this.experimentDateUtcValid));

            // Invalid Description
            Assert.Throws<ArgumentException>(() => new BusinessSignal(
                this.experimentNameValid,
                this.revisionValid,
                this.friendlyNameValid,
                this.shortNameValid,
                invalidParameter,
                this.totalCountValid,
                this.countAValid,
                this.countBValid,
                this.redCountValid,
                this.greenCountValid,
                this.yellowCountValid,
                this.greyCountValid,
                this.overallSignalValid,
                this.experimentDateUtcValid));

            // Invalid TotalCount
            Assert.Throws<ArgumentException>(() => new BusinessSignal(
                this.experimentNameValid,
                this.revisionValid,
                this.friendlyNameValid,
                this.shortNameValid,
                this.descriptionValid,
                -10,
                this.countAValid,
                this.countBValid,
                this.redCountValid,
                this.greenCountValid,
                this.yellowCountValid,
                this.greyCountValid,
                this.overallSignalValid,
                this.experimentDateUtcValid));

            // Invalid CountA
            Assert.Throws<ArgumentException>(() => new BusinessSignal(
                this.experimentNameValid,
                this.revisionValid,
                this.friendlyNameValid,
                this.shortNameValid,
                this.descriptionValid,
                this.totalCountValid,
                -10,
                this.countBValid,
                this.redCountValid,
                this.greenCountValid,
                this.yellowCountValid,
                this.greyCountValid,
                this.overallSignalValid,
                this.experimentDateUtcValid));

            // Invalid CountB
            Assert.Throws<ArgumentException>(() => new BusinessSignal(
                this.experimentNameValid,
                this.revisionValid,
                this.friendlyNameValid,
                this.shortNameValid,
                this.descriptionValid,
                this.totalCountValid,
                this.countAValid,
                -10,
                this.redCountValid,
                this.greenCountValid,
                this.yellowCountValid,
                this.greyCountValid,
                this.overallSignalValid,
                this.experimentDateUtcValid));

            // Invalid RedCount
            Assert.Throws<ArgumentException>(() => new BusinessSignal(
                this.experimentNameValid,
                this.revisionValid,
                this.friendlyNameValid,
                this.shortNameValid,
                this.descriptionValid,
                this.totalCountValid,
                this.countAValid,
                this.countBValid,
                -10,
                this.greenCountValid,
                this.yellowCountValid,
                this.greyCountValid,
                this.overallSignalValid,
                this.experimentDateUtcValid));

            // Invalid GreenCount
            Assert.Throws<ArgumentException>(() => new BusinessSignal(
                this.experimentNameValid,
                this.revisionValid,
                this.friendlyNameValid,
                this.shortNameValid,
                this.descriptionValid,
                this.totalCountValid,
                this.countAValid,
                this.countBValid,
                this.redCountValid,
                -10,
                this.yellowCountValid,
                this.greyCountValid,
                this.overallSignalValid,
                this.experimentDateUtcValid));

            // Invalid YellowCount
            Assert.Throws<ArgumentException>(() => new BusinessSignal(
                this.experimentNameValid,
                this.revisionValid,
                this.friendlyNameValid,
                this.shortNameValid,
                this.descriptionValid,
                this.totalCountValid,
                this.countAValid,
                this.countBValid,
                this.redCountValid,
                this.greenCountValid,
                -10,
                this.greyCountValid,
                this.overallSignalValid,
                this.experimentDateUtcValid));

            // Invalid GreyCount
            Assert.Throws<ArgumentException>(() => new BusinessSignal(
                this.experimentNameValid,
                this.revisionValid,
                this.friendlyNameValid,
                this.shortNameValid,
                this.descriptionValid,
                this.totalCountValid,
                this.countAValid,
                this.countBValid,
                this.redCountValid,
                this.greenCountValid,
                this.yellowCountValid,
                -10,
                this.overallSignalValid,
                this.experimentDateUtcValid));

            // Invalid OverallSignal
            Assert.Throws<ArgumentException>(() => new BusinessSignal(
                this.experimentNameValid,
                this.revisionValid,
                this.friendlyNameValid,
                this.shortNameValid,
                this.descriptionValid,
                this.totalCountValid,
                this.countAValid,
                this.countBValid,
                this.redCountValid,
                this.greenCountValid,
                this.yellowCountValid,
                this.greyCountValid,
                invalidParameter,
                this.experimentDateUtcValid));

            // Invalid ExperimentDateUtc
            Assert.Throws<ArgumentException>(() => new BusinessSignal(
                this.experimentNameValid,
                this.revisionValid,
                this.friendlyNameValid,
                this.shortNameValid,
                this.descriptionValid,
                this.totalCountValid,
                this.countAValid,
                this.countBValid,
                this.redCountValid,
                this.greenCountValid,
                this.yellowCountValid,
                this.greyCountValid,
                this.overallSignalValid,
                default(DateTime)));

            // Invalid ExperimentDateUtc
            Assert.Throws<ArgumentException>(() => new BusinessSignal(
                this.experimentNameValid,
                this.revisionValid,
                this.friendlyNameValid,
                this.shortNameValid,
                this.descriptionValid,
                this.totalCountValid,
                this.countAValid,
                this.countBValid,
                this.redCountValid,
                this.greenCountValid,
                this.yellowCountValid,
                this.greyCountValid,
                this.overallSignalValid,
                new DateTime(2100, 07, 20)));
        }

        [Test]
        public void BusinessSignalIsJsonSerializableByDefault()
        {
            SerializationAssert.IsJsonSerializable(this.businessSignal);
        }

        [Test]
        public void BusinessSignalIsJsonSerializableUsingExpectedSerializerSettings()
        {
            SerializationAssert.IsJsonSerializable(
                this.businessSignal,
                ContractSerialization.DefaultJsonSerializationSettings);
        }

        [Test]
        public void BusinessSignalEqualityTest()
        {
            BusinessSignal instance = this.businessSignal;
            BusinessSignal instanceWithSameReference = this.businessSignal;

            Assert.IsTrue(instance.Equals(instanceWithSameReference));
            Assert.IsTrue(instance == instanceWithSameReference);
            Assert.IsFalse(instance != instanceWithSameReference);

            BusinessSignal instanceWithSameValues = new BusinessSignal(instance);

            Assert.IsTrue(instance.Equals(instanceWithSameValues));
            Assert.IsTrue(instance == instanceWithSameValues);
            Assert.IsFalse(instance != instanceWithSameValues);

            BusinessSignal instanceWithDifferentValues = new BusinessSignal(
                instance.ExperimentName,
                instance.Revision,
                instance.FriendlyName,
                instance.ShortName,
                instance.Description,
                instance.TotalCount + 1000,
                instance.CountA,
                instance.CountB,
                instance.RedCount,
                instance.GreenCount,
                instance.YellowCount,
                instance.GreyCount,
                instance.OverallSignal,
                instance.ExperimentDateUtc);

            Assert.IsFalse(instance.Equals(instanceWithDifferentValues));
            Assert.IsFalse(instance == instanceWithDifferentValues);
            Assert.IsTrue(instance != instanceWithDifferentValues);

            EqualityAssert.CorrectlyImplementsEqualitySemantics<BusinessSignal>(() => instance, () => instanceWithDifferentValues);
        }

        [Test]
        public void BusinessSignalHashCodesAreNotCaseSensitive()
        {
            BusinessSignal instance1 = new BusinessSignal(
                this.businessSignal.ExperimentName.ToLowerInvariant(),
                this.businessSignal.Revision.ToLowerInvariant(),
                this.businessSignal.FriendlyName.ToLowerInvariant(),
                this.businessSignal.ShortName.ToLowerInvariant(),
                this.businessSignal.Description.ToLowerInvariant(),
                this.businessSignal.TotalCount,
                this.businessSignal.CountA,
                this.businessSignal.CountB,
                this.businessSignal.RedCount,
                this.businessSignal.GreenCount,
                this.businessSignal.YellowCount,
                this.businessSignal.GreyCount,
                this.businessSignal.OverallSignal.ToLowerInvariant(),
                this.businessSignal.ExperimentDateUtc);

            BusinessSignal instance2 = new BusinessSignal(
                this.businessSignal.ExperimentName.ToUpperInvariant(),
                this.businessSignal.Revision.ToUpperInvariant(),
                this.businessSignal.FriendlyName.ToUpperInvariant(),
                this.businessSignal.ShortName.ToUpperInvariant(),
                this.businessSignal.Description.ToUpperInvariant(),
                this.businessSignal.TotalCount,
                this.businessSignal.CountA,
                this.businessSignal.CountB,
                this.businessSignal.RedCount,
                this.businessSignal.GreenCount,
                this.businessSignal.YellowCount,
                this.businessSignal.GreyCount,
                this.businessSignal.OverallSignal.ToUpperInvariant(),
                this.businessSignal.ExperimentDateUtc);

            Assert.AreEqual(instance1.GetHashCode(), instance2.GetHashCode());
        }
    }
}