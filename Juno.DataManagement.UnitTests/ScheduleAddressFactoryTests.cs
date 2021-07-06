namespace Juno.DataManagement
{
    using System;
    using AutoFixture;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Repository.Cosmos;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ScheduleAddressFactoryTests
    {
        private Fixture mockFixture;

        [SetUp]
        public void SetUp()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetUpGoalBasedScheduleMocks();
        }
        
        [Test]
        public void SchedulerAddressFactoryGeneratesCorrectAddressForExecutionGoal()
        {
            GoalBasedSchedule component = this.mockFixture.Create<GoalBasedSchedule>();
            string teamName = component.TeamName;
            string executionGoalId = Guid.NewGuid().ToString();

            CosmosAddress address = ScheduleAddressFactory.CreateExecutionGoalAddress(teamName, executionGoalId);

            Assert.AreEqual(address.PartitionKey, teamName);
            Assert.AreEqual(address.DocumentId, executionGoalId);

        }

        [Test]
        public void ScheduleAddressFactoryCreateTheExpectedAddressForATargetGoalTriggerInstance()
        {
            CosmosTableAddress address = ScheduleAddressFactory.CreateTargetGoalTriggerAddress("2021-01-01");
            Assert.IsNotNull(address);
        }

        [Test]
        public void ScheduleAddressFactoryCreateTheExpectedAddressForATargetGoalTriggerWhenARowKeyIsProvided()
        {
            CosmosTableAddress address = ScheduleAddressFactory.CreateTargetGoalTriggerAddress("2021-01-01", "RowKey");
            Assert.IsNotNull(address);
        }
    }
}
