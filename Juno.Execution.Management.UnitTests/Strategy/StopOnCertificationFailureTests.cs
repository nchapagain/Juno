namespace Juno.Execution.Management.Strategy
{
    using System;
    using System.Collections.Generic;
    using Juno.Contracts;
    using Juno.Providers.Certification;
    using Juno.Providers.Diagnostics;
    using Juno.Providers.Environment;
    using Juno.Providers.Workloads;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class StopOnCertificationFailureTests
    {
        private ExperimentFixture mockFixture;
        private StopOnCertificationFailure stopOnCertificationFailureStrategy = new StopOnCertificationFailure();

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new ExperimentFixture()
                .Setup();

            this.mockFixture.ExperimentSteps.ForEach(step => step.Status = ExecutionStatus.Pending);
        }

        [Test]
        public void StopOnCertificationFailureStrategyPreventsAnyFurtherStepSelectionWhenThereAreCertificationStepsFailed()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleWorkloadProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Failed, typeof(ExampleCertificationProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleDiagnosticsProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleCleanupProvider))
            };
            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);

            StepSelectionResult stepSelectionResult = this.stopOnCertificationFailureStrategy.GetSteps(mockExperimentSteps);

            Assert.IsFalse(stepSelectionResult.ContinueSelection);
            Assert.IsEmpty(stepSelectionResult.StepsSelected);
        }

        [Test]
        public void StopOnCertificationFailureStrategyDoesNotPreventFurtherStepSelectionSoLongAsNoCertificationStepsThemselvesFailed_Scenario1()
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
            StepSelectionResult stepSelectionResult = this.stopOnCertificationFailureStrategy.GetSteps(mockExperimentSteps);

            // This strategy does not short-circuit the continued selection of steps if a certification
            // step itself is not failed.
            Assert.IsTrue(stepSelectionResult.ContinueSelection);
            Assert.IsEmpty(stepSelectionResult.StepsSelected);
        }

        [Test]
        public void StopOnCertificationFailureStrategyDoesNotPreventFurtherStepSelectionSoLongAsNoCertificationStepsThemselvesFailed_Scenario2()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleWorkloadProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleCertificationProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleDiagnosticsProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleCleanupProvider))
            };

            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);
            StepSelectionResult stepSelectionResult = this.stopOnCertificationFailureStrategy.GetSteps(mockExperimentSteps);

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
