namespace Juno.DataManagement
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;

    /// <summary>
    /// Provides methods for managing Target Goal operations.
    /// </summary>
    public interface IScheduleTimerDataManager
    {
        /// <summary>
        /// Creates TargetGoalTriggers in cosmos 
        /// </summary>
        /// <param name="executionGoal">The execution goal containing a valid list of target goals</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns></returns>
        Task CreateTargetGoalsAsync(GoalBasedSchedule executionGoal, CancellationToken cancellationToken);

        /// <summary>
        /// Retrieves Target goal triggers from Cosmos
        /// </summary>
        /// <param name="token"><see cref="CancellationToken"/></param>
        /// <returns><see cref="IEnumerable{TargetGoalTrigger}"/></returns>
        Task<IEnumerable<TargetGoalTrigger>> GetTargetGoalTriggersAsync(CancellationToken token);

        /// <summary>
        /// Retrieves a specific Target Goal Trigger from Cosmos
        /// </summary>
        /// <param name="version">The version of the schedule (Version is used as a partition key) </param>
        /// <param name="rowKey">Unique identifier for a TargetGoalTrigger</param>
        /// <param name="token"><see cref="CancellationToken"/></param>
        /// <returns><see cref="Task{TargetGoalTrigger}"/></returns>
        Task<TargetGoalTrigger> GetTargetGoalTriggerAsync(string version, string rowKey, CancellationToken token);
        
        /// <summary>
        /// Updates a specific Target Goal Trigger
        /// </summary>
        /// <param name="trigger"><see cref="TargetGoalTrigger"/></param>
        /// <param name="token"><see cref="CancellationToken"/></param>
        /// <returns><see cref="Task"/></returns>
        Task UpdateTargetGoalTriggerAsync(TargetGoalTrigger trigger, CancellationToken token);

        /// <summary>
        /// Update all of the target goals inside the execution goal
        /// </summary>
        /// <param name="executionGoal">The execution which consists the target goals</param>
        /// <param name="token"><see cref="CancellationToken"/></param>
        /// <returns><see cref="Task"/></returns>
        Task UpdateTargetGoalTriggersAsync(GoalBasedSchedule executionGoal, CancellationToken token);

        /// <summary>
        /// Delete all Target Goals assoicated with the given execution goal
        /// </summary>
        /// <param name="executionGoalId">Execution goal id that refers to the execution goal
        /// that has the Target goals</param>
        /// <param name="token"><see cref="CancellationToken"/></param>
        /// <returns><see cref="Task"/></returns>
        Task DeleteTargetGoalTriggersAsync(string executionGoalId, CancellationToken token);
    }
}
