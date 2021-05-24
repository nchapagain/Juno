namespace Juno.EnvironmentSelection.ClusterSelectionFilters
{
    using System;
    using Juno.Contracts;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Abstract class Cluster Selection Filters must implement. This abstract class offers a contract that each
    /// cluster selection filter must adhere to.
    /// </summary>
    [KustoColumn(Name = ProviderConstants.ClusterId, AdditionalInfo = false)]
    [KustoColumn(Name = ProviderConstants.Region, AdditionalInfo = false, ComposesCacheKey = true)]
    [SupportedFilter(Name = FilterParametersBase.IncludeRegion, Type = typeof(string), Required = true, CacheLabel = ProviderConstants.Region, SetName = ProviderConstants.Region)]
    [SupportedFilter(Name = FilterParametersBase.ExcludeRegion, Type = typeof(string), Required = false, SetName = ProviderConstants.Region)]
    public abstract class ClusterSelectionFilter : KustoFilterProvider
    {
        /// <inheritdoc/>
        public ClusterSelectionFilter(IServiceCollection services, TimeSpan ttl, IConfiguration configuration, ILogger logger, string query)
            : base(services, ttl, configuration, logger, query)
        {
            this.DictionaryKey = ProviderConstants.ClusterId;
        }

        internal static class FilterParametersBase
        {
            internal const string IncludeRegion = "includeRegion";
            internal const string ExcludeRegion = "excludeRegion";
        }
    }
}
