namespace Juno.Contracts.Data
{
    using System;
    using AutoFixture;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class AgentHeartbeatTests
    {
        private Fixture mockFixture;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupAgentMocks();
        }

        [Test]
        public void AgentHeartbeatConstructorsValidateRequiredParameters()
        {
            var validComponent = this.mockFixture.Create<AgentHeartbeat>();
            Assert.Throws<ArgumentException>(() => new AgentHeartbeat(null, AgentHeartbeatStatus.Failed, AgentType.GuestAgent));
        }

        [Test]
        public void AgentHeartbeatIsJsonSerializableByDefault()
        {
            SerializationAssert.IsJsonSerializable(this.mockFixture.Create<AgentHeartbeat>());
        }

        [Test]
        public void AgentHeartbeatIsJsonSerializableUsingExpectedSerializerSettings()
        {
            SerializationAssert.IsJsonSerializable(
                this.mockFixture.Create<AgentHeartbeat>(),
                ContractSerialization.DefaultJsonSerializationSettings);
        }

        [Test]
        public void AgentHeartbeatCorrectlyImplementsHashcodeSemantics()
        {
            var instance1 = this.mockFixture.Create<AgentHeartbeat>();
            var instance2 = this.mockFixture.Create<AgentHeartbeat>();

            EqualityAssert.CorrectlyImplementsHashcodeSemantics(() => instance1, () => instance2);
        }

        [Test]
        public void AgentHeartbeatCorrectlyImplementsEqualitySemantics()
        {
            AgentHeartbeat instance1 = this.mockFixture.Create<AgentHeartbeat>();
            AgentHeartbeat instance2 = this.mockFixture.Create<AgentHeartbeat>();
            EqualityAssert.CorrectlyImplementsEqualitySemantics(() => instance1, () => instance2);
        }
    }
}
