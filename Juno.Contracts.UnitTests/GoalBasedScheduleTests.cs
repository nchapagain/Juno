namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using AutoFixture;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class GoalBasedScheduleTests
    {
        private Fixture mockFixture;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks();
            this.mockFixture.SetUpGoalBasedScheduleMocks();
        }

        /* Tests for Preconditions */
        [Test]
        public void PreconditionIsJsonSerializableByDefault()
        {
            SerializationAssert.IsJsonSerializable(this.mockFixture.Create<Precondition>());
        }

        [Test]
        public void PreconditionIsJsonSerializableUsingExpectedSerializerSettings()
        {
            SerializationAssert.IsJsonSerializable(
                this.mockFixture.Create<Precondition>(),
                ContractSerialization.DefaultJsonSerializationSettings);
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void PreconditionsConstructorsValidateRequiredParameters(string invalidParam)
        {
            Precondition validComponent = this.mockFixture.Create<Precondition>();

            Assert.Throws<ArgumentException>(() => new Precondition(
                invalidParam,
                validComponent.Parameters));
        }

        /* Tests for Schedule Actions */
        [Test]
        public void SheduleActionIsJsonSerializableByDefault()
        {
            SerializationAssert.IsJsonSerializable(this.mockFixture.Create<ScheduleAction>());
        }

        [Test]
        public void ScheduleActionIsJsonSerializableUsingExpectedSerializerSettings()
        {
            SerializationAssert.IsJsonSerializable(
                this.mockFixture.Create<ScheduleAction>(),
                ContractSerialization.DefaultJsonSerializationSettings);
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void ScheduleActionConstructorValidatesRequiredParameters(string invalidParam)
        {
            ScheduleAction validComponent = this.mockFixture.Create<ScheduleAction>();
            Assert.Throws<ArgumentException>(() => new ScheduleAction(
                invalidParam,
                validComponent.Parameters));
        }

        /* Tests for Goal */
        [Test]
        public void GoalIsJsonSerializableByDefault()
        {
            SerializationAssert.IsJsonSerializable(this.mockFixture.Create<Goal>());
        }

        [Test]
        public void GoalIsJsonSerializableUsingExpectedSerializerSettings()
        {
            SerializationAssert.IsJsonSerializable(
                this.mockFixture.Create<Goal>(),
                ContractSerialization.DefaultJsonSerializationSettings);
        }

        [Test]
        [TestCase(null, null)]
        public void GoalConstructorValidatesRequiredParameters(List<Precondition> invalidPrecondition, List<ScheduleAction> invalidAction)
        {
            Goal validComponent = this.mockFixture.Create<Goal>();

            Assert.Throws<ArgumentNullException>(() => new Goal(
                validComponent.Name,
                invalidPrecondition,
                validComponent.Actions));

            Assert.Throws<ArgumentNullException>(() => new Goal(
                validComponent.Name,
                validComponent.Preconditions,
                invalidAction));
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void GoalConstructorValidatesRequiredStringParamater(string invalidParam)
        {
            Goal validComponent = this.mockFixture.Create<Goal>();

            Assert.Throws<ArgumentException>(() => new Goal(
                invalidParam,
                validComponent.Preconditions,
                validComponent.Actions));
        }

        /* Test for Goal Based Schedule */
        [Test]
        public void GoalBasedScheduleIsJsonSerializable()
        {
            SerializationAssert.IsJsonSerializable(this.mockFixture.Create<GoalBasedSchedule>());
        }

        [Test]
        public void GoalBasedScheduleIsJsonSerializableUsingExpectedSerializerSettings()
        {
            SerializationAssert.IsJsonSerializable(
                this.mockFixture.Create<GoalBasedSchedule>(),
                ContractSerialization.DefaultJsonSerializationSettings);
        }

        [Test]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase(null)]
        public void GoalBasedScheduleConstructorValidatesRequiredStringParameters(string invalidParameter)
        {
            GoalBasedSchedule validComponent = this.mockFixture.Create<GoalBasedSchedule>();

            Assert.Throws<ArgumentException>(() => new GoalBasedSchedule(
                invalidParameter,
                validComponent.ExecutionGoalId,
                validComponent.Name,
                validComponent.TeamName,
                validComponent.Description,
                validComponent.ScheduleMetadata,
                validComponent.Enabled,
                validComponent.Version,
                validComponent.Experiment,
                validComponent.TargetGoals,
                validComponent.ControlGoals));

            Assert.Throws<ArgumentException>(() => new GoalBasedSchedule(
                validComponent.ExperimentName,
                invalidParameter,
                validComponent.Name,
                validComponent.TeamName,
                validComponent.Description,
                validComponent.ScheduleMetadata,
                validComponent.Enabled,
                validComponent.Version,
                validComponent.Experiment,
                validComponent.TargetGoals,
                validComponent.ControlGoals));

            Assert.Throws<ArgumentException>(() => new GoalBasedSchedule(
                validComponent.ExperimentName,
                validComponent.ExecutionGoalId,
                invalidParameter,
                validComponent.TeamName,
                validComponent.Description,
                validComponent.ScheduleMetadata,
                validComponent.Enabled,
                validComponent.Version,
                validComponent.Experiment,
                validComponent.TargetGoals,
                validComponent.ControlGoals));

            Assert.Throws<ArgumentException>(() => new GoalBasedSchedule(
                validComponent.ExperimentName,
                validComponent.ExecutionGoalId,
                validComponent.Name,
                invalidParameter,
                validComponent.Description,
                validComponent.ScheduleMetadata,
                validComponent.Enabled,
                validComponent.Version,
                validComponent.Experiment,
                validComponent.TargetGoals,
                validComponent.ControlGoals));

            Assert.Throws<ArgumentException>(() => new GoalBasedSchedule(
                validComponent.ExperimentName,
                validComponent.ExecutionGoalId,
                validComponent.Name,
                validComponent.TeamName,
                invalidParameter,
                validComponent.ScheduleMetadata,
                validComponent.Enabled,
                validComponent.Version,
                validComponent.Experiment,
                validComponent.TargetGoals,
                validComponent.ControlGoals));

            Assert.Throws<ArgumentException>(() => new GoalBasedSchedule(
                validComponent.ExperimentName,
                validComponent.ExecutionGoalId,
                validComponent.Name,
                validComponent.TeamName,
                validComponent.Description,
                validComponent.ScheduleMetadata,
                validComponent.Enabled,
                null,
                validComponent.Experiment,
                validComponent.TargetGoals,
                validComponent.ControlGoals));
        }

        [Test]
        [TestCase(null)]
        public void GoalBasedSchedulerConstructorValidatesBoolParameter(bool? invalidParameter)
        {
            GoalBasedSchedule validComponent = this.mockFixture.Create<GoalBasedSchedule>();

            Assert.Throws<ArgumentException>(() => new GoalBasedSchedule(
                validComponent.ExperimentName,
                validComponent.ExecutionGoalId,
                validComponent.Name,
                validComponent.TeamName,
                validComponent.Description,
                validComponent.ScheduleMetadata,
                invalidParameter,
                validComponent.Version,
                validComponent.Experiment,
                validComponent.TargetGoals,
                validComponent.ControlGoals));
        }

        [Test]
        public void GoalBasedSchedulerSupportsDifferentVersions()
        {
            GoalBasedSchedule validComponent = this.mockFixture.Create<GoalBasedSchedule>();
            string oldVersion = "2020-07-27";
            string newVersion = "2021-01-01";

            Assert.DoesNotThrow(() => new GoalBasedSchedule(
                validComponent.ExperimentName,
                validComponent.ExecutionGoalId,
                validComponent.Name,
                validComponent.TeamName,
                validComponent.Description,
                validComponent.ScheduleMetadata,
                validComponent.Enabled,
                oldVersion,
                null,
                validComponent.TargetGoals,
                validComponent.ControlGoals));

            Assert.DoesNotThrow(() => new GoalBasedSchedule(
                validComponent.ExperimentName,
                validComponent.ExecutionGoalId,
                validComponent.Name,
                validComponent.TeamName,
                validComponent.Description,
                validComponent.ScheduleMetadata,
                validComponent.Enabled,
                newVersion,
                validComponent.Experiment,
                validComponent.TargetGoals,
                validComponent.ControlGoals));
        }
    }
}