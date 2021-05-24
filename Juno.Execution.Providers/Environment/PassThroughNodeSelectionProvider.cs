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
    using Juno.Execution.ArmIntegration;
    using Juno.Execution.TipIntegration;
    using Juno.Extensions.Telemetry;
    using Juno.Providers;
    using Kusto.Data.Exceptions;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Kusto;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;
    using Newtonsoft.Json;
    using Polly;

    /// <summary>
    /// Provides a method for selecting a set of clusters that meet the criteria/requirements
    /// of an experiment.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.EnvironmentCriteria, SupportedStepTarget.ExecuteRemotely)]
    [SupportedParameter(Name = Constants.Nodes, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Constants.VmSkus, Type = typeof(string), Required = false)]
    [SupportedParameter(Name = Constants.FalseRack, Type = typeof(string), Required = false)]
    [ProviderInfo(Name = "Define Environment Nodes", Description = "Allow explicit definition of specific nodes/blades in the Azure fleet that can support the requirements of the experiment", FullDescription = "Step to allow explicit definition of specific nodes/blades in the Azure fleet that can support the requirements of the experiment.")]
    public partial class PassThroughNodeSelectionProvider : ExperimentProvider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PassThroughNodeSelectionProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public PassThroughNodeSelectionProvider(IServiceCollection services)
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
                // adding support for multiple racks isn't too hard either
                DataTable results = await this.GetKustoResponseAsync(context, component).ConfigureAwait(false);

                string falseRack = null;
                if (component.Parameters.TryGetValue(Constants.FalseRack, out IConvertible parameterValue))
                {
                    falseRack = parameterValue.ToString();
                }

                IList<TipRack> tipRacks = PassThroughNodeSelectionProvider.ParseRack(results, falseRack);
                IEnumerable<EnvironmentEntity> entities = TipRack.ToEnvironmentEntities(tipRacks);

                telemetryContext.AddContext("matchingEntityCount", entities?.Count() ?? 0);

                if (entities?.Any() != true)
                {
                    throw new ProviderException(
                        $"Cluster/node selection failed. There are no entities that match the criteria of the experiment.",
                        ErrorReason.EntitiesMatchingCriteriaNotFound);
                }

                if (entities?.Any() == true)
                {
                    await this.ResolveAndSaveEntityPoolAsync(context, entities, cancellationToken).ConfigureAwait(false);
                    result = new ExecutionResult(ExecutionStatus.Succeeded);
                }
            }

            return result;
        }

        /// <summary>
        /// Convert data table to list of TipRack.
        /// </summary>
        /// <param name="dataTable">Input datatable</param>
        /// <param name="falseRack">Assign a false rack to nodes to group them in same Tip Rack</param>
        /// <returns>Converted list of tipRack</returns>
        internal static IList<TipRack> ParseRack(DataTable dataTable, string falseRack = null)
        {
            List<TipRack> tipRacks = new List<TipRack>();

            if (dataTable?.Rows != null)
            {
                foreach (DataRow row in dataTable.Rows)
                {
                    var nodes = (string)row[3];
                    var vms = (string)row[5];
                    TipRack rack = new TipRack()
                    {
                        RackLocation = falseRack ?? (string)row[1],
                        ClusterName = (string)row[4],
                        Region = (string)row[2],
                        // bogus for now. Assumption is if user is passing a specific
                        // node list, they know what they're doing. But, we can be
                        // smart here and add these checks later.
                        RemainingTipSessions = 20,
                        SupportedVmSkus = JsonConvert.DeserializeObject<List<string>>(vms),
                        NodeList = JsonConvert.DeserializeObject<List<string>>(nodes),
                        MachinePoolName = (string)row[0]
                    };

                    tipRacks.Add(rack);
                }
            }

            return tipRacks;
        }
        
        internal async Task<DataTable> GetKustoResponseAsync(ExperimentContext context, ExperimentComponent component)
        {
            DataTable dataTable = null;
            string[] nodes = null;

            if (component.Parameters.ContainsKey(Constants.Nodes))
            {
                nodes = ((string)component.Parameters[Constants.Nodes]).Split(',', ';');
            }

            // For now one of those two filters must be present, otherwise there is kusto streaming issue when sending over large data
            if (nodes.Length != 0)
            {
                string vmSkuParameter = "\"\"";
                if (component.Parameters.ContainsKey(Constants.VmSkus))
                {
                    string[] vmSkuList = ((string)component.Parameters[Constants.VmSkus]).Split(',', ';');
                    vmSkuParameter = string.Join(",", vmSkuList.Select(vmSku => $"'{vmSku}'"));
                    vmSkuParameter = $"dynamic([{vmSkuParameter}])";
                }

                string query = Properties.Resources.NodeInfoQuery;

                string nodeParameter = string.Join(",", nodes.Select(node => $"'{node}'"));
                nodeParameter = $"dynamic([{nodeParameter}])";
                query = query.Replace(Constants.NodesPlaceholder, nodeParameter, StringComparison.OrdinalIgnoreCase);

                query = query.Replace(Constants.VmSkusPlaceholder, vmSkuParameter, StringComparison.OrdinalIgnoreCase);

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
                        .ConfigureAwait(false);
                }).ConfigureDefaults();
            }

            return dataTable;
        }

        private Task ResolveAndSaveEntityPoolAsync(ExperimentContext context, IEnumerable<EnvironmentEntity> entities, CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            return this.UpdateEntityPoolAsync(context, entities, cancellationToken);
        }

        private class Constants
        {
            internal const string Nodes = "nodes";
            internal const string VmSkus = "vmSkus";
            internal const string FalseRack = "falseRack";
            internal const string VmSkusPlaceholder = "$vmSkus$";
            internal const string NodesPlaceholder = "$nodesList$";
        }
    }
}