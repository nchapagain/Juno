namespace Juno.GarbageCollector.WebJobHost
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Api.Client;
    using Juno.DataManagement;
    using Juno.Execution.TipIntegration;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Kusto;
    using Microsoft.Azure.CRC.Repository.KeyVault;
    using Microsoft.Azure.CRC.Subscriptions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Abstraction of Juno Garbage Collector
    /// </summary>
    public class GCExecutionFunction
    {
        private readonly ILogger logger;
        private readonly IExperimentClient experimentClient;
        private readonly IAzureKeyVault azureKeyVaut;
        private readonly IConfiguration configuration;
        private readonly IKustoQueryIssuer kustoIssuer;
        private readonly ITipClient tipClient;
        private readonly IExperimentTemplateDataManager experimentTemplate;
        private readonly IList<ISubscriptionManager> subscriptionManager;

        /// <summary>
        /// Initializes a new instance of <see cref="GCExecutionFunction"/>
        /// </summary>
        /// <param name="logger"><see cref="ILogger"/></param>
        /// <param name="experimentClient"><see cref="IExperimentClient"/></param>
        /// <param name="azureKeyVault"><see cref="IAzureKeyVault"/></param>
        /// <param name="configuration"><see cref="IConfiguration"/></param>
        /// <param name="kusto"><see cref="IKustoQueryIssuer"/></param>
        /// <param name="tipClient"><see cref="ITipClient"/></param>
        /// <param name="experimentTemplate"><see cref="IExperimentTemplateDataManager"/></param>
        /// <param name="subscriptionManager">List of <see cref="ISubscriptionManager"/></param>
        public GCExecutionFunction(
            ILogger logger,
            IExperimentClient experimentClient,
            IAzureKeyVault azureKeyVault,
            IConfiguration configuration,
            IKustoQueryIssuer kusto,
            ITipClient tipClient,
            IExperimentTemplateDataManager experimentTemplate,
            IList<ISubscriptionManager> subscriptionManager)
        {
            logger.ThrowIfNull(nameof(logger));
            experimentClient.ThrowIfNull(nameof(experimentClient));
            azureKeyVault.ThrowIfNull(nameof(azureKeyVault));
            configuration.ThrowIfNull(nameof(configuration));
            kusto.ThrowIfNull(nameof(kusto));
            tipClient.ThrowIfNull(nameof(tipClient));
            experimentTemplate.ThrowIfNull(nameof(experimentTemplate));
            subscriptionManager.ThrowIfNull(nameof(subscriptionManager));

            this.kustoIssuer = kusto;
            this.logger = logger;
            this.experimentClient = experimentClient;
            this.azureKeyVaut = azureKeyVault;
            this.configuration = configuration;
            this.tipClient = tipClient;
            this.experimentTemplate = experimentTemplate;
            this.subscriptionManager = subscriptionManager;
        }

        /// <summary>
        /// Entry point of Azure web job.
        /// Triggers Garbage Collector Execution
        /// </summary>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns><see cref="Task"/></returns>
        [NoAutomaticTrigger]
        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            using (CancellationTokenSource tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                EventContext workflowTelemetryContext = new EventContext(Program.SessionCorrelationId);

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

                                await this.logger.LogTelemetryAsync($"{nameof(GarbageCollectorExecution)}.RunAsync", telemetryContext, async () =>
                                {
                                    IServiceCollection services = new ServiceCollection();
                                    services.AddSingleton(this.logger);
                                    services.AddSingleton(this.configuration);
                                    services.AddSingleton(this.azureKeyVaut);
                                    services.AddSingleton(this.experimentClient);
                                    services.AddSingleton(this.kustoIssuer);
                                    services.AddSingleton(this.tipClient);
                                    services.AddSingleton(this.experimentTemplate);
                                    services.AddSingleton(this.subscriptionManager);
                                    GarbageCollectorExecution garbageCollectorExecution = new GarbageCollectorExecution(services);

                                    await garbageCollectorExecution.RunAsync(tokenSource.Token).ConfigureDefaults();
                                }).ConfigureDefaults();
                            }

                            await Task.Delay(TimeSpan.FromHours(1)).ConfigureDefaults();
                        }
                    }
                    catch (Exception exc)
                    {
                        EventContext telemetryContext = new EventContext(Guid.NewGuid())
                            .AddError(exc);

                        await this.logger.LogTelemetryAsync($"{nameof(GarbageCollectorExecution)}.StartupError", LogLevel.Error, telemetryContext)
                            .ConfigureDefaults();
                    }
                }).ConfigureDefaults();
            }
        }
    }
}
