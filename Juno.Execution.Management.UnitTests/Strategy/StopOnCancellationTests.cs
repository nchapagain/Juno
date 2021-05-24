namespace Juno.Execution.Management.Strategy
{
    using System.Collections.Generic;
    using System.Linq;
    using AutoFixture;
    using Juno.Contracts;
    using Juno.Providers.Cancellation;
    using Juno.Providers.Environment;
    using Juno.Providers.Payloads;
    using Juno.Providers.Workloads;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class StopOnCancellationTests
    {
        private Fixture mockFixture;
        private List<ExperimentStepInstance> mockSteps;
        private ExperimentStepInstance cancellationStep;
        private ExperimentStepInstance cancellationStep2;
        private StopOnCancellation stepSelectionStrategy;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks();

            this.mockSteps = new List<ExperimentStepInstance>
            {
                this.mockFixture.CreateExperimentStep(FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider).FullName)),
                this.mockFixture.CreateExperimentStep(FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider).FullName)),
                this.mockFixture.CreateExperimentStep(FixtureExtensions.CreateExperimentComponent(typeof(ExamplePayloadProvider).FullName))
            };

            this.mockSteps.ForEach(step => step.Status = ExecutionStatus.Pending);
            this.cancellationStep = this.mockFixture.CreateExperimentStep(FixtureExtensions.CreateExperimentComponent(typeof(ExampleCancellationProvider).FullName));
            this.cancellationStep2 = this.mockFixture.CreateExperimentStep(FixtureExtensions.CreateExperimentComponent(typeof(ExampleCancellationProvider).FullName));
            this.stepSelectionStrategy = new StopOnCancellation();
        }

        [Test]
        [TestCase(ExecutionStatus.Succeeded)]
        [TestCase(ExecutionStatus.Failed)]
        [TestCase(ExecutionStatus.Cancelled)]
        [TestCase(ExecutionStatus.SystemCancelled)]
        public void StopOnCancellationStrategyDoesNotReturnCancellationStepsThatAreAlreadyCompleted(ExecutionStatus status)
        {
            // All cancellation steps are completed
            this.mockSteps.Add(this.cancellationStep);
            this.mockSteps.ForEach(step => step.Status = ExecutionStatus.Pending);
            this.mockSteps.SetSequences();
            this.cancellationStep.Status = status;

            StepSelectionResult selectionResult = this.stepSelectionStrategy.GetSteps(this.mockSteps);

            Assert.IsTrue(selectionResult.ContinueSelection);
            Assert.IsEmpty(selectionResult.StepsSelected);
        }

        [Test]
        public void StopOnCancellationStrategyReturnsCancellationStepsEvenWhenThereAreFailedSteps()
        {
            // At least 1 step is in a Failed status.  This does not prevent the Cancellation step from executing.
            // The rest of the logic will continue on a subsequent execution as normal (e.g. Failed -> Cleanup).
            this.mockSteps.Add(this.cancellationStep);
            this.mockSteps.First().Status = ExecutionStatus.Failed;
            this.mockSteps.SetSequences();

            StepSelectionResult selectionResult = this.stepSelectionStrategy.GetSteps(this.mockSteps);

            Assert.IsFalse(selectionResult.ContinueSelection);
            Assert.IsTrue(object.ReferenceEquals(this.cancellationStep, selectionResult.StepsSelected.First()));
        }

        [Test]
        public void StopOnCancellationStrategyReturnsExpectedSteps_InTheScenarioWhereThereIsASingleCancellationStep()
        {
            // All steps are in a Pending state -> single cancellation step
            this.mockSteps.Add(this.cancellationStep);
            this.mockSteps.ForEach(step => step.Status = ExecutionStatus.Pending);
            this.mockSteps.SetSequences();

            StepSelectionResult selectionResult = this.stepSelectionStrategy.GetSteps(this.mockSteps);

            Assert.IsFalse(selectionResult.ContinueSelection);
            Assert.IsTrue(selectionResult.StepsSelected.Count() == 1);
            Assert.IsTrue(object.ReferenceEquals(this.cancellationStep, selectionResult.StepsSelected.First()));
        }

        [Test]
        public void StopOnCancellationStrategyReturnsExpectedSteps_InTheScenarioWhereThereAreMultipleCancellationStepsAllWithTheSameSequence()
        {
            // All steps are in a Pending state -> multiple cancellation steps with same sequence
            this.mockSteps.Insert(0, this.cancellationStep);
            this.mockSteps.Insert(1, this.cancellationStep2);
            this.mockSteps.ForEach(step => step.Status = ExecutionStatus.Pending);
            this.mockSteps.SetSequences();

            // Cancellation steps are the same sequence and should thus be expected to be executed
            // in-parallel.
            this.mockSteps.ElementAt(0).Sequence = 0;
            this.mockSteps.ElementAt(1).Sequence = 0;

            StepSelectionResult selectionResult = this.stepSelectionStrategy.GetSteps(this.mockSteps);

            Assert.IsFalse(selectionResult.ContinueSelection);
            Assert.IsTrue(selectionResult.StepsSelected.Count() == 2);
            Assert.IsTrue(object.ReferenceEquals(this.cancellationStep, selectionResult.StepsSelected.ElementAt(0)));
            Assert.IsTrue(object.ReferenceEquals(this.cancellationStep2, selectionResult.StepsSelected.ElementAt(1)));
        }

        [Test]
        public void StopOnCancellationStrategyReturnsExpectedSteps_InTheScenarioWhereThereAreMultipleCancellationStepsWithADifferentSequence()
        {
            // All steps are in a Pending state -> multiple cancellation steps with different sequence
            this.mockSteps.Insert(0, this.cancellationStep);
            this.mockSteps.Insert(1, this.cancellationStep2);
            this.mockSteps.ForEach(step => step.Status = ExecutionStatus.Pending);
            this.mockSteps.SetSequences();

            // Cancellation steps are the NOT the same sequence and should thus be expected to be executed
            // separately.
            this.mockSteps.ElementAt(0).Sequence = 0;
            this.mockSteps.ElementAt(1).Sequence = 1;

            StepSelectionResult selectionResult = this.stepSelectionStrategy.GetSteps(this.mockSteps);

            Assert.IsFalse(selectionResult.ContinueSelection);
            Assert.IsTrue(selectionResult.StepsSelected.Count() == 1);
            Assert.IsTrue(object.ReferenceEquals(this.cancellationStep, selectionResult.StepsSelected.First()));
        }

        [Test]
        public void StopOnCancellationStrategyReturnsExpectedSteps_InTheScenarioWhereThereAreStepsAlreadyInProgress()
        {
            // All steps are in an InProgress state
            this.mockSteps.Add(this.cancellationStep);
            this.mockSteps.ForEach(step => step.Status = ExecutionStatus.InProgress);
            this.mockSteps.SetSequences();

            StepSelectionResult selectionResult = this.stepSelectionStrategy.GetSteps(this.mockSteps);

            Assert.IsFalse(selectionResult.ContinueSelection);
            Assert.IsTrue(selectionResult.StepsSelected.Count() == 1);
            Assert.IsTrue(object.ReferenceEquals(this.cancellationStep, selectionResult.StepsSelected.First()));
        }
    }
}
