namespace Juno.Scheduler.WebJobHost
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Juno.DataManagement;
    using Juno.Scheduler.Management;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Repository.KeyVault;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// An abstraction of the Goal Based Scheduler Execution Functions
    /// </summary>
    public class ExecutionFunctions
    {
        private readonly ILogger logger;
        private readonly IExperimentClient experimentClient;
        private readonly IScheduleTimerDataManager scheduleTimerManager;
        private readonly IScheduleDataManager scheduleDataManager;
        private readonly IExperimentTemplateDataManager experimentTemplateDataManager;
        private readonly IAzureKeyVault azureKeyVaut;
        private readonly IConfiguration configuration;
        private readonly IExperimentDataManager experimentDataManager;

        /// <summary>
        /// Initializes a new instance of <see cref="ExecutionFunctions"/>
        /// </summary>
        /// <param name="logger">A logger used for capturing telemetry emitted from the webjob.</param>
        /// <param name="experimentClient">A client to interact with commonly used endpoints in the juno ecosystem.</param>
        /// <param name="scheduleTimerManager">A data manager used for interacting with target goals stored in a repository.</param>
        /// <param name="scheduleDataManager">A data manager used for interacting with execution goals stored in a repository.</param>
        /// <param name="experimentTemplateDataManager">A data manager used for interacting with experiment templates stored in a repository.</param>
        /// <param name="azureKeyVault">A client used for interacting with entities stored in azure key vault.</param>
        /// <param name="configuration">Settings that offer context into the current runtime environment.</param>
        /// <param name="experimentDataManager">A data manager used for interacting with experiments stored in a repository.</param>
        public ExecutionFunctions(ILogger logger, IExperimentClient experimentClient, IScheduleTimerDataManager scheduleTimerManager, IScheduleDataManager scheduleDataManager, IExperimentTemplateDataManager experimentTemplateDataManager, IAzureKeyVault azureKeyVault, IConfiguration configuration, IExperimentDataManager experimentDataManager)
        {
            logger.ThrowIfNull(nameof(logger));

            this.logger = logger;
            this.experimentClient = experimentClient;
            this.scheduleTimerManager = scheduleTimerManager;
            this.scheduleDataManager = scheduleDataManager;
            this.experimentTemplateDataManager = experimentTemplateDataManager;
            this.azureKeyVaut = azureKeyVault;
            this.configuration = configuration;
            this.experimentDataManager = experimentDataManager;
        }

        /// <summary>
        /// Entry point of Azure web job.
        /// Triggers all GoalBasedSchedule Execution
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the current thread of execution.</param>
        /// <returns>An awaitable <see cref="Task"/></returns>
        [NoAutomaticTrigger]
        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            using (CancellationTokenSource tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                EventContext workflowTelemetryContext = new EventContext(Program.SessionCorrelationId);

                try
                {
                    await this.logger.LogTelemetryAsync($"{Program.HostName}.Workflow", workflowTelemetryContext, async () =>
                    {
                        try
                        {
                            while (!tokenSource.IsCancellationRequested)
                            {
                                if (Program.IsShutdownRequested())
                                {
                                    await Program.Logger.LogTelemetryAsync($"{Program.HostName}.ShutdownRequested", LogLevel.Warning, new EventContext(Program.SessionCorrelationId))
                                        .ConfigureDefaults();

                                    tokenSource.Cancel();
                                    break;
                                }

                                if (!Program.IsRunningInStagingSlot())
                                {
                                    EventContext telemetryContext = EventContext.Persist(Guid.NewGuid());

                                    await this.logger.LogTelemetryAsync("GBSchedulerExecution.GBSExecution", telemetryContext, async () =>
                                    {
                                        try
                                        {
                                            IServiceCollection services = new ServiceCollection();
                                            services.AddSingleton(this.logger);
                                            services.AddSingleton(this.scheduleDataManager);
                                            services.AddSingleton(this.scheduleTimerManager);
                                            services.AddSingleton(this.configuration);
                                            services.AddSingleton(this.azureKeyVaut);
                                            services.AddSingleton(this.experimentClient);
                                            services.AddSingleton(this.experimentTemplateDataManager);
                                            services.AddSingleton(this.experimentDataManager);

                                            GoalBasedSchedulerExecution executionManager = new GoalBasedSchedulerExecution(services, this.configuration);
                                            await executionManager.TriggerSchedulesAsync(tokenSource.Token).ConfigureDefaults();
                                        }
                                        catch (SchedulerException exc)
                                        {
                                            telemetryContext.AddError(exc, true);
                                        }
                                    }).ConfigureDefaults();
                                }

                                await Task.Delay(TimeSpan.FromSeconds(10)).ConfigureDefaults();
                            }
                        }
                        catch (TaskCanceledException)
                        {
                            // When a request happens for the service to shutdown, it can throw a TaskCanceledException.
                            // This is expected and should be handled.
                        }
                    }).ConfigureDefaults();
                }
                catch (Exception exc)
                {
                    EventContext telemetryContext = new EventContext(Guid.NewGuid())
                        .AddError(exc);

                    await this.logger.LogTelemetryAsync("GBSchedulerExecution.StartupError", LogLevel.Error, telemetryContext)
                        .ConfigureDefaults();
                }
            }
        }
    }
}
