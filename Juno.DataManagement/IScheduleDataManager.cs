namespace Juno.DataManagement
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Contracts;

    /// <summary>
    /// Data manager for reading Execution Goals from cosmos
    /// </summary>
    public interface IScheduleDataManager
    {
        /// <summary>
        /// Creates a new execution goal in cosmos
        /// </summary>
        /// <param name="executionGoal">The execution goal to create</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns>The Item that was created in cosmos</returns>
        Task<Item<GoalBasedSchedule>> CreateExecutionGoalAsync(Item<GoalBasedSchedule> executionGoal, CancellationToken cancellationToken);

        /// <summary>
        /// Retrieves the Execution Goal definition from cosmos.
        /// </summary>
        /// <param name="executionGoal">Name of the execution goal</param>
        /// <param name="teamName">Team name that owns the execution goal</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The execution goal requested</returns>
        Task<Item<GoalBasedSchedule>> GetExecutionGoalAsync(string executionGoal, string teamName, CancellationToken cancellationToken);

        /// <summary>
        /// Retrieves the Execution Goal definitions from cosmos.
        /// </summary>
        /// <param name="teamName">The name of the team that owns the execution goals.</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns>An <see cref="IEnumerable{GoalBasedSchedule}"/> corresponding to the requested execution goals</returns>
        Task<IEnumerable<Item<GoalBasedSchedule>>> GetExecutionGoalsAsync(CancellationToken cancellationToken, string teamName = null);

        /// <summary>
        /// Retrieves the Execution Goal definitions from cosmos.
        /// </summary>
        /// <param name="teamName">The name of the team that owns the execution goals.</param>
        /// <param name="executionGoal">Name of the execution goal</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns>An <see cref="IEnumerable{GoalBasedSchedule}"/> corresponding to the requested execution goals</returns>
        Task<IEnumerable<ExecutionGoalSummary>> GetExecutionGoalsInfoAsync(CancellationToken cancellationToken, string teamName = null, string executionGoal = null);

        /// <summary>
        /// Updates the execution goal definition in cosmos.
        /// </summary>
        /// <param name="executionGoal">The execution goal to be updated</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns></returns>
        Task<Item<GoalBasedSchedule>> UpdateExecutionGoalAsync(Item<GoalBasedSchedule> executionGoal, CancellationToken cancellationToken);

        /// <summary>
        /// Deletes the Execution Goal from the system
        /// </summary>
        /// <param name="executionGoalId">The Id that corresponds to an execution goal in the system</param>
        /// <param name="teamName">The team name that owns the Execution Goal</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns><see cref="Task"/></returns>
        Task DeleteExecutionGoalAsync(string executionGoalId, string teamName, CancellationToken cancellationToken);

        /// <summary>
        /// Creates a new execution goal template in cosmos
        /// </summary>
        /// <param name="executionGoalItem">The execution goal to create</param>
        /// <param name="replaceIfExists">Replace file is already present</param>  
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns>The Item that was created in cosmos</returns>
        Task<Item<GoalBasedSchedule>> CreateExecutionGoalTemplateAsync(Item<GoalBasedSchedule> executionGoalItem, bool replaceIfExists, CancellationToken cancellationToken);

        /// <summary>
        /// Retrieve an execution goal template from the system
        /// </summary>
        /// <param name="executionGoalTemplateId">The id of the template to retrieve</param>
        /// <param name="teamName">The team name that owns the execution goal template</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns></returns>
        Task<Item<GoalBasedSchedule>> GetExecutionGoalTemplateAsync(string executionGoalTemplateId, string teamName, CancellationToken cancellationToken);

        /// <summary>
        /// Retrieve all execution goal templates associated with a team name
        /// </summary>
        /// <param name="teamName">Team that owns the execution goals</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns></returns>
        Task<IEnumerable<Item<GoalBasedSchedule>>> GetExecutionGoalTemplatesAsync(CancellationToken cancellationToken, string teamName = null);

        /// <summary>
        /// Deletes the Execution Goal Template from the system
        /// </summary>
        /// <param name="templateId">The Id that corresponds to an execution goal template in the system</param>
        /// <param name="teamName">The team name that owns the Execution Goal template</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns></returns>
        Task DeleteExecutionGoalTemplateAsync(string templateId, string teamName, CancellationToken cancellationToken);

        /// <summary>
        /// Retrieve a list of metadata about execution goal template from the system
        /// </summary>
        /// <param name="teamName">The team that owns the execution goal templates</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <param name="templateId">Optional Id of a specific execution goal template</param>
        /// <returns></returns>
        Task<IEnumerable<ExecutionGoalSummary>> GetExecutionGoalTemplateInfoAsync(CancellationToken cancellationToken, string teamName = null, string templateId = null);
    }
}
