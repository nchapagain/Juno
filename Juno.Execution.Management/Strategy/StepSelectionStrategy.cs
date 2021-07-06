namespace Juno.Execution.Management.Strategy
{
    using System.Collections.Generic;
    using System.Linq;
    using Juno.Contracts;
    using Juno.Execution.Providers.AutoTriage;
    using Microsoft.Azure.CRC.Extensions;

    /// <inheritdoc/>
    public class StepSelectionStrategy : IStepSelectionStrategy
    {
        private IEnumerable<IStepSelectionStrategy> stepSelectionStrategies;

        private StepSelectionStrategy()
        {
            this.stepSelectionStrategies = new List<IStepSelectionStrategy>()
            {
                new StopOnCertificationFailure(),
                new StopOnCancellation(),
                new EvaluateConditionalFlowOnFailure(),
                new RunDiagnosticsOnFailure(),
                new RunCertificationOnFailure(),
                new OverrideDefaultStrategy(),

                // Important:
                // Cleanup steps are added in this strategy. If it is undesirable for cleanup to
                // happen upon step failures, then at least 1 of the strategies above must terminate
                // the step selection continuation (i.e. result.ContinueSelection = false).
                new DefaultStepSelectionStrategy()
            };
        }

        /// <summary>
        /// Static instance of the <see cref="StepSelectionStrategy"/>
        /// </summary>
        public static StepSelectionStrategy Instance { get; } = new StepSelectionStrategy();

        /// <inheritdoc/>
        public StepSelectionResult GetSteps(IEnumerable<ExperimentStepInstance> allSteps)
        {
            // Get candidate steps based on strategy. These steps may be in any status (e.g. InProgress, InProgressContinue, Pending).
            // Each of the selection strategies may add steps to the candidates or may return a specific set of steps and stop
            // any additional selection strategies from running. This is a combination of 2 design patterns:
            // 1) Chain-of-Responsibility (adding steps to the set).
            // 2) Circuit Breaker (selecting a specific set of steps and stopping any remaining selections).
            var candidateSteps = new List<ExperimentStepInstance>();

            // Auto-Triage is a special step for which we do not want to factor into the typical step
            // selection logic. It runs at the end of the experiment after all other steps have completed.
            IEnumerable<ExperimentStepInstance> allStepsWithoutAutoTriage = allSteps.Where(step => !StepSelectionStrategy.IsAutoTriageStep(step));

            foreach (IStepSelectionStrategy stepSelectionStrategy in this.stepSelectionStrategies)
            {
                StepSelectionResult stepSelectionResult = stepSelectionStrategy.GetSteps(allStepsWithoutAutoTriage);

                if (stepSelectionResult.StepsSelected?.Any() == true)
                {
                    candidateSteps = candidateSteps.Union(stepSelectionResult.StepsSelected, StepIdEqualityComparer.Instance).ToList();
                }

                if (!stepSelectionResult.ContinueSelection)
                {
                    break;
                }
            }

            List<ExperimentStepInstance> nextSteps = new List<ExperimentStepInstance>();

            if (candidateSteps.Any())
            {
                // InProgress/InProgressContinue steps
                nextSteps.AddRange(StepSelectionStrategy.GetNextInProgressSteps(candidateSteps));

                // Pending steps
                nextSteps.AddRange(StepSelectionStrategy.GetNextPendingSteps(candidateSteps));

                // Parallel execution steps (i.e. in the same sequence as those previously selected).
                nextSteps.AddRange(StepSelectionStrategy.GetMatchingParallelSteps(nextSteps, candidateSteps));
            }

            // Auto-triage diagnostics steps are added at the very end of the experiment.
            nextSteps.AddRange(StepSelectionStrategy.GetAutoTriageSteps(allSteps));

            return new StepSelectionResult(false, nextSteps.OrderBy(step => step.Sequence));
        }

        /// <summary>
        /// Returns the any next auto triage steps.
        /// </summary>
        /// <param name="candidateSteps">A set of all candidate steps for next execution.</param>
        internal static IEnumerable<ExperimentStepInstance> GetAutoTriageSteps(IEnumerable<ExperimentStepInstance> candidateSteps)
        {
            candidateSteps.ThrowIfNull(nameof(candidateSteps));

            List<ExperimentStepInstance> selectedSteps = new List<ExperimentStepInstance>();

            // Auto-triage diagnostics is a special case that is only ran only at the end of the experiment after all
            // cleanup steps have run.
            IEnumerable<ExperimentStepInstance> autoTriageSteps = candidateSteps
                .Where(step => StepSelectionStrategy.IsAutoTriageStep(step) && !ExecutionResult.IsCompletedStatus(step.Status));

            if (autoTriageSteps?.Any() == true)
            {
                IEnumerable<ExperimentStepInstance> cleanupSteps = candidateSteps.Where(step => step.StepType == SupportedStepType.EnvironmentCleanup);
                IEnumerable<ExperimentStepInstance> allOtherSteps = candidateSteps.Except(autoTriageSteps, StepIdEqualityComparer.Instance);

                // To ensure that auto-triage runs at the end of an experiment, we have to account for experiments that
                // have cleanup steps as well as those that do not. Auto-triage runs if one of the following are true:
                // 1) All steps beforehand are in a completed state.
                // 2) All cleanup steps are in a completed state.
                if (allOtherSteps.All(step => ExecutionResult.IsCompletedStatus(step.Status))
                    || cleanupSteps.All(step => ExecutionResult.IsCompletedStatus(step.Status)))
                {
                    selectedSteps.AddRange(autoTriageSteps);
                }
            }

            return selectedSteps;
        }

        /// <summary>
        /// Returns any next steps that are parallel steps to those already selected to the set.
        /// </summary>
        /// <param name="candidateSteps">A set of all candidate steps for next execution.</param>
        /// <param name="allSteps">A set of ALL steps associated with an experiment.</param>
        internal static IEnumerable<ExperimentStepInstance> GetMatchingParallelSteps(IEnumerable<ExperimentStepInstance> candidateSteps, IEnumerable<ExperimentStepInstance> allSteps)
        {
            candidateSteps.ThrowIfNull(nameof(candidateSteps));

            List<ExperimentStepInstance> selectedSteps = new List<ExperimentStepInstance>();
            if (candidateSteps.Any() == true && allSteps?.Any() == true)
            {
                // Parallel steps will have the same sequence. They should be executed concurrently.
                IEnumerable<int> candidateStepSequences = candidateSteps.Select(step => step.Sequence).Distinct();
                IEnumerable<ExperimentStepInstance> parallelSteps = allSteps
                    ?.Where(step => candidateStepSequences.Contains(step.Sequence) && !ExecutionResult.CompletedStatuses.Contains(step.Status))
                    ?.Except(candidateSteps, StepIdEqualityComparer.Instance);

                if (parallelSteps?.Any() == true)
                {
                    selectedSteps.AddRange(parallelSteps);
                }
            }

            return selectedSteps;
        }

        /// <summary>
        /// Returns any next InProgress or InProgressContinue steps from the candidate set.
        /// </summary>
        /// <param name="candidateSteps">A set of all candidate steps for next execution.</param>
        internal static IEnumerable<ExperimentStepInstance> GetNextInProgressSteps(IEnumerable<ExperimentStepInstance> candidateSteps)
        {
            candidateSteps.ThrowIfNull(nameof(candidateSteps));

            List<ExperimentStepInstance> selectedSteps = new List<ExperimentStepInstance>();

            IEnumerable<ExperimentStepInstance> inProgressSteps = candidateSteps
                ?.Where(step => step.Status == ExecutionStatus.InProgress || step.Status == ExecutionStatus.InProgressContinue)
                ?.Except(selectedSteps, StepIdEqualityComparer.Instance);

            if (inProgressSteps?.Any() == true)
            {
                selectedSteps.AddRange(inProgressSteps);
            }

            return selectedSteps;
        }

        /// <summary>
        /// Extension adds the next Pending step to those already selected to the set.
        /// </summary>
        /// <param name="candidateSteps">A set of all candidate steps for next execution.</param>
        internal static IEnumerable<ExperimentStepInstance> GetNextPendingSteps(IEnumerable<ExperimentStepInstance> candidateSteps)
        {
            candidateSteps.ThrowIfNull(nameof(candidateSteps));

            List<ExperimentStepInstance> selectedSteps = new List<ExperimentStepInstance>();
            if (!candidateSteps.Any(s => s.Status == ExecutionStatus.InProgress))
            {
                // Get next pending step(s) if there are no steps in a status of 'InProgress'. In progress steps indicate
                // that no new 'Pending' steps should be started.
                ExperimentStepInstance nextPendingStep = candidateSteps
                    .OrderBy(s => s.Sequence)
                    .ThenBy(s => s.Created)
                    .Except(selectedSteps, StepIdEqualityComparer.Instance)
                    .FirstOrDefault(c => c.Status == ExecutionStatus.Pending);

                if (nextPendingStep != null)
                {
                    selectedSteps.Add(nextPendingStep);
                }
            }

            return selectedSteps;
        }

        private static bool IsAutoTriageStep(ExperimentStepInstance step)
        {
            return step.Definition.ComponentType == typeof(AutoTriageProvider).FullName;
        }
    }
}
