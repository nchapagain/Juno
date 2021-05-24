namespace Juno.Execution.Management.Strategy
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Juno.Contracts;

    /// <summary>
    /// Returns any certification steps when some steps fails. If certification fails, stop looking for more steps to process.
    /// </summary>
    public class StopOnCertificationFailure : IStepSelectionStrategy
    {
        /// <inheritdoc/>
        public StepSelectionResult GetSteps(IEnumerable<ExperimentStepInstance> allSteps)
        {
            bool terminalStepsExist = allSteps.Any(step => ExecutionResult.TerminalStatuses.Contains(step.Status));
            bool isCertificationFailure = allSteps.Any(step => step.StepType == SupportedStepType.Certification && ExecutionResult.TerminalStatuses.Contains(step.Status));

            // This strategy applies the "circuit breaker" pattern. Essentially, if there are certification steps that
            // have failed, then we want to stop ALL execution. To do this we signal for step selection to stop at the same
            // time as we return an empty array. This strategy should be placed high in the priority order of all step selection 
            // strategies to ensure that there are no unexpected steps that can be executed in the case of this certification
            // failure.
            return new StepSelectionResult(continueSelection: !(terminalStepsExist && isCertificationFailure), Array.Empty<ExperimentStepInstance>());
        }
    }
}
