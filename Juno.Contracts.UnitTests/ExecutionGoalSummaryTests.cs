namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using AutoFixture;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ExecutionGoalSummaryTests
    {
        private Fixture mockFixture;

        [SetUp]
        public void SetupTests()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetUpGoalBasedScheduleMocks();
        }

        [Test]
        public void ExecutionGoalMetadataIsJsonSerializableByDefault()
        {
            SerializationAssert.IsJsonSerializable(this.mockFixture.Create<ExecutionGoalSummary>());
        }

        [Test]
        public void ExecutionGoalMetadataIsJSonSerializableUsingExpectedSerializerSettings()
        {
            SerializationAssert.IsJsonSerializable(
                this.mockFixture.Create<ExecutionGoalSummary>(),
                ContractSerialization.DefaultJsonSerializationSettings);
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void ExecutionGoalMetadataConstructorValidatesParameters(string invalidPrameter)
        {
            ExecutionGoalSummary validComponent = this.mockFixture.Create<ExecutionGoalSummary>();

            Assert.Throws<ArgumentException>(() => new ExecutionGoalSummary(
                invalidPrameter,
                validComponent.Description,
                validComponent.TeamName,
                validComponent.ParameterNames));

            Assert.Throws<ArgumentException>(() => new ExecutionGoalSummary(
                validComponent.Id,
                invalidPrameter,
                validComponent.TeamName,
                validComponent.ParameterNames));

            Assert.Throws<ArgumentException>(() => new ExecutionGoalSummary(   
                validComponent.Id,   
                validComponent.Description,
                invalidPrameter,
                validComponent.ParameterNames));
        }

        [Test]
        public void ExecutionGoalMetadataCorrectlyImplementsHashCodeSemantics()
        {
            ExecutionGoalSummary instance1 = this.mockFixture.Create<ExecutionGoalSummary>();
            ExecutionGoalSummary instance2 = this.mockFixture.Create<ExecutionGoalSummary>();

            EqualityAssert.CorrectlyImplementsHashcodeSemantics(() => instance1, () => instance2);
        }

        [Test]
        public void ExecutionGoalMetadataHashCodesAreNotCaseSensitive()
        {
            ExecutionGoalSummary template = this.mockFixture.Create<ExecutionGoalSummary>();
            ExecutionGoalSummary instance1 = new ExecutionGoalSummary(
                template.Id.ToLowerInvariant(),
                template.Description.ToLowerInvariant(),
                template.TeamName.ToLowerInvariant(),
                template.ParameterNames);

            ExecutionGoalSummary instance2 = new ExecutionGoalSummary(
                template.Id.ToUpperInvariant(),
                template.Description.ToUpperInvariant(),
                template.TeamName.ToUpperInvariant(),
                template.ParameterNames);

            Assert.AreEqual(instance1.GetHashCode(), instance2.GetHashCode());
        }
    }
}
