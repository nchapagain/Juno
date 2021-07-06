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
    using Juno.DataManagement;
    using Juno.Extensions.Telemetry;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Subscriptions;
    using Microsoft.Azure.CRC.Telemetry;
    using TipGateway.Entities;

    /// <summary>
    /// Extension Methods for garbage collector
    /// </summary>
    public static class GarbageCollectorExtensions
    {
        private static int hoursLeakedConsideration = -30;

        internal static bool IsResourceLeaked(LeakedResource resource)
        {
            return resource.CreatedTime < DateTime.UtcNow.AddHours(GarbageCollectorExtensions.hoursLeakedConsideration);
        }

        /// <summary>
        /// Method to Parse Kusto Response to meaningful LeakedResources
        /// </summary>
        internal static IList<LeakedResource> ParseKustoResources(this DataTable dataTable)
        {
            List<LeakedResource> leakedNodes = new List<LeakedResource>();

            if (dataTable?.Rows != null)
            {
                foreach (DataRow row in dataTable.Rows)
                {
                    if (!DateTime.TryParse((string)row[KustoColumns.CreatedTime], out DateTime createdTime))
                    {
                        throw new FormatException("Unable to parse CreatedTime of TipSession from Kusto Response");
                    }

                    if (!int.TryParse((string)row[KustoColumns.DaysLeaked], out int leakedDays))
                    {
                        throw new FormatException("Unable to parse LeakedDays of TipSession from Kusto Response");
                    }

                    bool impactTypeFound = Enum.TryParse((string)row[KustoColumns.ImpactType], true, out ImpactType impactType);

                    if (createdTime < DateTime.UtcNow.AddHours(GarbageCollectorExtensions.hoursLeakedConsideration))
                    {
                        LeakedResource leakedSessions = new LeakedResource(
                        createdTime: createdTime,
                        id: (string)row[KustoColumns.TipNodeSessionId],
                        resourceType: GarbageCollectorResourceType.TipSession,
                        tipNodeSessionId: (string)row[KustoColumns.TipNodeSessionId],
                        nodeId: (string)row[KustoColumns.NodeId],
                        daysLeaked: leakedDays,
                        experimentId: (string)row[KustoColumns.ExperimentId],
                        experimentName: (string)row[KustoColumns.ExperimentName],
                        impactType: impactTypeFound ? impactType : ImpactType.Impactful, // if impactType is not found, mark as impactful
                        cluster: string.Empty,
                        subscriptionId: string.Empty,
                        owner: (string)row[KustoColumns.CreatedBy],
                        source: LeakedResourceSource.AzureCM);

                        leakedNodes.Add(leakedSessions);
                    }
                }
            }

            return leakedNodes;
        }

        /// <summary>
        /// Method to Parse Tip Sessions to meaningful LeakedResources
        /// </summary>
        internal static IList<LeakedResource> ParseTipResource(this IEnumerable<TipNodeSession> tipSessions)
        {
            List<LeakedResource> leakedNodes = new List<LeakedResource>();

            if (tipSessions.Any())
            {
                foreach (TipNodeSession tipNode in tipSessions)
                {
                    if (tipNode.CreatedTimeUtc < DateTime.UtcNow.AddHours(GarbageCollectorExtensions.hoursLeakedConsideration))
                    {
                        LeakedResource leakedSessions = new LeakedResource(
                            createdTime: tipNode.CreatedTimeUtc,
                            id: tipNode.Id,
                            resourceType: GarbageCollectorResourceType.TipSession,
                            tipNodeSessionId: tipNode.Id,
                            nodeId: string.Empty,
                            daysLeaked: (DateTime.Now - tipNode.CreatedTimeUtc).Days,
                            experimentId: string.Empty,
                            experimentName: string.Empty,
                            // marking all Tip Node Session as Impacful
                            impactType: ImpactType.Impactful,
                            cluster: tipNode.Cluster,
                            subscriptionId: string.Empty,
                            owner: tipNode.CreatedBy,
                            source: LeakedResourceSource.TipClient);

                        leakedNodes.Add(leakedSessions);
                    }
                }
            }

            return leakedNodes;
        }

        /// <summary>
        /// Method to Parse Azure Resources to meaningful LeakedResources
        /// </summary>
        internal static IList<LeakedResource> ParseAzureResources(this IEnumerable<AzureResourceGroup> resourceGroups)
        {
            IList<LeakedResource> leakedResourceGroup = new List<LeakedResource>();
            HashSet<string> distinctResourceName = new HashSet<string>();

            if (resourceGroups.Any())
            {
                foreach (AzureResourceGroup resource in resourceGroups)
                {
                    if (resource.CreatedDate < DateTime.UtcNow.AddHours(GarbageCollectorExtensions.hoursLeakedConsideration))
                    {
                        // there are times when resourceName are same. To avoid duplicates, apending subscription Id at the end. 
                        string resourceName = distinctResourceName.Contains(resource.ResourceName) ? resource.ResourceName + resource.SubscriptionId : resource.ResourceName;

                        bool tipAvailable = resource.ResourceTags.TryGetValue(AzureResourceTags.TipSessionId, out string tipId);
                        bool expIdAvailable = resource.ResourceTags.TryGetValue(AzureResourceTags.ExperimentId, out string expId);
                        bool nodeAvailable = resource.ResourceTags.TryGetValue(AzureResourceTags.NodeId, out string nodeId);
                        bool expNameAvailable = resource.ResourceTags.TryGetValue(AzureResourceTags.ExperimentName, out string experimentName);

                        LeakedResource leakedSessions = new LeakedResource(
                            createdTime: resource.CreatedDate,
                            id: resourceName,
                            resourceType: GarbageCollectorResourceType.AzureResourceGroup,
                            tipNodeSessionId: tipAvailable ? tipId : string.Empty,
                            nodeId: nodeAvailable ? nodeId : string.Empty,
                            daysLeaked: (DateTime.UtcNow - resource.ExpirationDate).Days,
                            experimentId: expIdAvailable ? expId : string.Empty,
                            experimentName: expNameAvailable ? experimentName : string.Empty,
                            // marking all Azure Resources as Non-Impacful
                            impactType: ImpactType.None,
                            cluster: string.Empty,
                            subscriptionId: resource.SubscriptionId,
                            source: LeakedResourceSource.AzureResourceGroupManagement);

                        distinctResourceName.Add(resource.ResourceName);
                        leakedResourceGroup.Add(leakedSessions);
                    }
                }
            }

            return leakedResourceGroup;
        }

        internal static async Task<Experiment> GetResourceCleanupExperimentTemplateAsync(this IExperimentTemplateDataManager experimentTemplateDataManager, string tipCleanupTemplateId, string teamName, CancellationToken token)
        {
            experimentTemplateDataManager.ThrowIfNull(nameof(experimentTemplateDataManager));
            ExperimentItem experimentItem = await experimentTemplateDataManager.GetExperimentTemplateAsync(
            tipCleanupTemplateId,
            teamName,
            token).ConfigureDefaults();

            return experimentItem.Definition;
        }

        internal static async Task<string> CreateResourceCleanupExperimentAsync(this IExperimentClient experimentClient, ExperimentTemplate experiment, LeakedResource resource, CancellationToken token)
        {
            EventContext telemetryContext = EventContext.Persisted();

            string experimentId = string.Empty;
            
            telemetryContext.AddContext("experimentOverride", experiment.Override);
            telemetryContext.AddContext("resource", resource);
            HttpResponseMessage response = await experimentClient.CreateExperimentFromTemplateAsync(experiment, token).ConfigureDefaults();
            telemetryContext.AddContext(response);

            if (!response.IsSuccessStatusCode)
            {
                telemetryContext.AddError(new Exception($"POST to experiment api failed {response.StatusCode}"));
                experimentId = string.Empty;
            }
            else
            {
                string stringResponse = await response.Content.ReadAsStringAsync().ConfigureDefaults();
                ExperimentItem experimentResponse = stringResponse.FromJson<ExperimentItem>();
                experimentId = experimentResponse.Id;
            }

            telemetryContext.AddContext("launchedExperimentId", experimentId);
            return experimentId;
        }

        private class KustoColumns
        {
            internal const string CreatedTime = "createdTime";
            internal const string TipNodeSessionId = "tipNodeSessionId";
            internal const string NodeId = "nodeId";
            internal const string DaysLeaked = "daysLeaked";
            internal const string ExperimentId = "experimentId";
            internal const string ExperimentName = "experimentName";
            internal const string ImpactType = "impactType";
            internal const string CreatedBy = "createdBy";
        }

        private class AzureResourceTags
        {
            internal const string TipSessionId = "tipSessionId";
            internal const string ExperimentId = "experimentId";
            internal const string NodeId = "nodeId";
            internal const string ExperimentName = "experimentName";
        }

        internal class GarbageCollectorResourceType
        {
            internal const string TipSession = "TipSession";
            internal const string AzureResourceGroup = "ResourceGroup";
        }
    }
}