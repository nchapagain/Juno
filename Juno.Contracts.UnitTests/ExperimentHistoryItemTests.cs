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
    public class ExperimentHistoryITemTests
    {
        private Fixture mockFixture;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks();
        }

        [Test]
        public void ExperimentHistoryItemIsJsonSerializableByDefault()
        {
            SerializationAssert.IsJsonSerializable(this.mockFixture.Create<ExperimentHistoryItem>());
        }

        [Test]
        public void ExperimentHistoryItemIsJsonSerializableUsingExpectedSerializerSettings()
        {
            SerializationAssert.IsJsonSerializable(
                this.mockFixture.Create<ExperimentHistoryItem>(),
                ContractSerialization.DefaultJsonSerializationSettings);
        }

        [Test]
        public void ExperimentHistoryItemCorrectlyImplementsEqualitySemantics()
        {
            // Because the IEquality<T> interface is on the Item<T> base, we have to use
            // that base to do the comparison.
            Item<ExperimentHistory> history1 = this.mockFixture.Create<ExperimentHistoryItem>();
            Item<ExperimentHistory> history2 = this.mockFixture.Create<ExperimentHistoryItem>();

            EqualityAssert.CorrectlyImplementsEqualitySemantics(() => history1, () => history2);
        }

        [Test]
        public void ExperimentHistoryItemCorrectlyImplementsHashcodeSemantics()
        {
            ExperimentHistoryItem history1 = this.mockFixture.Create<ExperimentHistoryItem>();
            ExperimentHistoryItem history2 = this.mockFixture.Create<ExperimentHistoryItem>();

            EqualityAssert.CorrectlyImplementsHashcodeSemantics(() => history1, () => history2);
        }

        [Test]
        public void ExperimentHistoryItemHashCodesAreNotCaseSensitive()
        {
            ExperimentHistoryItem historyItem = this.mockFixture.Create<ExperimentHistoryItem>();
            ExperimentHistoryItem history1 = new ExperimentHistoryItem(
                historyItem.Id.ToLowerInvariant(),
                historyItem.Definition);

            history1.Extensions.Add("Any".ToLowerInvariant(), JToken.Parse(new Dictionary<int, string>
            {
                [1] = "Any Value".ToLowerInvariant()
            }.ToJson()));

            ExperimentHistoryItem history2 = new ExperimentHistoryItem(
                historyItem.Id.ToUpperInvariant(),
                historyItem.Definition);

            history2.Extensions.Add("Any".ToUpperInvariant(), JToken.Parse(new Dictionary<int, string>
            {
                [1] = "Any Value".ToUpperInvariant()
            }.ToJson()));

            Assert.AreEqual(history1.GetHashCode(), history2.GetHashCode());
        }
    }
}
