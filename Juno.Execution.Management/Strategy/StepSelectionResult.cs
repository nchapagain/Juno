namespace Juno.Execution.Management.Strategy
{
    using System.Collections.Generic;    
    using Juno.Contracts;

    /// <summary>
    /// Represents the result of a step selection strategy
    /// </summary>
    public class StepSelectionResult
    {
        /// <summary>
        /// Creates and instance of <see cref="StepSelectionResult"/>
        /// </summary>
        /// <param name="continueSelection">True if the selection process should continue, false if the steps selected take priority.</param>
        /// <param name="stepsSelected">The steps selected as part of the selection strategy.</param>
        public StepSelectionResult(bool continueSelection, IEnumerable<ExperimentStepInstance> stepsSelected)
        {
            this.ContinueSelection = continueSelection;
            this.StepsSelected = stepsSelected;
        }

        /// <summary>
        /// Indicates if the step selection process should continue or stop.
        /// </summary>
        public bool ContinueSelection { get; }

        /// <summary>
        /// The candidate steps to be processed.
        /// </summary>
        public IEnumerable<ExperimentStepInstance> StepsSelected { get; }
    }
}
