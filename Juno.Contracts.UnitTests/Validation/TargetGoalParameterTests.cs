namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using AutoFixture;
    using Kusto.Cloud.Platform.Utils;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class TargetGoalParameterTests
    {
        private Fixture mockFixture;

        [SetUp]
        public void SetupTests()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetUpGoalBasedScheduleMocks();
        }

        [Test]
        public void TargetGoalParameterIsJsonSerializableByDefault()
        {
            IEnumerable<TargetGoalParameter> targetGoalParameters = this.mockFixture.Create<ExecutionGoalParameter>().TargetGoals;
            SerializationAssert.IsJsonSerializable(targetGoalParameters);
            foreach (TargetGoalParameter targetGoalParameter in targetGoalParameters)
            { 
                SerializationAssert.IsJsonSerializable(targetGoalParameter);
            }
        }

        [Test]
        public void TargetGoalParameterIsJSonSerializableUsingExpectedSerializerSettings()
        {
            IEnumerable<TargetGoalParameter> targetGoalParameters = this.mockFixture.Create<ExecutionGoalParameter>().TargetGoals;
            SerializationAssert.IsJsonSerializable(targetGoalParameters, ContractSerialization.DefaultJsonSerializationSettings);
            foreach (TargetGoalParameter targetGoalParameter in targetGoalParameters)
            {
                SerializationAssert.IsJsonSerializable(targetGoalParameter, ContractSerialization.DefaultJsonSerializationSettings);
            }
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void TargetGoalParameterConstructorValidatesPrameters(string invalidPrameter)
        {
            IEnumerable<TargetGoalParameter> validComponents = this.mockFixture.Create<ExecutionGoalParameter>().TargetGoals;
            foreach (TargetGoalParameter validComponent in validComponents)
            {
                Assert.Throws<ArgumentException>(() => new TargetGoalParameter(invalidPrameter, false, validComponent.Parameters));
            }
        }

        [Test]
        public void TargetGoalParameterCorrectlyImplementsHashCodeSemantics()
        {
            IEnumerable<TargetGoalParameter> instance1 = this.mockFixture.Create<ExecutionGoalParameter>().TargetGoals;
            IEnumerable<TargetGoalParameter> instance2 = this.mockFixture.Create<ExecutionGoalParameter>().TargetGoals;

            EqualityAssert.CorrectlyImplementsHashcodeSemantics(() => instance1, () => instance2);
        }

        [Test]
        public void TargetGoalParameterHashCodesHandlePropertiesWithNullValues()
        {
            // Parameters is null here.
            TargetGoalParameter template = this.mockFixture.Create<ExecutionGoalParameter>().TargetGoals.FirstOrDefault();

            TargetGoalParameter targetGoalParameter = new TargetGoalParameter(template.Name, true);
            Assert.DoesNotThrow(() => targetGoalParameter.GetHashCode());
        }

        [Test]
        public void ExecutionGoalParameterHashCodesAreNotCaseSensitive()
        {
            IEnumerable<TargetGoalParameter> templates = this.mockFixture.Create<ExecutionGoalParameter>().TargetGoals;

            foreach (var template in templates)
            {
                TargetGoalParameter instance1 = new TargetGoalParameter(
                    template.Name.ToLowerInvariant(),
                    true,
                    template.Parameters);

                TargetGoalParameter instance2 = new TargetGoalParameter(
                    template.Name.ToUpperInvariant(),
                    true,
                    template.Parameters);

                Assert.AreEqual(instance1.GetHashCode(), instance2.GetHashCode());
            }
        }
    }
}
