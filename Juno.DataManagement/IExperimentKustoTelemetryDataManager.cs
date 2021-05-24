namespace Juno.DataManagement
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Contracts;

    /// <summary>
    /// Data Manager for reading Telemetry for Execution Goal
    /// </summary>
    public interface IExperimentKustoTelemetryDataManager
    {
        /// <summary>
        /// Retrieves the Execution Goal Status from Telemetry.
        /// </summary>
        /// <param name="executionGoal">Name of the execution goal</param>
        /// <param name="teamName">Team name that owns the execution goal (optional)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Status of execution goal</returns>
        Task<IList<ExperimentInstanceStatus>> GetExecutionGoalStatusAsync(string executionGoal, CancellationToken cancellationToken, string teamName = null);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="executionGoal"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<IList<TargetGoalTimeline>> GetExecutionGoalTimelineAsync(GoalBasedSchedule executionGoal, CancellationToken cancellationToken);
    }
}
