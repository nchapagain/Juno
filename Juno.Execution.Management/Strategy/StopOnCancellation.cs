namespace Juno.Execution.Management.Strategy
{
    using System.Collections.Generic;
    using System.Linq;
    using Juno.Contracts;
    using Juno.Execution.Providers.Cancellation;

    /// <summary>
    /// Step selection strategy selects any steps that cause the experiment to be cancelled.
    /// </summary>
    public class StopOnCancellation : IStepSelectionStrategy
    {
        /// <inheritdoc />
        public StepSelectionResult GetSteps(IEnumerable<ExperimentStepInstance> allSteps)
        {
            List<ExperimentStepInstance> selectedSteps = new List<ExperimentStepInstance>();

            if (allSteps?.Any() == true)
            {
                // 1) Step provider type == Cancellation
                // 2) Step provider == Juno.Execution.Providers.Diagnostics.CancellationProvider (backwards compatibility)
                // 3) Step is not already in a completed state (i.e. Succeeded, Failed, Cancelled).
                // Order steps by sequence in ascending order.
                IEnumerable<ExperimentStepInstance> cancellationSteps = allSteps
                    .Where(step => (step.StepType == SupportedStepType.Cancellation || step.Definition.ComponentType == typeof(CancellationProvider).FullName)
                        && !ExecutionResult.CompletedStatuses.Contains(step.Status))?
                    .OrderBy(step => step.Sequence);

                if (cancellationSteps?.Any() == true)
                {
                    selectedSteps.AddRange(cancellationSteps
                        .OrderBy(step => step.Sequence)
                        .Where(step => step.Sequence == cancellationSteps.First().Sequence));
                }
            }

            return new StepSelectionResult(!selectedSteps.Any(), selectedSteps);
        }
    }
}
