namespace Juno.Execution.Providers.Environment
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.Execution.Providers.Properties;
    using Juno.Execution.TipIntegration;
    using Juno.Extensions.Telemetry;
    using Juno.Providers;
    using Kusto.Data.Exceptions;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Kusto;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Mono.Posix;
    using Newtonsoft.Json;
    using Polly;

    /// <summary>
    /// Provides a method for selecting a set of clusters that meet the criteria/requirements
    /// of an experiment.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.EnvironmentCriteria, SupportedStepTarget.ExecuteRemotely)]
    [SupportedParameter(Name = Constants.CpuId, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Constants.VmSkus, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Constants.Regions, Type = typeof(string), Required = false)]
    [SupportedParameter(Name = Constants.QueryName, Type = typeof(string), Required = false)]
    [ProviderInfo(Name = "Select Clusters and Nodes", Description = "Select clusters that have physical nodes that can support the requirements of the experiment", FullDescription = "Step to select a set of Azure data center clusters (and ultimately physical nodes within the clusters) as options for running experiments. This step is typically one of the first steps in a Juno experiment workflow. It has not special dependencies other than those related to quota and availability restrictions inside individual data center regions (e.g. supported VM families and SKUs).")]
    public partial class ClusterSelectionProvider : ExperimentProvider
    {
        private const int MaxSearchAttempts = 10;
        private readonly string emptyQueryValue = "\"\"";

        /// <summary>
        /// Initializes a new instance of the <see cref="ClusterSelectionProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public ClusterSelectionProvider(IServiceCollection services)
            : base(services)
        {
            // Retry Policies:
            // 1) Handle InternalServiceError issues differently than others. The Kusto cluster can get overloaded at times and
            //    we just need to backoff a bit more before retries.
            //
            // 2) Handle other exceptions that do not indicate the Kusto cluster itself is having load issues.
            this.RetryPolicy = Policy.WrapAsync(
                Policy.Handle<KustoClientException>().Or<Kusto.Cloud.Platform.Utils.UtilsException>().Or<AggregateException>()
                .WaitAndRetryAsync(retryCount: 5, (retries) =>
                {
                    return TimeSpan.FromSeconds(retries + 10);
                }),
                Policy.Handle<KustoServiceException>().WaitAndRetryAsync(retryCount: 10, (retries) =>
                {
                    return TimeSpan.FromSeconds(retries + 10);
                }));
        }

        /// <summary>
        /// The retry policy to use with the Kusto client call to the Kusto cluster.
        /// </summary>
        public IAsyncPolicy RetryPolicy { get; set; }

        /// <inheritdoc />
        protected override async Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            ExecutionResult result = new ExecutionResult(ExecutionStatus.InProgress);

            if (!cancellationToken.IsCancellationRequested)
            {
                State state = await this.GetStateAsync<State>(context, cancellationToken).ConfigureDefaults()
                    ?? new State();

                DataTable results = await this.GetKustoResponseAsync(context, component, telemetryContext).ConfigureAwait(false);
                IList<TipRack> tipRacks = ClusterSelectionProvider.ParseRack(results);
                IEnumerable<EnvironmentEntity> entities = TipRack.ToEnvironmentEntities(tipRacks);
                state.SearchAttempts++;

                await this.LogDataResultsAsync(tipRacks, telemetryContext).ConfigureDefaults();

                if (tipRacks?.Any() == true)
                {
                    telemetryContext.AddContext("matchingRackCount", tipRacks.Count);
                    telemetryContext.AddContext("matchingRacks", tipRacks.Select(r => new
                    {
                        region = r.Region,
                        cluster = r.ClusterName,
                        rack = r.RackLocation,
                        nodeCount = r.NodeList?.Count
                    }));
                }
                else
                {
                    telemetryContext.AddContext("matchingRackCount", 0);
                }

                if (entities?.Any() != true)
                {
                    if (state.SearchAttempts >= state.MaxSearchAttempts)
                    {
                        throw new ProviderException(
                            $"Cluster/node selection failed. There are no entities that match the criteria of the experiment.",
                            ErrorReason.EntitiesMatchingCriteriaNotFound);
                    }
                }

                if (entities?.Any() == true)
                {
                    await this.ResolveAndSaveEntityPoolAsync(context, entities, cancellationToken).ConfigureAwait(false);
                    result = new ExecutionResult(ExecutionStatus.Succeeded);
                }

                await this.SaveStateAsync(context, state, cancellationToken).ConfigureDefaults();
            }

            return result;
        }

        internal static IEnumerable<string> GetPinnedClusters()
        {
            return Resources.PinnedClusters.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                .Select(cl => cl.Trim());
        }

        /// <summary>
        /// Convert data table to list of TipRack.
        /// </summary>
        /// <param name="dataTable">Input datatable</param>
        /// <returns>Converted list of tipRack</returns>
        private static IList<TipRack> ParseRack(DataTable dataTable)
        {
            List<TipRack> tipRacks = new List<TipRack>();

            if (dataTable?.Rows != null)
            {
                string cluster = null;
                IEnumerable<string> pinnedClusters = ClusterSelectionProvider.GetPinnedClusters();

                foreach (DataRow row in dataTable.Rows)
                {
                    cluster = ((string)row[1])?.Trim();
                    if (!pinnedClusters.Contains(cluster))
                    {
                        TipRack rack = new TipRack()
                        {
                            RackLocation = (string)row[0],
                            ClusterName = (string)row[1],
                            CpuId = (string)row[2],
                            Region = (string)row[3],
                            RemainingTipSessions = Convert.ToInt32(row[4]),
                            SupportedVmSkus = JsonConvert.DeserializeObject<List<string>>((string)row[5]),
                            NodeList = JsonConvert.DeserializeObject<List<string>>((string)row[6]),
                            MachinePoolName = (string)row[7]
                        };

                        tipRacks.Add(rack);
                    }
                }
            }

            return tipRacks;
        }

        private async Task<DataTable> GetKustoResponseAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext)
        {
            DataTable dataTable = null;
            string cpuId = null;

            if (component.Parameters.ContainsKey(Constants.CpuId))
            {
                cpuId = (string)component.Parameters[Constants.CpuId];
            }

            string[] supportedVmSkus = null;
            if (component.Parameters.ContainsKey(Constants.VmSkus))
            {
                supportedVmSkus = ((string)component.Parameters[Constants.VmSkus]).Split(',', ';');
            }

            string[] regions = null;
            if (component.Parameters.ContainsKey(Constants.Regions))
            {
                regions = ((string)component.Parameters[Constants.Regions]).Split(',', ';');
            }

            string queryName = null;
            if (component.Parameters.ContainsKey(Constants.QueryName))
            {
                queryName = component.Parameters.GetValue<string>(Constants.QueryName);
            }

            // For now one of those two filters must be present, otherwise there is kusto streaming issue when sending over large data
            if (!string.IsNullOrWhiteSpace(cpuId) || (supportedVmSkus != null && supportedVmSkus.Any()))
            {
                string query = Properties.Resources.TipRackQuery;
                telemetryContext.AddContext(nameof(queryName), !string.IsNullOrWhiteSpace(queryName) ? queryName : "default");

                if (!string.IsNullOrWhiteSpace(queryName))
                {
                    query = Properties.Resources.ResourceManager.GetObject(queryName)?.ToString();

                    if (string.IsNullOrWhiteSpace(query))
                    {
                        throw new ProviderException($"The expected query defined '{queryName}' was not found in the set of available queries for the provider.");
                    }
                }

                query = !string.IsNullOrWhiteSpace(cpuId)
                    ? query.Replace(Constants.CpuIdPlaceHolder, $"dynamic(['{cpuId}'])", StringComparison.Ordinal)
                    : query.Replace(Constants.CpuIdPlaceHolder, this.emptyQueryValue, StringComparison.Ordinal);

                query = supportedVmSkus?.Any() == true
                    ? query.Replace(Constants.VmSkusPlaceHolder, $"dynamic([{string.Join(",", supportedVmSkus.Select(sku => $"'{sku}'"))}])", StringComparison.Ordinal)
                    : query.Replace(Constants.VmSkusPlaceHolder, this.emptyQueryValue, StringComparison.Ordinal);

                query = regions?.Any() == true
                    ? query.Replace(Constants.RegionsPlaceHolder, $"dynamic([{string.Join(",", regions.Select(region => $"'{region}'"))}])", StringComparison.Ordinal)
                    : query.Replace(Constants.RegionsPlaceHolder, this.emptyQueryValue, StringComparison.Ordinal);

                EnvironmentSettings settings = EnvironmentSettings.Initialize(context.Configuration);
                KustoSettings kustoSettings = settings.KustoSettings.Get(Setting.AzureCM);
                AadPrincipalSettings principalSettings = kustoSettings.AadPrincipals.Get(Setting.Default);

                if (!this.Services.TryGetService<IKustoQueryIssuer>(out IKustoQueryIssuer issuer))
                {
                    issuer = new KustoQueryIssuer(
                        principalSettings.PrincipalId,
                        principalSettings.PrincipalCertificateThumbprint,
                        principalSettings.TenantId);
                }

                await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    dataTable = await issuer.IssueAsync(kustoSettings.ClusterUri.AbsoluteUri, kustoSettings.ClusterDatabase, query)
                        .ConfigureDefaults();
                }).ConfigureDefaults();
            }

            return dataTable;
        }

        private async Task LogDataResultsAsync(IEnumerable<TipRack> results, EventContext telemetryContext)
        {
            if (results?.Any() == true)
            {
                // Context on the Grouping Below:
                // The size of the results are very often too large for a single telemetry event.  We are grouping
                // the events below into smaller subsets and each subset will contain 5 or 6 results at max each.
                //
                // Careful with the modulus '%' operation in the 'group by' clause below. The right side operand
                // cannot result in a value of 0 or there will be a 'divide by zero' exception.
                IEnumerable<IEnumerable<object>> resultSets = results.Select((item, index) => new { index, item })
                       .GroupBy(x => x.index % ((results.Count() / 5) + 1))
                       .Select(x => x.Select(y => new
                       {
                           rackLocation = y.item.RackLocation,
                           clusterName = y.item.ClusterName,
                           region = y.item.Region,
                           remainingTipSessions = y.item.RemainingTipSessions,
                           supportedVmSkus = y.item.SupportedVmSkus,
                           machinePoolName = y.item.MachinePoolName
                       }));

                if (resultSets?.Any() == true)
                {
                    foreach (IEnumerable<object> resultSet in resultSets)
                    {
                        EventContext relatedContext = telemetryContext.Clone()
                             .AddContext("results", resultSet);

                        await this.Logger.LogTelemetryAsync($"{nameof(ClusterSelectionProvider)}.QueryResults", LogLevel.Information, relatedContext)
                            .ConfigureDefaults();
                    }
                }
            }
        }

        private Task ResolveAndSaveEntityPoolAsync(ExperimentContext context, IEnumerable<EnvironmentEntity> entities, CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));

            return this.UpdateEntityPoolAsync(context, entities, cancellationToken);
        }

        internal class State
        {
            public int MaxSearchAttempts { get; set; } = ClusterSelectionProvider.MaxSearchAttempts;

            public int SearchAttempts { get; set; }

            public DateTime NextSearchAttemptTime { get; set; }
        }

        private class Constants
        {
            internal const string CpuId = "cpuId";
            internal const string CpuIdPlaceHolder = "$cpuIDList$";
            internal const string VmSkus = "vmSkus";
            internal const string VmSkusPlaceHolder = "$vmSkus$";
            internal const string Regions = "regions";
            internal const string RegionsPlaceHolder = "$regions$";
            internal const string QueryName = "queryName";
        }
    }
}