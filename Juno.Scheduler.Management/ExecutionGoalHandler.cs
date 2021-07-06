namespace Juno.Scheduler.Management
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Extensions.Telemetry;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Executes a Schedule defined by a <see cref="GoalBasedSchedule"/>
    /// </summary>
    public class ExecutionGoalHandler
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExecutionGoalHandler"/> class.
        /// </summary>
        /// <param name="services">Collection of services that can be utilized for dependency injection.</param>
        public ExecutionGoalHandler(IServiceCollection services)
        {
            services.ThrowIfNull(nameof(services));

            this.Services = services;
            this.Logger = services.GetService<ILogger>();

            if (!services.TryGetService<GoalHandler>(out GoalHandler goalHandler))
            {
                goalHandler = new GoalHandler(services);
            }

            this.GoalHandler = goalHandler;
        }

        /// <summary>
        /// Collection of services that can be used for dependency injection.
        /// </summary>
        protected IServiceCollection Services { get; }

        /// <summary>
        /// Service that can execute a <see cref="Goal"/>
        /// </summary>
        protected GoalHandler GoalHandler { get; }

        /// <summary>
        /// Logger used for capturing telemetry
        /// </summary>
        protected ILogger Logger { get; }

        /// <summary>
        /// Evaluates all of the control goals, and if all of them are NOT satisfied then 
        /// Evaluates all of the target goals. 
        /// </summary>
        /// <param name="executionGoal">An execution goal that offers instruction on how to execute.</param>
        /// <param name="scheduleContext">A object that offers context to the current target goal execution.</param>
        /// <param name="token">A token that can be used to cancel the current thread of execution.</param>
        /// <returns>An awaitable task.</returns>
        public virtual async Task ExecuteExecutionGoalAsync(GoalBasedSchedule executionGoal, ScheduleContext scheduleContext, CancellationToken token)
        {
            executionGoal.ThrowIfNull(nameof(executionGoal));
            scheduleContext.ThrowIfNull(nameof(scheduleContext));

            EventContext telemetryContext = EventContext.Persisted();

            await this.Logger.LogTelemetryAsync($"{typeof(ExecutionGoalHandler).Name}.ExecuteExecutionGoal", telemetryContext, async () =>
            {
                telemetryContext.AddContext(nameof(executionGoal.ExperimentName), executionGoal.ExperimentName);

                try
                {
                    bool controlGoalsSatisfied = await this.ExecuteControlGoalsAsync(executionGoal.ControlGoals, scheduleContext, telemetryContext, token)
                        .ConfigureDefaults();

                    if (!controlGoalsSatisfied)
                    {
                        Goal targetGoal = executionGoal.GetGoal(scheduleContext.TargetGoalTrigger.Name);
                        await this.GoalHandler.ExecuteGoalAsync(targetGoal, scheduleContext, token).ConfigureDefaults();
                    }
                }
                catch (Exception exc)
                {
                    telemetryContext.AddError(exc);
                }

            }).ConfigureDefaults();
        }

        private async Task<bool> ExecuteControlGoalsAsync(IList<Goal> goals, ScheduleContext scheduleContext, EventContext telemetryContext, CancellationToken token)
        {
            return await this.Logger.LogTelemetryAsync($"{typeof(ExecutionGoalHandler).Name}.ExecuteControlGoals", telemetryContext, async () =>
            {
                IEnumerable<Task<bool>> tasks = new List<Task<bool>>(goals.Select(g => this.GoalHandler.ExecuteGoalAsync(g, scheduleContext, token)));
                IEnumerable<bool> goalsSatisfied = await Task.WhenAll(tasks).ConfigureDefaults();

                bool result = goalsSatisfied.Any(value => value);
                telemetryContext.AddContext(nameof(result), result);

                return result;
            }).ConfigureDefaults();
        }
    }
}
