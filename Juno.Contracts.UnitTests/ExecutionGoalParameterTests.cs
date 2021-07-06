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
        public void ExecutionGoalParametersConstructorValidatesRequiredParameters()
        {
            Assert.Throws<ArgumentNullException>(() => new ExecutionGoalParameter(null));
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
            ExecutionGoalParameter instance = new ExecutionGoalParameter(template.TargetGoals);

            Assert.DoesNotThrow(() => instance.GetHashCode());
        }
    }
}