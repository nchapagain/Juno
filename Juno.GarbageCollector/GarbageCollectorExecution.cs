namespace Juno.GarbageCollector
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Juno.DataManagement;
    using Juno.Execution.TipIntegration;
    using Juno.Extensions.Telemetry;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Kusto;
    using Microsoft.Azure.CRC.Repository.KeyVault;
    using Microsoft.Azure.CRC.Subscriptions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Executes Garbage Collector 
    /// </summary>
    public class GarbageCollectorExecution
    {
        private IList<IGarbageCollector> garbageCollectors;

        /// <summary>
        /// Creates an Instance of <see cref="GarbageCollectorExecution"/>
        /// </summary>
        /// <param name="services"><see cref="IServiceCollection"/></param>
        public GarbageCollectorExecution(IServiceCollection services)
        {
            services.ThrowIfNull(nameof(services));

            this.ValidateServices(services);
            this.Services = services;
            this.Logger = services.GetService<ILogger>();
        }

        /// <summary>
        /// Collection of services used for execution of Garbage Collector
        /// </summary>
        protected IServiceCollection Services { get; }

        /// <summary>
        /// <see cref="IConfiguration"/>
        /// </summary>
        protected IConfiguration Configuration { get; }

        /// <summary>
        /// <see cref="ILogger"/>
        /// </summary>
        protected ILogger Logger { get; }

        /// <summary>
        /// Executes the Garbage Collector
        /// </summary>
        /// <param name="token"><see cref="CancellationToken"/></param>
        /// <returns></returns>
        public async Task RunAsync(CancellationToken token)
        {
            if (!this.Services.TryGetService<IList<IGarbageCollector>>(out this.garbageCollectors))
            {
                this.SetUpGarbageCollectors();
            }

            this.garbageCollectors.ThrowIfNullOrEmpty(nameof(this.garbageCollectors));
            EventContext telemetryContext = EventContext.Persisted();

            Func<IGarbageCollector, Task> garbageCollectors = async (garbageCollector) =>
            {
                // 1. Get Leaked Resources
                IDictionary<string, LeakedResource> leakedResources = null;
                await this.Logger.LogTelemetryAsync($"{nameof(GarbageCollectorExecution)}.GetLeakedResources", telemetryContext, async () =>
                {
                    leakedResources = await garbageCollector.GetLeakedResourcesAsync(token).ConfigureDefaults();
                }).ConfigureDefaults();

                telemetryContext.AddContext(nameof(leakedResources) + "Count", leakedResources.Count);
                await this.Logger.LogTelemetryAsync($"{nameof(GarbageCollectorExecution)}.{garbageCollector.GetType().Name}.{nameof(leakedResources)}", telemetryContext, leakedResources, 15)
                    .ConfigureDefaults();

                // 2. Clean Leaked Resources
                if (leakedResources?.Any() == true)
                {
                    IDictionary<string, string> cleanedResources = null;

                    await this.Logger.LogTelemetryAsync($"{nameof(GarbageCollectorExecution)}.CleanLeakedResources", telemetryContext, async () =>
                    {
                        cleanedResources = await garbageCollector.CleanupLeakedResourcesAsync(leakedResources, token)
                            .ConfigureDefaults();
                    }).ConfigureDefaults();

                    telemetryContext.AddContext(nameof(cleanedResources) + "Count", cleanedResources.Count);
                    await this.Logger.LogTelemetryAsync($"{nameof(GarbageCollectorExecution)}.{garbageCollector.GetType().Name}.{nameof(cleanedResources)}", telemetryContext, cleanedResources, 15)
                        .ConfigureDefaults();
                }
            };

            // Garbage Collector Execution
            await this.Logger.LogTelemetryAsync($"{nameof(GarbageCollectorExecution)}.GarbageCollectorRun", telemetryContext, async () =>
            {
                List<Task> garbageCollectorTasks = new List<Task>();
                foreach (IGarbageCollector garbageCollector in this.garbageCollectors)
                {
                    garbageCollectorTasks.Add(garbageCollectors.Invoke(garbageCollector));
                }

                await Task.WhenAll(garbageCollectorTasks).ConfigureDefaults();
            }).ConfigureDefaults();
        }

        private void SetUpGarbageCollectors()
        {
            this.garbageCollectors = new List<IGarbageCollector>
            {
                new ResourceGroupGarbageCollector(this.Services),
                new TipGarbageCollector(this.Services)
            };

        }

        /// <summary>
        /// Method to validate the Paramaters given to the Execution Engine
        /// The Services that are required to be passed in are:
        /// 1. IExperimentClient : Access to Experiment API
        /// 2. ILogger : Log information during Execution
        /// 3. IKustoIssuer: Access to Kusto Issuer to query
        /// 4. IAzureKeyValut : Ability to Access APIs and Azure Resources    
        /// 6. IConfiguration : Ability to get Experiment Context
        /// 7. ITipClient: Ability to get Tip Sessions
        /// 8. IExperimentTemplateDataManager: Retrieving Experiment Definitions
        /// 9. IList[ISubscriptionManager]: Retrieving resources under Azure Subscription
        /// </summary>
        private void ValidateServices(IServiceCollection services)
        {
            List<Type> missingServices = new List<Type>();

            if (!services.HasService<IExperimentClient>())
            {
                missingServices.Add(typeof(IExperimentClient));
            }

            if (!services.HasService<ILogger>())
            {
                missingServices.Add(typeof(ILogger));
            }

            if (!services.HasService<IKustoQueryIssuer>())
            {
                missingServices.Add(typeof(IKustoQueryIssuer));
            }

            if (!services.HasService<IAzureKeyVault>())
            {
                missingServices.Add(typeof(IAzureKeyVault));
            }

            if (!services.HasService<IConfiguration>())
            {
                missingServices.Add(typeof(IConfiguration));
            }

            if (!services.HasService<ITipClient>())
            {
                missingServices.Add(typeof(ITipClient));
            }

            if (!services.HasService<IExperimentTemplateDataManager>())
            {
                missingServices.Add(typeof(IExperimentTemplateDataManager));
            }

            if (!services.HasService<IList<ISubscriptionManager>>())
            {
                missingServices.Add(typeof(IList<ISubscriptionManager>));
            }

            if (missingServices.Any())
            {
                // [TODO: Create a GC Exception Contract]
                throw new Exception(
                    $"Required dependencies missing. The {nameof(GarbageCollectorExecution)} requires the following dependencies that were " +
                    $"not provided: {string.Join(", ", missingServices.Select(d => d.Name))}");
            }
        }
    }
}
