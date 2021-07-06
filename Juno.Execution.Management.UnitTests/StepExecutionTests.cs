namespace Juno.Execution.Management
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Execution.Providers.AutoTriage;
    using Juno.Execution.Providers.Demo;
    using Juno.Providers.Certification;
    using Juno.Providers.Diagnostics;
    using Juno.Providers.Environment;
    using Juno.Providers.Payloads;
    using Juno.Providers.Workloads;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class StepExecutionTests
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
        public void StepExecutionSelectsTheExpectedStepsToProcessWhenAnExperimentBegins()
        {
            // All steps are in a 'Pending' status when an experiment begins. The very first 'Pending' step
            // should be selected.
            IEnumerable<ExperimentStepInstance> nextSteps = TestStepExecution.GetNextExperimentSteps(this.mockFixture.ExperimentSteps);

            Assert.IsNotEmpty(nextSteps);
            Assert.IsTrue(nextSteps.Count() == 1);
            Assert.IsTrue(object.ReferenceEquals(nextSteps.First(), this.mockFixture.ExperimentSteps.First()));
        }

        [Test]
        public void StepExecutionSelectsTheExpectedStepsToProcessWhenPreviousStepsAreSucceeded()
        {
            // When all previous steps are 'Succeeded', the next 'Pending' step is executed.
            this.mockFixture.ExperimentSteps.ElementAt(0).Status = ExecutionStatus.Succeeded;
            this.mockFixture.ExperimentSteps.ElementAt(1).Status = ExecutionStatus.Succeeded;

            IEnumerable<ExperimentStepInstance> nextSteps = TestStepExecution.GetNextExperimentSteps(this.mockFixture.ExperimentSteps);

            Assert.IsNotEmpty(nextSteps);
            CollectionAssert.AreEqual(nextSteps, this.mockFixture.ExperimentSteps.Skip(2).Take(1));
        }

        [Test]
        public void StepExecutionDoesNotSelectAnyStepsWhenAllStepsAreSucceeded()
        {
            // Scenario:
            // All steps are 'Succeeded'.  There aren't any steps left to process when all are Succeeded.
            this.mockFixture.ExperimentSteps.ForEach(step => step.Status = ExecutionStatus.Succeeded);
            IEnumerable<ExperimentStepInstance> nextSteps = TestStepExecution.GetNextExperimentSteps(this.mockFixture.ExperimentSteps);

            Assert.IsEmpty(nextSteps);
        }

        [Test]
        public void StepExecutionDoesNotSelectAnyStepsWhenAllStepsAreEitherSucceededFailedOrCancelled()
        {
            // Scenario:
            // All steps are 'Succeeded'.  There aren't any steps left to process when all are Succeeded.
            this.mockFixture.ExperimentSteps.ForEach(step => step.Status = ExecutionStatus.Succeeded);
            this.mockFixture.ExperimentSteps.Last().Status = ExecutionStatus.Failed;
            IEnumerable<ExperimentStepInstance> nextSteps = TestStepExecution.GetNextExperimentSteps(this.mockFixture.ExperimentSteps);

            Assert.IsEmpty(nextSteps);

            this.mockFixture.ExperimentSteps.Last().Status = ExecutionStatus.Cancelled;
            nextSteps = TestStepExecution.GetNextExperimentSteps(this.mockFixture.ExperimentSteps);

            Assert.IsEmpty(nextSteps);
        }

        [Test]
        public void StepExecutionSelectsTheExpectedStepsToProcessWhenPreviousStepsAreFailed()
        {
            // When any previous steps are Failed, the logic selects cleanup steps skipping any
            // steps in-between.
            this.mockFixture.ExperimentSteps.ElementAt(0).Status = ExecutionStatus.Succeeded;
            this.mockFixture.ExperimentSteps.ElementAt(1).Status = ExecutionStatus.Failed;

            IEnumerable<ExperimentStepInstance> nextSteps = TestStepExecution.GetNextExperimentSteps(this.mockFixture.ExperimentSteps);

            Assert.IsNotEmpty(nextSteps);
            CollectionAssert.AreEquivalent(nextSteps, this.mockFixture.ExperimentSteps.Where(step => step.StepType == SupportedStepType.EnvironmentCleanup).Take(1));
        }

        [Test]
        public void StepExecutionDoesNotSelectAnyStepsWhenPreviousStepsAreFailedIfThereAreNoCleanupStepsDefined()
        {
            // When any previous steps are Failed, the logic selects cleanup steps skipping any
            // steps in-between.
            this.mockFixture.ExperimentSteps.ElementAt(0).Status = ExecutionStatus.Succeeded;
            this.mockFixture.ExperimentSteps.ElementAt(1).Status = ExecutionStatus.Failed;

            IEnumerable<ExperimentStepInstance> nextSteps = TestStepExecution.GetNextExperimentSteps(
                this.mockFixture.ExperimentSteps.Where(step => step.StepType != SupportedStepType.EnvironmentCleanup));

            Assert.IsEmpty(nextSteps);
        }

        [Test]
        public void StepExecutionSelectsTheExpectedStepsToProcessWhenPreviousStepsAreCancelled()
        {
            // When any previous steps are Cancelled, the logic selects cleanup steps skipping any
            // steps in-between.
            this.mockFixture.ExperimentSteps.ElementAt(0).Status = ExecutionStatus.Succeeded;
            this.mockFixture.ExperimentSteps.ElementAt(1).Status = ExecutionStatus.Cancelled;

            IEnumerable<ExperimentStepInstance> nextSteps = TestStepExecution.GetNextExperimentSteps(this.mockFixture.ExperimentSteps);

            Assert.IsNotEmpty(nextSteps);
            CollectionAssert.AreEquivalent(nextSteps, this.mockFixture.ExperimentSteps.Where(step => step.StepType == SupportedStepType.EnvironmentCleanup).Take(1));
        }

        [Test]
        public void StepExecutionDoesNotSelectAnyStepsWhenPreviousStepsAreCancelledIfThereAreNoCleanupStepsDefined()
        {
            // When any previous steps are Failed, the logic selects cleanup steps skipping any
            // steps in-between.
            this.mockFixture.ExperimentSteps.ElementAt(0).Status = ExecutionStatus.Succeeded;
            this.mockFixture.ExperimentSteps.ElementAt(1).Status = ExecutionStatus.Cancelled;

            IEnumerable<ExperimentStepInstance> nextSteps = TestStepExecution.GetNextExperimentSteps(
                this.mockFixture.ExperimentSteps.Where(step => step.StepType != SupportedStepType.EnvironmentCleanup));

            Assert.IsEmpty(nextSteps);
        }

        [Test]
        public void StepExecutionDoesNotSelectAnyCleanupStepsWhenThereIsNotAtLeastOneFailedStep()
        {
            this.mockFixture.ExperimentSteps.ElementAt(0).Status = ExecutionStatus.Succeeded;
            this.mockFixture.ExperimentSteps.ElementAt(1).Status = ExecutionStatus.InProgress;

            IEnumerable<ExperimentStepInstance> nextSteps = TestStepExecution.GetNextExperimentSteps(this.mockFixture.ExperimentSteps);

            Assert.IsNotEmpty(nextSteps);
            Assert.IsFalse(nextSteps.Any(step => step.StepType == SupportedStepType.EnvironmentCleanup));
            CollectionAssert.AreEquivalent(this.mockFixture.ExperimentSteps.Skip(1).Take(1), nextSteps);
        }

        [Test]
        [TestCase(ExecutionStatus.Cancelled)]
        [TestCase(ExecutionStatus.Failed)]
        [TestCase(ExecutionStatus.SystemCancelled)]
        public void StepExecutionSelectsCleanupStepsWhenThereIsAtLeastOneTerminalStep_Scenario1(ExecutionStatus stepStatus)
        {
            // In this scenario, there are no steps that are in-progress. There is 1 or more steps that are in a
            // terminal state (e.g. Failed, Cancelled, SystemCancelled).
            this.mockFixture.ExperimentSteps.ElementAt(0).Status = ExecutionStatus.Succeeded;
            this.mockFixture.ExperimentSteps.ElementAt(1).Status = stepStatus;

            IEnumerable<ExperimentStepInstance> nextSteps = TestStepExecution.GetNextExperimentSteps(this.mockFixture.ExperimentSteps);

            Assert.IsNotEmpty(nextSteps);
            Assert.IsTrue(nextSteps.All(step => step.StepType == SupportedStepType.EnvironmentCleanup));

            // The first Pending cleanup step should be selected.
            CollectionAssert.AreEquivalent(this.mockFixture.ExperimentSteps.Where(step => step.StepType == SupportedStepType.EnvironmentCleanup).Take(1), nextSteps);
        }

        [Test]
        [TestCase(ExecutionStatus.Cancelled)]
        [TestCase(ExecutionStatus.Failed)]
        [TestCase(ExecutionStatus.SystemCancelled)]
        public void StepExecutionSelectsCleanupStepsWhenThereIsAtLeastOneTerminalStep_Scenario2(ExecutionStatus stepStatus)
        {
            // In this scenario, there are steps that are in-progress. However, there is also 1 or more steps that are in a
            // terminal state (e.g. Failed, Cancelled, SystemCancelled).
            this.mockFixture.ExperimentSteps.ElementAt(0).Status = ExecutionStatus.InProgress;
            this.mockFixture.ExperimentSteps.ElementAt(1).Status = stepStatus;

            IEnumerable<ExperimentStepInstance> nextSteps = TestStepExecution.GetNextExperimentSteps(this.mockFixture.ExperimentSteps);

            Assert.IsNotEmpty(nextSteps);
            Assert.IsTrue(nextSteps.All(step => step.StepType == SupportedStepType.EnvironmentCleanup));

            // The first Pending cleanup step should be selected.
            CollectionAssert.AreEquivalent(this.mockFixture.ExperimentSteps.Where(step => step.StepType == SupportedStepType.EnvironmentCleanup).Take(1), nextSteps);
        }

        [Test]
        public void StepExecutionSelectsTheExpectedStepsToProcessWhenPreviousStepsAreFailedOrCancelled_Cleanup_Scenario1()
        {
            // When any previous steps are Failed or Cancelled, the logic selects cleanup steps skipping any
            // steps in-between. The logic for selecting cleanup steps works the same as for any steps though.
            this.mockFixture.ExperimentSteps.ElementAt(0).Status = ExecutionStatus.Succeeded;
            this.mockFixture.ExperimentSteps.ElementAt(1).Status = ExecutionStatus.Failed;

            // One of the cleanup steps is InProgress. That step should be re-evaluated but no new Pending steps should
            // be executed.
            this.mockFixture.ExperimentSteps.Where(step => step.StepType == SupportedStepType.EnvironmentCleanup)
                .First().Status = ExecutionStatus.InProgress;

            IEnumerable<ExperimentStepInstance> nextSteps = TestStepExecution.GetNextExperimentSteps(this.mockFixture.ExperimentSteps);

            Assert.IsNotEmpty(nextSteps);
            CollectionAssert.AreEquivalent(nextSteps, this.mockFixture.ExperimentSteps.Where(step => step.StepType == SupportedStepType.EnvironmentCleanup).Take(1));
        }

        [Test]
        public void StepExecutionSelectsTheExpectedStepsToProcessWhenPreviousStepsAreFailedOrCancelled_Cleanup_Scenario2()
        {
            // When any previous steps are Failed or Cancelled, the logic selects cleanup steps skipping any
            // steps in-between. The logic for selecting cleanup steps works the same as for any steps though.
            this.mockFixture.ExperimentSteps.ElementAt(0).Status = ExecutionStatus.Succeeded;
            this.mockFixture.ExperimentSteps.ElementAt(1).Status = ExecutionStatus.Failed;

            // One of the cleanup steps is InProgressContinue. That step should be re-evaluated and the next Pending step should
            // be executed.
            this.mockFixture.ExperimentSteps.Where(step => step.StepType == SupportedStepType.EnvironmentCleanup)
                .First().Status = ExecutionStatus.InProgressContinue;

            IEnumerable<ExperimentStepInstance> nextSteps = TestStepExecution.GetNextExperimentSteps(this.mockFixture.ExperimentSteps);

            Assert.IsNotEmpty(nextSteps);
            CollectionAssert.AreEquivalent(nextSteps, this.mockFixture.ExperimentSteps.Where(step => step.StepType == SupportedStepType.EnvironmentCleanup).Take(2));
        }

        [Test]
        public void StepExecutionSelectsTheExpectedStepsToProcessWhenPreviousStepsAreFailedOrCancelled_Cleanup_Scenario3()
        {
            // When any previous steps are Failed or Cancelled, the logic selects cleanup steps skipping any
            // steps in-between. The logic for selecting cleanup steps works the same as for any steps though.
            this.mockFixture.ExperimentSteps.ElementAt(0).Status = ExecutionStatus.Succeeded;
            this.mockFixture.ExperimentSteps.ElementAt(1).Status = ExecutionStatus.Failed;

            // One of the cleanup steps is Failed. The remaining cleanup steps should still be evaluated.
            this.mockFixture.ExperimentSteps.Where(step => step.StepType == SupportedStepType.EnvironmentCleanup)
                .First().Status = ExecutionStatus.Failed;

            IEnumerable<ExperimentStepInstance> nextSteps = TestStepExecution.GetNextExperimentSteps(this.mockFixture.ExperimentSteps);

            Assert.IsNotEmpty(nextSteps);
            CollectionAssert.AreEquivalent(nextSteps, this.mockFixture.ExperimentSteps.Where(step => step.StepType == SupportedStepType.EnvironmentCleanup).Skip(1).Take(1));
        }

        [Test]
        public void StepExecutionSelectsTheExpectedStepsToProcessWhenPreviousStepsAreFailedOrCancelled_Cleanup_Scenario4()
        {
            // When any step fails we must execute a certification step if it exists.
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleTipCreationProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Failed, typeof(ExamplePayloadProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExamplePayloadProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleDiagnosticsProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleCertificationProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleCleanupProvider))
            };

            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);

            IEnumerable<ExperimentStepInstance> nextSteps = TestStepExecution.GetNextExperimentSteps(mockExperimentSteps);

            Assert.IsNotEmpty(nextSteps);
            Assert.IsTrue(nextSteps.Count() == 1);
            CollectionAssert.AreEquivalent(nextSteps, mockExperimentSteps.Where(step => step.StepType == SupportedStepType.Diagnostics).Take(1));
        }

        [Test]
        public void StepExecutionSelectsTheExpectedStepsToProcessWhenPreviousStepsAreFailedOrCancelled_Cleanup_Scenario5()
        {
            // When a certification step fails we must execute any diagnostic steps and skip cleanup
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleTipCreationProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExamplePayloadProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Failed, typeof(ExamplePayloadProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleCleanupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleDiagnosticsProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleCleanupProvider))
            };

            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);
            IEnumerable<ExperimentStepInstance> nextSteps = TestStepExecution.GetNextExperimentSteps(mockExperimentSteps);

            Assert.IsNotEmpty(nextSteps);
            Assert.IsTrue(nextSteps.Count() == 1);
            CollectionAssert.AreEquivalent(nextSteps, mockExperimentSteps.Where(step => step.StepType == SupportedStepType.Diagnostics).Take(1));
        }

        [Test]
        public void StepExecutionSelectsDoesNotExecuteAnyCleanupStepsWhenCertificationStepsFail()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleTipCreationProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExamplePayloadProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExamplePayloadProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Failed, typeof(ExampleCertificationProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleDiagnosticsProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleCleanupProvider)),
            };

            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);
            IEnumerable<ExperimentStepInstance> nextSteps = TestStepExecution.GetNextExperimentSteps(mockExperimentSteps);

            Assert.IsEmpty(nextSteps);
        }

        [Test]
        public void StepExecutionSelectsACleanupStepWhenItIsPresentInTheMiddleOfTheWorkflow_1()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleTipCreationProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExamplePayloadProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExamplePayloadProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCertificationProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleDiagnosticsProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleCleanupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExamplePayloadProvider)),
            };

            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);
            mockExperimentSteps.ElementAt(5).Definition.Extensions[ContractExtension.Flow] = JToken.FromObject(new ExperimentFlow(null, null, true));
            IEnumerable<ExperimentStepInstance> nextSteps = TestStepExecution.GetNextExperimentSteps(mockExperimentSteps);

            Assert.IsNotEmpty(nextSteps);
            Assert.IsTrue(nextSteps.Count() == 1);
            CollectionAssert.AreEquivalent(nextSteps, mockExperimentSteps.Where(step => step.StepType == SupportedStepType.EnvironmentCleanup).Take(1));
        }

        [Test]
        public void StepExecutionSelectsACleanupStepWhenItIsPresentInTheMiddleOfTheWorkflow_2()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleTipCreationProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExamplePayloadProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Failed, typeof(ExamplePayloadProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExamplePayloadProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleCleanupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExamplePayloadProvider)),
            };

            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);
            mockExperimentSteps.ElementAt(5).Definition.Extensions[ContractExtension.Flow] = JToken.FromObject(new ExperimentFlow(null, null, true));
            IEnumerable<ExperimentStepInstance> nextSteps = TestStepExecution.GetNextExperimentSteps(mockExperimentSteps);

            Assert.IsNotEmpty(nextSteps);
            Assert.IsTrue(nextSteps.Count() == 1);
            CollectionAssert.AreEquivalent(nextSteps, mockExperimentSteps.Where(step => step.StepType == SupportedStepType.EnvironmentCleanup).Take(1));
        }

        [Test]
        public void StepExecutionSelectsTheExpectedStepsToProcessWhenPreviousStepsAreInProgress_Scenario1()
        {
            // Scenario:
            // There are steps InProgress.
            // When there are steps 'InProgress', they are re-evaluated. No new 'Pending' steps will be executed.
            this.mockFixture.ExperimentSteps.First().Status = ExecutionStatus.InProgress;
            IEnumerable<ExperimentStepInstance> nextSteps = TestStepExecution.GetNextExperimentSteps(this.mockFixture.ExperimentSteps);

            Assert.IsNotEmpty(nextSteps);
            Assert.IsTrue(nextSteps.Count() == 1);
            Assert.IsTrue(object.ReferenceEquals(nextSteps.First(), this.mockFixture.ExperimentSteps.First()));
        }

        [Test]
        public void StepExecutionSelectsTheExpectedStepsToProcessWhenPreviousStepsAreInProgress_Scenario2()
        {
            // Scenario:
            // More than one step InProgress
            // When there multiple steps 'InProgress', they are ALL re-evaluated. No new 'Pending' steps will be executed.
            this.mockFixture.ExperimentSteps.ElementAt(0).Status = ExecutionStatus.Succeeded;
            this.mockFixture.ExperimentSteps.ElementAt(1).Status = ExecutionStatus.InProgress;
            this.mockFixture.ExperimentSteps.ElementAt(2).Status = ExecutionStatus.InProgress;

            IEnumerable<ExperimentStepInstance> nextSteps = TestStepExecution.GetNextExperimentSteps(this.mockFixture.ExperimentSteps);

            Assert.IsNotEmpty(nextSteps);
            Assert.IsTrue(nextSteps.Count() == 2);
            CollectionAssert.AreEqual(nextSteps, this.mockFixture.ExperimentSteps.Skip(1).Take(2));
        }

        [Test]
        public void StepExecutionSelectsTheExpectedStepsToProcessWhenPreviousStepsAreInProgress_Scenario3()
        {
            // Scenario:
            // There are steps InProgress and InProgressContinue
            // When there multiple steps 'InProgress', they are ALL re-evaluated. Any steps 'InProgressContinue' are also
            // re-evaluated. However, no new 'Pending' steps will be executed.
            this.mockFixture.ExperimentSteps.ElementAt(0).Status = ExecutionStatus.Succeeded;
            this.mockFixture.ExperimentSteps.ElementAt(1).Status = ExecutionStatus.InProgressContinue;
            this.mockFixture.ExperimentSteps.ElementAt(2).Status = ExecutionStatus.InProgress;

            IEnumerable<ExperimentStepInstance> nextSteps = TestStepExecution.GetNextExperimentSteps(this.mockFixture.ExperimentSteps);

            Assert.IsNotEmpty(nextSteps);
            Assert.IsTrue(nextSteps.Count() == 2);
            CollectionAssert.AreEqual(nextSteps, this.mockFixture.ExperimentSteps.Skip(1).Take(2));
        }

        [Test]
        public void StepExecutionSelectsTheExpectedStepsToProcessWhenPreviousStepsAreInProgress_Scenario4()
        {
            // Scenario
            // There are steps InProgressContinue but none that are InProgress
            // Any steps 'InProgressContinue' are re-evaluated. And the next 'Pending' step will be executed.
            this.mockFixture.ExperimentSteps.ElementAt(0).Status = ExecutionStatus.Succeeded;
            this.mockFixture.ExperimentSteps.ElementAt(1).Status = ExecutionStatus.InProgressContinue;
            this.mockFixture.ExperimentSteps.ElementAt(2).Status = ExecutionStatus.Pending;

            IEnumerable<ExperimentStepInstance> nextSteps = TestStepExecution.GetNextExperimentSteps(this.mockFixture.ExperimentSteps);

            Assert.IsNotEmpty(nextSteps);
            CollectionAssert.AreEqual(nextSteps, this.mockFixture.ExperimentSteps.Skip(1).Take(2));
        }

        [Test]
        public void StepExecutionSelectsTheExpectedStepsToProcessWhenPreviousStepsAreInProgress_Scenario5()
        {
            // Scenario
            // There are steps InProgressContinue but none that are InProgress
            // Any steps 'InProgressContinue' are re-evaluated. And the next 'Pending' step will be executed.
            this.mockFixture.ExperimentSteps.ElementAt(0).Status = ExecutionStatus.Succeeded;
            this.mockFixture.ExperimentSteps.ElementAt(1).Status = ExecutionStatus.InProgressContinue;
            this.mockFixture.ExperimentSteps.ElementAt(2).Status = ExecutionStatus.InProgressContinue;

            IEnumerable<ExperimentStepInstance> nextSteps = TestStepExecution.GetNextExperimentSteps(this.mockFixture.ExperimentSteps);

            Assert.IsNotEmpty(nextSteps);
            CollectionAssert.AreEqual(nextSteps, this.mockFixture.ExperimentSteps.Skip(1).Take(3));
        }

        [Test]
        public void StepExecutionSelectsTheExpectedStepsToProcessWhenPreviousStepsAreSucceeded_AndParallelExecutionStepsExist()
        {
            // Setup steps for parallel execution. Parallel execution steps will have the same sequence.
            // When all previous steps are 'Succeeded', the next 'Pending' steps (of same sequence) are executed.
            this.mockFixture.ExperimentSteps.First().Status = ExecutionStatus.Succeeded;
            this.mockFixture.ExperimentSteps.Skip(1).Take(3).ToList().ForEach(step =>
            {
                step.Sequence = 200;
                step.Status = ExecutionStatus.Pending;
            });

            IEnumerable<ExperimentStepInstance> nextSteps = TestStepExecution.GetNextExperimentSteps(this.mockFixture.ExperimentSteps);

            Assert.IsNotEmpty(nextSteps);
            CollectionAssert.AreEqual(nextSteps, this.mockFixture.ExperimentSteps.Skip(1).Take(3));
        }

        [Test]
        public void StepExecutionSelectsTheExpectedStepsToProcessWhenOneOrMoreParallelExecutionStepsAreSucceededWhileOthersAreInProgress()
        {
            // Setup steps for parallel execution. Parallel execution steps will have the same sequence.
            this.mockFixture.ExperimentSteps.First().Status = ExecutionStatus.Succeeded;
            this.mockFixture.ExperimentSteps.Skip(1).Take(3).ToList().ForEach(step =>
            {
                step.Sequence = 200;
                step.Status = ExecutionStatus.Succeeded;
            });

            // 2 of the 3 steps marked to run in-parallel are Succeeded, while 1 step remains InProgress. Only
            // the InProgress steps should be returned.
            this.mockFixture.ExperimentSteps.ElementAt(3).Status = ExecutionStatus.InProgress;

            IEnumerable<ExperimentStepInstance> nextSteps = TestStepExecution.GetNextExperimentSteps(this.mockFixture.ExperimentSteps);

            Assert.IsNotEmpty(nextSteps);
            CollectionAssert.AreEqual(nextSteps, this.mockFixture.ExperimentSteps.Skip(3).Take(1));
        }

        [Test]
        public void StepExecutionSelectsTheExpectedStepsToProcessWhenPreviousStepsAreFailed_AndParallelExecutionStepsExist()
        {
            // When any previous steps are Failed, the logic selects cleanup steps skipping any
            // steps in-between. The existence of parallel execution steps should not change this.
            this.mockFixture.ExperimentSteps.First().Status = ExecutionStatus.Failed;
            this.mockFixture.ExperimentSteps.Skip(1).Take(3).ToList().ForEach(step =>
            {
                step.Sequence = 200;
                step.Status = ExecutionStatus.Pending;
            });

            IEnumerable<ExperimentStepInstance> nextSteps = TestStepExecution.GetNextExperimentSteps(this.mockFixture.ExperimentSteps);

            Assert.IsNotEmpty(nextSteps);
            CollectionAssert.AreEquivalent(nextSteps, this.mockFixture.ExperimentSteps.Where(step => step.StepType == SupportedStepType.EnvironmentCleanup).Take(1));
        }

        [Test]
        public void StepExecutionSelectsTheExpectedStepsToProcessWhenPreviousStepsAreCancelled_AndParallelExecutionStepsExist()
        {
            // When any previous steps are Cancelled, the logic selects cleanup steps skipping any
            // steps in-between. The existence of parallel execution steps should not change this.
            this.mockFixture.ExperimentSteps.First().Status = ExecutionStatus.Cancelled;
            this.mockFixture.ExperimentSteps.Skip(1).Take(3).ToList().ForEach(step =>
            {
                step.Sequence = 200;
                step.Status = ExecutionStatus.Pending;
            });

            IEnumerable<ExperimentStepInstance> nextSteps = TestStepExecution.GetNextExperimentSteps(this.mockFixture.ExperimentSteps);

            Assert.IsNotEmpty(nextSteps);
            CollectionAssert.AreEquivalent(nextSteps, this.mockFixture.ExperimentSteps.Where(step => step.StepType == SupportedStepType.EnvironmentCleanup).Take(1));
        }

        [Test]
        public void StepExecutionSelectsTheExpectedStepsToProcessWhenAnExperimentBegins_AndParallelExecutionStepsExist()
        {
            // Setup steps for parallel execution. Parallel execution steps will have the same sequence.
            // When all previous steps are 'Succeeded', the next 'Pending' steps (of same sequence) are executed.
            this.mockFixture.ExperimentSteps.Take(3).ToList().ForEach(step =>
            {
                step.Sequence = 100;
                step.Status = ExecutionStatus.Pending;
            });

            IEnumerable<ExperimentStepInstance> nextSteps = TestStepExecution.GetNextExperimentSteps(this.mockFixture.ExperimentSteps);

            Assert.IsNotEmpty(nextSteps);
            CollectionAssert.AreEqual(nextSteps, this.mockFixture.ExperimentSteps.Take(3));
        }

        [Test]
        public void StepExecutionSelectsTheExpectedStepsToProcessWhenPreviousStepsAreInProgress_AndParallelExecutionStepsExist_Scenario1()
        {
            // Scenario:
            // There are steps InProgress. Parallel execution steps exist.
            // When there are steps 'InProgress', they are re-evaluated. No new 'Pending' steps will be executed.
            this.mockFixture.ExperimentSteps.First().Status = ExecutionStatus.Succeeded;
            this.mockFixture.ExperimentSteps.ElementAt(1).Status = ExecutionStatus.Pending;
            this.mockFixture.ExperimentSteps.Skip(2).Take(3).ToList().ForEach(step =>
            {
                step.Sequence = 300;
                step.Status = ExecutionStatus.Pending;
            });

            IEnumerable<ExperimentStepInstance> nextSteps = TestStepExecution.GetNextExperimentSteps(this.mockFixture.ExperimentSteps);

            Assert.IsNotEmpty(nextSteps);
            CollectionAssert.AreEqual(nextSteps, this.mockFixture.ExperimentSteps.Skip(1).Take(1));
        }

        [Test]
        public void StepExecutionSelectsTheExpectedStepsToProcessWhenPreviousStepsAreInProgress_AndParallelExecutionStepsExist_Scenario2()
        {
            // Scenario:
            // There are steps InProgress. Parallel execution steps exist.
            // When there are steps 'InProgress', they are re-evaluated. No new 'Pending' steps will be executed.
            this.mockFixture.ExperimentSteps.First().Status = ExecutionStatus.Succeeded;
            this.mockFixture.ExperimentSteps.ElementAt(1).Status = ExecutionStatus.InProgress;
            this.mockFixture.ExperimentSteps.Skip(2).Take(3).ToList().ForEach(step =>
            {
                step.Sequence = 300;
                step.Status = ExecutionStatus.Pending;
            });

            IEnumerable<ExperimentStepInstance> nextSteps = TestStepExecution.GetNextExperimentSteps(this.mockFixture.ExperimentSteps);

            Assert.IsNotEmpty(nextSteps);
            CollectionAssert.AreEqual(nextSteps, this.mockFixture.ExperimentSteps.Skip(1).Take(1));
        }

        [Test]
        public void StepExecutionSelectsTheExpectedStepsToProcessWhenPreviousStepsAreInProgress_AndParallelExecutionStepsExist_Scenario3()
        {
            // Scenario:
            // There are steps InProgress and InProgressContinue. Parallel execution steps exist.
            // Steps 'InProgress' or 'InProgressContinue' are re-evaluated. No new 'Pending' steps will be executed.
            this.mockFixture.ExperimentSteps.First().Status = ExecutionStatus.Succeeded;
            this.mockFixture.ExperimentSteps.ElementAt(1).Status = ExecutionStatus.InProgressContinue;
            this.mockFixture.ExperimentSteps.ElementAt(2).Status = ExecutionStatus.InProgress;
            this.mockFixture.ExperimentSteps.Skip(3).Take(3).ToList().ForEach(step =>
            {
                step.Sequence = 400;
                step.Status = ExecutionStatus.Pending;
            });

            IEnumerable<ExperimentStepInstance> nextSteps = TestStepExecution.GetNextExperimentSteps(this.mockFixture.ExperimentSteps);

            Assert.IsNotEmpty(nextSteps);
            CollectionAssert.AreEqual(nextSteps, this.mockFixture.ExperimentSteps.Skip(1).Take(2));
        }

        [Test]
        public void StepExecutionSelectsTheExpectedStepsToProcessWhenPreviousStepsAreInProgress_AndParallelExecutionStepsExist_Scenario4()
        {
            // Scenario:
            // All previous steps are Succeeded. Parallel execution steps are the next steps and they are in
            // Pending status.
            this.mockFixture.ExperimentSteps.First().Status = ExecutionStatus.Succeeded;
            this.mockFixture.ExperimentSteps.ElementAt(1).Status = ExecutionStatus.Succeeded;
            this.mockFixture.ExperimentSteps.Skip(2).Take(2).ToList().ForEach(step =>
            {
                step.Sequence = 300;
                step.Status = ExecutionStatus.Pending;
            });

            IEnumerable<ExperimentStepInstance> nextSteps = TestStepExecution.GetNextExperimentSteps(this.mockFixture.ExperimentSteps);

            Assert.IsNotEmpty(nextSteps);
            CollectionAssert.AreEqual(nextSteps, this.mockFixture.ExperimentSteps.Skip(2).Take(2));
        }

        [Test]
        public void StepExecutionSelectsTheExpectedStepsToProcessWhenPreviousStepsAreInProgress_AndDiagnosticsStepsExist_Scenario1()
        {
            // Scenario:
            // There are steps InProgress. There are also Diagnostics steps. Diagnostics steps should not run until that step completes.
            this.mockFixture.ExperimentSteps.Insert(3, this.mockFixture.CreateExperimentStep(FixtureExtensions.CreateExperimentComponent(typeof(ExampleDiagnosticsProvider))));

            this.mockFixture.ExperimentSteps.First().Status = ExecutionStatus.Succeeded;
            this.mockFixture.ExperimentSteps.ElementAt(1).Status = ExecutionStatus.InProgress;
            this.mockFixture.ExperimentSteps.Skip(2).Take(3).ToList().ForEach(step =>
            {
                step.Status = ExecutionStatus.Pending;
            });

            IEnumerable<ExperimentStepInstance> nextSteps = TestStepExecution.GetNextExperimentSteps(this.mockFixture.ExperimentSteps);

            Assert.IsNotEmpty(nextSteps);
            CollectionAssert.AreEqual(nextSteps, this.mockFixture.ExperimentSteps.Skip(1).Take(1));
        }

        [Test]
        public void StepExecutionSelectsTheExpectedStepsToProcessWhenPreviousStepsAreInProgress_AndDiagnosticsStepsExist_Scenario2()
        {
            // Scenario:
            // There are steps InProgress. There are also Diagnostics steps that are directly after the InProgress step.
            // Diagnostics steps should not run until that step completes.
            this.mockFixture.ExperimentSteps.Insert(2, this.mockFixture.CreateExperimentStep(FixtureExtensions.CreateExperimentComponent(typeof(ExampleDiagnosticsProvider))));

            this.mockFixture.ExperimentSteps.First().Status = ExecutionStatus.Succeeded;
            this.mockFixture.ExperimentSteps.ElementAt(1).Status = ExecutionStatus.InProgress;
            this.mockFixture.ExperimentSteps.Skip(2).Take(3).ToList().ForEach(step =>
            {
                step.Status = ExecutionStatus.Pending;
            });

            IEnumerable<ExperimentStepInstance> nextSteps = TestStepExecution.GetNextExperimentSteps(this.mockFixture.ExperimentSteps);

            Assert.IsNotEmpty(nextSteps);
            CollectionAssert.AreEqual(nextSteps, this.mockFixture.ExperimentSteps.Skip(1).Take(1));
        }

        [Test]
        public void StepExecutionSelectsTheExpectedStepsToProcessWhenPreviousStepsAreInProgressContinue_AndDiagnosticsStepsExist_Scenario3()
        {
            // Scenario:
            // There are steps InProgressContinue. There are also Diagnostics steps. Diagnostics steps should run in this
            // case as they are the next Pending steps.
            this.mockFixture.ExperimentSteps.Insert(2, this.mockFixture.CreateExperimentStep(FixtureExtensions.CreateExperimentComponent(typeof(ExampleDiagnosticsProvider))));
            this.mockFixture.ExperimentSteps.SetSequences();

            this.mockFixture.ExperimentSteps.First().Status = ExecutionStatus.Succeeded;
            this.mockFixture.ExperimentSteps.ElementAt(1).Status = ExecutionStatus.InProgressContinue;
            this.mockFixture.ExperimentSteps.Skip(2).Take(3).ToList().ForEach(step =>
            {
                step.Status = ExecutionStatus.Pending;
            });

            IEnumerable<ExperimentStepInstance> nextSteps = TestStepExecution.GetNextExperimentSteps(this.mockFixture.ExperimentSteps);

            Assert.IsNotEmpty(nextSteps);
            CollectionAssert.AreEqual(nextSteps, this.mockFixture.ExperimentSteps.Skip(1).Take(2));
        }

        [Test]
        public void StepExecutionSelectsTheExpectedStepsToProcessWhenPreviousStepsAreInProgressContinue_AndDiagnosticsStepsExist_Scenario4()
        {
            // Scenario:
            // There are steps InProgressContinue. There are also Diagnostics steps. Diagnostics steps should run in this
            // case as they are the next Pending steps.
            this.mockFixture.ExperimentSteps.Insert(5, this.mockFixture.CreateExperimentStep(FixtureExtensions.CreateExperimentComponent(typeof(ExampleDiagnosticsProvider))));
            this.mockFixture.ExperimentSteps.SetSequences();

            this.mockFixture.ExperimentSteps.First().Status = ExecutionStatus.Succeeded;
            this.mockFixture.ExperimentSteps.ElementAt(1).Status = ExecutionStatus.InProgressContinue;
            this.mockFixture.ExperimentSteps.Skip(2).Take(3).ToList().ForEach(step =>
            {
                step.Status = ExecutionStatus.Pending;
            });

            IEnumerable<ExperimentStepInstance> nextSteps = TestStepExecution.GetNextExperimentSteps(this.mockFixture.ExperimentSteps);

            Assert.IsNotEmpty(nextSteps);
            CollectionAssert.AreEqual(nextSteps, this.mockFixture.ExperimentSteps.Skip(1).Take(2));
            Assert.IsFalse(nextSteps.Any(step => step.StepType == SupportedStepType.Diagnostics));
        }

        [Test]
        public void StepExecutionHandlesRaceConditionsWhichCauseSubsetsOfParallelStepsToGetMissedInInitialExecution_Scenario1()
        {
            // Scenario:
            // Some of the parallel execution steps were already executed and are InProgress. The rest of the parallel execution
            // steps are Pending. The Pending steps should be picked up and executed despite steps before them being InProgress
            // because they are the same sequence and the intent thus was to execute them in-parallel.
            this.mockFixture.ExperimentSteps.First().Status = ExecutionStatus.Succeeded;
            this.mockFixture.ExperimentSteps.ElementAt(1).Status = ExecutionStatus.InProgressContinue;
            this.mockFixture.ExperimentSteps.Skip(2).Take(1).ToList().ForEach(step =>
            {
                step.Sequence = 300;
                step.Status = ExecutionStatus.InProgress;
            });

            // A race condition can happen that causes a subset of parallel execution steps to get executed before the remaining
            // parallel steps. This can happen for example if a portion of the steps are committed to the data store after the first
            // set of parallel steps were committed to the data store and the first set has already been picked up by the execution
            // manager. The logic in the execution manager is written to handle this so that the remaining steps get picked up and
            // executed as soon as they are available.
            this.mockFixture.ExperimentSteps.Skip(3).Take(2).ToList().ForEach(step =>
            {
                step.Sequence = 300;
                step.Status = ExecutionStatus.Pending;
            });

            IEnumerable<ExperimentStepInstance> nextSteps = TestStepExecution.GetNextExperimentSteps(this.mockFixture.ExperimentSteps);

            Assert.IsNotEmpty(nextSteps);
            CollectionAssert.AreEqual(nextSteps, this.mockFixture.ExperimentSteps.Skip(1).Take(4));
        }

        [Test]
        public void StepExecutionHandlesRaceConditionsWhichCauseSubsetsOfParallelStepsToGetMissedInInitialExecution_Scenario2()
        {
            // Scenario:
            // Some of the parallel execution steps were already executed and are InProgressContinue. The rest of the parallel execution
            // steps are Pending. The Pending steps should be picked up and executed because they are the same sequence and the intent thus
            // was to execute them in-parallel.
            this.mockFixture.ExperimentSteps.First().Status = ExecutionStatus.Succeeded;
            this.mockFixture.ExperimentSteps.ElementAt(1).Status = ExecutionStatus.InProgressContinue;
            this.mockFixture.ExperimentSteps.Skip(2).Take(2).ToList().ForEach(step =>
            {
                step.Sequence = 300;
                step.Status = ExecutionStatus.InProgressContinue;
            });

            // If a race condition happened that caused a subset of parallel execution steps to get
            // executed (e.g. if a portion of the steps are committed to the data store after the first set
            // is picked up by the execution manager).
            this.mockFixture.ExperimentSteps.Skip(4).Take(2).ToList().ForEach(step =>
            {
                step.Sequence = 300;
                step.Status = ExecutionStatus.Pending;
            });

            IEnumerable<ExperimentStepInstance> nextSteps = TestStepExecution.GetNextExperimentSteps(this.mockFixture.ExperimentSteps);

            Assert.IsNotEmpty(nextSteps);
            CollectionAssert.AreEqual(nextSteps, this.mockFixture.ExperimentSteps.Skip(1).Take(5));
        }

        [Test]
        public void StepExecutionHandlesRaceConditionsWhichCauseSubsetsOfParallelStepsToGetMissedInInitialExecution_Scenario3()
        {
            // Scenario:
            // A portion of the parallel execution steps were picked up and executed. These steps failed. The second
            // set of parallel steps has not been executed. This is the same as the default scenario where there are failed
            // steps. The logic immediately moves to execution of cleanup steps skipping any steps in between regardless of
            // their state.
            this.mockFixture.ExperimentSteps.First().Status = ExecutionStatus.Succeeded;
            this.mockFixture.ExperimentSteps.ElementAt(1).Status = ExecutionStatus.InProgressContinue;
            this.mockFixture.ExperimentSteps.Skip(2).Take(2).ToList().ForEach(step =>
            {
                step.Sequence = 300;
                step.Status = ExecutionStatus.Failed;
            });

            // If a race condition happened that caused a subset of parallel execution steps to get
            // executed (e.g. if a portion of the steps are committed to the data store after the first set
            // is picked up by the execution manager).
            this.mockFixture.ExperimentSteps.Skip(4).Take(2).ToList().ForEach(step =>
            {
                step.Sequence = 300;
                step.Status = ExecutionStatus.Pending;
            });

            IEnumerable<ExperimentStepInstance> nextSteps = TestStepExecution.GetNextExperimentSteps(this.mockFixture.ExperimentSteps);

            Assert.IsNotEmpty(nextSteps);
            CollectionAssert.AreEquivalent(nextSteps.Where(step => step.StepType == SupportedStepType.EnvironmentCleanup), this.mockFixture.ExperimentSteps.Where(step => step.StepType == SupportedStepType.EnvironmentCleanup).Take(1));
        }

        [Test]
        public void IsExperimentCompletedReturnsCorrectStatusWhenAllStepsAreSucceeded()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCleanupProvider)),
            };

            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);
            var isCompleted = TestStepExecution.IsExperimentCompleted(mockExperimentSteps, out ExperimentStatus status);

            Assert.IsTrue(isCompleted);
            Assert.AreEqual(status, ExperimentStatus.Succeeded);
        }

        [Test]
        public void IsExperimentCompletedReturnsCorrectStatusWhenAllStepsAreTerminal_OneOrMoreFailed()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Failed, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCleanupProvider)),
            };

            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);
            var isCompleted = TestStepExecution.IsExperimentCompleted(mockExperimentSteps, out ExperimentStatus status);

            Assert.IsTrue(isCompleted);
            Assert.AreEqual(status, ExperimentStatus.Failed);
        }

        [Test]
        public void IsExperimentCompletedReturnsCorrectStatusWhenAllStepsAreTerminal_OneOrMoreCancelled()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Cancelled, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCleanupProvider)),
            };

            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);
            var isCompleted = TestStepExecution.IsExperimentCompleted(mockExperimentSteps, out ExperimentStatus status);

            Assert.IsTrue(isCompleted);
            Assert.AreEqual(status, ExperimentStatus.Cancelled);
        }

        [Test]
        public void IsExperimentCompletedReturnsCorrectStatusWithAllOtherStepsSucceeded_AndCleanupStepsAreInProgress()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.InProgress, typeof(ExampleCleanupProvider)),
            };

            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);
            var isCompleted = TestStepExecution.IsExperimentCompleted(mockExperimentSteps, out ExperimentStatus status);

            Assert.IsFalse(isCompleted);
            Assert.AreEqual(status, ExperimentStatus.InProgress);
        }

        [Test]
        public void IsExperimentCompletedReturnsCorrectStatusWithSomeStepsFailed()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Failed, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCleanupProvider)),
            };

            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);
            var isCompleted = TestStepExecution.IsExperimentCompleted(mockExperimentSteps, out ExperimentStatus status);

            Assert.IsTrue(isCompleted);
            Assert.AreEqual(status, ExperimentStatus.Failed);
        }

        [Test]
        public void IsExperimentCompletedReturnsCorrectStatusWithSomeStepsFailed_AndCleanupStepsExist()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Failed, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleCleanupProvider)),
            };

            // Cleanup steps Pending
            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);
            var isCompleted = TestStepExecution.IsExperimentCompleted(mockExperimentSteps, out ExperimentStatus status);

            Assert.IsFalse(isCompleted);
            Assert.AreEqual(status, ExperimentStatus.InProgress);

            // Cleanup steps InProgress
            mockExperimentSteps.Last().Status = ExecutionStatus.InProgress;
            isCompleted = TestStepExecution.IsExperimentCompleted(mockExperimentSteps, out status);

            Assert.IsFalse(isCompleted);
            Assert.AreEqual(status, ExperimentStatus.InProgress);

            // Cleanup steps InProgressContinue
            mockExperimentSteps.Last().Status = ExecutionStatus.InProgressContinue;
            isCompleted = TestStepExecution.IsExperimentCompleted(mockExperimentSteps, out status);

            Assert.IsFalse(isCompleted);
            Assert.AreEqual(status, ExperimentStatus.InProgress);
        }

        [Test]
        public void IsExperimentCompletedReturnsCorrectStatusWithSomeStepsFailed_AndCleanupStepsDoNotExist()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Failed, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleWorkloadProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExamplePayloadProvider))
            };

            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);
            var isCompleted = TestStepExecution.IsExperimentCompleted(mockExperimentSteps, out ExperimentStatus status);

            Assert.IsFalse(isCompleted);
            Assert.AreEqual(status, ExperimentStatus.InProgress);
        }

        [Test]
        public void IsExperimentCompletedReturnsCorrectStatusWithSomeStepsInProgress()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.InProgress, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.InProgress, typeof(ExampleCleanupProvider)),
            };

            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);
            var isCompleted = TestStepExecution.IsExperimentCompleted(mockExperimentSteps, out ExperimentStatus status);

            Assert.IsFalse(isCompleted);
            Assert.AreEqual(status, ExperimentStatus.InProgress);
        }

        [Test]
        public void IsExperimentCompletedReturnsCorrectStatusWhenSomeStepsAreSystemCancelled_1()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Failed, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.SystemCancelled, typeof(ExampleCleanupProvider)),
            };

            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);
            var isCompleted = TestStepExecution.IsExperimentCompleted(mockExperimentSteps, out ExperimentStatus status);

            Assert.IsTrue(isCompleted);
            Assert.AreEqual(status, ExperimentStatus.Failed);
        }

        [Test]
        public void IsExperimentCompletedReturnsCorrectStatusWhenSomeStepsAreSystemCancelled_2()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Cancelled, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.SystemCancelled, typeof(ExampleCleanupProvider)),
            };

            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);
            var isCompleted = TestStepExecution.IsExperimentCompleted(mockExperimentSteps, out ExperimentStatus status);

            Assert.IsTrue(isCompleted);
            Assert.AreEqual(status, ExperimentStatus.Cancelled);
        }

        [Test]
        public void IsExperimentCompletedReturnsCorrectStatusWithSomeStepsInProgress_AndCleanupStepsDoNotExist()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.InProgress, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleWorkloadProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExamplePayloadProvider))
            };

            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);
            var isCompleted = TestStepExecution.IsExperimentCompleted(mockExperimentSteps, out ExperimentStatus status);

            Assert.IsFalse(isCompleted);
            Assert.AreEqual(status, ExperimentStatus.InProgress);
        }

        [Test]
        public void IsExperimentCompletedReturnsCorrectStatusWithSomeStepsCancelled()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Cancelled, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCleanupProvider)),
            };

            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);
            var isCompleted = TestStepExecution.IsExperimentCompleted(mockExperimentSteps, out ExperimentStatus status);

            Assert.IsTrue(isCompleted);
            Assert.AreEqual(status, ExperimentStatus.Cancelled);
        }

        [Test]
        public void IsExperimentCompletedReturnsCorrectStatusWithSomeStepsCancelled_AndCleanupStepsExist()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Cancelled, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleCleanupProvider)),
            };

            // Cleanup steps Pending
            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);
            var isCompleted = TestStepExecution.IsExperimentCompleted(mockExperimentSteps, out ExperimentStatus status);

            Assert.IsFalse(isCompleted);
            Assert.AreEqual(status, ExperimentStatus.InProgress);

            // Cleanup steps InProgress
            mockExperimentSteps.Last().Status = ExecutionStatus.InProgress;
            isCompleted = TestStepExecution.IsExperimentCompleted(mockExperimentSteps, out status);

            Assert.IsFalse(isCompleted);
            Assert.AreEqual(status, ExperimentStatus.InProgress);

            // Cleanup steps InProgressContinue
            mockExperimentSteps.Last().Status = ExecutionStatus.InProgressContinue;
            isCompleted = TestStepExecution.IsExperimentCompleted(mockExperimentSteps, out status);

            Assert.IsFalse(isCompleted);
            Assert.AreEqual(status, ExperimentStatus.InProgress);
        }

        [Test]
        public void IsExperimentCompletedReturnsCorrectStatusWithSomeStepsCancelled_AndCleanupStepsDoNotExist()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Cancelled, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleWorkloadProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExamplePayloadProvider))
            };

            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);
            var isCompleted = TestStepExecution.IsExperimentCompleted(mockExperimentSteps, out ExperimentStatus status);

            Assert.IsFalse(isCompleted);
            Assert.AreEqual(status, ExperimentStatus.InProgress);
        }

        [Test]
        public void IsExperimentCompletedReturnsCorrectStatusWithSomeStepsInProgressContinue()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.InProgressContinue, typeof(ExampleCriteriaProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.InProgressContinue, typeof(ExampleSetupProvider))
            };

            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);

            var isCompleted = TestStepExecution.IsExperimentCompleted(mockExperimentSteps, out ExperimentStatus status);

            Assert.IsFalse(isCompleted);
            Assert.AreEqual(status, ExperimentStatus.InProgress);
        }

        [Test]
        public void IsExperimentCompletedReturnsCorrectStatusWithSomeStepsInProgressContinue_AndCleanupStepsDoNotExist()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.InProgressContinue, typeof(ExampleCriteriaProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.InProgressContinue, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleWorkloadProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExamplePayloadProvider))
            };

            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);

            var isCompleted = TestStepExecution.IsExperimentCompleted(mockExperimentSteps, out ExperimentStatus status);

            Assert.IsFalse(isCompleted);
            Assert.AreEqual(status, ExperimentStatus.InProgress);
        }

        [Test]
        public void IsExperimentCompletedReturnsCorrectStatusWithSomeStepsFailed_AndHasDiagnostics()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.InProgressContinue, typeof(ExampleCriteriaProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Failed, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Failed, typeof(ExampleWorkloadProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Failed, typeof(ExamplePayloadProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleDiagnosticsProvider))
            };

            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);

            var isCompleted = TestStepExecution.IsExperimentCompleted(mockExperimentSteps, out ExperimentStatus status);

            Assert.IsFalse(isCompleted);
            Assert.AreEqual(status, ExperimentStatus.InProgress);
        }

        [Test]
        public void GetNextExperimentStepsReturnsCorrectStepWithSomeStepsFailed_AndHasDiagnostics()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.InProgressContinue, typeof(ExampleCriteriaProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Failed, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Failed, typeof(ExampleWorkloadProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExamplePayloadProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleDiagnosticsProvider))
            };

            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);

            IEnumerable<ExperimentStepInstance> nextSteps = TestStepExecution.GetNextExperimentSteps(mockExperimentSteps);
            Assert.IsNotEmpty(nextSteps);
            CollectionAssert.AreEquivalent(nextSteps.Where(step => step.StepType == SupportedStepType.Diagnostics), mockExperimentSteps.Where(step => step.StepType == SupportedStepType.Diagnostics));
        }

        [Test]
        public void IsExperimentCompletedReturnsCorrectStatusWithHasDiagnosticsAndAllSucceeded()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleWorkloadProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExamplePayloadProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleDiagnosticsProvider))
            };

            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);

            var isCompleted = TestStepExecution.IsExperimentCompleted(mockExperimentSteps, out ExperimentStatus status);

            Assert.IsTrue(isCompleted);
            Assert.AreEqual(status, ExperimentStatus.Succeeded);
        }

        [Test]
        public void IsExperimentCompletedReturnsCorrectStatusWhenCertificationFails()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCriteriaProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleWorkloadProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Failed, typeof(ExampleCertificationProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleDiagnosticsProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Cancelled, typeof(ExampleCleanupProvider))
            };

            IEnumerable<ExperimentStepInstance> mockExperimentSteps = this.CreateSteps(steps);

            var isCompleted = TestStepExecution.IsExperimentCompleted(mockExperimentSteps, out ExperimentStatus status);

            Assert.IsTrue(isCompleted);
            Assert.AreEqual(status, ExperimentStatus.Failed);
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

        private class TestStepExecution : StepExecution
        {
            public TestStepExecution(IServiceCollection services, IConfiguration configuration)
                : base(services, configuration)
            {
            }

            public static new IEnumerable<ExperimentStepInstance> GetNextExperimentSteps(IEnumerable<ExperimentStepInstance> steps)
            {
                return StepExecution.GetNextExperimentSteps(steps);
            }

            public static new bool IsExperimentCompleted(IEnumerable<ExperimentStepInstance> steps, out ExperimentStatus experimentStatus)
            {
                return StepExecution.IsExperimentCompleted(steps, out experimentStatus);
            }

            public override Task ExecuteAsync(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }
    }
}
