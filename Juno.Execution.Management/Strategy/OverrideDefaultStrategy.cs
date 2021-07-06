namespace Juno.Execution.Management.Strategy
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Juno.Contracts;

    /// <summary>
    /// Returns steps tagged with OverrideDefault.
    /// </summary>
    public class OverrideDefaultStrategy : IStepSelectionStrategy
    {
        /// <inheritdoc/>
        public StepSelectionResult GetSteps(IEnumerable<ExperimentStepInstance> allSteps)
        {
            List<ExperimentStepInstance> selectedSteps = new List<ExperimentStepInstance>();
            selectedSteps.AddRange(allSteps.Where(step => step.Definition.Flow() != null
                        && step.Definition.Flow().OverrideDefault == true
                        && !ExecutionResult.CompletedStatuses.Contains(step.Status)));

            return new StepSelectionResult(continueSelection: true, selectedSteps);
        }
    }
}
