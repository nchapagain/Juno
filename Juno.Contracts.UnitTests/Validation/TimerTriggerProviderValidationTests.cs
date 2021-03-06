namespace Juno.Contracts.Validation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using AutoFixture;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class TimerTriggerProviderValidationTests
    {
        private Fixture mockFixture;
        private GoalBasedSchedule validExecutionGoal;
        private Precondition timerTriggerPrecondition;
        private Precondition successfulExperimentPrecondition;

        [SetUp]
        public void SetupTests()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetUpGoalBasedScheduleMocks();

            this.timerTriggerPrecondition = new Precondition(
                type: ContractExtension.TimerTriggerType,
                parameters: new Dictionary<string, IConvertible>()
                {
                    [ContractExtension.CronExpression] = "* * * * *"
                });

            this.successfulExperimentPrecondition = new Precondition(
                type: ContractExtension.SuccessfulExperimentsProvider,
                parameters: new Dictionary<string, IConvertible>()
                {
                    [ContractExtension.TargetExperimentInstances] = 10
                });

            this.validExecutionGoal = this.CreateExecutionGoalWithGivenPrecondition(this.timerTriggerPrecondition, this.successfulExperimentPrecondition);
        }

        [Test]
        public void TargetGoalValidationPassesForValidExecutionGoal()
        {
            ValidationResult result = TimerTriggerProviderRules.Instance.Validate(this.validExecutionGoal);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid);
            Assert.IsTrue(result.ValidationErrors?.Any() != true);
        }

        [Test]
        public void TargetGoalValidationFailsForMissingTimerTriggerPrecondtion()
        {
            GoalBasedSchedule invalidExecutionGoal = this.CreateExecutionGoalWithGivenPrecondition(this.successfulExperimentPrecondition);

            ValidationResult result = TimerTriggerProviderRules.Instance.Validate(invalidExecutionGoal);

            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.ValidationErrors.Count() == 1);
        }

        [Test]
        public void TargetGoalValidationFailsForMissingCronExpression()
        {
            Precondition invalidTimerTriggerPrecondition = new Precondition(
                type: ContractExtension.TimerTriggerType,
                new Dictionary<string, IConvertible>()
                {
                    ["NotCronExpression"] = "* * * * *"
                });

            GoalBasedSchedule invalidExecutionGoal = this.CreateExecutionGoalWithGivenPrecondition(invalidTimerTriggerPrecondition, this.successfulExperimentPrecondition);

            ValidationResult result = TimerTriggerProviderRules.Instance.Validate(invalidExecutionGoal);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.ValidationErrors?.Count() == 1);
            string error = result.ValidationErrors.First();
            Assert.AreEqual($"{ContractExtension.TimerTriggerType} must contain a field with key `cronExpression` with a valid cron expression as a value in the Target Goal: TargetGoal1.", error);
        }

        [Test]
        public void TargetGoalValidationFailsForInvalidCronExpression()
        {
            Precondition invalidTimerTriggerPrecondition = new Precondition(
                type: ContractExtension.TimerTriggerType,
                new Dictionary<string, IConvertible>()
                {
                    [ContractExtension.CronExpression] = "What Is A cron expression?"
                });

            GoalBasedSchedule invalidExecutionGoal = this.CreateExecutionGoalWithGivenPrecondition(invalidTimerTriggerPrecondition, this.successfulExperimentPrecondition);
            ValidationResult result = TimerTriggerProviderRules.Instance.Validate(invalidExecutionGoal);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.ValidationErrors?.Count() == 1);
            string error = result.ValidationErrors.First();
            Assert.AreEqual($"The cron expression in the {ContractExtension.TimerTriggerType} precondition in the Target Goal: TargetGoal1 is invalid: Invalid crontab expression: What Is A cron expression?", error);
        }

        private GoalBasedSchedule CreateExecutionGoalWithGivenPrecondition(params Precondition[] preconditions)
        {
            GoalBasedSchedule mockSchedule = this.mockFixture.Create<GoalBasedSchedule>();
            return new GoalBasedSchedule(
                    mockSchedule.ExperimentName,
                    mockSchedule.Description,
                    mockSchedule.Experiment,
                    new List<TargetGoal>
                    {
                    new (
                        name: "TargetGoal1",
                        true,
                        preconditions: new List<Precondition>(preconditions),
                        actions: new List<ScheduleAction>()
                        {
                            this.mockFixture.Create<ScheduleAction>()
                        })
                    },
                    mockSchedule.ControlGoals);
        }
    }
}