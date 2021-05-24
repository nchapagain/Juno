namespace Juno.Execution.Management.Strategy
{
    using System.Collections.Generic;
    using Juno.Contracts;

    /// <summary>
    /// Provides functionality for managing step strategy.
    /// </summary>
    public interface IStepSelectionStrategy
    {
        /// <summary>
        /// Gets candidate steps to process based on a strategy.
        /// </summary>
        /// <param name="allSteps">All steps defined for the experiment.</param>
        /// <returns><see cref="StepSelectionResult"/></returns>
        StepSelectionResult GetSteps(IEnumerable<ExperimentStepInstance> allSteps);
    }
}
