namespace Juno.GarbageCollector
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Juno.DataManagement;
    using Juno.Execution.TipIntegration;
    using Juno.Extensions.Telemetry;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Subscriptions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// 
    /// </summary>
    public class ResourceGroupGarbageCollector : IGarbageCollector
    {
        private readonly int loggingBatchSize = 15;
        private readonly string teamName = "CRC AIR";
        private readonly string templateId = "Garbage_Collector_ResourceGroupCleanup.Template.v1.json";

        private ILogger logger;
        private IExperimentTemplateDataManager experimentTemplateDataManager;
        private IExperimentClient experimentClient;
        private IList<ISubscriptionManager> subscriptionManagers;
        private Experiment experimentTemplate;

        /// <summary>
        /// Initializes a new instance of the <see cref="ResourceGroupGarbageCollector"/> class.
        /// </summary>
        /// <param name="services">Service collections</param>
        public ResourceGroupGarbageCollector(IServiceCollection services)
        {
            services.ThrowIfNullOrEmpty(nameof(services));
            this.Services = services;

            this.logger = this.Services.GetService<ILogger>();
            this.logger.ThrowIfNull(nameof(this.logger));

            // this.subscriptionManager = this.Services.GetService<IList<SubscriptionManager>>();

            if (!services.TryGetService<IList<ISubscriptionManager>>(out this.subscriptionManagers))
            {
                this.SetUpSubscriptionManager();
            }

            this.subscriptionManagers.ThrowIfNull(nameof(this.subscriptionManagers));
        }

        private IServiceCollection Services { get; set; }

        /// <summary>
        /// <see cref="IGarbageCollector.GetLeakedResourcesAsync"/>
        /// </summary>
        public async Task<IDictionary<string, LeakedResource>> GetLeakedResourcesAsync(CancellationToken token)
        {
            EventContext telemetryContext = EventContext.Persisted();
            Dictionary<string, LeakedResource> leakedResourceGroups = new Dictionary<string, LeakedResource>();
            IList<LeakedResource> leakedResourceFromAzure = await this.GetLeakedAzureResourcesAsync(token).ConfigureDefaults();

            foreach (LeakedResource item in leakedResourceFromAzure)
            {
                leakedResourceGroups.Add(item.Id, item);
            }

            await this.logger.LogTelemetryAsync($"{nameof(ResourceGroupGarbageCollector)}.LeakedResourceGroups", telemetryContext, leakedResourceGroups, this.loggingBatchSize)
                .ConfigureDefaults();

            telemetryContext.AddContext(nameof(leakedResourceGroups) + "Count", leakedResourceGroups.Count);

            return leakedResourceGroups;
        }

        /// <summary>
        /// <see cref="IGarbageCollector.CleanupLeakedResourcesAsync"/>
        /// </summary>
        public async Task<IDictionary<string, string>> CleanupLeakedResourcesAsync(IDictionary<string, LeakedResource> leakedResources, CancellationToken token)
        {
            leakedResources.ThrowIfEmpty(nameof(leakedResources));
            this.experimentTemplateDataManager = this.Services.GetService<IExperimentTemplateDataManager>();
            this.experimentTemplateDataManager.ThrowIfNull(nameof(this.experimentTemplateDataManager));

            this.experimentClient = this.Services.GetService<IExperimentClient>();
            this.experimentClient.ThrowIfNull(nameof(this.experimentClient));

            EventContext telemetryContext = EventContext.Persisted();
            Dictionary<string, string> result = new Dictionary<string, string>();

            foreach (var leakedResource in leakedResources)
            {
                if (GarbageCollectorExtensions.IsResourceLeaked(leakedResource.Value) && leakedResource.Value.ImpactType == ImpactType.None)
                {
                    try
                    {
                        if (this.experimentTemplate == null)
                        {
                            // Getting experiment definition from cosmos
                            this.experimentTemplate = await this.logger.LogTelemetryAsync($"{nameof(ResourceGroupGarbageCollector)}.GetResourceCleanupExperimentTemplate", telemetryContext, async () =>
                            {
                                return await this.experimentTemplateDataManager.GetResourceCleanupExperimentTemplateAsync(this.templateId, this.teamName, token)
                                    .ConfigureDefaults();
                            }).ConfigureDefaults();
                        }

                        ExperimentTemplate experimentTemplatePayload = new ExperimentTemplate()
                        {
                            Experiment = this.experimentTemplate,
                            Override = this.ReplaceParameters(leakedResource.Value)
                        };

                        // Making API call to create experiment
                        string experimentId = await this.logger.LogTelemetryAsync($"{nameof(ResourceGroupGarbageCollector)}.CreateResourceCleanupExperiment", telemetryContext, async () =>
                        {
                            return await this.experimentClient.CreateResourceCleanupExperimentAsync(experimentTemplatePayload, leakedResource.Value, token)
                                .ConfigureDefaults();
                        }).ConfigureDefaults();

                        // Key: ResourceGroup Name, Value: ExperimentID
                        result.Add(leakedResource.Key, experimentId);
                    }
                    catch (Exception exe)
                    {
                        // swallowing this exception so that we don't lose the result
                        telemetryContext.AddContext("failedLeakedSessionId", leakedResource.Value.Id);
                        telemetryContext.AddError(exe);
                    }
                }
            }

            await this.logger.LogTelemetryAsync($"{nameof(ResourceGroupGarbageCollector)}.CleanedResources", telemetryContext, result, this.loggingBatchSize)
                .ConfigureDefaults();

            return result;
        }

        private async Task<IList<LeakedResource>> GetLeakedAzureResourcesAsync(CancellationToken token)
        {
            EventContext telemetryContext = EventContext.Persisted();

            IEnumerable<AzureResourceGroup> leakedResourceGroups = await this.logger.LogTelemetryAsync($"{nameof(ResourceGroupGarbageCollector)}.GetLeakedAzureResources", telemetryContext, async () =>
            {
                List<Task<IList<AzureResourceGroup>>> resourceGroupTasks = new List<Task<IList<AzureResourceGroup>>>();
                foreach (ISubscriptionManager sub in this.subscriptionManagers)
                {
                    resourceGroupTasks.Add(sub.GetAllResourceGroupsAsync(token));
                }

                IList<AzureResourceGroup>[] allResourceGroupArr = await Task.WhenAll(resourceGroupTasks).ConfigureDefaults();
                return allResourceGroupArr.SelectMany(x => x);

            }).ConfigureDefaults();

            await this.logger.LogTelemetryAsync($"{nameof(ResourceGroupGarbageCollector)}.ActiveAzureResourcesGroups", telemetryContext, leakedResourceGroups, this.loggingBatchSize)
                .ConfigureDefaults();

            return leakedResourceGroups.ParseAzureResources();
        }

        private string ReplaceParameters(LeakedResource leakedResource)
        {
            Dictionary<string, IConvertible> experimentParameter = new Dictionary<string, IConvertible>();
            experimentParameter.Add("resourceGroupName", leakedResource.Id);
            experimentParameter.Add("subscriptionId", leakedResource.SubscriptionId);

            var experimentParameters = new TemplateOverride(experimentParameter);
            return experimentParameters.ToJson();
        }

        private void SetUpSubscriptionManager()
        {
            ISubscriptionManager subscriptionManager = this.Services.GetService<ISubscriptionManager>();

            this.subscriptionManagers = new List<ISubscriptionManager>()
            {
                subscriptionManager
            };
        }
            
    }
}
