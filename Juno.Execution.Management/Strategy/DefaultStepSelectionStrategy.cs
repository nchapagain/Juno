namespace Juno.Execution.Management.Strategy
{
    using System.Collections.Generic;
    using System.Linq;
    using Juno.Contracts;
    using Juno.Execution.Providers.AutoTriage;
    using Microsoft.Azure.CRC.Extensions;

    /// <summary>
    /// The default step strategy is to return all steps as candidate steps.
    /// </summary>
    public class DefaultStepSelectionStrategy : IStepSelectionStrategy
    {
        /// <inheritdoc/>
        public virtual StepSelectionResult GetSteps(IEnumerable<ExperimentStepInstance> allSteps)
        {
            // The default step selection strategy works as such:
            // 1) When there are any failed steps, go to cleanup steps immediately (skipping any other steps in between).
            // 2) If there are InProgress steps, select those.
            // 3) If there are InProgressContinue steps, select those also.
            // 4) If all previous steps are Succeeded or InProgressContinue, select the next Pending step(s) in sequence.
            // 5) Select all steps with the same sequence as any selected in the above steps. These are "parallel execution" steps.
            List<ExperimentStepInstance> selectedSteps = new List<ExperimentStepInstance>();

            bool terminalStepsExist = allSteps.Any(step => ExecutionResult.TerminalStatuses.Contains(step.Status));
            bool allNonCleanupStepsSucceeded = allSteps.Where(
                step => step.StepType != SupportedStepType.EnvironmentCleanup).All(step => step.Status == ExecutionStatus.Succeeded);

            if (terminalStepsExist || allNonCleanupStepsSucceeded)
            {
                // Cleanup Steps
                // If all previous steps are Succeeded or there are any failures run cleanup
                // and stop processing any other steps.
                selectedSteps.AddRange(allSteps.Where(step => step.StepType == SupportedStepType.EnvironmentCleanup
                    && !ExecutionResult.CompletedStatuses.Contains(step.Status)));
            }
            else
            {
                // All non-Cleanup/non-AutoTriage Steps
                // If there aren't any failed steps, we do not want to allow any cleanup steps to run, so we remove
                // them from the result set.
                selectedSteps.AddRange(allSteps.Where(step => step.StepType != SupportedStepType.EnvironmentCleanup
                    && !ExecutionResult.CompletedStatuses.Contains(step.Status)));
            }

            return new StepSelectionResult(continueSelection: false, stepsSelected: selectedSteps);
        }
    }
}
