namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
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
        public void ScheduleActionConstructorValidatesRequiredParameter(string invalidParam)
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
        public void GoalConstructorValidatesRequiredStringParameter(string invalidParam)
        {
            Goal validComponent = this.mockFixture.Create<Goal>();

            Assert.Throws<ArgumentException>(() => new Goal(
                invalidParam,
                validComponent.Preconditions,
                validComponent.Actions));
        }

        /* Tests for Target Goal */

        [Test]
        public void TargetGoalIsJsonSerializableByDefault()
        {
            SerializationAssert.IsJsonSerializable(this.mockFixture.Create<TargetGoal>());
        }

        [Test]
        public void TargetGoalIsJsonSerializableUsingExpectedSerializerSettings()
        {
            SerializationAssert.IsJsonSerializable(
                this.mockFixture.Create<TargetGoal>(),
                ContractSerialization.DefaultJsonSerializationSettings);
        }

        [Test]
        [TestCase(null, null)]
        public void TargetGoalConstructorValidatesRequiredParameters(List<Precondition> invalidPrecondition, List<ScheduleAction> invalidAction)
        {
            Goal validComponent = this.mockFixture.Create<Goal>();

            Assert.Throws<ArgumentNullException>(() => new TargetGoal(
                validComponent.Name,
                true,
                invalidPrecondition,
                validComponent.Actions));

            Assert.Throws<ArgumentNullException>(() => new TargetGoal(
                validComponent.Name,
                false,
                validComponent.Preconditions,
                invalidAction));
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void TargetGoalConstructorValidatesRequiredStringParameter(string invalidParam)
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
                validComponent.Description,
                validComponent.Experiment,
                validComponent.TargetGoals,
                validComponent.ControlGoals));

            Assert.Throws<ArgumentException>(() => new GoalBasedSchedule(
                validComponent.ExperimentName,
                invalidParameter,
                validComponent.Experiment,
                validComponent.TargetGoals,
                validComponent.ControlGoals));
        }
    }
}