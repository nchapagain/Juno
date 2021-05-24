namespace Juno.EnvironmentSelection.NodeSelectionFilters
{
    using Juno.Contracts;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Filter that retrieves nodes with the specified or hardware sku.
    /// </summary>
    [SupportedFilter(Name = FilterParameters.ExcludeHwSku, Type = typeof(string), Required = false, SetName = ProviderConstants.HwSku)]
    [SupportedFilter(Name = FilterParameters.IncludeHwSku, Type = typeof(string), Required = true, SetName = ProviderConstants.HwSku, CacheLabel =ProviderConstants.HwSku)]
    [KustoColumn(Name = ProviderConstants.HwSku, AdditionalInfo = true, ComposesCacheKey = true)]
    public class HardwareSkuFilterProvider : NodeSelectionFilter
    {
        /// <inheritdoc/>
        public HardwareSkuFilterProvider(IServiceCollection services, IConfiguration configuration, ILogger logger)
            : base(services, CacheConstants.HwSkuTtl, configuration, logger, Properties.Resources.HardwareSkuFilterQuery)
        {
        }

        internal static class FilterParameters
        {
            internal const string ExcludeHwSku = "excludeHwSku";
            internal const string IncludeHwSku = "includeHwSku";
        }
    }
}
