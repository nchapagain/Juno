namespace Juno.Execution.Management.Strategy
{
    using System.Collections.Generic;
    using System.Linq;
    using Juno.Contracts;

    /// <summary>
    /// Returns any certification steps when some steps fails.
    /// </summary>
    public class RunCertificationOnFailure : IStepSelectionStrategy
    {
        /// <inheritdoc/>
        public StepSelectionResult GetSteps(IEnumerable<ExperimentStepInstance> allSteps)
        {
            List<ExperimentStepInstance> selectedSteps = new List<ExperimentStepInstance>();
            bool terminalStepsExist = allSteps.Any(step => ExecutionResult.TerminalStatuses.Contains(step.Status));
            bool certificationStepsExist = allSteps.Any(step => step.StepType == SupportedStepType.Certification);

            if (terminalStepsExist && certificationStepsExist)
            {
                // If there is any failure, always process certification steps.
                IEnumerable<ExperimentStepInstance> certificationSteps = allSteps.Where(
                    step => step.StepType == SupportedStepType.Certification && !ExecutionResult.CompletedStatuses.Contains(step.Status));

                if (certificationSteps?.Any() == true)
                {
                    selectedSteps.AddRange(certificationSteps);
                }
            }

            // We do not continue selecting other steps if there are certification steps found that
            // need to be executed.
            return new StepSelectionResult(continueSelection: !selectedSteps.Any(), selectedSteps);
        }
    }
}
