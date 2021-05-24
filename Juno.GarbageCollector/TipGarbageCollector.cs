namespace Juno.GarbageCollector
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.DataManagement;
    using Juno.Execution.TipIntegration;
    using Juno.Extensions.Telemetry;
    using Juno.GarbageCollector.Resources;
    using Kusto.Cloud.Platform.Utils;
    using Kusto.Data.Exceptions;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Kusto;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Polly;
    using TipGateway.Entities;
    using static Juno.GarbageCollector.GarbageCollectorExtensions;

    /// <summary>
    /// <see cref="IGarbageCollector"/>
    /// </summary>
    public class TipGarbageCollector : IGarbageCollector
    {
        private readonly int retryCounter = 5;
        private readonly int loggingBatchSize = 20;
        private readonly string teamName = "CRC AIR";
        private readonly int[] kustoFailureCodes = { 408, 500, 504 };
        private readonly string templateId = "Garbage_Collector_TipCleanup.Template.v1.json";

        private ILogger logger;
        private ITipClient tipClient;
        private IAsyncPolicy retryPolicy;
        private IConfiguration configuration;
        private IKustoQueryIssuer kustoIssuer;
        private Experiment experimentTemplate;
        private IExperimentClient experimentClient;
        private IExperimentTemplateDataManager experimentTemplateDataManager;

        private string absoluteUri;
        private string clusterDatabase;
        private string applicationId;

        /// <summary>
        /// Initializes a new instance of the <see cref="TipGarbageCollector"/> class.
        /// </summary>
        /// <param name="services">Service collections</param>
        /// <param name="kustoSettings"></param>
        /// <param name="appPrincipleId"></param>
        public TipGarbageCollector(IServiceCollection services, KustoSettings kustoSettings = null, string appPrincipleId = null)
        {
            services.ThrowIfNullOrEmpty(nameof(services));
            this.Services = services;

            this.configuration = this.Services.GetService<IConfiguration>();
            this.logger = this.Services.GetService<ILogger>();

            if (!services.TryGetService<ITipClient>(out ITipClient tipClient))
            {
                this.SetTipSettings();
            }
            else
            {
                this.tipClient = tipClient;
            }

            if (kustoSettings == null)
            {
                this.SetKustoSettings();
            }
            else
            {
                this.absoluteUri = kustoSettings.ClusterUri.AbsoluteUri;
                this.clusterDatabase = kustoSettings.ClusterDatabase;
            }

            this.applicationId = string.IsNullOrEmpty(appPrincipleId) ? this.SetApplicationId() : appPrincipleId;

            this.configuration.ThrowIfNull(nameof(this.configuration));
            this.logger.ThrowIfNull(nameof(this.logger));
            this.clusterDatabase.ThrowIfNullOrWhiteSpace(nameof(this.clusterDatabase));
            this.absoluteUri.ThrowIfNullOrWhiteSpace(nameof(this.absoluteUri));
            this.applicationId.ThrowIfNullOrWhiteSpace(nameof(this.applicationId));
        }

        private IServiceCollection Services { get; set; }

        /// <summary>
        /// <see cref="IGarbageCollector.GetLeakedResourcesAsync"/>
        /// </summary>
        public async Task<IDictionary<string, LeakedResource>> GetLeakedResourcesAsync(CancellationToken token)
        {
            string tipSessionMappingQuery = Resource.FetchTipSessionsWithExperimentDataQuery;
            string leakedTipSessionQuery = Resource.LeakedTipSessionQuery;

            tipSessionMappingQuery.ThrowIfNullOrWhiteSpace(nameof(tipSessionMappingQuery));
            leakedTipSessionQuery.ThrowIfNullOrEmpty(nameof(leakedTipSessionQuery));

            this.kustoIssuer = this.Services.GetService<IKustoQueryIssuer>();
            this.kustoIssuer.ThrowIfNull(nameof(this.kustoIssuer));

            this.SetKustoRetryPolicy();
            this.retryPolicy.ThrowIfNull(nameof(this.retryPolicy));

            this.tipClient = this.Services.GetService<ITipClient>();
            this.tipClient.ThrowIfNull(nameof(this.tipClient));

            EventContext telemetryContext = EventContext.Persisted();
            Dictionary<string, LeakedResource> leakedResources = new Dictionary<string, LeakedResource>();

            IEnumerable<LeakedResource> tipSessionsWithExperimentMapping = await this.ExecuteKustoAsync(tipSessionMappingQuery, token).ConfigureDefaults();
            IEnumerable<LeakedResource> validLeakedSessionsFromSource = await this.GetLeakedTipSessionAsync(leakedTipSessionQuery, token).ConfigureDefaults();

            foreach (LeakedResource item in validLeakedSessionsFromSource)
            {
                if (!leakedResources.ContainsKey(item.Id))
                {
                    leakedResources.Add(item.Id, item);
                }
                else
                {
                    leakedResources[item.Id].Source = LeakedResourceSource.TipClientAndAzureCM;
                }
            }

            foreach (LeakedResource item in tipSessionsWithExperimentMapping)
            {
                bool duplicateExist = leakedResources.ContainsKey(item.Id);

                if (duplicateExist)
                {
                    LeakedResource duplicateRecord = leakedResources[item.Id];
                    leakedResources.Remove(item.Id);

                    leakedResources.Add(item.Id, new LeakedResource(
                            createdTime: duplicateRecord.CreatedTime,
                            id: item.Id,
                            resourceType: GarbageCollectorResourceType.TipSession,
                            tipNodeSessionId: item.TipNodeSessionId,
                            nodeId: item.NodeId,
                            daysLeaked: (DateTime.Now - item.CreatedTime).Days,
                            experimentId: item.ExperimentId,
                            experimentName: item.ExperimentName,
                            impactType: item.ImpactType == ImpactType.None ? item.ImpactType : ImpactType.Impactful,
                            cluster: duplicateRecord.Cluster,
                            subscriptionId: item.SubscriptionId,
                            source: duplicateRecord.Source));
                }
            }

            await this.logger.LogTelemetryAsync($"{nameof(TipGarbageCollector)}.LeakedResources", telemetryContext, leakedResources, this.loggingBatchSize).ConfigureDefaults();
            telemetryContext.AddContext(nameof(leakedResources) + "Count", leakedResources.Count);

            return leakedResources;
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
                            this.experimentTemplate = await this.logger.LogTelemetryAsync($"{nameof(TipGarbageCollector)}.GetResourceCleanupExperimentTemplate", telemetryContext, async () =>
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
                        string experimentId = await this.logger.LogTelemetryAsync($"{nameof(TipGarbageCollector)}.CreateResourceCleanupExperiment", telemetryContext, async () =>
                        {
                            return await this.experimentClient.CreateResourceCleanupExperimentAsync(experimentTemplatePayload, leakedResource.Value, token)
                                .ConfigureDefaults();
                        }).ConfigureDefaults();

                        // Key: TipSession, Value: ExperimentID
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

            await this.logger.LogTelemetryAsync($"{nameof(TipGarbageCollector)}.CleanedResources", telemetryContext, result, this.loggingBatchSize)
                .ConfigureDefaults();

            return result;
        }

        private async Task<IList<LeakedResource>> ExecuteKustoAsync(string query, CancellationToken token)
        {
            EventContext telemetryContext = EventContext.Persisted();
            DataTable kustoTable = null;

            if (!token.IsCancellationRequested)
            {
                try
                {
                    kustoTable = await this.logger.LogTelemetryAsync($"{nameof(TipGarbageCollector)}.ExecuteKusto", telemetryContext, async () =>
                    {
                        return await this.retryPolicy.ExecuteAsync(async () =>
                        {
                            return await this.kustoIssuer.IssueAsync(this.absoluteUri, this.clusterDatabase, query).ConfigureDefaults();
                        }).ConfigureDefaults();
                    }).ConfigureDefaults();
                }
                catch (KustoException exe)
                {
                    telemetryContext.AddContext("kustoFailureCode", exe.FailureCode.ToString());
                    telemetryContext.AddError(exe, true);
                }
            }

            return kustoTable.ParseKustoResources();
        }

        private async Task<IEnumerable<LeakedResource>> GetLeakedTipSessionAsync(string queryToCheckAgainstCompute, CancellationToken token)
        {
            EventContext telemetryContext = EventContext.Persisted();

            // Leaked Resources are gather from two sources
            // 1. By querying against Azure Compute DB and relying on LogTipNodeSessionSnapShot
            // 2. By making an API call to TipClient and getting resources associated with App ID. 

            List<Task<IList<LeakedResource>>> leakedResourcesTask = new List<Task<IList<LeakedResource>>>();

            leakedResourcesTask.Add(this.ExecuteKustoAsync(queryToCheckAgainstCompute, token));

            leakedResourcesTask.Add(this.GetLeakedTipSessionFromTipClientAsync(token));

            IList<LeakedResource>[] leakedResources = await Task.WhenAll(leakedResourcesTask).ConfigureAwait(false);

            return leakedResources.SelectMany(x => x);
        }

        private async Task<IList<LeakedResource>> GetLeakedTipSessionFromTipClientAsync(CancellationToken token)
        {
            EventContext telemetryContext = EventContext.Persisted();

            IList<TipNodeSession> tipNodeSessionsByAppId = new List<TipNodeSession>();

            await this.logger.LogTelemetryAsync($"{nameof(TipGarbageCollector)}.GetTipSessionsByAppId", telemetryContext, async () =>
            {
                IEnumerable<TipNodeSession> allTipNodeSessionsByAppId = await this.tipClient.GetTipSessionsByAppIdAsync(this.applicationId, token)
                    .ConfigureDefaults();

                IEnumerable<IGrouping<TipNodeSessionStatus, TipNodeSession>> tipNodeSessionsGroupByStatus = allTipNodeSessionsByAppId.GroupBy(d => d.Status);
                IDictionary<string, int> tipSessionStatusCount = new Dictionary<string, int>();
                tipNodeSessionsGroupByStatus.ForEach(x => tipSessionStatusCount.Add(x.Key.ToString(), x.Count()));

                telemetryContext.AddContext(nameof(tipNodeSessionsByAppId), allTipNodeSessionsByAppId.Count());
                telemetryContext.AddContext($"{nameof(TipNodeSessionStatus)}.Count", tipSessionStatusCount);

                foreach (TipNodeSession tipNodeSession in allTipNodeSessionsByAppId)
                {
                    if (tipNodeSession.Status == TipNodeSessionStatus.Creating || tipNodeSession.Status == TipNodeSessionStatus.Deleted)
                    {
                        continue;
                    }

                    tipNodeSessionsByAppId.Add(tipNodeSession);
                }

                await this.logger.LogTelemetryAsync($"{nameof(TipGarbageCollector)}.TipNodeSessionsByAppId", telemetryContext, tipNodeSessionsByAppId, this.loggingBatchSize).ConfigureDefaults();

            }).ConfigureDefaults();

            IDictionary<string, bool> tipNodeSessionStatus = null;

            await this.logger.LogTelemetryAsync($"{nameof(TipGarbageCollector)}.ValidateTipNodeSessions", telemetryContext, async () =>
            {
                tipNodeSessionStatus = await this.ValidateTipNodeSessionsAsync(tipNodeSessionsByAppId, token).ConfigureAwait(false);

                await this.logger.LogTelemetryAsync($"{nameof(TipGarbageCollector)}.ValidTipNodeSessions", telemetryContext, tipNodeSessionStatus, this.loggingBatchSize).ConfigureDefaults();

            }).ConfigureDefaults();

            IList<TipNodeSession> validTipNodeSessions = new List<TipNodeSession>();
            if (tipNodeSessionStatus.Any())
            {
                foreach (TipNodeSession tipNodeSession in tipNodeSessionsByAppId)
                {
                    // filtering for tipsessions that are verified to have been created.
                    if (tipNodeSessionStatus.ContainsKey(tipNodeSession.Id) && tipNodeSessionStatus[tipNodeSession.Id])
                    {
                        validTipNodeSessions.Add(tipNodeSession);
                    }
                }
            }

            telemetryContext.AddContext("validTipNodeSessions", validTipNodeSessions.Count);

            IList<LeakedResource> leakedResources = validTipNodeSessions.ParseTipResource();
            await this.logger.LogTelemetryAsync($"{nameof(TipGarbageCollector)}.LeakedTipSessionFromTipClient", telemetryContext, leakedResources, this.loggingBatchSize).ConfigureDefaults();
            return leakedResources;
        }

        private Task<IDictionary<string, bool>> ValidateTipNodeSessionsAsync(IEnumerable<TipNodeSession> availableTipNodeSessions, CancellationToken token)
        {
            IList<string> tipSessions = new List<string>();
            availableTipNodeSessions.ForEach(x => tipSessions.Add(x.Id));

            if (tipSessions.Any())
            {
                return this.tipClient.IsTipSessionCreatedAsync(tipSessions, token);
            }

            return Task.FromResult<IDictionary<string, bool>>(new Dictionary<string, bool>());
        }

        private string ReplaceParameters(LeakedResource leakedResource)
        {
            Dictionary<string, IConvertible> experimentParameter = new Dictionary<string, IConvertible>();
            experimentParameter.Add("tipSessionId", leakedResource.Id);

            TemplateOverride experimentParameters = new TemplateOverride(experimentParameter);
            return experimentParameters.ToJson();
        }

        private void SetKustoRetryPolicy()
        {
            this.retryPolicy = Policy.Handle<KustoException>(e => this.kustoFailureCodes.Contains(e.FailureCode))
                .WaitAndRetryAsync(retryCount: this.retryCounter, (retries) => TimeSpan.FromSeconds(Math.Pow(2, this.retryCounter)));
        }

        private void SetKustoSettings()
        {
            KustoSettings kustoSettings = EnvironmentSettings.Initialize(this.configuration).KustoSettings.Get(Setting.AzureCM);
            this.absoluteUri = kustoSettings.ClusterUri.AbsoluteUri;
            this.clusterDatabase = kustoSettings.ClusterDatabase;
        }

        private void SetTipSettings()
        {
            this.tipClient = new TipClient(this.configuration);
        }

        private string SetApplicationId()
        {
            return EnvironmentSettings.Initialize(this.configuration).TipSettings.AadPrincipals.Get(Setting.Default).PrincipalId;
        }
    }
}
