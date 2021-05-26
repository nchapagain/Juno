namespace Juno.Contracts
{
    using System;
    using AutoFixture;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ExecutionGoalParameterTests
    {
        private Fixture mockFixture;

        [SetUp]
        public void SetupTests()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetUpGoalBasedScheduleMocks();
        }

        [Test]
        public void ExecutionGoalParametersIsJsonSerializableByDefault()
        {
            SerializationAssert.IsJsonSerializable(this.mockFixture.Create<ExecutionGoalParameter>());
        }

        [Test]
        public void ExecutionGoalParametersIsJSonSerializableUsingExpectedSerializerSettings()
        {
            SerializationAssert.IsJsonSerializable(
                this.mockFixture.Create<ExecutionGoalParameter>(),
                ContractSerialization.DefaultJsonSerializationSettings);
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void ExecutionGoalParametersConstructorValidatesRequiredParameters(string invalidParameter)
        {
            ExecutionGoalSummary validComponent = this.mockFixture.Create<ExecutionGoalSummary>();

            Assert.Throws<ArgumentException>(() => new ExecutionGoalParameter(
                invalidParameter,
                validComponent.ParameterNames.ExperimentName,
                validComponent.ParameterNames.TeamName,
                validComponent.ParameterNames.Owner,
                validComponent.ParameterNames.Enabled,
                validComponent.ParameterNames.TargetGoals,
                validComponent.ParameterNames.SharedParameters));

            Assert.Throws<ArgumentException>(() => new ExecutionGoalParameter(
                validComponent.ParameterNames.ExecutionGoalId,
                invalidParameter,
                validComponent.ParameterNames.TeamName,
                validComponent.ParameterNames.Owner,
                validComponent.ParameterNames.Enabled,
                validComponent.ParameterNames.TargetGoals,
                validComponent.ParameterNames.SharedParameters));

            Assert.Throws<ArgumentException>(() => new ExecutionGoalParameter(
                validComponent.ParameterNames.ExecutionGoalId,
                validComponent.ParameterNames.ExperimentName,
                invalidParameter,
                validComponent.ParameterNames.Owner,
                validComponent.ParameterNames.Enabled,
                validComponent.ParameterNames.TargetGoals,
                validComponent.ParameterNames.SharedParameters));

            Assert.Throws<ArgumentException>(() => new ExecutionGoalParameter(
                validComponent.ParameterNames.ExecutionGoalId,
                validComponent.ParameterNames.ExperimentName,
                validComponent.ParameterNames.TeamName,
                invalidParameter,
                validComponent.ParameterNames.Enabled,
                validComponent.ParameterNames.TargetGoals,
                validComponent.ParameterNames.SharedParameters));

            Assert.Throws<ArgumentNullException>(() => new ExecutionGoalParameter(
                validComponent.ParameterNames.ExecutionGoalId,
                validComponent.ParameterNames.ExperimentName,
                validComponent.ParameterNames.TeamName,
                validComponent.ParameterNames.Owner,
                validComponent.ParameterNames.Enabled,
                null,
                validComponent.ParameterNames.SharedParameters));
        }

        [Test]
        public void ExecutionGoalParametersCorrectlyImplementsHashCodeSemantics()
        {
            ExecutionGoalParameter instance1 = this.mockFixture.Create<ExecutionGoalParameter>();
            ExecutionGoalParameter instance2 = this.mockFixture.Create<ExecutionGoalParameter>();

            EqualityAssert.CorrectlyImplementsHashcodeSemantics(() => instance1, () => instance2);
        }

        [Test]
        public void ExecutionGoalParametersHashCodesHandlePropertiesWithNullValues()
        {
            // Metadata is null here.
            ExecutionGoalParameter template = this.mockFixture.Create<ExecutionGoalParameter>();
            ExecutionGoalParameter instance =
                new ExecutionGoalParameter(
                    template.ExecutionGoalId,
                    template.ExperimentName,
                    template.TeamName,
                    template.Owner,
                    template.Enabled,
                    template.TargetGoals,
                    template.SharedParameters);

            Assert.DoesNotThrow(() => instance.GetHashCode());
        }

        [Test]
        public void ExecutionGoalParametersHashCodesAreNotCaseSensitive()
        {
            ExecutionGoalParameter template = this.mockFixture.Create<ExecutionGoalParameter>();
            ExecutionGoalParameter instance1 = new ExecutionGoalParameter(
                template.ExecutionGoalId.ToLowerInvariant(),
                template.ExperimentName.ToLowerInvariant(),
                template.TeamName.ToLowerInvariant(),
                template.Owner.ToLowerInvariant(),
                template.Enabled,
                template.TargetGoals,
                template.SharedParameters);

            ExecutionGoalParameter instance2 = new ExecutionGoalParameter(
                template.ExecutionGoalId.ToUpperInvariant(),
                template.ExperimentName.ToUpperInvariant(),
                template.TeamName.ToUpperInvariant(),
                template.Owner.ToUpperInvariant(),
                template.Enabled,
                template.TargetGoals,
                template.SharedParameters);

            Assert.AreEqual(instance1.GetHashCode(), instance2.GetHashCode());
        }

        [Test]
        public void ExecutionGoalParameterEnablesMonitoringByDefault()
        {
            ExecutionGoalParameter validComponent = this.mockFixture.Create<ExecutionGoalParameter>();

            ExecutionGoalParameter result = new ExecutionGoalParameter(
                validComponent.ExecutionGoalId,
                validComponent.ExperimentName,
                validComponent.TeamName,
                validComponent.Owner,
                validComponent.Enabled,
                validComponent.TargetGoals,
                validComponent.SharedParameters);

            Assert.IsTrue(result.MonitoringEnabled == true);
        }

        [Test]
        [TestCase(null)]
        [TestCase(true)]
        [TestCase(false)]
        public void ExecutionGoalParameterEnablesMonitoringAsExpected(bool? testOutput)
        {
            bool expectedOutcome = (bool)(testOutput == null ? true : testOutput);
            ExecutionGoalParameter validComponent = this.mockFixture.Create<ExecutionGoalParameter>();

            ExecutionGoalParameter result = new ExecutionGoalParameter(
                validComponent.ExecutionGoalId,
                validComponent.ExperimentName,
                validComponent.TeamName,
                validComponent.Owner,
                validComponent.Enabled,
                validComponent.TargetGoals,
                validComponent.SharedParameters,
                testOutput);

            Assert.AreEqual(expectedOutcome, result.MonitoringEnabled);
        }
    }
}