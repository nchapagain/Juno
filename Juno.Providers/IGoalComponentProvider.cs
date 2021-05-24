namespace Juno.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// 
    /// </summary>
    public interface IGoalComponentProvider
    {
        /// <summary>
        /// Allows derived classes to configure services needed
        /// for proper execution of other implemented methods
        /// </summary>
        /// <param name="component">
        /// Component that provides a list of parameters which
        /// details the services needed for execution
        /// </param>
        /// <param name="scheduleContext">Gives context to the Provider so that it understands
        /// in the context in which it is executing in.</param>
        /// <returns></returns>
        Task ConfigureServicesAsync(GoalComponent component, ScheduleContext scheduleContext);
    }
}
