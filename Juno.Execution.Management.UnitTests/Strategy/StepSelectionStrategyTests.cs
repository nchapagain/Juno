namespace Juno.Execution.Management.Strategy
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Juno.Contracts;
    using Juno.Execution.Providers.AutoTriage;
    using Juno.Providers.Certification;
    using Juno.Providers.Diagnostics;
    using Juno.Providers.Environment;
    using Juno.Providers.Payloads;
    using Juno.Providers.Workloads;
    using Kusto.Cloud.Platform.Utils;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class StepSelectionStrategyTests
    {
        private ExperimentFixture mockFixture;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new ExperimentFixture()
                .Setup();

            this.mockFixture.ExperimentSteps.ForEach(step => step.Status = ExecutionStatus.Pending);
        }

        [Test]
        public void StepSelectionStrategyGetsExpectedInProgressStepsFromAGivenSetOfCandidateSteps_Scenario1()
        {
            // All steps are in a Pending state here. Thus, no steps should be added.
            IEnumerable<ExperimentStepInstance> selectedSteps = StepSelectionStrategy.GetNextInProgressSteps(this.mockFixture.ExperimentSteps);
            Assert.IsEmpty(selectedSteps);
        }

        [Test]
        [TestCase(ExecutionStatus.InProgress)]
        [TestCase(ExecutionStatus.InProgressContinue)]
        public void StepSelectionStrategyGetExpectedInProgressStepsFromAGivenSetOfCandidateSteps_Scenario2(ExecutionStatus status)
        {
            // One or more steps are InProgress
            this.mockFixture.ExperimentSteps.ElementAt(0).Status = status;
            this.mockFixture.ExperimentSteps.ElementAt(1).Status = status;

            IEnumerable<ExperimentStepInstance> selectedSteps = StepSelectionStrategy.GetNextInProgressSteps(this.mockFixture.ExperimentSteps);
            Assert.IsNotEmpty(selectedSteps);
            CollectionAssert.AreEquivalent(this.mockFixture.ExperimentSteps.Take(2), selectedSteps);
        }

        [Test]
        public void StepSelectionStrategyGetsExpectedPendingStepsFromAGivenSetOfCandidateSteps_Scenario1()
        {
            // All steps are in a Pending state here. The very first Pending step should be added.
            IEnumerable<ExperimentStepInstance> selectedSteps = StepSelectionStrategy.GetNextPendingSteps(this.mockFixture.ExperimentSteps);

            Assert.IsNotEmpty(selectedSteps);
            CollectionAssert.AreEquivalent(this.mockFixture.ExperimentSteps.Take(1), selectedSteps);
        }

        [Test]
        public void StepSelectionStrategyGetsExpectedPendingStepsFromAGivenSetOfCandidateSteps_Scenario2()
        {
            // Some steps are in a Succeeded state. The very next Pending step should be added.
            this.mockFixture.ExperimentSteps.ElementAt(0).Status = ExecutionStatus.Succeeded;

            IEnumerable<ExperimentStepInstance> selectedSteps = StepSelectionStrategy.GetNextPendingSteps(this.mockFixture.ExperimentSteps);

            Assert.IsNotEmpty(selectedSteps);
            CollectionAssert.AreEquivalent(this.mockFixture.ExperimentSteps.Skip(1).Take(1), selectedSteps);
        }

        [Test]
        public void StepSelectionStrategyGetsExpectedPendingStepsFromAGivenSetOfCandidateSteps_Scenario3()
        {
            // Some steps are in a InProgressContinue state. The very next Pending step should be added.
            this.mockFixture.ExperimentSteps.ElementAt(0).Status = ExecutionStatus.InProgressContinue;

            IEnumerable<ExperimentStepInstance> selectedSteps = StepSelectionStrategy.GetNextPendingSteps(this.mockFixture.ExperimentSteps);

            Assert.IsNotEmpty(selectedSteps);
            CollectionAssert.AreEquivalent(this.mockFixture.ExperimentSteps.Skip(1).Take(1), selectedSteps);
        }

        [Test]
        public void StepSelectionStrategyGetsExpectedPendingStepsFromAGivenSetOfCandidateSteps_Scenario4()
        {
            // Some steps are in a InProgress state. In this case, no Pending steps should be added.
            this.mockFixture.ExperimentSteps.ElementAt(0).Status = ExecutionStatus.InProgress;

            IEnumerable<ExperimentStepInstance> selectedSteps = StepSelectionStrategy.GetNextPendingSteps(this.mockFixture.ExperimentSteps);
            Assert.IsEmpty(selectedSteps);
        }

        [Test]
        public void StepSelectionStrategyGetsExpectedParallelExecutionStepsFromAGivenSetOfCandidateSteps_Scenario1()
        {
            // Steps with the same sequence are executed in parallel...except where one is in a terminal
            // state.
            this.mockFixture.ExperimentSteps.ElementAt(0).Sequence = 100;
            this.mockFixture.ExperimentSteps.ElementAt(1).Sequence = 100;

            List<ExperimentStepInstance> candidateSteps = new List<ExperimentStepInstance>
            {
                this.mockFixture.ExperimentSteps.ElementAt(0)
            };

            IEnumerable<ExperimentStepInstance> selectedSteps = StepSelectionStrategy.GetMatchingParallelSteps(candidateSteps, this.mockFixture.ExperimentSteps);
            CollectionAssert.AreEquivalent(this.mockFixture.ExperimentSteps.Skip(1).Take(1), selectedSteps);
        }

        [Test]
        [TestCase(ExecutionStatus.Cancelled)]
        [TestCase(ExecutionStatus.Failed)]
        [TestCase(ExecutionStatus.Succeeded)]
        [TestCase(ExecutionStatus.SystemCancelled)]
        public void StepSelectionStrategyGetsExpectedParallelExecutionStepsFromAGivenSetOfCandidateSteps_Scenario2(ExecutionStatus status)
        {
            // Steps with the same sequence are executed in parallel...except where one is in a terminal
            // state.
            this.mockFixture.ExperimentSteps.ElementAt(0).Sequence = 100;
            this.mockFixture.ExperimentSteps.ElementAt(1).Sequence = 100;
            this.mockFixture.ExperimentSteps.ElementAt(1).Status = status; // no need to execute this one!

            List<ExperimentStepInstance> candidateSteps = new List<ExperimentStepInstance>
            {
                this.mockFixture.ExperimentSteps.ElementAt(0)
            };

            IEnumerable<ExperimentStepInstance> selectedSteps = StepSelectionStrategy.GetMatchingParallelSteps(candidateSteps, this.mockFixture.ExperimentSteps);
            Assert.IsEmpty(selectedSteps);
        }

        [Test]
        public void StepSelectionStrategyReturnsTheFirstStepWhenAllStepsArePending()
        {
            StepSelectionResult stepSelectionResult = StepSelectionStrategy.Instance.GetSteps(this.mockFixture.ExperimentSteps);

            Assert.AreEqual(1, stepSelectionResult.StepsSelected.Count());
            Assert.AreEqual(this.mockFixture.ExperimentSteps.First(), stepSelectionResult.StepsSelected.First());
        }

        [Test]
        public void StepSelectionStrategyEvaluatesAnyInProgessContinueAndInProgressStepsIfTheyArePresent()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.InProgressContinue, typeof(ExampleSetupProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.InProgress, typeof(ExampleWorkloadProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleCertificationProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleDiagnosticsProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleCleanupProvider))
            };

            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);
            StepSelectionResult stepSelectionResult = StepSelectionStrategy.Instance.GetSteps(mockExperimentSteps);

            Assert.AreEqual(2, stepSelectionResult.StepsSelected.Count());
            CollectionAssert.AreEquivalent(mockExperimentSteps.Where(step => step.Status == ExecutionStatus.InProgress || step.Status == ExecutionStatus.InProgressContinue),
                stepSelectionResult.StepsSelected);
        }

        [Test]
        public void StepSelectionStrategyEvaluatesAllParallelStepsAfterSelectingTheStepsToEvaluate()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleSetupProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.InProgress, typeof(ExampleSetupProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.InProgress, typeof(ExampleSetupProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleSetupProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleCleanupProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleCleanupProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleCleanupProvider))
            };

            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);
            mockExperimentSteps.Where(step => step.StepType == SupportedStepType.EnvironmentSetup).ForEach(step => step.Sequence = 200);

            StepSelectionResult stepSelectionResult = StepSelectionStrategy.Instance.GetSteps(mockExperimentSteps);

            Assert.AreEqual(4, stepSelectionResult.StepsSelected.Count());
            Assert.IsTrue(stepSelectionResult.StepsSelected.All(step => step.Sequence == 200));
        }

        [Test]
        public void StepSelectionStrategyEvaluatesAnyDiagnosticStepsIfThereIsAFailure()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Failed, typeof(ExampleSetupProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleDiagnosticsProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleCertificationProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleCleanupProvider))
            };

            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);
            StepSelectionResult stepSelectionResult = StepSelectionStrategy.Instance.GetSteps(mockExperimentSteps);

            Assert.AreEqual(1, stepSelectionResult.StepsSelected.Count());
            CollectionAssert.AreEqual(mockExperimentSteps.Where(step => step.StepType == SupportedStepType.Diagnostics),
                stepSelectionResult.StepsSelected);
        }

        [Test]
        public void StepSelectionStrategyEvaluatesAnyCertificationStepsIfThereIsAFailure()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Failed, typeof(ExampleSetupProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleCertificationProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleCleanupProvider))
            };

            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);
            StepSelectionResult stepSelectionResult = StepSelectionStrategy.Instance.GetSteps(mockExperimentSteps);

            Assert.AreEqual(1, stepSelectionResult.StepsSelected.Count());
            CollectionAssert.AreEqual(mockExperimentSteps.Where(step => step.StepType == SupportedStepType.Certification),
                stepSelectionResult.StepsSelected);
        }

        [Test]
        public void StepSelectionStrategyDoesNotCleanupIfThereIsACertificationFailure()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleDiagnosticsProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Failed, typeof(ExampleCertificationProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleCleanupProvider))
            };

            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);
            StepSelectionResult stepSelectionResult = StepSelectionStrategy.Instance.GetSteps(mockExperimentSteps);

            Assert.IsEmpty(stepSelectionResult.StepsSelected);
        }

        [Test]
        public void StepSelectionStrategyDoesNotSelectCleanupStepsWhenPreviousStepsHaveNotCompleted()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.InProgressContinue, typeof(ExampleWorkloadProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.InProgressContinue, typeof(ExampleWorkloadProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExamplePayloadProvider)),

                // This step would normally be selected as the next Pending step. However, it is a cleanup step and
                // thus should not be unless there are previously Failed steps.
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleCleanupProvider)) 
            };

            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);
            StepSelectionResult stepSelectionResult = StepSelectionStrategy.Instance.GetSteps(mockExperimentSteps);

            // Only the workload provider steps should be selected here.
            Assert.IsTrue(stepSelectionResult.StepsSelected.Count() == 2);
            Assert.IsTrue(stepSelectionResult.StepsSelected.Select(step => step.StepType == SupportedStepType.Workload).Count() == 2);
        }

        [Test]
        public void StepSelectionStrategySelectsCleanupStepsWhenAnyPreviousStepsHaveFailed()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Failed, typeof(ExampleWorkloadProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.InProgressContinue, typeof(ExampleWorkloadProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExamplePayloadProvider)),

                // This step would normally be selected as the next Pending step. However, it is a cleanup step and
                // thus should not be unless there are previously Failed steps.
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleCleanupProvider))
            };

            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);
            StepSelectionResult stepSelectionResult = StepSelectionStrategy.Instance.GetSteps(mockExperimentSteps);

            Assert.IsTrue(stepSelectionResult.StepsSelected.Count() == 1);
            Assert.IsTrue(stepSelectionResult.StepsSelected.First().StepType == SupportedStepType.EnvironmentCleanup);
        }

        [Test]
        public void StepSelectionStrategySelectCleanupStepsIfAllPreviousStepsHaveSucceeded()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleWorkloadProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleWorkloadProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExamplePayloadProvider)),

                // This step would normally be selected as the next Pending step. However, it is a cleanup step and
                // thus should not be unless there are previously Failed steps.
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleCleanupProvider))
            };

            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);
            StepSelectionResult stepSelectionResult = StepSelectionStrategy.Instance.GetSteps(mockExperimentSteps);

            Assert.IsTrue(stepSelectionResult.StepsSelected.Count() == 1);
            Assert.IsTrue(stepSelectionResult.StepsSelected.First().StepType == SupportedStepType.EnvironmentCleanup);
        }

        [Test]
        public void StepSelectionStrategySelectsAutoTriageStepsWhenAllOtherStepsHaveCompleted()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleWorkloadProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleWorkloadProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExamplePayloadProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCleanupProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(AutoTriageProvider))
            };

            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);
            StepSelectionResult stepSelectionResult = StepSelectionStrategy.Instance.GetSteps(mockExperimentSteps);

            Assert.IsTrue(stepSelectionResult.StepsSelected.Count() == 1);
            Assert.AreEqual(mockExperimentSteps.ElementAt(7), stepSelectionResult.StepsSelected.First());
        }

        [Test]
        public void StepSelectionStrategySelectsAutoTriageStepsWhenAllOtherCleanupStepsHaveCompleted()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.InProgressContinue, typeof(ExampleWorkloadProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.InProgressContinue, typeof(ExampleWorkloadProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Failed, typeof(ExamplePayloadProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCleanupProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCleanupProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(AutoTriageProvider))
            };

            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);
            StepSelectionResult stepSelectionResult = StepSelectionStrategy.Instance.GetSteps(mockExperimentSteps);

            Assert.IsTrue(stepSelectionResult.StepsSelected.Count() == 1);
            Assert.AreEqual(mockExperimentSteps.ElementAt(8), stepSelectionResult.StepsSelected.First());
        }

        [Test]
        public void StepSelectionStrategyDoesNotMissCleanupStepsBecauseOfTheExistenceOfAutoTriageSteps_Scenario1()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleWorkloadProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleWorkloadProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExamplePayloadProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleCleanupProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(AutoTriageProvider))
            };

            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);
            StepSelectionResult stepSelectionResult = StepSelectionStrategy.Instance.GetSteps(mockExperimentSteps);

            Assert.IsTrue(stepSelectionResult.StepsSelected.Count() == 1);
            Assert.AreEqual(mockExperimentSteps.ElementAt(6), stepSelectionResult.StepsSelected.First());
        }

        [Test]
        public void StepSelectionStrategyDoesNotMissCleanupStepsBecauseOfTheExistenceOfAutoTriageSteps_Scenario2()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Failed, typeof(ExampleWorkloadProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.InProgressContinue, typeof(ExampleWorkloadProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExamplePayloadProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleCleanupProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(AutoTriageProvider))
            };

            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);
            StepSelectionResult stepSelectionResult = StepSelectionStrategy.Instance.GetSteps(mockExperimentSteps);

            Assert.IsTrue(stepSelectionResult.StepsSelected.Count() == 1);
            Assert.AreEqual(mockExperimentSteps.ElementAt(6), stepSelectionResult.StepsSelected.First());
        }

        [Test]
        public void StepSelectionStrategyDoesNotSelectAutoTriageStepsWhenAllOtherStepsHaveNotCompleted_Scenario1()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.InProgressContinue, typeof(ExampleWorkloadProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.InProgressContinue, typeof(ExampleWorkloadProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExamplePayloadProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleCleanupProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(AutoTriageProvider))
            };

            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);
            StepSelectionResult stepSelectionResult = StepSelectionStrategy.Instance.GetSteps(mockExperimentSteps);

            Assert.IsNotEmpty(stepSelectionResult.StepsSelected);
            CollectionAssert.DoesNotContain(stepSelectionResult.StepsSelected, mockExperimentSteps.ElementAt(7));
        }

        [Test]
        public void StepSelectionStrategyDoesNotSelectAutoTriageStepsWhenAllOtherStepsHaveNotCompleted_Scenario2()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleWorkloadProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleWorkloadProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExamplePayloadProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleCleanupProvider)),
                new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(AutoTriageProvider))
            };

            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);
            StepSelectionResult stepSelectionResult = StepSelectionStrategy.Instance.GetSteps(mockExperimentSteps);

            Assert.IsNotEmpty(stepSelectionResult.StepsSelected);
            CollectionAssert.DoesNotContain(stepSelectionResult.StepsSelected, mockExperimentSteps.ElementAt(7));
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
