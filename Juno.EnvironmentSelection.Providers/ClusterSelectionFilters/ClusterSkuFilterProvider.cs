namespace Juno.EnvironmentSelection.ClusterSelectionFilters
{
    using Juno.Contracts;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Retrieves clusters that support the cluster sku given.
    /// </summary>
    [SupportedFilter(Name = FilterParameters.IncludeClusterSku, Type = typeof(string), Required = true, SetName = ProviderConstants.ClusterSku, CacheLabel = ProviderConstants.ClusterSku)]
    [SupportedFilter(Name = FilterParameters.ExcludeClusterSku, Type = typeof(string), Required = false, SetName = ProviderConstants.ClusterSku)]
    [KustoColumn(Name = ProviderConstants.ClusterSku, AdditionalInfo = true, ComposesCacheKey = true)]
    public class ClusterSkuFilterProvider : ClusterSelectionFilter
    {
        /// <inheritdoc/>
        public ClusterSkuFilterProvider(IServiceCollection services, IConfiguration configuration, ILogger logger)
            : base(services, CacheConstants.ClusterSkuTtl, configuration, logger, Properties.Resources.ClusterSkuFilterQuery)
        {
        }

        internal static class FilterParameters
        {
            internal const string IncludeClusterSku = "includeClusterSku";
            internal const string ExcludeClusterSku = "excludeClusterSku";
        }
    }
}
