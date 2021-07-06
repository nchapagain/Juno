namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AutoFixture;
    using Microsoft.Azure.CRC.Contracts;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class GoalBasedScheduleExtensionsTests
    {
        private Fixture mockFixture;
        private GoalBasedSchedule executionGoal;
        private TargetGoal targetGoalTemplate;
        private ScheduleAction actionTemplate;
        private ExecutionGoalParameter executionGoalParameter;
        private TargetGoalParameter targetGoalParamter;

        [SetUp]
        public void SetupTests()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetUpGoalBasedScheduleMocks();

            this.executionGoal = this.mockFixture.Create<GoalBasedSchedule>();
            this.targetGoalTemplate = this.mockFixture.Create<TargetGoal>();
            this.executionGoal.TargetGoals.Add(this.targetGoalTemplate);

            this.actionTemplate = this.mockFixture.Create<ScheduleAction>();
            this.targetGoalTemplate.Actions.Add(this.actionTemplate);

            this.targetGoalParamter = new TargetGoalParameter(this.targetGoalTemplate.Name, true);
            this.executionGoalParameter = new ExecutionGoalParameter(
                new List<TargetGoalParameter>() { this.targetGoalParamter }, 
                new Dictionary<string, IConvertible>() { { ExecutionGoalMetadata.ExperimentName, "experiment name" } });
        }

        [Test]
        public void InlinedValidatesParameters()
        {
            GoalBasedSchedule nullSchedule = null;
            ExecutionGoalParameter executionGoalMetadata = FixtureExtensions.CreateExecutionGoalMetadata().ParameterNames;
            Assert.Throws<ArgumentException>(() => nullSchedule.Inlined(executionGoalMetadata));
        }

        [Test]
        public void InlinedReturnsExpectedResultWithoutSharedParameters()
        {
            string expectedValue = Guid.NewGuid().ToString();
            this.actionTemplate.Parameters.Add("test", "$.parameters.myparameter");
            this.targetGoalParamter.Parameters.Add("myparameter", expectedValue);

            this.executionGoal.Inlined(this.executionGoalParameter);

            string actualValue = this.actionTemplate.Parameters["test"].ToString();

            Assert.AreEqual(expectedValue, actualValue);
        }

        [Test]
        public void InlinedReturnsExpectedResultWhenSharedParametersAreFilled()
        {
            string expectedValue = Guid.NewGuid().ToString();
            this.actionTemplate.Parameters.Add("test", "$.parameters.myparameter");
            this.executionGoalParameter.SharedParameters.Add("myparameter", expectedValue);

            this.executionGoal.Inlined(this.executionGoalParameter);

            string actualValue = this.actionTemplate.Parameters["test"].ToString();

            Assert.AreEqual(expectedValue, actualValue);
        }

        [Test]
        public void InlinedCopiesParametersFromTemplateIfNotPresentInExecutionGoalParameter()
        {
            string expectedKey = Guid.NewGuid().ToString();
            string expectedValue = Guid.NewGuid().ToString();
            this.executionGoal.Metadata.Add(expectedKey, expectedValue);

            GoalBasedSchedule inlinedGoal = this.executionGoal.Inlined(this.executionGoalParameter);

            Assert.IsTrue(inlinedGoal.Metadata.ContainsKey(expectedKey));
            Assert.AreEqual(inlinedGoal.Metadata[expectedKey], expectedValue);
        }

        [Test]
        public void InlinedUsesEnabledFieldFromParametersAndNotTemplate()
        {
            this.executionGoal.TargetGoals.Clear();
            this.executionGoal.TargetGoals.AddRange(Enumerable.Range(0, 5).Select(num => FixtureExtensions.CreateTargetGoal(enabled: false)));

            this.executionGoalParameter.TargetGoals.Clear();
            this.executionGoalParameter.TargetGoals.AddRange(this.executionGoal.TargetGoals.Select(tg => new TargetGoalParameter(tg.Name, true)));

            GoalBasedSchedule result = this.executionGoal.Inlined(this.executionGoalParameter);
            Assert.IsTrue(result.TargetGoals.All(tg => tg.Enabled));
        }

        [Test]
        public void InlinedDoesNotOverwriteExistingMetadata()
        {
            string expectedKey = Guid.NewGuid().ToString();
            string expectedValue = Guid.NewGuid().ToString();
            string nonExpectedValue = Guid.NewGuid().ToString();
            this.executionGoal.Metadata.Add(expectedKey, nonExpectedValue);
            this.executionGoalParameter.Metadata.Add(expectedKey, expectedValue);

            GoalBasedSchedule inlinedGoal = this.executionGoal.Inlined(this.executionGoalParameter);

            Assert.IsTrue(inlinedGoal.Metadata.ContainsKey(expectedKey));
            Assert.AreEqual(inlinedGoal.Metadata[expectedKey], expectedValue);
        }

        [Test]
        public void SharedParameterInlinesOnlyForMissingTargetGoalParameters()
        {
            string expectedValue = Guid.NewGuid().ToString();
            string expectedValue2 = Guid.NewGuid().ToString();
            this.actionTemplate.Parameters.Add("test", "$.parameters.myparameter");
            this.actionTemplate.Parameters.Add("test2", "$.parameters.myotherparameter");

            this.executionGoalParameter.SharedParameters.Add("myparameter", expectedValue);
            this.executionGoalParameter.SharedParameters.Add("myotherparameter", expectedValue);
            this.targetGoalParamter.Parameters.Add("myotherparameter", expectedValue2);

            this.executionGoal.Inlined(this.executionGoalParameter);

            string actualValue = this.actionTemplate.Parameters["test"].ToString();
            string actualValue2 = this.actionTemplate.Parameters["test2"].ToString();
            Assert.AreEqual(expectedValue, actualValue);
            Assert.AreEqual(expectedValue2, actualValue2);
        }

        [Test]
        public void GetParametersFromTemplateReturnsExpectedValue()
        {
            this.executionGoal.Parameters.Add("myParameter", "$.parameters.replaceme");
            ExecutionGoalParameter value = this.executionGoal.GetParametersFromTemplate();

            Assert.IsTrue(value.SharedParameters.ContainsKey("replaceme"));
        }

        [Test]
        public void GetParametersFromTemplateDoesNotDuplicateParameters()
        {
            string parameterReference = "$.parameters.replaceme";
            this.executionGoal.Parameters.Add("myParameter", parameterReference);
            this.actionTemplate.Parameters.Add("myActionParameter", parameterReference);

            ExecutionGoalParameter value = this.executionGoal.GetParametersFromTemplate();

            Assert.IsTrue(value.SharedParameters.ContainsKey("replaceme"));
            Assert.IsFalse(value.TargetGoals.Any(tg => tg.Parameters.ContainsKey("replaceme")));
        }

        [Test]
        public void GetParameterFromTemplateRetrievesTargetGoalSpecificParameters()
        {
            string parameterReference = "$.parameters.replaceme";
            this.actionTemplate.Parameters.Add("myActionParameter", parameterReference);

            ExecutionGoalParameter value = this.executionGoal.GetParametersFromTemplate();

            Assert.IsFalse(value.SharedParameters.ContainsKey("replaceme"));
            Assert.IsTrue(value.TargetGoals.Any(tg => tg.Parameters.ContainsKey("replaceme")));
        }

        [Test]
        public void GetParametersFromTemplateReturnsEmptyListWhenNoParametersArePresent()
        {
            ExecutionGoalParameter value = this.executionGoal.GetParametersFromTemplate();

            Assert.IsEmpty(value.SharedParameters);
            Assert.IsTrue(value.TargetGoals.All(tg => !tg.Parameters.Any()));
        }

        [Test]
        public void GetWorkLoadFromTargetGoalReturnsProperTargetGoal()
        {
            TargetGoal template = FixtureExtensions.CreateTargetGoal();
            string expectedWorkload = template.Actions.SelectMany(a => a.Parameters).First(p => p.Key.Equals("metadata.workload")).Value.ToString();
            Assert.AreEqual(expectedWorkload, template.GetWorkload());
        }

        [Test]
        public void GetWorkLoadFromTargetGoalThrowsProperErrorWhenWorkloadIsNotFound()
        {
            Goal workingGoal = FixtureExtensions.CreateTargetGoal();

            ScheduleAction scheduleAction = new ScheduleAction(
                "ScheduleActionTest",
                parameters: new Dictionary<string, IConvertible>
                {
                    ["Parameter1"] = "AnyValue",
                    ["Parameter2"] = 1234,
                    ["Parameter3"] = true
                });

            TargetGoal template = new (workingGoal.Name, true, workingGoal.Preconditions, new List<ScheduleAction>() { scheduleAction });

            Assert.Throws<SchemaException>(() => template.GetWorkload());
        }

        [Test]
        public void ExecutionGoalCannotBeInlinedWithoutAllTargetGoalParametersSpecified()
        {
            GoalBasedSchedule executionGoalTemplate = GoalBasedScheduleExtensionsTests.GetExecutionGoalTemplateWithFiveWorkLoad();
            TargetGoal oneTargetGoal = new (executionGoalTemplate.TargetGoals.FirstOrDefault());
            executionGoalTemplate.TargetGoals.Clear();
            executionGoalTemplate.TargetGoals.Add(oneTargetGoal);
            ExecutionGoalParameter executionGoalParameter = executionGoalTemplate.GetParametersFromTemplate();

            executionGoalParameter.SharedParameters.Add("targetGoalName", Guid.NewGuid().ToString());
            executionGoalParameter.SharedParameters.Add("targetInstances", "10");
            executionGoalParameter.SharedParameters.Add("experiment.name", Guid.NewGuid().ToString());
            executionGoalParameter.SharedParameters.Add("intelCpuId", Guid.NewGuid().ToString());
            executionGoalParameter.SharedParameters.Add("vmSku", Guid.NewGuid().ToString());
            // Missing generation parameter

            executionGoalParameter.TargetGoals.FirstOrDefault().Parameters.Clear();

            Assert.Throws<SchemaException>(() => executionGoalTemplate.Inlined(executionGoalParameter));
        }

        [Test]
        [TestCase("")]
        [TestCase(" ")]
        public void GetGoalValidatesStringParameters(string invalidParameter)
        {
            GoalBasedSchedule executionGoal = FixtureExtensions.CreateExecutionGoalFromTemplate();
            Assert.Throws<ArgumentException>(() => executionGoal.GetGoal(invalidParameter));
        }

        [Test]
        public void GetGoalValidatesNonStringParameters()
        {
            GoalBasedSchedule invalidParameter = null;
            Assert.Throws<ArgumentException>(() => invalidParameter.GetGoal("string"));
        }

        [Test]
        public void GetGoalReturnsExpectedGoalWhenIsTargetGoalWithoutTeamNameSuffix()
        {
            string goalName = Guid.NewGuid().ToString();
            List<TargetGoal> targetGoals = new List<TargetGoal>() { FixtureExtensions.CreateTargetGoal(goalName) };
            GoalBasedSchedule executionGoal = FixtureExtensions.CreateExecutionGoalFromTemplate(targetGoals: targetGoals);

            Goal actualGoal = executionGoal.GetGoal(goalName);
            Assert.IsNotNull(actualGoal);
            Assert.AreEqual(goalName, actualGoal.Name);
        }

        [Test]
        public void GetGoalReturnsExpectedGoalWhenIsTargetGoalWithTeamNameSuffix()
        {
            string goalName = Guid.NewGuid().ToString();
            string teamName = Guid.NewGuid().ToString();
            List<TargetGoal> targetGoals = new List<TargetGoal>() { FixtureExtensions.CreateTargetGoal(goalName) };
            Dictionary<string, IConvertible> metadata = new Dictionary<string, IConvertible>() { { ExecutionGoalMetadata.TeamName, teamName } };
            GoalBasedSchedule executionGoal = FixtureExtensions.CreateExecutionGoalFromTemplate(metadata: metadata, targetGoals: targetGoals);

            Goal actualGoal = executionGoal.GetGoal($"{goalName}-{teamName}");
            Assert.IsNotNull(actualGoal);
            Assert.AreEqual(goalName, actualGoal.Name);
        }

        [Test]
        public void GetGoalReturnsExpectedGoalWhenIsControlGoal()
        {
            string goalName = Guid.NewGuid().ToString();
            List<Goal> controlGoals = new List<Goal>() { FixtureExtensions.CreateTargetGoal(goalName) };
            GoalBasedSchedule executionGoal = FixtureExtensions.CreateExecutionGoalFromTemplate(controlGoals: controlGoals);

            Goal actualGoal = executionGoal.GetGoal(goalName);
            Assert.IsNotNull(actualGoal);
            Assert.AreEqual(goalName, actualGoal.Name);
        }

        [Test]
        public void GetGoalThrowsArgumentExceptionIfGoalIsNotPresent()
        {
            string goalName = Guid.NewGuid().ToString();
            GoalBasedSchedule executionGoal = FixtureExtensions.CreateExecutionGoalFromTemplate();

            Assert.Throws<SchedulerException>(() => executionGoal.GetGoal(goalName));
        }

        private static GoalBasedSchedule GetExecutionGoalTemplateWithFiveWorkLoad()
        {
            GoalBasedSchedule template = FixtureExtensions.CreateExecutionGoalFromTemplate();

            var targetGoalParameters = new Dictionary<string, IConvertible>()
            {
                { "experiment.name", "$.parameters.experiment.name" },
                { "metadata.intelCpuId", "$.parameters.intelCpuId" },
                { "vmSku", "$.parameters.vmSku" },
                { "metadata.generation", "$.parameters.generation" },
                { "metadata.workloadType", "thisIsARandomWorkloadType" },
                { "metadata.workload",  "thisIsARandomWorkload" }
            };

            List<ScheduleAction> scheduleActions = new List<ScheduleAction>()
            {
                new ScheduleAction("ExperimentProvider", targetGoalParameters)
            };

            List<Precondition> preconditions = new List<Precondition>()
            {
                new Precondition("TimerTriggerProvider", new Dictionary<string, IConvertible>() { { "targetExperimentInstances", "$.parameters.targetInstances" } })
            };

            List<TargetGoal> targetGoals = new List<TargetGoal>()
            {
                new ("$.parameters.targetGoalName", true, preconditions, scheduleActions, "1"),
                new ("$.parameters.targetGoalName", true, preconditions, scheduleActions, "2"),
                new ("$.parameters.targetGoalName", true, preconditions, scheduleActions, "3"),
                new ("$.parameters.targetGoalName", true, preconditions, scheduleActions, "4"),
                new ("$.parameters.targetGoalName", true, preconditions, scheduleActions, "5"),
            };

            return new GoalBasedSchedule(
                template.ExperimentName,
                template.Description,
                template.Experiment,
                targetGoals,
                template.ControlGoals,
                template.Metadata);
        }
    }
}