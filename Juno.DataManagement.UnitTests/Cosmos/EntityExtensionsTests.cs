namespace Juno.DataManagement.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using AutoFixture;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.OData.Edm;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class EntityAddressExtensionsTests
    {
        private Fixture mockFixture;
        private ExperimentStepInstance mockStep;
        private ExperimentStepTableEntity mockStepEntity;
        private TargetGoalTrigger mockTargetGoal;
        private TargetGoalTableEntity mockTargetGoalEntity;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks(ExperimentType.AB);
            this.mockFixture.SetupAgentMocks();
            this.mockFixture.SetUpGoalBasedScheduleMocks();

            this.mockStep = this.mockFixture.Create<ExperimentStepInstance>();
            this.mockStep.Extensions.Clear();

            this.mockStepEntity = new ExperimentStepTableEntity
            {
                ExperimentGroup = this.mockStep.ExperimentGroup,
                Name = this.mockStep.Definition.Name,
                StepType = this.mockStep.StepType.ToString(),
                Status = this.mockStep.Status.ToString(),
                Sequence = this.mockStep.Sequence,
                Attempts = this.mockStep.Attempts,
                Created = this.mockStep.Created,
                Timestamp = this.mockStep.Created,
                StartTime = this.mockStep.StartTime.Value.ToString("o"),
                EndTime = this.mockStep.EndTime.Value.ToString("o"),
                Definition = this.mockStep.Definition.ToJson(),
                PartitionKey = this.mockStep.ExperimentId,
                RowKey = this.mockStep.Id,
                ExperimentId = this.mockStep.ExperimentId
            };

            this.mockTargetGoal = this.mockFixture.Create<TargetGoalTrigger>();
            this.mockTargetGoalEntity = this.mockFixture.Create<TargetGoalTableEntity>();
        }

        [Test]
        public void ToHeartbeatExtensionIsBackwardsCompatibleWithEntitiesThatDoNotContainACreatedDateProperty()
        {
            // We introduced a 'Created' property after we had experiment step data in the system. In order to support
            // backwards compatibility, we are setting it to a value that is relevant for that previous data. Cosmos Table
            // will set the value to DateTime.UtcNow if the data in the table does not include a 'Created' column. Thus the
            // 'Created' timestamp will be greater than the 'Timestamp' value and this is the hallmark that we use to
            // make the distinction.
            AgentHeartbeatTableEntity tableEntity = new AgentHeartbeatTableEntity
            {
                AgentId = "a,b,c",
                AgentType = AgentType.GuestAgent.ToString(),
                Created = DateTime.UtcNow.AddDays(1), // The Created timestamp will be greater than the Timestamp.
                Id = "Any ID",
                Message = "Any message",
                PartitionKey = Guid.NewGuid().ToString(),
                RowKey = Guid.NewGuid().ToString(),
                Status = "Running",
                Timestamp = DateTime.UtcNow
            };

            AgentHeartbeatInstance heartbeat = tableEntity.ToHeartbeat();

            // In this scenario, the heartbeat 'Created' timestamp will be set to the same value as the 'Timestamp' value.
            // Whereas, this is not technically correct, this is the indirectly the behavior that was happening before we made
            // this change. Previously, we made the assumption that the 'Timestamp' property represented the time at which the heartbeat
            // was created. And we are implementing this based on the assumption we made previously.
            Assert.AreEqual(tableEntity.Timestamp.DateTime, heartbeat.Created);
        }

        [Test]
        public void ToStepExtensionDoesNotLoseExperimentComponentInformationInTheConversion()
        {
            ExperimentStepInstance expectedStep = this.mockStep;
            ExperimentStepInstance actualStep = this.mockStepEntity.ToStep();

            Assert.IsTrue(expectedStep.Definition.Equals(actualStep.Definition));
        }

        [Test]
        public void ToStepExtensionCreatesTheExpectedExperimentStepFromATableEntity()
        {
            ExperimentStepInstance expectedStep = this.mockFixture.Create<ExperimentStepInstance>();
            expectedStep.Extensions.Clear();

            ExperimentStepTableEntity tableEntity = new ExperimentStepTableEntity
            {
                ExperimentGroup = expectedStep.ExperimentGroup,
                Name = expectedStep.Definition.Name,
                StepType = expectedStep.StepType.ToString(),
                Status = expectedStep.Status.ToString(),
                Sequence = expectedStep.Sequence,
                Attempts = expectedStep.Attempts,
                Timestamp = expectedStep.Created,
                Created = this.mockStep.Created,
                StartTime = expectedStep.StartTime?.ToString("o"),
                EndTime = expectedStep.EndTime?.ToString("o"),
                Definition = expectedStep.Definition.ToJson(),
                PartitionKey = expectedStep.ExperimentId,
                RowKey = expectedStep.Id,
                ExperimentId = expectedStep.ExperimentId
            };

            ExperimentStepInstance actualStep = tableEntity.ToStep();
            Assert.IsTrue(expectedStep.Equals(actualStep));
        }

        [Test]
        public void ToStepExtensionIncludesErrorDetailsIfTheyExist()
        {
            ExperimentStepInstance expectedStep = this.mockFixture.Create<ExperimentStepInstance>();
            ExperimentException expectedError = new ExperimentException("This error should be added to the step");
            expectedStep.Extensions.Clear();

            ExperimentStepTableEntity tableEntity = new ExperimentStepTableEntity
            {
                ExperimentGroup = expectedStep.ExperimentGroup,
                Name = expectedStep.Definition.Name,
                StepType = expectedStep.StepType.ToString(),
                Status = expectedStep.Status.ToString(),
                Sequence = expectedStep.Sequence,
                Attempts = expectedStep.Attempts,
                Created = this.mockStep.Created,
                Timestamp = expectedStep.Created,
                Definition = expectedStep.Definition.ToJson(),
                PartitionKey = expectedStep.ExperimentId,
                RowKey = expectedStep.Id,
                ExperimentId = expectedStep.ExperimentId,
                Error = expectedError.ToJson()
            };

            ExperimentStepInstance actualStep = tableEntity.ToStep();
            Assert.IsTrue(actualStep.Extensions.ContainsKey("error"));

            ExperimentException actualError = actualStep.Extensions["error"].ToObject<ExperimentException>();
            Assert.IsNotNull(actualError);
            Assert.AreEqual(expectedError.Message, actualError.Message);
        }

        [Test]
        public void ToStepExtensionHandlesErrorsThatAreSimpleStrings()
        {
            ExperimentStepInstance expectedStep = this.mockFixture.Create<ExperimentStepInstance>();
            expectedStep.Extensions.Clear();

            string expectedError = "Any error details";

            // Results that are objects
            ExperimentStepTableEntity tableEntity = new ExperimentStepTableEntity
            {
                ExperimentGroup = expectedStep.ExperimentGroup,
                Name = expectedStep.Definition.Name,
                StepType = expectedStep.StepType.ToString(),
                Status = expectedStep.Status.ToString(),
                Sequence = expectedStep.Sequence,
                Attempts = expectedStep.Attempts,
                Created = this.mockStep.Created,
                Timestamp = expectedStep.Created,
                Definition = expectedStep.Definition.ToJson(),
                PartitionKey = expectedStep.ExperimentId,
                RowKey = expectedStep.Id,
                ExperimentId = expectedStep.ExperimentId,
                Error = expectedError
            };

            ExperimentStepInstance actualStep = tableEntity.ToStep();
            Assert.IsTrue(actualStep.Extensions.ContainsKey("error"));

            ExperimentException actualError = actualStep.Extensions["error"].ToObject<ExperimentException>();
            Assert.IsNotNull(actualError);
            Assert.AreEqual(expectedError, actualError.Message);
        }

        [Test]
        public void ToStepExtensionUsesRoundtripUtcFormattedDateTimeValues()
        {
            ExperimentStepInstance template = this.mockFixture.Create<ExperimentStepInstance>();

            DateTime expectedStartTime = DateTime.Parse("2020-02-05T22:34:41.431748Z", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
                .ToUniversalTime();

            DateTime expectedEndTime = DateTime.Parse("2020-02-05T22:34:51.431748Z", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
                .ToUniversalTime();

            // "2020-02-05T22:34:41.431748Z"
            ExperimentStepTableEntity tableEntity = new ExperimentStepTableEntity
            {
                ExperimentGroup = template.ExperimentGroup,
                Name = template.Definition.Name,
                StepType = template.StepType.ToString(),
                Status = template.Status.ToString(),
                Sequence = template.Sequence,
                Attempts = template.Attempts,
                Created = this.mockStep.Created,
                Timestamp = template.Created,
                Definition = template.Definition.ToJson(),
                PartitionKey = template.ExperimentId,
                RowKey = template.Id,
                ExperimentId = template.ExperimentId,

                // Date/Time values will be in round-trip/UTC format.
                StartTime = expectedStartTime.ToString("o"),
                EndTime = expectedEndTime.ToString("o")
            };

            ExperimentStepInstance actualStep = tableEntity.ToStep();

            Assert.AreEqual(expectedStartTime, actualStep.StartTime);
            Assert.AreEqual(expectedEndTime, actualStep.EndTime);
        }

        [Test]
        public void ToStepExtensionIsBackwardsCompatibleWithEntitiesThatDoNotContainACreatedDateProperty()
        {
            // We introduced a 'Created' property after we had experiment step data in the system. In order to support
            // backwards compatibility, we are setting it to a value that is relevant for that previous data. Cosmos Table
            // will set the value to DateTime.UtcNow if the data in the table does not include a 'Created' column. Thus the
            // 'Created' timestamp will be greater than the 'Timestamp' value and this is the hallmark that we use to
            // make the distinction.
            ExperimentStepInstance template = this.mockFixture.Create<ExperimentStepInstance>();

            ExperimentStepTableEntity tableEntity = new ExperimentStepTableEntity
            {
                ExperimentGroup = template.ExperimentGroup,
                Name = template.Definition.Name,
                StepType = template.StepType.ToString(),
                Status = template.Status.ToString(),
                Sequence = template.Sequence,
                Attempts = template.Attempts,
                Created = template.Created.AddDays(1), // The Created timestamp will be greater than the Timestamp.
                Timestamp = template.Created,
                Definition = template.Definition.ToJson(),
                PartitionKey = template.ExperimentId,
                RowKey = template.Id,
                ExperimentId = template.ExperimentId,
                StartTime = DateTime.UtcNow.AddHours(-2).ToString("o"),
                EndTime = DateTime.UtcNow.AddHours(-1).ToString("o")
            };

            ExperimentStepInstance step = tableEntity.ToStep();

            // In this scenario, the step 'Created' timestamp will be set to the same value as the 'Timestamp' value.
            // Whereas, this is not technically correct, this is the indirectly the behavior that was happening before we made
            // this change. Previously, we made the assumption that the 'Timestamp' property represented the time at which the step
            // was created. And we are implementing this based on the assumption we made previously.
            Assert.AreEqual(tableEntity.Timestamp.DateTime, step.Created);
        }

        [Test]
        public void ToTableEntityExtensionCreatesTheExpectedEntityFromAStep()
        {
            ExperimentStepInstance step = this.mockFixture.Create<ExperimentStepInstance>();
            step.Extensions.Clear();

            ExperimentStepTableEntity tableEntity = step.ToTableEntity();

            Assert.AreEqual(step.ExperimentId, tableEntity.ExperimentId);
            Assert.AreEqual(step.ExperimentGroup, tableEntity.ExperimentGroup);
            Assert.AreEqual(step.Definition.Name, tableEntity.Name);
            Assert.AreEqual(step.StepType.ToString(), tableEntity.StepType);
            Assert.AreEqual(step.Status.ToString(), tableEntity.Status);
            Assert.AreEqual(step.Sequence, tableEntity.Sequence);
            Assert.AreEqual(step.Attempts, tableEntity.Attempts);
            Assert.AreEqual(step.Created, tableEntity.Created);
            Assert.AreEqual(step.StartTime, DateTime.Parse(tableEntity.StartTime).ToUniversalTime());
            Assert.AreEqual(step.EndTime, DateTime.Parse(tableEntity.EndTime).ToUniversalTime());
            Assert.AreEqual(step.ExperimentId, tableEntity.PartitionKey);
            Assert.AreEqual(step.Id, tableEntity.RowKey);
            Assert.AreEqual(step.Definition, tableEntity.Definition.FromJson<ExperimentComponent>());
        }

        [Test]
        public void ToTableEntityExtensionUsesRoundtripUtcFormattedDateTimeValues()
        {
            ExperimentStepInstance step = this.mockFixture.Create<ExperimentStepInstance>();

            string expectedStartTime = step.StartTime.Value.ToString("o");
            string expectedEndTime = step.EndTime.Value.ToString("o");

            ExperimentStepTableEntity tableEntity = step.ToTableEntity();

            Assert.AreEqual(expectedStartTime, tableEntity.StartTime);
            Assert.AreEqual(expectedEndTime, tableEntity.EndTime);
        }

        [Test]
        public void ToStepAndToEntityConversionsDoNotLoseTheFidelityOfDateTimeValues()
        {
            ExperimentStepInstance originalStep = this.mockFixture.Create<ExperimentStepInstance>();
            originalStep.StartTime = DateTime.UtcNow;
            originalStep.EndTime = DateTime.UtcNow;

            ExperimentStepTableEntity tableEntity = originalStep.ToTableEntity();
            ExperimentStepInstance convertedStep = tableEntity.ToStep();

            Assert.AreEqual(originalStep.Created, convertedStep.Created);
            Assert.AreEqual(originalStep.StartTime, convertedStep.StartTime);
            Assert.AreEqual(originalStep.EndTime, convertedStep.EndTime);
        }

        [Test]
        public void ToTargetGoalTriggerExtensionCreatesTheExpectedTriggerFromAnEntity()
        {
            TargetGoalTrigger expectedResult = new TargetGoalTrigger(
                this.mockTargetGoalEntity.Id, 
                this.mockTargetGoalEntity.ExecutionGoal, 
                this.mockTargetGoalEntity.Name, 
                this.mockTargetGoalEntity.CronExpression, 
                this.mockTargetGoalEntity.Enabled, 
                this.mockTargetGoalEntity.PartitionKey, 
                this.mockTargetGoalEntity.TeamName, 
                DateTime.UtcNow, 
                DateTime.UtcNow);

            TargetGoalTrigger actualResult = this.mockTargetGoalEntity.ToTargetGoalTrigger();

            Assert.IsTrue(expectedResult.Equals(actualResult));
        }

        [Test]
        public void ToTargetGoalTriggerExtensionIsBackwardsCompatibleWithEntitiesThatDoNotContainACreatedDateProperty()
        {
            // We introduced a 'Created' property after we had target goal data in the system. In order to support
            // backwards compatibility, we are setting it to a value that is relevant for that previous data. Cosmos Table
            // will set the value to DateTime.UtcNow if the data in the table does not include a 'Created' column. Thus the
            // 'Created' timestamp will be greater than the 'Timestamp' value and this is the hallmark that we use to
            // make the distinction.
            TargetGoalTableEntity tableEntity = this.mockFixture.Create<TargetGoalTableEntity>();
            tableEntity.Created = tableEntity.Timestamp.DateTime.AddDays(1); // The Created timestamp will be greater than the Timestamp.

            TargetGoalTrigger trigger = tableEntity.ToTargetGoalTrigger();

            // In this scenario, the target goal 'Created' timestamp will be set to the same value as the 'Timestamp' value.
            // Whereas, this is not technically correct, this is the indirectly the behavior that was happening before we made
            // this change. Previously, we made the assumption that the 'Timestamp' property represented the time at which the target goal
            // was created. And we are implementing this based on the assumption we made previously.
            Assert.AreEqual(tableEntity.Timestamp.DateTime, trigger.Created);
        }

        [Test]
        public void ToTargetGoalTableEntityExtensionCreatesTheExpectedEntityFromTargetGoalTrigger()
        {
            TargetGoalTableEntity expectedResult = new TargetGoalTableEntity
            { 
                Name = this.mockTargetGoal.Name,
                Id = this.mockTargetGoal.Id,
                TeamName = this.mockTargetGoal.TeamName,
                CronExpression = this.mockTargetGoal.CronExpression,
                ExecutionGoal = this.mockTargetGoal.ExecutionGoal,
                PartitionKey = this.mockTargetGoal.Version
            };

            TargetGoalTableEntity actualResult = this.mockTargetGoal.ToTargetGoalTableEntity();

            Assert.AreEqual(expectedResult.Id, actualResult.Id);
            Assert.AreEqual(expectedResult.PartitionKey, actualResult.PartitionKey);
            Assert.AreEqual(expectedResult.RowKey, actualResult.RowKey);
            Assert.AreEqual(expectedResult.CronExpression, actualResult.CronExpression);
            Assert.AreEqual(expectedResult.ExecutionGoal, actualResult.ExecutionGoal);
        }

        [Test]
        public void ToTargetGoalTableEntityExtenstionCreatesTheExpectedEntityFromTargetGoal()
        {
            TargetGoal targetGoal = FixtureExtensions.CreateTargetGoal();
            Item<GoalBasedSchedule> executionGoal = new Item<GoalBasedSchedule>(Guid.NewGuid().ToString(), FixtureExtensions.CreateExecutionGoalFromTemplate(targetGoals: new List<TargetGoal>() { targetGoal }));

            TargetGoalTableEntity expectedResult = FixtureExtensions.CreateTargetTableEntityFromTemplates(executionGoal, targetGoal);
            TargetGoalTableEntity actualResult = targetGoal.ToTableEntity(executionGoal);

            Assert.AreEqual(expectedResult.Id, actualResult.Id);
            Assert.AreEqual(expectedResult.PartitionKey, actualResult.PartitionKey);
            Assert.AreEqual(expectedResult.RowKey, actualResult.RowKey);
            Assert.AreEqual(expectedResult.CronExpression, actualResult.CronExpression);
            Assert.AreEqual(expectedResult.ExecutionGoal, actualResult.ExecutionGoal);
        }
    }
}
