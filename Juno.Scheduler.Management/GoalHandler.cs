namespace Juno.Scheduler.Management
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Extensions.Telemetry;
    using Juno.Providers;
    using Juno.Scheduler.Preconditions;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Executes a Goal defined in a goal based schedule. 
    /// </summary>
    public class GoalHandler
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GoalHandler"/> class.
        /// </summary>
        /// <param name="services">Collection of services that can be used for dependency injection.</param>
        public GoalHandler(IServiceCollection services)
        {
            services.ThrowIfNull(nameof(services));
            this.Services = services;
            this.Logger = services.GetService<ILogger>();
        }

        /// <summary>
        /// Collection of services that can be used for dependency injection.
        /// </summary>
        protected IServiceCollection Services { get; }

        /// <summary>
        /// Logger used for capturing telemetry
        /// </summary>
        protected ILogger Logger { get; }

        /// <summary>
        /// Evaluates the preconditions of a Goal and if all conditions are satisfied then 
        /// executes all of the actions that are tied with it. 
        /// </summary>
        /// <param name="goal">A goal that offers instructions on how to evaluate.</param>
        /// <param name="scheduleContext">A object that offers context to the current target goal execution.</param>
        /// <param name="token">A token that can be used to cancel the current thread of execution.</param>
        public virtual async Task<bool> ExecuteGoalAsync(Goal goal, ScheduleContext scheduleContext, CancellationToken token)
        {
            goal.ThrowIfNull(nameof(goal));
            scheduleContext.ThrowIfNull(nameof(scheduleContext));

            EventContext telemetryContext = EventContext.Persisted();
            return await this.Logger.LogTelemetryAsync($"{typeof(GoalHandler).Name}.ExecuteGoal", telemetryContext, async () =>
            {
                bool conditionSatisfied = await this.EvaluatePreconditionsAsync(goal, scheduleContext, telemetryContext, token).ConfigureDefaults();
                telemetryContext.AddContext(nameof(conditionSatisfied), conditionSatisfied);

                if (conditionSatisfied)
                {
                    await this.ExecuteActionsAsync(goal, scheduleContext, telemetryContext, token).ConfigureDefaults();
                }

                return conditionSatisfied;
            }).ConfigureDefaults();
        }

        private async Task<bool> EvaluatePreconditionsAsync(Goal goal, ScheduleContext scheduleContext, EventContext telemetryContext, CancellationToken token)
        {
            return await this.Logger.LogTelemetryAsync($"{typeof(GoalHandler).Name}.EvaluatePrecondition", telemetryContext, async () =>
            {
                List<Task<bool>> preconditionTasks = new List<Task<bool>>();
                
                bool isTargetGoal = goal.IsTargetGoal(scheduleContext.ExecutionGoal);
                foreach (Precondition precondition in goal.Preconditions)
                {
                    // Timer logic is handled by GoalbasedScheulerExecution for target goals. Although control goals may still have this and need to be executed.
                    if (!precondition.Type.Equals(typeof(TimerTriggerProvider).FullName, StringComparison.OrdinalIgnoreCase) || !isTargetGoal)
                    {
                        IPreconditionProvider preconditionProvider = GoalComponentProviderFactory.CreatePreconditionProvider(precondition, this.Services);
                        preconditionTasks.Add(preconditionProvider.IsConditionSatisfiedAsync(precondition, scheduleContext, token));
                    }
                }

                IEnumerable<bool> results = await Task.WhenAll(preconditionTasks).ConfigureDefaults();
                return !results?.Any() == true || results?.All(result => result) == true;
            }).ConfigureDefaults();
        }

        private async Task ExecuteActionsAsync(Goal goal, ScheduleContext scheduleContext, EventContext telemetryContext, CancellationToken token)
        {
            await this.Logger.LogTelemetryAsync($"{typeof(GoalHandler).Name}.ExecuteScheduleAction", telemetryContext, async () =>
            {
                foreach (ScheduleAction action in goal.Actions)
                {
                    IScheduleActionProvider actionProvider = GoalComponentProviderFactory.CreateScheduleActionProvider(action, this.Services);
                    await actionProvider.ExecuteActionAsync(action, scheduleContext, token).ConfigureDefaults();
                }
            }).ConfigureDefaults();
        }
    }
}
