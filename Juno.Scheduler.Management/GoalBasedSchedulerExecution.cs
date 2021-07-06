namespace Juno.Scheduler.Management
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Juno.DataManagement;
    using Juno.Extensions.Telemetry;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Repository.KeyVault;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using NCrontab;

    /// <summary>
    /// Executes a set of GoalBasedSchedules.
    /// </summary>
    public class GoalBasedSchedulerExecution
    {
        private static readonly TimeSpan LookupGracePeriod = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Creates an Instance of <see cref="GoalBasedSchedulerExecution"/>
        /// </summary>
        /// <param name="services">A list of services used for dependency injection.</param>
        /// <param name="configuration">A configuration that offers information about the runtime environment.</param>
        public GoalBasedSchedulerExecution(IServiceCollection services, IConfiguration configuration)
        {
            services.ThrowIfNull(nameof(services));
            configuration.ThrowIfNull(nameof(configuration));

            this.ValidateServices(services);
            this.Services = services;
            this.Configuration = configuration;
            this.Logger = services.GetService<ILogger>();

            if (!services.TryGetService<ExecutionGoalHandler>(out ExecutionGoalHandler executionGoalHandler))
            {
                executionGoalHandler = new ExecutionGoalHandler(services);
            }

            this.ExecutionGoalHandler = executionGoalHandler;
        }

        /// <summary>
        /// List of services used for execution of Execution Goals
        /// </summary>
        protected IServiceCollection Services { get; }

        /// <summary>
        /// Mechanism that can evaluate an entire Execution Goal.
        /// </summary>
        protected ExecutionGoalHandler ExecutionGoalHandler { get; }

        /// <summary>
        /// Configuration for current execution
        /// </summary>
        protected IConfiguration Configuration { get; }

        /// <summary>
        /// A logger that can be used to capture telemetry.
        /// </summary>
        protected ILogger Logger { get; }

        /// <summary>
        /// Executes the Goal Based Schedules that are stored in cosmos DB
        /// </summary>
        /// <param name="token">A token that can be used to cancel the current thread of execution.</param>
        /// <returns>An awaitable <see cref="Task"/></returns>
        public async Task TriggerSchedulesAsync(CancellationToken token)
        {
            EventContext telemetryContext = EventContext.Persisted();
            IScheduleTimerDataManager timerDataManager = this.Services.GetService<IScheduleTimerDataManager>();
            await this.Logger.LogTelemetryAsync($"{nameof(GoalBasedSchedulerExecution)}.ExecuteExecutionGoals", telemetryContext, async () =>
            {
                IEnumerable<TargetGoalTrigger> goals = await timerDataManager.GetTargetGoalTriggersAsync(token).ConfigureDefaults();

                IEnumerable<TargetGoalTrigger> enabledGoals = goals.Where(trigger => trigger.Enabled);
                telemetryContext.AddContext(enabledGoals);

                IEnumerable<TargetGoalTrigger> candidateGoals = this.PruneTriggers(enabledGoals, telemetryContext);
                telemetryContext.AddContext(candidateGoals);

                await this.ExecuteSchedulesAsync(candidateGoals, token).ConfigureDefaults();
            }).ConfigureDefaults();
        }

        private IEnumerable<TargetGoalTrigger> PruneTriggers(IEnumerable<TargetGoalTrigger> targetGoals, EventContext telemetryContext)
        {
            DateTime startTime = DateTime.UtcNow.Subtract(GoalBasedSchedulerExecution.LookupGracePeriod);
            DateTime endTime = DateTime.UtcNow.Add(GoalBasedSchedulerExecution.LookupGracePeriod);

            return this.Logger.LogTelemetry($"{nameof(GoalBasedSchedulerExecution)}.PruneTriggers", telemetryContext, () =>
            {
                List<TargetGoalTrigger> result = new List<TargetGoalTrigger>();
                foreach (TargetGoalTrigger targetGoal in targetGoals)
                {
                    try
                    {
                        // Note: This can be acheived with a LINQ expression. Although that is not done here
                        // for the sake of local debuggability, and being able to override this conditional.
                        if (targetGoal.HasOccurence(startTime, endTime))
                        {
                            result.Add(targetGoal);
                        }
                    }
                    catch (CrontabException exc)
                    {
                        this.Logger.LogError($"{nameof(GoalBasedSchedulerExecution)}.PruneTriggersError", exc);
                    }
                }

                return result;
            });
        }

        private Task ExecuteSchedulesAsync(IEnumerable<TargetGoalTrigger> targetGoals, CancellationToken token)
        {
            List<Task> tasks = new List<Task>(targetGoals.Select(targetGoal => this.ExecuteScheduleAsync(targetGoal, token)));
            return Task.WhenAll(tasks);
        }

        private async Task ExecuteScheduleAsync(TargetGoalTrigger targetGoal, CancellationToken token)
        {
            Item<GoalBasedSchedule> executionGoal = await this.GetExecutionGoalAsync(targetGoal, token).ConfigureDefaults();
            ScheduleContext scheduleContext = new ScheduleContext(executionGoal, targetGoal, this.Configuration);
            await this.ExecutionGoalHandler.ExecuteExecutionGoalAsync(executionGoal.Definition, scheduleContext, token).ConfigureDefaults();
        }

        private Task<Item<GoalBasedSchedule>> GetExecutionGoalAsync(TargetGoalTrigger targetGoal, CancellationToken token)
        {
            IScheduleDataManager dataManager = this.Services.GetService<IScheduleDataManager>();
            return dataManager.GetExecutionGoalAsync(targetGoal.ExecutionGoal, targetGoal.TeamName, token);
        }

        /// <summary>
        /// Method to validate the Paramaters given to the Execution Engine
        /// The Services that are required to be passed in are:
        /// 1. IScheduleDataManager : Retrieving Schedules From Cosmos
        /// 2. IScheduleTimerDataManager : Retrieving Timer Data from Cosmos Table
        /// 3. IExperimentClient : Access to Experiment API
        /// 4. IExperimentTemplateDataManager : Retrieving Experiment Definitions
        /// 5. ILogger : Log information during Execution
        /// 6. IAzureKeyValut : Ability to Access APIs and Azure Resources
        /// </summary>
        private void ValidateServices(IServiceCollection services)
        {
            List<Type> missingServices = new List<Type>();
            if (!services.HasService<IScheduleDataManager>())
            {
                missingServices.Add(typeof(IScheduleDataManager));
            }

            if (!services.HasService<IScheduleTimerDataManager>())
            {
                missingServices.Add(typeof(IScheduleTimerDataManager));
            }

            if (!services.HasService<IExperimentClient>())
            {
                missingServices.Add(typeof(IExperimentClient));
            }

            if (!services.HasService<IExperimentTemplateDataManager>())
            {
                missingServices.Add(typeof(IExperimentTemplateDataManager));
            }

            if (!services.HasService<ILogger>())
            {
                missingServices.Add(typeof(ILogger));
            }

            if (!services.HasService<IAzureKeyVault>())
            {
                missingServices.Add(typeof(IAzureKeyVault));
            }

            if (missingServices.Any())
            {
                throw new SchedulerException(
                    $"Required dependencies missing. The GBScheduler Execution requires the following dependencies that were " +
                    $"not provided: {string.Join(", ", missingServices.Select(d => d.Name))}");
            }

        }
    }
}
