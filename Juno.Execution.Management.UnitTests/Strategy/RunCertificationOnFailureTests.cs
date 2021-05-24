namespace Juno.Execution.Management.Strategy
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Juno.Contracts;
    using Juno.Providers.Certification;
    using Juno.Providers.Diagnostics;
    using Juno.Providers.Environment;
    using Juno.Providers.Workloads;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class RunCertificationOnFailureTests
    {
        private ExperimentFixture mockFixture;
        private RunCertificationOnFailure runCertificationOnFailureStrategy = new RunCertificationOnFailure();

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new ExperimentFixture()
                .Setup();

            this.mockFixture.ExperimentSteps.ForEach(step => step.Status = ExecutionStatus.Pending);
        }

        [Test]
        public void RunCertificationOnFailureStrategySelectsCertificationStepsWhenThereAreFailedSteps()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Failed, typeof(ExampleWorkloadProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleCertificationProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleDiagnosticsProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleCleanupProvider))
            };
            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);

            StepSelectionResult stepSelectionResult = this.runCertificationOnFailureStrategy.GetSteps(mockExperimentSteps);

            Assert.IsFalse(stepSelectionResult.ContinueSelection);
            CollectionAssert.AreEqual(
                mockExperimentSteps.Where(step => step.StepType == SupportedStepType.Certification),
                stepSelectionResult.StepsSelected);
        }

        [Test]
        public void RunCertificationOnFailureStrategyContinuesStepSelectionWhenThereAreNoFailedSteps()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleWorkloadProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleCertificationProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleDiagnosticsProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleCleanupProvider))
            };
            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);

            StepSelectionResult stepSelectionResult = this.runCertificationOnFailureStrategy.GetSteps(mockExperimentSteps);

            Assert.IsTrue(stepSelectionResult.ContinueSelection);
            Assert.IsEmpty(stepSelectionResult.StepsSelected);
        }

        private List<ExperimentStepInstance> CreateSteps(List<KeyValuePair<ExecutionStatus, Type>> stepsInfo)
        {
            int sequence = 0;
            List<ExperimentStepInstance> steps = new List<ExperimentStepInstance>();
            foreach (var item in stepsInfo)
            {
                var component = new ExperimentComponent(item.Value.FullName, "Any Name", "Any Description", "Any Group");

                ExperimentStepInstance step = this.mockFixture.CreateExperimentStep(component);
                step.Status = item.Key;
                step.Sequence = sequence += 100;
                steps.Add(step);
            }

            return steps;
        }
    }
}
