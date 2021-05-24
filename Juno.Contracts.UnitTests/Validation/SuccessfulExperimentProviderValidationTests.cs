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
    public class SuccessfulExperimentProviderValidationTests
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
            ValidationResult result = SuccessfulExperimentsProviderRules.Instance.Validate(this.validExecutionGoal);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid);
            Assert.IsTrue(result.ValidationErrors?.Any() != true);
        }

        [Test]
        public void TargetGoalValidationFailsForMissingSuccessfulExperimentPrecondtion()
        {
            Precondition invalidPrecondition = this.mockFixture.Create<GoalBasedSchedule>().TargetGoals.FirstOrDefault().Preconditions.FirstOrDefault();
            GoalBasedSchedule invalidExecutionGoal = this.CreateExecutionGoalWithGivenPrecondition(this.timerTriggerPrecondition, invalidPrecondition);
            ValidationResult result = SuccessfulExperimentsProviderRules.Instance.Validate(invalidExecutionGoal);

            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.ValidationErrors.Count() == 1);
            string error = result.ValidationErrors.First();
            Assert.AreEqual($"TargetGoal: TargetGoal1 must contain a Precondition of type {ContractExtension.SuccessfulExperimentsProvider}", error);
        }

        [Test]
        public void TargetGoalValidationFailsForMissingTargetExperimentInstances()
        {
            Precondition invalidSuccessfulExperimentPrecondition = new Precondition(
                type: ContractExtension.SuccessfulExperimentsProvider,
                new Dictionary<string, IConvertible>()
                {
                    ["Not-targetExperimentInstances"] = 5
                });

            GoalBasedSchedule invalidExecutionGoal = this.CreateExecutionGoalWithGivenPrecondition(this.timerTriggerPrecondition, invalidSuccessfulExperimentPrecondition);

            ValidationResult result = SuccessfulExperimentsProviderRules.Instance.Validate(invalidExecutionGoal);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.ValidationErrors?.Count() == 1);
            string error = result.ValidationErrors.First();
            Assert.AreEqual($"{ContractExtension.SuccessfulExperimentsProvider} must contain a field with key `targetExperimentInstances` with a valid integer value in the Target Goal: TargetGoal1.", error);
        }

        [Test]
        [TestCase("")]
        [TestCase("This is an integer")]
        [TestCase(null)]
        [TestCase(0)]
        [TestCase(-1)]
        public void TargetGoalValidationFailsForInvalidTargetExperimentInstances(IConvertible value)
        {
            Precondition invalidSuccessfulExperimentPrecondition = new Precondition(
                type: ContractExtension.SuccessfulExperimentsProvider,
                new Dictionary<string, IConvertible>()
                {
                    [ContractExtension.TargetExperimentInstances] = value
                });

            GoalBasedSchedule invalidExecutionGoal = this.CreateExecutionGoalWithGivenPrecondition(this.timerTriggerPrecondition, invalidSuccessfulExperimentPrecondition);

            ValidationResult result = SuccessfulExperimentsProviderRules.Instance.Validate(invalidExecutionGoal);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.ValidationErrors?.Count() == 1);
            string error = result.ValidationErrors.First();
            Assert.AreEqual($"The {ContractExtension.TargetExperimentInstances} in the {ContractExtension.SuccessfulExperimentsProvider} precondition in the Target Goal: TargetGoal1 is invalid.", error);
        }

        private GoalBasedSchedule CreateExecutionGoalWithGivenPrecondition(Precondition timerTriggerPrecondition, Precondition successfulExperimentPrecondition)
        {
            GoalBasedSchedule mockSchedule = this.mockFixture.Create<GoalBasedSchedule>();
            return new GoalBasedSchedule(
                    mockSchedule.ExperimentName,
                    mockSchedule.ExecutionGoalId,
                    mockSchedule.Name,
                    mockSchedule.TeamName,
                    mockSchedule.Description,
                    mockSchedule.ScheduleMetadata,
                    mockSchedule.Enabled,
                    mockSchedule.Version,
                    mockSchedule.Experiment,
                    new List<Goal>
                    {
                    new Goal(
                        name: "TargetGoal1",
                        preconditions: new List<Precondition>()
                        {
                            timerTriggerPrecondition, successfulExperimentPrecondition
                        },
                        actions: new List<ScheduleAction>()
                        {
                            this.mockFixture.Create<ScheduleAction>()
                        })
                    },
                    mockSchedule.ControlGoals);
        }
    }
}