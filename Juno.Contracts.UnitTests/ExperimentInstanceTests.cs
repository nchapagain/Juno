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
    public class ExperimentInstanceTests
    {
        private Fixture mockFixture;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks();
        }

        [Test]
        public void ExperimentInstanceIsJsonSerializableByDefault()
        {
            SerializationAssert.IsJsonSerializable(this.mockFixture.Create<ExperimentInstance>());
        }

        [Test]
        public void ExperimentInstanceIsJsonSerializableUsingExpectedSerializerSettings()
        {
            SerializationAssert.IsJsonSerializable(
                this.mockFixture.Create<ExperimentInstance>(),
                ContractSerialization.DefaultJsonSerializationSettings);
        }

        [Test]
        public void ExperimentInstanceCorrectlyImplementsEqualitySemantics()
        {
            // Because the IEquality<T> interface is on the Item<T> base, we have to use
            // that base to do the comparison.
            Item<Experiment> instance1 = this.mockFixture.Create<ExperimentInstance>();
            Item<Experiment> instance2 = this.mockFixture.Create<ExperimentInstance>();

            EqualityAssert.CorrectlyImplementsEqualitySemantics(() => instance1, () => instance2);
        }

        [Test]
        public void ExperimentInstanceCorrectlyImplementsHashcodeSemantics()
        {
            ExperimentInstance instance1 = this.mockFixture.Create<ExperimentInstance>();
            ExperimentInstance instance2 = this.mockFixture.Create<ExperimentInstance>();

            EqualityAssert.CorrectlyImplementsHashcodeSemantics(() => instance1, () => instance2);
        }

        [Test]
        public void ExperimentInstanceHashCodesAreNotCaseSensitive()
        {
            ExperimentInstance template = this.mockFixture.Create<ExperimentInstance>();
            ExperimentInstance instance1 = new ExperimentInstance(
                template.Id.ToLowerInvariant(),
                template.Definition);

            instance1.Extensions.Add("Any".ToLowerInvariant(), JToken.Parse(new Dictionary<int, string>
            {
                [1] = "Any Value".ToLowerInvariant()
            }.ToJson()));

            ExperimentInstance instance2 = new ExperimentInstance(
                template.Id.ToUpperInvariant(),
                template.Definition);

            instance2.Extensions.Add("Any".ToUpperInvariant(), JToken.Parse(new Dictionary<int, string>
            {
                [1] = "Any Value".ToUpperInvariant()
            }.ToJson()));

            Assert.AreEqual(instance1.GetHashCode(), instance2.GetHashCode());
        }
    }
}
