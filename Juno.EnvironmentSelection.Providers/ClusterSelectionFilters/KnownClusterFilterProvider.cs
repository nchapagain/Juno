namespace Juno.EnvironmentSelection.ClusterSelectionFilters
{
    using Juno.Contracts;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Filter provider that allows an entry point into detailing specific clusters into an environment query
    /// </summary>
    [KustoColumn(Name = ProviderConstants.ClusterId, AdditionalInfo = false, ComposesCacheKey = true)]
    [SupportedFilter(Name = FilterParameters.IncludeCluster, Type = typeof(string), Required = true, CacheLabel = ProviderConstants.ClusterId, SetName = ProviderConstants.ClusterId)]
    [SupportedFilter(Name = FilterParameters.ExcludeCluster, Type = typeof(string), Required = false, SetName = ProviderConstants.ClusterId)]
    public class KnownClusterFilterProvider : ClusterSelectionFilter
    {
        /// <inheritdoc />
        public KnownClusterFilterProvider(IServiceCollection services, IConfiguration configuration, ILogger logger)
            : base(services, CacheConstants.KnownClusterTtl, configuration, logger, Properties.Resources.KnownClusterQuery)
        {
        }
    }

    internal static class FilterParameters
    {
        internal const string IncludeCluster = "includeCluster";
        internal const string ExcludeCluster = "excludeCluster";
    }
}
