namespace Juno.Execution.Management.Strategy
{
    using System.Collections.Generic;
    using System.Linq;
    using Juno.Contracts;
    using Juno.Execution.Providers.AutoTriage;

    /// <summary>
    /// Returns any diagnostic steps when some steps fails and continue looking for more steps to run.
    /// </summary>
    public class RunDiagnosticsOnFailure : IStepSelectionStrategy
    {
        /// <inheritdoc/>
        public StepSelectionResult GetSteps(IEnumerable<ExperimentStepInstance> allSteps)
        {
            List<ExperimentStepInstance> selectedSteps = new List<ExperimentStepInstance>();
            bool terminalStepsExist = allSteps.Any(step => ExecutionResult.TerminalStatuses.Contains(step.Status));

            if (terminalStepsExist)
            {
                // If there is any failure, always process any diagnostics steps and continue looking for more steps to process.
                selectedSteps.AddRange(allSteps
                    .Where(step => step.StepType == SupportedStepType.Diagnostics && !ExecutionResult.IsCompletedStatus(step.Status))
                    .Where(step => step.Definition.ComponentType != typeof(AutoTriageProvider).FullName));
            }

            return new StepSelectionResult(continueSelection: !selectedSteps.Any(), selectedSteps);
        }
    }
}
