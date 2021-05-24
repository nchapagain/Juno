namespace Juno.Execution.Management.Strategy
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Juno.Contracts;
    using Juno.Providers.Environment;
    using Juno.Providers.Payloads;
    using Juno.Providers.Workloads;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class EvaluateConditionalFlowOnFailureTests
    {
        private ExperimentFixture mockFixture;
        private EvaluateConditionalFlowOnFailure evaluateConditionalFlowOnFailure = new EvaluateConditionalFlowOnFailure();

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new ExperimentFixture()
                .Setup();

            this.mockFixture.ExperimentSteps.ForEach(step => step.Status = ExecutionStatus.Pending);
        }

        [Test]
        public void EvaluateConditionalFlowOnFailureStrategyReturnsNoStepsWhenThereAreNoStepsWithOnFailureExecuteBlock()
        {
            var steps = new List<StepInfo>()
            {
              new StepInfo(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider), new ExperimentFlow(null, null)),
              new StepInfo(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider), new ExperimentFlow(null, null)),
              new StepInfo(ExecutionStatus.Pending, typeof(ExampleSetupProvider), new ExperimentFlow(null, null)),
              new StepInfo(ExecutionStatus.Pending, typeof(ExampleWorkloadProvider), new ExperimentFlow(null, null)),
              new StepInfo(ExecutionStatus.Pending, typeof(ExamplePayloadProvider), new ExperimentFlow(null, null)),
              new StepInfo(ExecutionStatus.Pending, typeof(ExamplePayloadProvider), new ExperimentFlow(null, null)),
              new StepInfo(ExecutionStatus.Pending, typeof(ExampleCleanupProvider), new ExperimentFlow(null, null))
            };
            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);

            StepSelectionResult stepSelectionResult = this.evaluateConditionalFlowOnFailure.GetSteps(mockExperimentSteps);

            Assert.IsTrue(stepSelectionResult.ContinueSelection);
            Assert.IsEmpty(stepSelectionResult.StepsSelected);
        }

        [Test]
        public void EvaluateConditionalFlowOnFailureStrategyReturnsNoStepsWhenAStepWithOnFailureExecuteBlockIsPending()
        {
            var steps = new List<StepInfo>()
            {
              new StepInfo(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider), new ExperimentFlow(null, null)),
              new StepInfo(ExecutionStatus.Pending, typeof(ExampleSetupProvider), new ExperimentFlow(null, "Block A")),
              new StepInfo(ExecutionStatus.Pending, typeof(ExampleSetupProvider), new ExperimentFlow(null, null)),
              new StepInfo(ExecutionStatus.Pending, typeof(ExampleWorkloadProvider), new ExperimentFlow(null, null)),
              new StepInfo(ExecutionStatus.Pending, typeof(ExamplePayloadProvider), new ExperimentFlow("Block A", null)),
              new StepInfo(ExecutionStatus.Pending, typeof(ExamplePayloadProvider), new ExperimentFlow("Block A", null)),
              new StepInfo(ExecutionStatus.Pending, typeof(ExampleCleanupProvider), new ExperimentFlow(null, null))
            };
            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);

            StepSelectionResult stepSelectionResult = this.evaluateConditionalFlowOnFailure.GetSteps(mockExperimentSteps);

            Assert.IsTrue(stepSelectionResult.ContinueSelection);
            Assert.IsEmpty(stepSelectionResult.StepsSelected);
        }

        [Test]
        public void EvaluateConditionalFlowOnFailureStrategyReturnsNoStepsWhenAStepWithOnFailureExecuteBlockHasSucceeded()
        {
            var steps = new List<StepInfo>()
            {
              new StepInfo(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider), new ExperimentFlow(null, null)),
              new StepInfo(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider), new ExperimentFlow(null, "Block A")),
              new StepInfo(ExecutionStatus.Pending, typeof(ExampleSetupProvider), new ExperimentFlow(null, null)),
              new StepInfo(ExecutionStatus.Pending, typeof(ExampleWorkloadProvider), new ExperimentFlow(null, null)),
              new StepInfo(ExecutionStatus.Pending, typeof(ExamplePayloadProvider), new ExperimentFlow("Block A", null)),
              new StepInfo(ExecutionStatus.Pending, typeof(ExamplePayloadProvider), new ExperimentFlow("Block A", null)),
              new StepInfo(ExecutionStatus.Pending, typeof(ExampleCleanupProvider), new ExperimentFlow(null, null))
            };
            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);

            StepSelectionResult stepSelectionResult = this.evaluateConditionalFlowOnFailure.GetSteps(mockExperimentSteps);

            Assert.IsTrue(stepSelectionResult.ContinueSelection);
            Assert.IsEmpty(stepSelectionResult.StepsSelected);
        }

        [Test]
        public void EvaluateConditionalFlowOnFailureStrategyReturnsNoStepsWhenAStepWithOnFailureExecuteBlockHasFailedButBlockNameIsDifferent()
        {
            var steps = new List<StepInfo>()
            {
              new StepInfo(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider), new ExperimentFlow(null, null)),
              new StepInfo(ExecutionStatus.Failed, typeof(ExampleSetupProvider), new ExperimentFlow(null, "Block A")),
              new StepInfo(ExecutionStatus.Pending, typeof(ExampleSetupProvider), new ExperimentFlow(null, null)),
              new StepInfo(ExecutionStatus.Pending, typeof(ExampleWorkloadProvider), new ExperimentFlow(null, null)),
              new StepInfo(ExecutionStatus.Pending, typeof(ExamplePayloadProvider), new ExperimentFlow("Block B", null)),
              new StepInfo(ExecutionStatus.Pending, typeof(ExamplePayloadProvider), new ExperimentFlow("Block B", null)),
              new StepInfo(ExecutionStatus.Pending, typeof(ExampleCleanupProvider), new ExperimentFlow(null, null))
            };
            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);

            StepSelectionResult stepSelectionResult = this.evaluateConditionalFlowOnFailure.GetSteps(mockExperimentSteps);

            Assert.IsTrue(stepSelectionResult.ContinueSelection);
            Assert.IsEmpty(stepSelectionResult.StepsSelected);
        }

        [Test]
        public void EvaluateConditionalFlowOnFailureStrategyReturnsStepsWithBlockNameWhenAStepWithOnFailureExecuteBlockHasFailed()
        {
            var steps = new List<StepInfo>()
            {
              new StepInfo(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider), new ExperimentFlow(null, null)),
              new StepInfo(ExecutionStatus.Failed, typeof(ExampleSetupProvider), new ExperimentFlow(null, "Block A")),
              new StepInfo(ExecutionStatus.Pending, typeof(ExampleSetupProvider), new ExperimentFlow(null, null)),
              new StepInfo(ExecutionStatus.Pending, typeof(ExampleWorkloadProvider), new ExperimentFlow(null, null)),
              new StepInfo(ExecutionStatus.Pending, typeof(ExamplePayloadProvider), new ExperimentFlow("Block A", null)),
              new StepInfo(ExecutionStatus.Pending, typeof(ExamplePayloadProvider), new ExperimentFlow("Block A", null)),
              new StepInfo(ExecutionStatus.Pending, typeof(ExampleCleanupProvider), new ExperimentFlow(null, null))
            };
            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);

            StepSelectionResult stepSelectionResult = this.evaluateConditionalFlowOnFailure.GetSteps(mockExperimentSteps);

            Assert.IsTrue(stepSelectionResult.ContinueSelection);
            Assert.IsNotEmpty(stepSelectionResult.StepsSelected);
            Assert.AreEqual(stepSelectionResult.StepsSelected.Count(), steps.Where(step => step.Flow.BlockName == "Block A").Count());
        }

        [Test]
        public void EvaluateConditionalFlowOnFailureStrategyReturnsNoStepsWhenAStepWithOnFailureExecuteBlockHasFailedButBlockDoesNotExist()
        {
            var steps = new List<StepInfo>()
            {
              new StepInfo(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider), new ExperimentFlow(null, null)),
              new StepInfo(ExecutionStatus.Failed, typeof(ExampleSetupProvider), new ExperimentFlow(null, "Block A")),
              new StepInfo(ExecutionStatus.Pending, typeof(ExampleSetupProvider), new ExperimentFlow(null, null)),
              new StepInfo(ExecutionStatus.Pending, typeof(ExampleWorkloadProvider), new ExperimentFlow(null, null)),
              new StepInfo(ExecutionStatus.Pending, typeof(ExamplePayloadProvider), new ExperimentFlow(null, null)),
              new StepInfo(ExecutionStatus.Pending, typeof(ExamplePayloadProvider), new ExperimentFlow(null, null)),
              new StepInfo(ExecutionStatus.Pending, typeof(ExampleCleanupProvider), new ExperimentFlow(null, null))
            };
            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);

            StepSelectionResult stepSelectionResult = this.evaluateConditionalFlowOnFailure.GetSteps(mockExperimentSteps);

            Assert.IsTrue(stepSelectionResult.ContinueSelection);
            Assert.IsEmpty(stepSelectionResult.StepsSelected);
        }

        [Test]
        public void EvaluateConditionalFlowOnFailureStrategyReturnsStepsWithBlockNameWhenMultipleStepsWithOnFailureExecuteBlockHasFailed()
        {
            var steps = new List<StepInfo>()
            {
              new StepInfo(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider), new ExperimentFlow(null, null)),
              new StepInfo(ExecutionStatus.Failed, typeof(ExampleSetupProvider), new ExperimentFlow(null, "Block A")),
              new StepInfo(ExecutionStatus.Failed, typeof(ExampleSetupProvider), new ExperimentFlow(null, "Block B")),
              new StepInfo(ExecutionStatus.Pending, typeof(ExampleWorkloadProvider), new ExperimentFlow(null, null)),
              new StepInfo(ExecutionStatus.Pending, typeof(ExamplePayloadProvider), new ExperimentFlow("Block A", null)),
              new StepInfo(ExecutionStatus.Pending, typeof(ExamplePayloadProvider), new ExperimentFlow("Block B", null)),
              new StepInfo(ExecutionStatus.Pending, typeof(ExampleCleanupProvider), new ExperimentFlow(null, null))
            };
            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);

            StepSelectionResult stepSelectionResult = this.evaluateConditionalFlowOnFailure.GetSteps(mockExperimentSteps);

            Assert.IsTrue(stepSelectionResult.ContinueSelection);
            Assert.IsNotEmpty(stepSelectionResult.StepsSelected);
            Assert.AreEqual(stepSelectionResult.StepsSelected.Count(), steps.Where(step => step.Flow.BlockName == "Block A" || step.Flow.BlockName == "Block B").Count());
        }

        private List<ExperimentStepInstance> CreateSteps(List<StepInfo> stepsInfo)
        {
            int sequence = 0;
            List<ExperimentStepInstance> steps = new List<ExperimentStepInstance>();
            foreach (var item in stepsInfo)
            {
                var component = new ExperimentComponent(item.Type.FullName, "Any Name", "Any Description", "Any Group");
                component.Extensions[ContractExtension.Flow] = JToken.FromObject(item.Flow);

                ExperimentStepInstance step = this.mockFixture.CreateExperimentStep(component);
                step.Status = item.ExecutionStatus;
                step.Sequence = sequence += 100;
                steps.Add(step);
            }

            return steps;
        }
    }

    internal class StepInfo
    {
        public StepInfo(ExecutionStatus executionStatus, Type type, ExperimentFlow flow)
        {
            this.ExecutionStatus = executionStatus;
            this.Type = type;
            this.Flow = flow;
        }

        internal ExecutionStatus ExecutionStatus { get; set; }

        internal Type Type { get; set; }

        internal ExperimentFlow Flow { get; set; }
    }
}
