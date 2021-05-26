namespace Juno.Providers
{
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;

    /// <summary>
    /// Provides methods for handling the runtime requirements of
    /// a Control Goal's and Target Goal's ScheduleActions.
    /// </summary>
    public interface IScheduleActionProvider : IGoalComponentProvider
    {
        /// <summary>
        /// Executes the logic required to handle the runtime requirements of the 
        /// Schedule Action
        /// </summary>
        /// <param name="component">The Schedule Action component that describes the runtime requirements </param>
        /// <param name="scheduleContext"></param>
        /// <param name="cancellationToken">A token that can be used to request the provider to cancel its operations</param>
        /// <returns>An awaitable task.</returns>
        Task ExecuteActionAsync(ScheduleAction component, ScheduleContext scheduleContext, CancellationToken cancellationToken);
    }
}
