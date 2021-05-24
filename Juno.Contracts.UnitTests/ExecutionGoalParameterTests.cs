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
        public void ExecutionGoalParametersConstructorValidatesPrameters(string invalidPrameter)
        {
            ExecutionGoalSummary validComponent = this.mockFixture.Create<ExecutionGoalSummary>();

            Assert.Throws<ArgumentException>(() => new ExecutionGoalParameter(
                invalidPrameter,
                validComponent.ParameterNames.ExperimentName,
                validComponent.ParameterNames.Owner,
                validComponent.ParameterNames.Enabled,
                validComponent.ParameterNames.TargetGoals,
                validComponent.ParameterNames.SharedParameters));

            Assert.Throws<ArgumentException>(() => new ExecutionGoalParameter(
                validComponent.ParameterNames.ExecutionGoalId,
                invalidPrameter,
                validComponent.ParameterNames.Owner,
                validComponent.ParameterNames.Enabled,
                validComponent.ParameterNames.TargetGoals,
                validComponent.ParameterNames.SharedParameters));

            Assert.Throws<ArgumentException>(() => new ExecutionGoalParameter(
                validComponent.ParameterNames.ExecutionGoalId,
                validComponent.ParameterNames.ExperimentName,
                invalidPrameter,
                validComponent.ParameterNames.Enabled,
                validComponent.ParameterNames.TargetGoals,
                validComponent.ParameterNames.SharedParameters));

            Assert.Throws<ArgumentNullException>(() => new ExecutionGoalParameter(
               validComponent.ParameterNames.ExecutionGoalId,
               validComponent.ParameterNames.ExperimentName,
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
            ExecutionGoalParameter instance = new ExecutionGoalParameter(template.ExecutionGoalId, template.ExperimentName, template.Owner, template.Enabled, template.TargetGoals, template.SharedParameters);

            Assert.DoesNotThrow(() => instance.GetHashCode());
        }

        [Test]
        public void ExecutionGoalParametersHashCodesAreNotCaseSensitive()
        {
            ExecutionGoalParameter template = this.mockFixture.Create<ExecutionGoalParameter>();
            ExecutionGoalParameter instance1 = new ExecutionGoalParameter(
                template.ExecutionGoalId.ToLowerInvariant(),
                template.ExperimentName.ToLowerInvariant(),
                template.Owner.ToLowerInvariant(),
                template.Enabled,
                template.TargetGoals,
                template.SharedParameters);

            ExecutionGoalParameter instance2 = new ExecutionGoalParameter(
                template.ExecutionGoalId.ToUpperInvariant(),
                template.ExperimentName.ToUpperInvariant(),
                template.Owner.ToUpperInvariant(),
                template.Enabled,
                template.TargetGoals,
                template.SharedParameters);

            Assert.AreEqual(instance1.GetHashCode(), instance2.GetHashCode());
        }
    }
}
