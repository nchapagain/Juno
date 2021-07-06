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
    public class TargetGoalRulesTests
    {
        private Fixture mockFixture;
        private GoalBasedSchedule validExecutionGoal;
        private List<Precondition> preconditions;
        private List<ScheduleAction> scheduleActions;

        [SetUp]
        public void SetupTests()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetUpGoalBasedScheduleMocks();

            this.validExecutionGoal = this.mockFixture.Create<GoalBasedSchedule>();

            this.preconditions = new List<Precondition>()
            {
                new Precondition(
                type: ContractExtension.TimerTriggerType,
                new Dictionary<string, IConvertible>()
                {
                    ["cronExpression"] = "*/20 * * * *"
                }),

                new Precondition(
                type: ContractExtension.SuccessfulExperimentsProvider,
                new Dictionary<string, IConvertible>()
                {
                    ["targetExperimentInstances"] = 5
                })
            };

            this.scheduleActions = new List<ScheduleAction>()
            {
                new ScheduleAction(
                type: ContractExtension.SelectEnvironmentAndCreateExperimentProvider,
                new Dictionary<string, IConvertible>()
                {
                    ["metadata.workload"] = "WorkloadABC"
                })
            };
        }

        [Test]
        public void TargetGoalValidationPassesForValidExecutionGoal()
        {
            TargetGoal targetGoal = new ("myNewTargetGoal", true, this.preconditions, this.scheduleActions);

            GoalBasedSchedule executionGoal = this.CreateExecutionGoalWithGivenTargetGoal(new List<TargetGoal>() { targetGoal });
            ValidationResult result = TargetGoalRules.Instance.Validate(executionGoal);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid);
            Assert.IsTrue(result.ValidationErrors?.Any() != true);
        }

        [Test]
        public void TargetGoalValidationFailsWhenWorkloadParameterIsMissing()
        {
            var invalidScheduleActions = new List<ScheduleAction>()
            {
                new ScheduleAction(
                type: ContractExtension.SelectEnvironmentAndCreateExperimentProvider,
                new Dictionary<string, IConvertible>()
                {
                    ["testPram"] = "value",
                })
            };

            TargetGoal targetGoal = new ("myNewTargetGoal", true, this.preconditions, invalidScheduleActions);

            GoalBasedSchedule invalidExecutionGoal = this.CreateExecutionGoalWithGivenTargetGoal(new List<TargetGoal>() { targetGoal });
            ValidationResult result = TargetGoalRules.Instance.Validate(invalidExecutionGoal);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.ValidationErrors?.Count() == 1);
            string error = result.ValidationErrors.First();
        }

        [Test]
        public void TargetGoalValidationFailsWhenPreconditionIsMissingFromTargetGoal()
        {
            var invalidPreconditions = new List<Precondition>()
            {
                new Precondition(
                type: ContractExtension.TimerTriggerType,
                new Dictionary<string, IConvertible>()
                {
                    ["cronExpression"] = "*/20 * * * *",
                })
            };
            TargetGoal targetGoal = new ("myNewTargetGoal", true, invalidPreconditions, this.scheduleActions);
            GoalBasedSchedule invalidExecutionGoal = this.CreateExecutionGoalWithGivenTargetGoal(new List<TargetGoal>() { targetGoal });
            ValidationResult result = TargetGoalRules.Instance.Validate(invalidExecutionGoal);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.ValidationErrors?.Count() == 1);
            string error = result.ValidationErrors.First();
            Assert.AreEqual($"Target Goal: myNewTargetGoal must contain a Precondition of type {ContractExtension.TimerTriggerType} and {ContractExtension.SuccessfulExperimentsProvider}", error);
        }

        [Test]
        public void TargetGoalValidationFailsWhenExecutionGoalContainsDuplicateWorkloadParameter()
        {
            TargetGoal targetGoal = new ("myNewTargetGoal", true, this.preconditions, this.scheduleActions);

            GoalBasedSchedule invalidExecutionGoal = this.CreateExecutionGoalWithGivenTargetGoal(new List<TargetGoal>() { targetGoal, targetGoal });
            ValidationResult result = TargetGoalRules.Instance.Validate(invalidExecutionGoal);

            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.ValidationErrors.Count() == 1);
            string error = result.ValidationErrors.First();
            Assert.AreEqual($"TargetGoal: myNewTargetGoal contains workload that is already used for another target goal. Duplicate Workload: WorkloadABC", error);
        }

        private GoalBasedSchedule CreateExecutionGoalWithGivenTargetGoal(List<TargetGoal> targetGoals)
        {
            GoalBasedSchedule mockSchedule = this.mockFixture.Create<GoalBasedSchedule>();
            return new GoalBasedSchedule(
                    mockSchedule.ExperimentName,
                    mockSchedule.Description,
                    mockSchedule.Experiment,
                    targetGoals,
                    mockSchedule.ControlGoals);
        }
    }
}