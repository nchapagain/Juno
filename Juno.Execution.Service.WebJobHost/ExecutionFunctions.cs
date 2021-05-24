namespace Juno.Execution.Service
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Api.Client;
    using Juno.Contracts.Configuration;
    using Juno.Execution.Management;
    using Juno.Extensions.Telemetry;
    using Juno.Hosting.Common;
    using Juno.Providers;
    using Microsoft.Azure.CRC.AspNetCore;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Repository.KeyVault;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Encapsulate juno azure webjob functions
    /// </summary>
    public class ExecutionFunctions
    {
        private const string ExecutionManagerInstancesVariable = "EXECUTION_MANAGER_INSTANCES";
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly EnvironmentSettings settings;
        private readonly SamplingOptions workflowTelemetrySampling;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExecutionFunctions"/> class.
        /// </summary>
        /// <param name="logger">The telemetry logger.</param>
        /// <param name="configuration">Configuration settings for the environment.</param>
        public ExecutionFunctions(ILogger logger, IConfiguration configuration)
        {
            this.logger = logger;
            this.configuration = configuration;
            this.settings = EnvironmentSettings.Initialize(configuration);
            this.workflowTelemetrySampling = new SamplingOptions
            {
                SamplingRate = 20
            };
        }

        /// <summary>
        /// This is entry point of azure webjob. Currently it is set to 'no automatic trigger' and this will be
        /// executed only once when the host start.
        /// </summary>
        /// <returns></returns>
        [NoAutomaticTrigger]
        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            // Create linked cancellation token. 
            // The execution can be cancelled when the job stopped by host or when the cancel signal is stored in juno cosmos table
            using (CancellationTokenSource tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                EventContext workflowTelemetryContext = new EventContext(Program.SessionCorrelationId);

                try
                {
                    bool runningInStagingSlot = Program.IsRunningInStagingSlot();
                    workflowTelemetryContext.AddContext("runningInStagingSlot", runningInStagingSlot);

                    int executionManagerInstances = ExecutionFunctions.GetExecutionManagerInstanceCount();
                    workflowTelemetryContext.AddContext("executionManagerInstances", executionManagerInstances);

                    await this.logger.LogTelemetryAsync($"{Program.HostName}.Workflow", workflowTelemetryContext, async () =>
                    {
                        try
                        {
                            while (!tokenSource.IsCancellationRequested)
                            {
                                if (!runningInStagingSlot)
                                {
                                    // Create execution managers each with its own separate dependencies.
                                    List<ExecutionManager> executionManagers = new List<ExecutionManager>();
                                    for (int instanceCount = 0; instanceCount < executionManagerInstances; instanceCount++)
                                    {
                                        // Execution Service and Execution API AAD principals used to authenticate. The Execution Service principal is used to authenticate with AAD
                                        // in order to get a JWT/token that can be used to authenticate with the Execution API.
                                        EnvironmentSettings settings = EnvironmentSettings.Initialize(this.configuration);
                                        AadPrincipalSettings executionSvcPrincipal = settings.ExecutionSettings.AadPrincipals.Get(Setting.ExecutionSvc);
                                        AadPrincipalSettings executionApiPrincipal = settings.ExecutionSettings.AadPrincipals.Get(Setting.ExecutionApi);

                                        ExecutionClient apiClient = HostDependencies.CreateExecutionApiClient(
                                            executionSvcPrincipal,
                                            executionApiPrincipal,
                                            settings.ExecutionSettings.ExecutionApiUri);

                                        IServiceCollection services = new ServiceCollection()
                                            .AddSingleton<ILogger>(this.logger)
                                            .AddSingleton<IConfiguration>(this.configuration)
                                            .AddSingleton<ExecutionClient>(apiClient)
                                            .AddSingleton<IProviderDataClient>((provider) => new ProviderDataClient(apiClient, logger: this.logger))
                                            .AddSingleton<IAzureKeyVault>(HostDependencies.CreateKeyVaultClient(executionSvcPrincipal, this.settings.KeyVaultSettings.Get(Setting.Default)));

                                        // If a work queue is defined on the command-line, it will be used to override
                                        // the queue defined in the configuration file.
                                        executionManagers.Add(new ExecutionManager(services, this.configuration, workQueue: Program.OverrideWorkQueue));
                                    }

                                    while (true)
                                    {
                                        if (tokenSource.IsCancellationRequested)
                                        {
                                            tokenSource.Cancel();
                                            break;
                                        }

                                        if (Program.IsShutdownRequested())
                                        {
                                            await Program.Logger.LogTelemetryAsync($"{Program.HostName}.ShutdownRequested", LogLevel.Warning, new EventContext(Program.SessionCorrelationId))
                                                .ConfigureDefaults();

                                            tokenSource.Cancel();
                                            break;
                                        }

                                        // We want to persist a new activity ID for each round of execution.  This will group each round
                                        // of execution with all events emitted by the execution manager...and experiment providers.
                                        EventContext telemetryContext = EventContext.Persist(Guid.NewGuid());

                                        var stopwatch = new Stopwatch();
                                        stopwatch.Start();
                                        int numExperimentsProcessed = 0;

                                        try
                                        {
                                            List<Task> executionTasks = new List<Task>();
                                            foreach (ExecutionManager executionManager in executionManagers)
                                            {
                                                executionTasks.Add(executionManager.ExecuteAsync(tokenSource.Token));
                                            }

                                            await Task.WhenAll(executionTasks).ConfigureDefaults();
                                            numExperimentsProcessed = executionManagers.Sum(mgr => mgr.ExperimentsProcessedCount);
                                        }
                                        catch (Exception exception)
                                        {
                                            telemetryContext.AddError(exception, true);
                                            await this.logger.LogTelemetryAsync($"{Program.HostName}.WorkflowError", LogLevel.Error, telemetryContext)
                                                .ConfigureDefaults();
                                        }
                                        finally
                                        {
                                            // Do not wait if cancellation has been requested. Exit promptly.
                                            if (!tokenSource.IsCancellationRequested)
                                            {
                                                // If we did not process any experiments at all, we need to throttle down. Otherwise, we cycle around
                                                // back into processing immediately.
                                                if (numExperimentsProcessed <= 0)
                                                {
                                                    // If we did not process anything, let's not throttle the VM unnecessarily.
                                                    await Task.Delay(TimeSpan.FromMinutes(1), tokenSource.Token).ConfigureDefaults();
                                                }
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    // If we are running in the staging slot lets not throttle the VM unnecessarily.
                                    await Task.Delay(TimeSpan.FromMinutes(5), tokenSource.Token).ConfigureDefaults();
                                }
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
                    workflowTelemetryContext.AddError(exc);
                    await this.logger.LogTelemetryAsync($"{Program.HostName}.StartupError", LogLevel.Error, workflowTelemetryContext)
                        .ConfigureDefaults();
                }
            }
        }

        private static int GetExecutionManagerInstanceCount()
        {
            // By default, 
            int instanceCount = Environment.ProcessorCount;
            string configuredInstancesSetting = HostingContext.GetEnvironmentVariableValue(ExecutionFunctions.ExecutionManagerInstancesVariable);
            int configuredInstances;
            if (!string.IsNullOrWhiteSpace(configuredInstancesSetting) && int.TryParse(configuredInstancesSetting, out configuredInstances))
            {
                instanceCount = configuredInstances;
            }

            return instanceCount <= 0 ? 1 : instanceCount; // must be at least 1 thread.
        }

        private static void InitializeTelemetrySamplingDefinitions()
        {
            SamplingOptions.Definitions.AddRange(new List<SamplingOptions>
            {
                // Minimize the number of highly redundant telemetry events in the
                // execution workflow
                new SamplingOptions
                {
                    Name = nameof(ExecutionManager),
                    SamplingRate = 100
                },
                new SamplingOptions
                {
                    Name = $"{nameof(ExecutionManager)}.GetNotice",
                    SamplingRate = 100
                }
            });
        }
    }
}
