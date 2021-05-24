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
                bool conditionSatisfied = await this.EvaluatePreconditionsAsync(goal.Preconditions, scheduleContext, telemetryContext, token).ConfigureDefaults();
                telemetryContext.AddContext(nameof(conditionSatisfied), conditionSatisfied);

                if (conditionSatisfied)
                {
                    await this.ExecuteActionsAsync(goal.Actions, scheduleContext, telemetryContext, token).ConfigureDefaults();
                }

                return conditionSatisfied;
            }).ConfigureDefaults();
        }

        private async Task<bool> EvaluatePreconditionsAsync(IEnumerable<Precondition> preconditions, ScheduleContext scheduleContext, EventContext telemetryContext, CancellationToken token)
        {
            return await this.Logger.LogTelemetryAsync($"{typeof(GoalHandler).Name}.EvaluatePrecondition", telemetryContext, async () =>
            {
                List<Task<PreconditionResult>> preconditionTasks = new List<Task<PreconditionResult>>();
                foreach (Precondition precondition in preconditions)
                {
                    if (!precondition.Type.Equals(typeof(TimerTriggerProvider).FullName, StringComparison.OrdinalIgnoreCase))
                    {
                        IPreconditionProvider preconditionProvider = GoalComponentProviderFactory.CreatePreconditionProvider(precondition, this.Services);
                        preconditionTasks.Add(preconditionProvider.IsConditionSatisfiedAsync(precondition, scheduleContext, token));
                    }
                }

                IEnumerable<PreconditionResult> results = await Task.WhenAll(preconditionTasks).ConfigureDefaults();
                return results.ArePreconditionsSatisfied();
            }).ConfigureDefaults();
        }

        private async Task ExecuteActionsAsync(IEnumerable<ScheduleAction> actions, ScheduleContext scheduleContext, EventContext telemetryContext, CancellationToken token)
        {
            await this.Logger.LogTelemetryAsync($"{typeof(GoalHandler).Name}.ExecuteScheduleAction", telemetryContext, async () =>
            {
                foreach (ScheduleAction action in actions)
                {
                    IScheduleActionProvider actionProvider = GoalComponentProviderFactory.CreateScheduleActionProvider(action, this.Services);
                    await actionProvider.ExecuteActionAsync(action, scheduleContext, token).ConfigureDefaults();
                }
            }).ConfigureDefaults();
        }
    }
}
