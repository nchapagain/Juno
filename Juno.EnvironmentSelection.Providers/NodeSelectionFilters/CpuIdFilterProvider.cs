namespace Juno.EnvironmentSelection.NodeSelectionFilters
{
    using Juno.Contracts;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Retrieves clusters that support the CPUID given.
    /// </summary>
    [SupportedFilter(Name = FilterParameters.IncludeCpuId, Type = typeof(string), Required = true, SetName = ProviderConstants.CpuId, CacheLabel = ProviderConstants.CpuId)]
    [SupportedFilter(Name = FilterParameters.ExcludeCpuId, Type = typeof(string), Required = false, SetName = ProviderConstants.CpuId)]
    [KustoColumn(Name = ProviderConstants.CpuId, AdditionalInfo = false, ComposesCacheKey = true)]
    public class CpuIdFilterProvider : NodeSelectionFilter
    {
        /// <inheritdoc/>
        public CpuIdFilterProvider(IServiceCollection services, IConfiguration configuration, ILogger logger)
            : base(services, CacheConstants.CpuIdTtl, configuration, logger, Properties.Resources.CpuIdFilterQuery)
        {
        }

        internal static class FilterParameters
        {
            internal const string IncludeCpuId = "includeCpuId";
            internal const string ExcludeCpuId = "excludeCpuId";
        }
    }
}
