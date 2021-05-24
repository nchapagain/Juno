namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.CRC.Contracts;
    using NuGet.ContentModel;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class GoalBasedScheduleExtensionsTests
    {
        [SetUp]
        public void SetupTests()
        {
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
            string owner = "Joe@microsoft.com";
            GoalBasedSchedule executionGoalTemplate = FixtureExtensions.CreateExecutionGoalTemplate();
            string targetGoalIdA = executionGoalTemplate.TargetGoals.Where(x => x.Name == "$.parameters.targetGoal1").FirstOrDefault().Id;
            string targetGoalIdB = executionGoalTemplate.TargetGoals.Where(x => x.Name == "$.parameters.targetGoal2").FirstOrDefault().Id;

            IList<TargetGoalParameter> targetGoals = new List<TargetGoalParameter>()
            {
                new TargetGoalParameter(targetGoalIdA, "WorkloadA", new Dictionary<string, IConvertible>() { { "targetGoal1", "targetGoalOneParam" } }),
                new TargetGoalParameter(targetGoalIdB, "WorkloadA", new Dictionary<string, IConvertible>() { { "targetGoal2", "targetGoalTwoParam" } })
            };

            Dictionary<string, IConvertible> executionGoalMetadata = new Dictionary<string, IConvertible>()
            {
                { "owner", owner },
                { "newMetadataPram", "newMetadataValue" }
            };

            ExecutionGoalParameter executionGoalParameter = new ExecutionGoalParameter(
                "IdFromParameters",
                "ExperimentName",
                owner,
                true,
                targetGoals);

            executionGoalTemplate.ScheduleMetadata.Add("newMetadataPram", "newMetadataValue");
            GoalBasedSchedule actualResult = executionGoalTemplate.Inlined(executionGoalParameter);

            GoalBasedSchedule expectedResult = new GoalBasedSchedule(
                experimentName: "ExperimentName",
                executionGoalId: "IdFromParameters",
                name: "ExecutionGoalTemplate",
                teamName: "teamName",
                description: "description",
                metaData: executionGoalMetadata,
                enabled: true,
                version: "2021-01-01",
                experiment: actualResult.Experiment,
                targetGoals: new List<Goal>()
                {
                    FixtureExtensions.CreateTargetGoal("targetGoalOneParam", targetGoalIdA),
                    FixtureExtensions.CreateTargetGoal("targetGoalTwoParam", targetGoalIdB)
                },
                controlGoals: actualResult.ControlGoals);

            Assert.IsNotNull(actualResult);
            Assert.AreEqual(expectedResult, actualResult);
        }

        [Test]
        public void InlinedReturnsExpectedResultWhenSharedParametersAreFilled()
        {
            string expectedOwner = "Joe@microsoft.com";
            string expectedExecutionGoal = "MyNewExecutionGoal";
            string experimentNameInExecutionGoal = "MyNewExperimentName";
            string expectedExperimentNameInTargetGoal = "myNewExperimentIsAwesome!!!!";
            string expectedNodeCpuId = "MyNodeCpuID";
            string expectedVmSku = "myVmSku";
            string expectedGeneration = "Millennial";
            string randomParameter = "thisParameterIsRandomlyAddedByUser";
            string expectedTargetGoalName = Guid.NewGuid().ToString();

            GoalBasedSchedule executionGoalTemplate = GoalBasedScheduleExtensionsTests.GetExecutionGoalTemplateWithFiveWorkLoad();

            IList<TargetGoalParameter> targetGoals = new List<TargetGoalParameter>()
            {
                new TargetGoalParameter("1", "thisIsARandomWorkload", null),
                new TargetGoalParameter("2", "thisIsARandomWorkload", null),
                new TargetGoalParameter("3", "thisIsARandomWorkload", null),
                new TargetGoalParameter("4", "thisIsARandomWorkload", null),
                new TargetGoalParameter("5", "thisIsARandomWorkload", null),
            };

            var sharedParameters = new Dictionary<string, IConvertible>()
            {
                { "targetGoalName", expectedTargetGoalName },
                { "targetInstances", "10" },
                { "experiment.name", expectedExperimentNameInTargetGoal },
                { "intelCpuId", expectedNodeCpuId },
                { "vmSku", expectedVmSku },
                { "generation", expectedGeneration },
                { randomParameter, "ItShouldBeSkipped" }
            };

            ExecutionGoalParameter executionGoalParameter = new ExecutionGoalParameter(
                expectedExecutionGoal,
                experimentNameInExecutionGoal,
                expectedOwner,
                false,
                targetGoals,
                sharedParameters);

            GoalBasedSchedule actualResult = executionGoalTemplate.Inlined(executionGoalParameter);

            Assert.IsNotNull(actualResult);
            Assert.AreEqual(actualResult.ExecutionGoalId, expectedExecutionGoal);
            Assert.AreEqual(actualResult.ExperimentName, experimentNameInExecutionGoal);
            Assert.IsTrue(actualResult.ScheduleMetadata.ContainsKey("owner"));
            Assert.AreEqual(actualResult.ScheduleMetadata["owner"], expectedOwner);
            Assert.IsFalse(actualResult.Enabled);
            Assert.IsNotEmpty(actualResult.TargetGoals);
            Assert.AreEqual(actualResult.TargetGoals.Count, targetGoals.Count);

            foreach (Goal targetGoal in actualResult.TargetGoals)
            {
                Assert.AreEqual(targetGoal.Name, expectedTargetGoalName);
                Assert.AreEqual("thisIsARandomWorkload", targetGoal.GetWorkLoadFromTargetGoal());

                Dictionary<string, IConvertible> preconditionParameter = targetGoal.Preconditions.FirstOrDefault().Parameters;
                Assert.IsTrue(!preconditionParameter.ContainsKey(randomParameter));
                Assert.AreEqual(preconditionParameter["targetExperimentInstances"], "10");

                Dictionary<string, IConvertible> actionsParameter = targetGoal.Actions.FirstOrDefault().Parameters;
                Assert.IsTrue(!actionsParameter.ContainsKey(randomParameter));
                Assert.AreEqual(actionsParameter["experiment.name"], expectedExperimentNameInTargetGoal);
                Assert.AreEqual(actionsParameter["metadata.intelCpuId"], expectedNodeCpuId);
                Assert.AreEqual(actionsParameter["metadata.generation"], expectedGeneration);
                Assert.AreEqual(actionsParameter["vmSku"], expectedVmSku);
            }
        }

        [Test]
        public void SharedParameterInlinesOnlyForMissingTargetGoalParameters()
        {
            string expectedOwner = "Joe@microsoft.com";
            string expectedExecutionGoal = "MyNewExecutionGoal";
            string workload = "thisIsARandomWorkload";
            string experimentNameInExecutionGoal = "MyNewExperimentName";
            string expectedExperimentNameInTargetGoal = "myNewExperimentIsAwesome!!!!";
            string expectedNodeCpuId = "MyNodeCpuID";
            string expectedVmSku = "myVmSku";
            string expectedGeneration = "Millennial";
            string randomParameter = "thisParameterIsRandomlyAddedByUser";
            string expectedTargetGoalName = Guid.NewGuid().ToString();

            GoalBasedSchedule executionGoalTemplate = GoalBasedScheduleExtensionsTests.GetExecutionGoalTemplateWithFiveWorkLoad();

            var targetGoalParameterWithSuppliedKeyValuesPairs = new TargetGoalParameter("3", workload, new Dictionary<string, IConvertible>()
                {
                    { "targetGoalName", Guid.NewGuid().ToString() },
                    { "targetInstances", 15 },
                    { "experiment.name", Guid.NewGuid().ToString() },
                    { "intelCpuId", Guid.NewGuid().ToString() },
                   // { "vmSku", expectedVmSku }, // expecting this key-value pair  to be supplied from shared parameter
                   // { "generation", expectedGeneration }, // expecting this key-value pair to be supplied from shared parameter
                    { "Gibberish", "ItShouldBeSkipped" }
                });

            var targetGoalParameterWithNullOrEmptyValues = new TargetGoalParameter("4", workload, new Dictionary<string, IConvertible>()
                {
                    { "targetGoalName", null },
                    { "targetInstances", string.Empty },
                    { "experiment.name", " " },
                    { "intelCpuId", "   " }
                });

            IList<TargetGoalParameter> targetGoals = new List<TargetGoalParameter>()
            {
                new TargetGoalParameter("1", workload, null),
                new TargetGoalParameter("2", workload, null),
                targetGoalParameterWithSuppliedKeyValuesPairs,
                targetGoalParameterWithNullOrEmptyValues,
                new TargetGoalParameter("10", workload, null) // Should not be present, since id: 10, doesn't exist.
            };

            var sharedParameters = new Dictionary<string, IConvertible>()
            {
                { "targetGoalName", expectedTargetGoalName },
                { "targetInstances", "10" },
                { "experiment.name", expectedExperimentNameInTargetGoal },
                { "intelCpuId", expectedNodeCpuId },
                { "vmSku", expectedVmSku },
                { "generation", expectedGeneration },
                { randomParameter, "ItShouldBeSkipped" }
            };

            ExecutionGoalParameter executionGoalParameter = new ExecutionGoalParameter(
                expectedExecutionGoal,
                experimentNameInExecutionGoal,
                expectedOwner,
                true,
                targetGoals,
                sharedParameters);

            GoalBasedSchedule actualResult = executionGoalTemplate.Inlined(executionGoalParameter);

            Assert.IsNotNull(actualResult);
            Assert.IsTrue(actualResult.Enabled);
            Assert.IsNotEmpty(actualResult.TargetGoals);
            Assert.AreEqual(actualResult.TargetGoals.Count, 4);

            var targetGoalWithIdThree = actualResult.TargetGoals.Where(x => x.Id == "3");
            Assert.IsNotEmpty(targetGoalWithIdThree);
            foreach (Goal targetGoal in targetGoalWithIdThree)
            {
                Assert.AreEqual(targetGoal.Name, targetGoalParameterWithSuppliedKeyValuesPairs.Parameters["targetGoalName"]);
                Assert.AreEqual(workload, targetGoal.GetWorkLoadFromTargetGoal());

                Dictionary<string, IConvertible> preconditionParameter = targetGoal.Preconditions.FirstOrDefault().Parameters;
                Assert.IsTrue(!preconditionParameter.ContainsKey(randomParameter));
                Assert.AreEqual(preconditionParameter["targetExperimentInstances"], "15");

                Dictionary<string, IConvertible> actionsParameter = targetGoal.Actions.FirstOrDefault().Parameters;
                Assert.IsTrue(!actionsParameter.ContainsKey(randomParameter));
                Assert.AreEqual(actionsParameter["experiment.name"], targetGoalParameterWithSuppliedKeyValuesPairs.Parameters["experiment.name"]);
                Assert.AreEqual(actionsParameter["metadata.intelCpuId"], targetGoalParameterWithSuppliedKeyValuesPairs.Parameters["intelCpuId"]);
                Assert.AreEqual(actionsParameter["metadata.generation"], expectedGeneration);
                Assert.AreEqual(actionsParameter["vmSku"], expectedVmSku);
            }

            var targetGoalWithIdOneTwoFour = actualResult.TargetGoals.Where(x => x.Id != "3");
            Assert.IsNotEmpty(targetGoalWithIdOneTwoFour);
            foreach (Goal targetGoal in targetGoalWithIdOneTwoFour)
            {
                Assert.AreEqual(targetGoal.Name, expectedTargetGoalName);
                Assert.AreEqual(workload, targetGoal.GetWorkLoadFromTargetGoal());

                Dictionary<string, IConvertible> preconditionParameter = targetGoal.Preconditions.FirstOrDefault().Parameters;
                Assert.IsTrue(!preconditionParameter.ContainsKey(randomParameter));
                Assert.AreEqual(preconditionParameter["targetExperimentInstances"], "10");

                Dictionary<string, IConvertible> actionsParameter = targetGoal.Actions.FirstOrDefault().Parameters;
                Assert.IsTrue(!actionsParameter.ContainsKey(randomParameter));
                Assert.AreEqual(actionsParameter["experiment.name"], expectedExperimentNameInTargetGoal);
                Assert.AreEqual(actionsParameter["metadata.intelCpuId"], expectedNodeCpuId);
                Assert.AreEqual(actionsParameter["metadata.generation"], expectedGeneration);
                Assert.AreEqual(actionsParameter["vmSku"], expectedVmSku);
            }
        }

        [Test]
        public void GoalBasedScheduleInlinedForOneTargetGoalReturnsExpectedResult()
        {
            string owner = "Joe@microsoft.com";
            GoalBasedSchedule executionGoalTemplate = FixtureExtensions.CreateExecutionGoalTemplate();
            string targetGoalId = executionGoalTemplate.TargetGoals.Where(x => x.Name == "$.parameters.targetGoal1").FirstOrDefault().Id;

            IList<TargetGoalParameter> targetGoals = new List<TargetGoalParameter>()
            {
                new TargetGoalParameter(targetGoalId, "WorkloadA", new Dictionary<string, IConvertible>() { { "targetGoal1", "IAmTargetGoalA" } }),
                new TargetGoalParameter(targetGoalId, "WorkloadA", new Dictionary<string, IConvertible>() { { "targetGoal4", "IAmTargetGoalB" } }),
                new TargetGoalParameter(targetGoalId, "WorkloadA", new Dictionary<string, IConvertible>() { { "targetGoal5", "IAmTargetGoalC" } })
            };

            Dictionary<string, IConvertible> executionGoalMetadata = new Dictionary<string, IConvertible>() { { "owner", owner } };

            ExecutionGoalParameter executionGoalParameter = new ExecutionGoalParameter(
                "ThisIsNewExecutionGoalId",
                "ThisIsNewExperimentName",
                owner,
                true,
                targetGoals);

            GoalBasedSchedule actualResult = executionGoalTemplate.Inlined(executionGoalParameter);

            GoalBasedSchedule expectedResult = new GoalBasedSchedule(
                experimentName: "ThisIsNewExperimentName",
                executionGoalId: "ThisIsNewExecutionGoalId",
                name: "ExecutionGoalTemplate",
                teamName: "teamName",
                description: "description",
                metaData: executionGoalMetadata,
                enabled: true,
                version: "2021-01-01",
                experiment: actualResult.Experiment,
                targetGoals: new List<Goal>()
                {
                    FixtureExtensions.CreateTargetGoal("IAmTargetGoalA")
                },
                controlGoals: actualResult.ControlGoals);

            Assert.IsNotNull(actualResult);
            Assert.AreEqual(expectedResult, actualResult);
        }

        [Test]
        public void GetParametersFromTemplateReturnsExpectedValue()
        {
            GoalBasedSchedule executionGoalTemplate = FixtureExtensions.CreateExecutionGoalTemplate();
            string targetGoalIdA = executionGoalTemplate.TargetGoals.Where(x => x.Name == "$.parameters.targetGoal1").FirstOrDefault().Id;
            string targetGoalIdB = executionGoalTemplate.TargetGoals.Where(x => x.Name == "$.parameters.targetGoal2").FirstOrDefault().Id;

            ExecutionGoalParameter expectedResult = new ExecutionGoalParameter(
                "$.parameters.executionGoalId",
                "ExperimentName",
                executionGoalTemplate.TeamName,
                executionGoalTemplate.Enabled,
                new List<TargetGoalParameter>()
                {
                    new TargetGoalParameter(targetGoalIdA, "WorkloadA", new Dictionary<string, IConvertible>() { ["targetGoal1"] = string.Empty }),
                    new TargetGoalParameter(targetGoalIdB, "WorkloadA", new Dictionary<string, IConvertible>() { ["targetGoal2"] = string.Empty })
                });

            ExecutionGoalParameter actualResult = executionGoalTemplate.GetParametersFromTemplate();
            Assert.AreEqual(expectedResult, actualResult);
        }

        [Test]
        public void GetParamtersFromTemplateReturnsEmptyListWhenNoParametersArePresent()
        {
            GoalBasedSchedule template = FixtureExtensions.CreateExecutionGoalFromTemplate();
            string targetGoalId = template.TargetGoals.Where(x => x.Name == "TargetGoal1").FirstOrDefault().Id;

            ExecutionGoalParameter expectedResult = new ExecutionGoalParameter(
                "MockExecutionGoal.json",
                "MockExperiment",
                template.TeamName,
                template.Enabled,
                new List<TargetGoalParameter>()
                {
                    new TargetGoalParameter(targetGoalId, "WorkloadA", new Dictionary<string, IConvertible>())
                },
                template.Parameters);

            ExecutionGoalParameter actualResult = template.GetParametersFromTemplate();
            Assert.AreEqual(expectedResult, actualResult);
        }

        [Test]
        public void GetWorkLoadFromTargetGoalReturnsProperTargetGoal()
        {
            Goal template = FixtureExtensions.CreateTargetGoal();
            Assert.AreEqual("WorkloadA", template.GetWorkLoadFromTargetGoal());
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

            Goal template = new Goal(workingGoal.Name, workingGoal.Preconditions, new List<ScheduleAction>() { scheduleAction });

            Assert.Throws<SchemaException>(() => template.GetWorkLoadFromTargetGoal());
        }

        [Test]
        public void ExecutionGoalCannotBeInlinedWithoutAllTargetGoalParametersSpecified()
        {
            GoalBasedSchedule executionGoalTemplate = GoalBasedScheduleExtensionsTests.GetExecutionGoalTemplateWithFiveWorkLoad();
            Goal oneTargetGoal = new Goal(executionGoalTemplate.TargetGoals.FirstOrDefault());
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
        public void ExecutionGoalSummaryCanGetSharedParametersFromTemplate()
        {
            GoalBasedSchedule template = FixtureExtensions.CreateExecutionGoalFromTemplate();
            var parameters = new Dictionary<string, IConvertible>()
            {
                { "experiment.name", "$.parameters.experiment.name" },
                { "metadata.intelCpuId", "$.parameters.intelCpuId" },
                { "vmSku", "$.parameters.vmSku" },
                { "metadata.generation", "$.parameters.generation" }
            };

            GoalBasedSchedule goalBasedSchedule =
                new GoalBasedSchedule(
                    template.ExperimentName,
                    template.ExecutionGoalId,
                    template.Name,
                    template.TeamName,
                    template.Description,
                    template.ScheduleMetadata,
                    true,
                    template.Version,
                    template.Experiment,
                    template.TargetGoals,
                    template.ControlGoals,
                    parameters);

            var actualValue = goalBasedSchedule.GetSharedParametersFromTemplate();
            List<string> expectedValue = parameters.Values.Select(x => x.ToString().Replace("$.parameters.", string.Empty)).ToList<string>();
            expectedValue.Sort();

            Assert.IsNotNull(actualValue);
            Assert.AreEqual(actualValue.Keys, expectedValue);
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
            List<Goal> targetGoals = new List<Goal>() { FixtureExtensions.CreateTargetGoal(goalName) };
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
            List<Goal> targetGoals = new List<Goal>() { FixtureExtensions.CreateTargetGoal(goalName) };
            GoalBasedSchedule executionGoal = FixtureExtensions.CreateExecutionGoalFromTemplate(teamName: teamName, targetGoals: targetGoals);

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

        [Test]
        public void ArePreconditionsSatisfiedValidatesParameters()
        {
            List<PreconditionResult> results = null;
            Assert.Throws<ArgumentException>(() => results.ArePreconditionsSatisfied());
        }

        [Test]
        public void ArePreconditionsSatisfiedReturnsExpectedResultIfListIsEmpty()
        {
            List<PreconditionResult> results = new List<PreconditionResult>();
            bool result = results.ArePreconditionsSatisfied();

            Assert.IsTrue(result);
        }

        [Test]
        public void ArePreconditionsSatisfiedReturnsExpectedResultIfAllResultsAreSatisfiedAndSucceededExecution()
        {
            PreconditionResult successfulInstance = new PreconditionResult(ExecutionStatus.Succeeded, true);
            List<PreconditionResult> results = new List<PreconditionResult>() { successfulInstance, successfulInstance, successfulInstance };

            bool result = results.ArePreconditionsSatisfied();
            Assert.IsTrue(result);
        }

        [Test]
        public void ArePrecondtionsSatsifedReturnsExpectedResultIfSomeResultsAreNotSatisfied()
        {
            PreconditionResult failedInstance = new PreconditionResult(ExecutionStatus.Succeeded, false);
            PreconditionResult successfulInstance = new PreconditionResult(ExecutionStatus.Succeeded, true);
            List<PreconditionResult> results = new List<PreconditionResult>() { failedInstance, successfulInstance, failedInstance };

            bool result = results.ArePreconditionsSatisfied();
            Assert.IsFalse(result);
        }

        [Test]
        public void ArePreconditionSatsifedReturnsExpectedResultIfSomeResultsFailedExecution()
        {
            PreconditionResult failedInstance = new PreconditionResult(ExecutionStatus.Failed, true);
            PreconditionResult successfulInstance = new PreconditionResult(ExecutionStatus.Succeeded, true);
            List<PreconditionResult> results = new List<PreconditionResult>() { failedInstance, successfulInstance, failedInstance };

            bool result = results.ArePreconditionsSatisfied();
            Assert.IsFalse(result);
        }

        [Test]
        public void ArePreconditionSatisfiedReturnsExpectedResultIfSomeResultsFailedExecutionAndAreNotSatisfied()
        {
            PreconditionResult failedInstance = new PreconditionResult(ExecutionStatus.Failed, false);
            PreconditionResult successfulInstance = new PreconditionResult(ExecutionStatus.Succeeded, true);
            List<PreconditionResult> results = new List<PreconditionResult>() { failedInstance, successfulInstance, failedInstance };

            bool result = results.ArePreconditionsSatisfied();
            Assert.IsFalse(result);
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

            List<Goal> targetGoals = new List<Goal>()
            {
                new Goal("$.parameters.targetGoalName", preconditions, scheduleActions, "1"),
                new Goal("$.parameters.targetGoalName", preconditions, scheduleActions, "2"),
                new Goal("$.parameters.targetGoalName", preconditions, scheduleActions, "3"),
                new Goal("$.parameters.targetGoalName", preconditions, scheduleActions, "4"),
                new Goal("$.parameters.targetGoalName", preconditions, scheduleActions, "5")
            };

            return new GoalBasedSchedule(
                template.ExperimentName,
                template.ExecutionGoalId,
                template.Name,
                template.TeamName,
                template.Description,
                template.ScheduleMetadata,
                template.Enabled,
                template.Version,
                template.Experiment,
                targetGoals,
                template.ControlGoals,
                null);
        }
    }
}