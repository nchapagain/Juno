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
    public class OverrideDefaultStrategyTests
    {
        private ExperimentFixture mockFixture;
        private OverrideDefaultStrategy overrideDefaultStrategy = new OverrideDefaultStrategy();

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new ExperimentFixture()
                .Setup();

            this.mockFixture.ExperimentSteps.ForEach(step => step.Status = ExecutionStatus.Pending);
        }

        [Test]
        public void OverrideDefaultStrategyReturnsNoStepsWhenThereAreNoStepsWithAnOverrideDefaultFlag()
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

            StepSelectionResult stepSelectionResult = this.overrideDefaultStrategy.GetSteps(mockExperimentSteps);

            Assert.IsTrue(stepSelectionResult.ContinueSelection);
            Assert.IsEmpty(stepSelectionResult.StepsSelected);
        }

        [Test]
        public void OverrideDefaultStrategyReturnsAllStepsWithAnOverrideDefaultFlag()
        {
            var steps = new List<StepInfo>()
            {
              new StepInfo(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider), new ExperimentFlow(null, null)),
              new StepInfo(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider), new ExperimentFlow(null, null)),
              new StepInfo(ExecutionStatus.Pending, typeof(ExampleSetupProvider), new ExperimentFlow(null, null)),
              new StepInfo(ExecutionStatus.Pending, typeof(ExampleWorkloadProvider), new ExperimentFlow(null, null)),
              new StepInfo(ExecutionStatus.Pending, typeof(ExampleCleanupProvider), new ExperimentFlow(null, null, true)),
              new StepInfo(ExecutionStatus.Pending, typeof(ExampleCleanupProvider), new ExperimentFlow(null, null, true)),
              new StepInfo(ExecutionStatus.Pending, typeof(ExamplePayloadProvider), new ExperimentFlow(null, null)),
              new StepInfo(ExecutionStatus.Pending, typeof(ExamplePayloadProvider), new ExperimentFlow(null, null))
            };
            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);

            StepSelectionResult stepSelectionResult = this.overrideDefaultStrategy.GetSteps(mockExperimentSteps);

            Assert.IsTrue(stepSelectionResult.ContinueSelection);
            Assert.IsNotEmpty(stepSelectionResult.StepsSelected);
            Assert.IsTrue(stepSelectionResult.StepsSelected.Count() == 2);
            CollectionAssert.AreEquivalent(stepSelectionResult.StepsSelected, mockExperimentSteps.Where(step => step.StepType == SupportedStepType.EnvironmentCleanup));
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
}
