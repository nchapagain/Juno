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
            Goal targetGoal = new Goal("myNewTargetGoal", this.preconditions, this.scheduleActions);

            GoalBasedSchedule executionGoal = this.CreateExecutionGoalWithGivenTargetGoal(new List<Goal>() { targetGoal });
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

            Goal targetGoal = new Goal("myNewTargetGoal", this.preconditions, invalidScheduleActions);

            GoalBasedSchedule invalidExecutionGoal = this.CreateExecutionGoalWithGivenTargetGoal(new List<Goal>() { targetGoal });
            ValidationResult result = TargetGoalRules.Instance.Validate(invalidExecutionGoal);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.ValidationErrors?.Count() == 1);
            string error = result.ValidationErrors.First();
            Assert.AreEqual($"There is no parameter named: metadata.workload referred in targetGoal: myNewTargetGoal under actiontype: {ContractExtension.SelectEnvironmentAndCreateExperimentProvider}", error);
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
            Goal targetGoal = new Goal("myNewTargetGoal", invalidPreconditions, this.scheduleActions);
            GoalBasedSchedule invalidExecutionGoal = this.CreateExecutionGoalWithGivenTargetGoal(new List<Goal>() { targetGoal });
            ValidationResult result = TargetGoalRules.Instance.Validate(invalidExecutionGoal);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.ValidationErrors?.Count() == 1);
            string error = result.ValidationErrors.First();
            Assert.AreEqual($"Target Goal: myNewTargetGoal must contain a Precondition of type {ContractExtension.TimerTriggerType} and {ContractExtension.SuccessfulExperimentsProvider} for Execution Goal: {invalidExecutionGoal.Name}", error);
        }

        [Test]
        public void TargetGoalValidationFailsWhenExecutionGoalContainsDuplicateWorkloadParameter()
        {
            Goal targetGoal = new Goal("myNewTargetGoal", this.preconditions, this.scheduleActions);

            GoalBasedSchedule invalidExecutionGoal = this.CreateExecutionGoalWithGivenTargetGoal(new List<Goal>() { targetGoal, targetGoal });
            ValidationResult result = TargetGoalRules.Instance.Validate(invalidExecutionGoal);

            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.ValidationErrors.Count() == 1);
            string error = result.ValidationErrors.First();
            Assert.AreEqual($"TargetGoal: myNewTargetGoal contains workload that is already used for another target goal. Duplicate Workload: WorkloadABC", error);
        }

        [Test]
        public void TargetGoalValidationDoesnotAccountForOlderVersionOfExecutionGoal()
        {
            var oldExecutionGoal = new GoalBasedSchedule(
                this.validExecutionGoal.ExperimentName,
                this.validExecutionGoal.ExecutionGoalId,
                this.validExecutionGoal.Name,
                this.validExecutionGoal.TeamName,
                this.validExecutionGoal.Description,
                this.validExecutionGoal.ScheduleMetadata,
                this.validExecutionGoal.Enabled,
                version: "2020-07-27",
                experiment: null,
                this.validExecutionGoal.TargetGoals,
                this.validExecutionGoal.ControlGoals);

            ValidationResult result = TargetGoalRules.Instance.Validate(oldExecutionGoal);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid);
            Assert.IsTrue(result.ValidationErrors?.Any() != true);
        }

        private GoalBasedSchedule CreateExecutionGoalWithGivenTargetGoal(List<Goal> targetGoals)
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
                    targetGoals,
                    mockSchedule.ControlGoals);
        }
    }
}