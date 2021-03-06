namespace Juno.Contracts
{
    using System;
    using AutoFixture;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class TargetGoalTriggerUnitTests
    {
        private Fixture mockFixture;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetUpGoalBasedScheduleMocks();
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void TargetGoalTriggerConstructorValidatesRequiredParamaeters(string invalidParameter)
        {
            TargetGoalTrigger validComponent = this.mockFixture.Create<TargetGoalTrigger>();

            Assert.Throws<ArgumentException>(() => new TargetGoalTrigger(
                validComponent.Id,
                invalidParameter,
                validComponent.Name,
                validComponent.CronExpression,
                validComponent.Enabled,
                validComponent.Version,
                validComponent.TeamName,
                validComponent.Created,
                validComponent.LastModified));

            Assert.Throws<ArgumentException>(() => new TargetGoalTrigger(
                validComponent.Id,
                validComponent.ExecutionGoal,
                invalidParameter,
                validComponent.CronExpression,
                validComponent.Enabled,
                validComponent.Version,
                validComponent.TeamName,
                validComponent.Created,
                validComponent.LastModified));

            Assert.Throws<ArgumentException>(() => new TargetGoalTrigger(
                validComponent.Id,
                validComponent.ExecutionGoal,
                validComponent.Name,
                invalidParameter,
                validComponent.Enabled,
                validComponent.Version,
                validComponent.TeamName,
                validComponent.Created,
                validComponent.LastModified));

            Assert.Throws<ArgumentException>(() => new TargetGoalTrigger(
                validComponent.Id,
                validComponent.ExecutionGoal,
                validComponent.Name,
                validComponent.CronExpression,
                validComponent.Enabled,
                invalidParameter,
                validComponent.TeamName,
                validComponent.Created,
                validComponent.LastModified));
        }

        [Test]
        public void TargetGoalTriggerIsJsonSerializable()
        {
            SerializationAssert.IsJsonSerializable(this.mockFixture.Create<TargetGoalTrigger>());
        }

        [Test]
        public void TargetGoalTriggerIsJsonSerializableUsingExpectedSerializerSettings()
        {
            SerializationAssert.IsJsonSerializable(
                this.mockFixture.Create<TargetGoalTrigger>(),
                ContractSerialization.DefaultJsonSerializationSettings);
        }

        [Test]
        public void TargetGoalTriggerHashCodesAreNotCaseSensitive()
        { 
            TargetGoalTrigger template = this.mockFixture.Create<TargetGoalTrigger>();
            TargetGoalTrigger instance1 = new TargetGoalTrigger(
                template.Id.ToLowerInvariant(),
                template.ExecutionGoal.ToLowerInvariant(),
                template.Name.ToLowerInvariant(),
                template.CronExpression.ToLowerInvariant(),
                template.Enabled,
                template.Version.ToLowerInvariant(),
                template.TeamName.ToLowerInvariant(),
                template.Created,
                template.LastModified);

            TargetGoalTrigger instance2 = new TargetGoalTrigger(
                template.Id.ToUpperInvariant(),
                template.ExecutionGoal.ToUpperInvariant(),
                template.Name.ToUpperInvariant(),
                template.CronExpression.ToUpperInvariant(),
                template.Enabled,
                template.Version.ToUpperInvariant(),
                template.TeamName.ToLowerInvariant(),
                template.Created,
                template.LastModified);

            Assert.AreEqual(instance1.GetHashCode(), instance2.GetHashCode());
        }
    }
}
