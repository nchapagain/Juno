namespace Juno.Providers
{
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;

    /// <summary>
    /// Provides methods for handling the runtime requirements of 
    /// a Control Goal's and Target Goal's Preconditions.
    /// </summary>
    public interface IPreconditionProvider : IGoalComponentProvider
    {
        /// <summary>
        /// Executes the logic required to handle the runtime requirements of the 
        /// Precondition component. 
        /// </summary>
        /// <param name="component">The Precondition component that describes the runtime requirements </param>
        /// <param name="scheduleContext"></param>
        /// <param name="cancellationToken">A token that can be used to request the provider to cancel it's operations.</param>
        /// <returns>True/False if the precondition is satisfied.</returns>
        Task<bool> IsConditionSatisfiedAsync(Precondition component, ScheduleContext scheduleContext, CancellationToken cancellationToken);
    }
}
