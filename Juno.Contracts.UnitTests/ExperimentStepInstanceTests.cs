namespace Juno.Contracts
{
    using System.Collections.Generic;
    using AutoFixture;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ExperimentStepInstanceTests
    {
        private Fixture mockFixture;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks();
        }

        [Test]
        public void ExperimentStepIsJsonSerializableByDefault()
        {
            SerializationAssert.IsJsonSerializable(this.mockFixture.Create<ExperimentStepInstance>());
        }

        [Test]
        public void ExperimentStepIsJsonSerializableUsingExpectedSerializerSettings()
        {
            SerializationAssert.IsJsonSerializable(
                this.mockFixture.Create<ExperimentStepInstance>(),
                ContractSerialization.DefaultJsonSerializationSettings);
        }

        [Test]
        public void ExperimentStepCorrectlyImplementsEqualitySemantics()
        {
            Item<ExperimentComponent> instance1 = this.mockFixture.Create<ExperimentStepInstance>();
            Item<ExperimentComponent> instance2 = this.mockFixture.Create<ExperimentStepInstance>();

            EqualityAssert.CorrectlyImplementsEqualitySemantics(() => instance1, () => instance2);
        }

        [Test]
        public void EnvironmentEntityCorrectlyImplementsHashcodeSemantics()
        {
            ExperimentStepInstance instance1 = this.mockFixture.Create<ExperimentStepInstance>();
            ExperimentStepInstance instance2 = this.mockFixture.Create<ExperimentStepInstance>();

            EqualityAssert.CorrectlyImplementsHashcodeSemantics(() => instance1, () => instance2);
        }

        [Test]
        public void ExperimentStepHashCodesAreNotCaseSensitive()
        {
            ExperimentStepInstance template = this.mockFixture.Create<ExperimentStepInstance>();
            ExperimentStepInstance instance1 = new ExperimentStepInstance(
                template.Id.ToLowerInvariant(),
                template.ExperimentId.ToLowerInvariant(),
                template.ExperimentGroup,
                template.StepType,
                template.Status,
                template.Sequence,
                template.Attempts,
                template.Definition,
                template.StartTime,
                template.EndTime);

            instance1.Extensions.Add("Any".ToLowerInvariant(), JToken.Parse(new Dictionary<int, string>
            {
                [1] = "Any Value".ToLowerInvariant()
            }.ToJson()));

            ExperimentStepInstance instance2 = new ExperimentStepInstance(
                template.Id.ToUpperInvariant(),
                template.ExperimentId.ToUpperInvariant(),
                template.ExperimentGroup,
                template.StepType,
                template.Status,
                template.Sequence,
                template.Attempts,
                template.Definition,
                template.StartTime,
                template.EndTime);

            instance2.Extensions.Add("Any".ToUpperInvariant(), JToken.Parse(new Dictionary<int, string>
            {
                [1] = "Any Value".ToUpperInvariant()
            }.ToJson()));

            Assert.AreEqual(instance1.GetHashCode(), instance2.GetHashCode());
        }
    }
}
