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
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Kusto;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Polly;

    /// <summary>
    /// Provides a method for selecting a set of nodes for a distinct group.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.EnvironmentCriteria, SupportedStepTarget.ExecuteRemotely)]
    [SupportedParameter(Name = Constants.Nodes, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Constants.VmSkus, Type = typeof(string), Required = false)]
    [SupportedParameter(Name = Constants.SearchPeriod, Type = typeof(int), Required = false)]
    [ProviderInfo(Name = "Define Environment Nodes for a Distinct Group", Description = "Allow explicit definition of specific nodes/blades in the Azure fleet that can support the requirements of the experiment", FullDescription = "Step to allow explicit definition of specific nodes/blades in the Azure fleet that can support the requirements of the experiment.")]

    public class NodeSelectionProvider : ExperimentProvider
    {
        private IAsyncPolicy retryPolicy;
        private static int defaultSearchPeriod = 4;

        /// <summary>
        /// Initializes a new instance of the <see cref="NodeSelectionProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public NodeSelectionProvider(IServiceCollection services)
            : base(services)
        {
            // Retry Policies:
            // 1) Handle InternalServiceError issues differently than others. The Kusto cluster can get overloaded at times and
            //    we just need to backoff a bit more before retries.
            //
            // 2) Handle other exceptions that do not indicate the Kusto cluster itself is having load issues.
            this.retryPolicy = Policy.WrapAsync(
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
        /// An entity manager used to manage the state of TiP node entities for
        /// the experiment.
        /// </summary>
        protected EntityManager EntityManager { get; set; }

        /// <inheritdoc />
        public override Task ConfigureServicesAsync(ExperimentContext context, ExperimentComponent component)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));

            EnvironmentSettings settings = EnvironmentSettings.Initialize(context.Configuration);
            KustoSettings kustoSettings = settings.KustoSettings.Get(Setting.AzureCM);
            AadPrincipalSettings principalSettings = kustoSettings.AadPrincipals.Get(Setting.Default);

            if (!this.Services.TryGetService<IKustoQueryIssuer>(out IKustoQueryIssuer issuer))
            {
                issuer = new KustoQueryIssuer(
                    principalSettings.PrincipalId,
                    principalSettings.PrincipalCertificateThumbprint,
                    principalSettings.TenantId);

                this.Services.AddSingleton<IKustoQueryIssuer>(issuer);
            }

            if (!this.Services.TryGetService<EntityManager>(out EntityManager entityManager))
            {
                this.Services.AddSingleton(new EntityManager(this.Services.GetService<IProviderDataClient>()));
            }

            return base.ConfigureServicesAsync(context, component);
        }

        /// <summary>
        /// Convert data table to list of TipRack.
        /// </summary>
        /// <param name="dataTable">The results from the Kusto query execution containing additional/full node information.</param>
        /// <param name="environmentGroups">The distinct environment groups for the experiment.</param>
        /// <returns>
        /// The set of entities subdivided into environement groups (e.g. Group A, Group B).
        /// </returns>
        protected static IList<EnvironmentEntity> ConvertToEntities(DataTable dataTable, params string[] environmentGroups)
        {
            dataTable.ThrowIfNull(nameof(dataTable));
            environmentGroups.ThrowIfNullOrEmpty(nameof(environmentGroups));

            List<EnvironmentEntity> entities = new List<EnvironmentEntity>();

            if (dataTable?.Rows != null)
            {
                List<EnvironmentEntity> entityPool = new List<EnvironmentEntity>();

                // Convert the entities into a generic pool having no alignment to specific environment
                // groups.
                foreach (DataRow row in dataTable.Rows)
                {
                    string rackLocation = (string)row[Constants.RackLocation];
                    string machinePool = (string)row[Constants.MachinePoolName];
                    string region = (string)row[Constants.Region];
                    string clusterName = (string)row[Constants.ClusterName];
                    string[] supportedVmSkus = row[Constants.SupportedVmSkus].ToString().FromJson<string[]>();

                    if (string.IsNullOrWhiteSpace(rackLocation)
                        || string.IsNullOrWhiteSpace(machinePool)
                        || string.IsNullOrWhiteSpace(region)
                        || string.IsNullOrWhiteSpace(clusterName)
                        || supportedVmSkus?.Any() != true)
                    {
                        throw new ProviderException(
                            $"Invalid Kusto query results. The results did not include valid values for the expected columns. Values returned = " +
                            $"(Racklocation = '{rackLocation}', MachinePoolName = '{machinePool}', Region = '{region}', ClusterName = '{clusterName}', " +
                            $"SupportedVmSkus = '{string.Join(",", supportedVmSkus ?? Array.Empty<string>())}",
                            ErrorReason.SchemaInvalid);
                    }

                    EnvironmentEntity nodeEntity = EnvironmentEntity.Node((string)row[Constants.NodeId], ExperimentComponent.AllGroups, new Dictionary<string, IConvertible>()
                    {
                        [Constants.RackLocation] = rackLocation,
                        [Constants.MachinePoolName] = machinePool,
                        [Constants.Region] = region,
                        [Constants.ClusterName] = clusterName
                    });

                    nodeEntity.SupportedVmSkus(supportedVmSkus);
                    entityPool.Add(nodeEntity);
                }

                if (entityPool?.Any() == true)
                {
                    int nodeCount = 0;
                    int environmentGroupCount = environmentGroups.Length;

                    // Order the entities by the lowest common denominator, their rack location. This enables us
                    // to subdivide the entities in such a way as to enable the experiment to support/account for any number of
                    // different node affinities during TiP node selection (e.g. SameRack, SameCluster, DifferentCluster).
                    foreach (EnvironmentEntity node in entityPool.OrderBy(node => node.RackLocation()))
                    {
                        string environmentGroup = environmentGroups.ElementAt(nodeCount % environmentGroupCount);
                        EnvironmentEntity nodeInGroup = EnvironmentEntity.Node(node.Id, environmentGroup, node.Metadata);

                        // *** For backwards compatibility ***
                        // We need to update the TipSession class to use the EnvironmentEntity.EnvironmentGroup property
                        // in the future so that the class does not need to get it from metadata on the environment entities.
                        nodeInGroup.GroupName(environmentGroup);

                        entities.Add(nodeInGroup);
                        nodeCount++;
                    }
                }
            }

            return entities;
        }

        /// <inheritdoc />
        protected async override Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            ExecutionResult result = new ExecutionResult(ExecutionStatus.InProgress);
            this.EntityManager = this.Services.GetService<EntityManager>();

            if (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await this.EntityManager.LoadEntityPoolAsync(context.Experiment.Id, cancellationToken)
                        .ConfigureDefaults();

                    IEnumerable<EnvironmentEntity> nodeEntities = null;
                    DataTable table = await this.GetKustoResponseAsync(context, component).ConfigureDefaults();

                    if (context.ExperimentStep.ExperimentGroup != ExperimentComponent.AllGroups)
                    {
                        // If the step explicitly defines a step, then we add node entities to the pool
                        // for just that group (e.g. Group A, Group B).
                        nodeEntities = NodeSelectionProvider.ConvertToEntities(table, context.ExperimentStep.ExperimentGroup);
                    }
                    else
                    {
                        // If the step uses the global wildcard (e.g. group = '*'), then we will add node entities to the pool
                        // for all groups associated with the experiment.
                        nodeEntities = NodeSelectionProvider.ConvertToEntities(table, context.GetExperimentGroups().ToArray());
                    }

                    telemetryContext.AddContext("matchingEntityCount", nodeEntities?.Count() ?? 0);
                    await this.Logger.LogTelemetryAsync($"{this.TelemetryEventNamePrefix}.NodeEntities", LogLevel.Information, telemetryContext.Clone()
                        .AddContext("entities", nodeEntities)).ConfigureDefaults();

                    if (nodeEntities?.Any() != true)
                    {
                        throw new ProviderException(
                            $"Cluster/node selection failed. There are no entities that match the criteria of the experiment.",
                            ErrorReason.EntitiesMatchingCriteriaNotFound);
                    }

                    if (nodeEntities?.Any() == true)
                    {
                        nodeEntities.OrderBy(node => node.EnvironmentGroup).ToList().ForEach(node => this.EntityManager.EntityPool.Add(node));
                        result = new ExecutionResult(ExecutionStatus.Succeeded);
                    }
                }
                finally
                {
                    await this.EntityManager.SaveEntityPoolAsync(context.Experiment.Id, cancellationToken)
                        .ConfigureDefaults();
                }
            }

            return result;
        }

        /// <summary>
        /// Queries the Kusto data source to get additional/full required information for the nodes
        /// supplied to the provider (by ID).
        /// </summary>
        /// <param name="context">Provides context to the experiment and experiment step for which this provider is related.</param>
        /// <param name="component">The experiment workflow definition for which this provider is related.</param>
        /// <returns></returns>
        protected async Task<DataTable> GetKustoResponseAsync(ExperimentContext context, ExperimentComponent component)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));

            string[] nodes = component.Parameters.GetValue<string>(Constants.Nodes).Split(',', ';');
            string nodeParameter = string.Join(",", nodes.Select(node => $"'{node}'"));
            nodeParameter = $"dynamic([{nodeParameter}])";

            string vmSkuParameter = "\"\"";
            if (component.Parameters.ContainsKey(Constants.VmSkus))
            {
                string[] vmSkuList = ((string)component.Parameters[Constants.VmSkus]).Split(',', ';');
                vmSkuParameter = string.Join(",", vmSkuList.Select(vmSku => $"'{vmSku}'"));
                vmSkuParameter = $"dynamic([{vmSkuParameter}])";
            }

            bool requestedSearchPeriod = component.Parameters.TryGetValue(Constants.SearchPeriod, out IConvertible dictionaryValue);
            int searchPeriod = requestedSearchPeriod ? (int)Convert.ChangeType(dictionaryValue, typeof(int)) : NodeSelectionProvider.defaultSearchPeriod;

            string query = Properties.Resources.DistinctNodeInfoQuery;
            query = query.Replace(Constants.NodesPlaceholder, nodeParameter, StringComparison.Ordinal);
            query = query.Replace(Constants.VmSkusPlaceholder, vmSkuParameter, StringComparison.Ordinal);
            query = query.Replace(Constants.SearchPeriodPlaceHolder, searchPeriod.ToString(), StringComparison.Ordinal);

            IKustoQueryIssuer issuer = this.Services.GetService<IKustoQueryIssuer>();

            EnvironmentSettings settings = EnvironmentSettings.Initialize(context.Configuration);
            KustoSettings kustoSettings = settings.KustoSettings.Get(Setting.AzureCM);
            AadPrincipalSettings principalSettings = kustoSettings.AadPrincipals.Get(Setting.Default);

            return await this.retryPolicy.ExecuteAsync(async () =>
            {
                return await issuer.IssueAsync(kustoSettings.ClusterUri.AbsoluteUri, kustoSettings.ClusterDatabase, query)
                    .ConfigureDefaults();
            }).ConfigureDefaults();
        }

        private class Constants
        {
            internal const string Nodes = "nodes";
            internal const string VmSkus = "vmSkus";
            internal const string SearchPeriod = "searchPeriod";
            internal const string VmSkusPlaceholder = "$vmSkus$";
            internal const string NodesPlaceholder = "$nodesList$";
            internal const string SearchPeriodPlaceHolder = "$searchPeriod$";
            internal const string NodeId = nameof(Constants.NodeId);
            internal const string RackLocation = nameof(Constants.RackLocation);
            internal const string ClusterName = nameof(Constants.ClusterName);
            internal const string MachinePoolName = nameof(Constants.MachinePoolName);
            internal const string Region = nameof(Constants.Region);
            internal const string SupportedVmSkus = nameof(Constants.SupportedVmSkus);
        }
    }
}
