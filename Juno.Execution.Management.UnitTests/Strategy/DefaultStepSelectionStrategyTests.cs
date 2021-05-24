namespace Juno.Execution.Management.Strategy
{
    using System.Linq;
    using Juno.Contracts;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class DefaultStepSelectionStrategyTests
    {
        private ExperimentFixture mockFixture;
        private DefaultStepSelectionStrategy defaultStepSelectionStrategy = new DefaultStepSelectionStrategy();

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new ExperimentFixture()
                .Setup();

            this.mockFixture.ExperimentSteps.ForEach(step => step.Status = ExecutionStatus.Pending);
        }

        [Test]
        public void DefaultStepSelectionStrategySelectsCleanupStepsWhenThereAreFailedSteps()
        {
            this.mockFixture.ExperimentSteps.ElementAt(1).Status = ExecutionStatus.Failed;

            StepSelectionResult stepSelectionResult = this.defaultStepSelectionStrategy.GetSteps(this.mockFixture.ExperimentSteps);
            Assert.IsFalse(stepSelectionResult.ContinueSelection);
            Assert.IsTrue(stepSelectionResult.StepsSelected.All(step => step.StepType == SupportedStepType.EnvironmentCleanup));

            // Next pending cleanup step should be selected
            CollectionAssert.AreEquivalent(
                this.mockFixture.ExperimentSteps.Where(step => step.StepType == SupportedStepType.EnvironmentCleanup),
                stepSelectionResult.StepsSelected);
        }

        [Test]
        public void DefaultStepSelectionStrategySelectsCleanupStepsWhenAllPreviousStepsHaveSucceeded()
        {
            // All steps before the cleanup steps are Succeeded. Cleanup steps can now proceed at-will.
            this.mockFixture.ExperimentSteps
                .Where(step => step.StepType != SupportedStepType.EnvironmentCleanup).ToList()
                .ForEach(step => step.Status = ExecutionStatus.Succeeded);

            StepSelectionResult stepSelectionResult = this.defaultStepSelectionStrategy.GetSteps(this.mockFixture.ExperimentSteps);
            Assert.IsFalse(stepSelectionResult.ContinueSelection);
            Assert.IsNotEmpty(stepSelectionResult.StepsSelected);
            Assert.IsTrue(stepSelectionResult.StepsSelected.All(step => step.StepType == SupportedStepType.EnvironmentCleanup));

            // Next pending cleanup step should be selected
            CollectionAssert.AreEquivalent(
                this.mockFixture.ExperimentSteps.Where(step => step.StepType == SupportedStepType.EnvironmentCleanup),
                stepSelectionResult.StepsSelected);
        }

        [Test]
        public void DefaultStepSelectionStrategySelectsStepsOtherThanCleanupStepsWhenThereAreNoFailedSteps()
        {
            StepSelectionResult stepSelectionResult = this.defaultStepSelectionStrategy.GetSteps(this.mockFixture.ExperimentSteps);

            Assert.IsNotEmpty(stepSelectionResult.StepsSelected);
            Assert.IsFalse(stepSelectionResult.StepsSelected.Any(step => step.StepType == SupportedStepType.EnvironmentCleanup));

            CollectionAssert.AreEquivalent(this.mockFixture.ExperimentSteps
                .Where(step => step.StepType != SupportedStepType.EnvironmentCleanup), stepSelectionResult.StepsSelected);
        }

        [Test]
        public void DefaultStepSelectionStrategySelectsExpectedStepsWhenThereAreNoFailedSteps_Scenario1()
        {
            // Experiment beginning. All steps are in a Pending state
            StepSelectionResult stepSelectionResult = this.defaultStepSelectionStrategy.GetSteps(this.mockFixture.ExperimentSteps);

            // Any steps not in a terminal state and not cleanup steps are selected.
            CollectionAssert.AreEquivalent(this.mockFixture.ExperimentSteps
                .Where(step => !ExecutionResult.CompletedStatuses.Contains(step.Status) && step.StepType != SupportedStepType.EnvironmentCleanup),
                stepSelectionResult.StepsSelected);
        }

        [Test]
        public void DefaultStepSelectionStrategySelectsExpectedStepsWhenThereAreNoFailedSteps_Scenario2()
        {
            // A single InProgress step
            this.mockFixture.ExperimentSteps.ElementAt(0).Status = ExecutionStatus.Succeeded;
            this.mockFixture.ExperimentSteps.ElementAt(1).Status = ExecutionStatus.InProgress;

            StepSelectionResult stepSelectionResult = this.defaultStepSelectionStrategy.GetSteps(this.mockFixture.ExperimentSteps);

            // Any steps not in a terminal state and not cleanup steps are selected.
            CollectionAssert.AreEquivalent(this.mockFixture.ExperimentSteps
                .Where(step => !ExecutionResult.CompletedStatuses.Contains(step.Status) && step.StepType != SupportedStepType.EnvironmentCleanup),
                stepSelectionResult.StepsSelected);
        }

        [Test]
        public void DefaultStepSelectionStrategySelectsExpectedStepsWhenThereAreNoFailedSteps_Scenario3()
        {
            // This is not a scenario that would normally happen because an InProgress step must typically
            // be finished before a subsequent step proceeds. However, the logic should handle this as expected
            // should it exist due to some step selection strategy employed in the future.
            this.mockFixture.ExperimentSteps.ElementAt(0).Status = ExecutionStatus.Succeeded;
            this.mockFixture.ExperimentSteps.ElementAt(1).Status = ExecutionStatus.InProgress;
            this.mockFixture.ExperimentSteps.ElementAt(2).Status = ExecutionStatus.InProgress;

            StepSelectionResult stepSelectionResult = this.defaultStepSelectionStrategy.GetSteps(this.mockFixture.ExperimentSteps);

            // Any steps not in a terminal state and not cleanup steps are selected.
            CollectionAssert.AreEquivalent(this.mockFixture.ExperimentSteps
                .Where(step => !ExecutionResult.CompletedStatuses.Contains(step.Status) && step.StepType != SupportedStepType.EnvironmentCleanup),
                stepSelectionResult.StepsSelected);
        }

        [Test]
        public void DefaultStepSelectionStrategySelectsExpectedStepsWhenThereAreNoFailedSteps_Scenario4()
        {
            // A single InProgressContinue step
            this.mockFixture.ExperimentSteps.ElementAt(0).Status = ExecutionStatus.Succeeded;
            this.mockFixture.ExperimentSteps.ElementAt(1).Status = ExecutionStatus.InProgressContinue;

            StepSelectionResult stepSelectionResult = this.defaultStepSelectionStrategy.GetSteps(this.mockFixture.ExperimentSteps);

            // Any steps not in a terminal state and not cleanup steps are selected.
            CollectionAssert.AreEquivalent(this.mockFixture.ExperimentSteps
                .Where(step => !ExecutionResult.CompletedStatuses.Contains(step.Status) && step.StepType != SupportedStepType.EnvironmentCleanup),
                stepSelectionResult.StepsSelected);
        }

        [Test]
        public void DefaultStepSelectionStrategySelectsExpectedStepsWhenThereAreNoFailedSteps_Scenario5()
        {
            // A single InProgressContinue step
            this.mockFixture.ExperimentSteps.ElementAt(0).Status = ExecutionStatus.Succeeded;
            this.mockFixture.ExperimentSteps.ElementAt(1).Status = ExecutionStatus.InProgressContinue;
            this.mockFixture.ExperimentSteps.ElementAt(2).Status = ExecutionStatus.InProgressContinue;

            StepSelectionResult stepSelectionResult = this.defaultStepSelectionStrategy.GetSteps(this.mockFixture.ExperimentSteps);

            // Any steps not in a terminal state and not cleanup steps are selected.
            CollectionAssert.AreEquivalent(this.mockFixture.ExperimentSteps
                .Where(step => !ExecutionResult.CompletedStatuses.Contains(step.Status) && step.StepType != SupportedStepType.EnvironmentCleanup),
                stepSelectionResult.StepsSelected);
        }

        [Test]
        public void DefaultStepSelectionStrategySelectsExpectedStepsWhenThereAreNoFailedSteps_Scenario6()
        {
            // step #3 will be the next Pending step
            this.mockFixture.ExperimentSteps.ElementAt(0).Status = ExecutionStatus.Succeeded;
            this.mockFixture.ExperimentSteps.ElementAt(1).Status = ExecutionStatus.Succeeded;

            StepSelectionResult stepSelectionResult = this.defaultStepSelectionStrategy.GetSteps(this.mockFixture.ExperimentSteps);

            // Any steps not in a terminal state and not cleanup steps are selected.
            CollectionAssert.AreEquivalent(this.mockFixture.ExperimentSteps
                .Where(step => !ExecutionResult.CompletedStatuses.Contains(step.Status) && step.StepType != SupportedStepType.EnvironmentCleanup),
                stepSelectionResult.StepsSelected);
        }

        [Test]
        public void DefaultStepSelectionStrategySelectsNextPendingStepsWhenPreviousStepsAreInProgressContinue_Scenario1()
        {
            // step #2 will be the next Pending step
            this.mockFixture.ExperimentSteps.ElementAt(0).Status = ExecutionStatus.InProgressContinue;

            StepSelectionResult stepSelectionResult = this.defaultStepSelectionStrategy.GetSteps(this.mockFixture.ExperimentSteps);
            Assert.AreEqual(this.mockFixture.ExperimentSteps.ElementAt(1), stepSelectionResult.StepsSelected.ElementAt(1));
        }

        [Test]
        public void DefaultStepSelectionStrategyReturnsTheExpectedStepSelectionContinuationResponse()
        {
            // Step selection should not continue in any case.
            // Non-Cleanup steps
            StepSelectionResult stepSelectionResult = this.defaultStepSelectionStrategy.GetSteps(this.mockFixture.ExperimentSteps);
            Assert.IsFalse(stepSelectionResult.ContinueSelection);

            // Cleanup steps
            this.mockFixture.ExperimentSteps.ElementAt(1).Status = ExecutionStatus.Failed;
            stepSelectionResult = this.defaultStepSelectionStrategy.GetSteps(this.mockFixture.ExperimentSteps);
            Assert.IsFalse(stepSelectionResult.ContinueSelection);
        }
    }
}
