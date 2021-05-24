namespace Juno.Execution.Management.Strategy
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Juno.Contracts;

    /// <summary>
    /// Returns steps tagged with a block name when a step with an OnFailureExecuteBlock fails.
    /// </summary>
    public class EvaluateConditionalFlowOnFailure : IStepSelectionStrategy
    {
        /// <inheritdoc/>
        public StepSelectionResult GetSteps(IEnumerable<ExperimentStepInstance> allSteps)
        {
            List<ExperimentStepInstance> selectedSteps = new List<ExperimentStepInstance>();
            var terminalStepsWithConditionalFlow = allSteps.Where(step => ExecutionResult.TerminalStatuses.Contains(step.Status)
                && step.Definition.Flow() != null
                && !string.IsNullOrEmpty(step.Definition.Flow().OnFailureExecuteBlock));

            // If there is any terminal step with an OnFailureExecuteBlock, always process steps tagged with the block name.
            if (terminalStepsWithConditionalFlow.Any())
            {
                terminalStepsWithConditionalFlow.ToList().ForEach(stepWithOnFailureExecuteBlock =>
                {                    
                    selectedSteps.AddRange(allSteps.Where(step => step.Definition.Flow() != null 
                        && step.Definition.Flow().BlockName == stepWithOnFailureExecuteBlock.Definition.Flow().OnFailureExecuteBlock 
                        && !ExecutionResult.CompletedStatuses.Contains(step.Status)));
                });
            }

            return new StepSelectionResult(continueSelection: true, selectedSteps);
        }
    }
}
