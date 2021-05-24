namespace Juno.Contracts
{
    using AutoFixture;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class AgentHeartbeatInstanceTests
    {
        private Fixture mockFixture;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
        }

        [Test]
        public void AgentHeartbeatInstanceIsJsonSerializableByDefault()
        {
            SerializationAssert.IsJsonSerializable(this.mockFixture.Create<AgentHeartbeatInstance>());
        }

        [Test]
        public void AgentHeartbeatInstanceIsJsonSerializableUsingExpectedSerializerSettings()
        {
            SerializationAssert.IsJsonSerializable(
                this.mockFixture.Create<AgentHeartbeatInstance>(),
                ContractSerialization.DefaultJsonSerializationSettings);
        }

        [Test]
        public void AgentHeartbeatInstanceCorrectlyImplementsHashcodeSemantics()
        {
            var instance1 = this.mockFixture.Create<AgentHeartbeatInstance>();
            var instance2 = this.mockFixture.Create<AgentHeartbeatInstance>();

            EqualityAssert.CorrectlyImplementsHashcodeSemantics(() => instance1, () => instance2);
        }

        [Test]
        public void AgentHeartbeatInstanceCorrectlyImplementsEqualitySemantics()
        {
            ItemBase instance1 = this.mockFixture.Create<AgentHeartbeatInstance>();
            ItemBase instance2 = this.mockFixture.Create<AgentHeartbeatInstance>();

            EqualityAssert.CorrectlyImplementsEqualitySemantics(() => instance1, () => instance2);
        }
    }
}
