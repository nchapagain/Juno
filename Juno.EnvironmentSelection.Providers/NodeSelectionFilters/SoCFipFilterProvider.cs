namespace Juno.EnvironmentSelection.NodeSelectionFilters
{
    using Juno.Contracts;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Filter that retrieves nodes with the specified SoC Firmware
    /// </summary>
    [SupportedFilter(Name = FilterParameters.IncludeFirmware, Type = typeof(string), Required = true, SetName = ProviderConstants.SoCFip, CacheLabel = ProviderConstants.SoCFip)]
    [SupportedFilter(Name = FilterParameters.ExcludeFirmware, Type = typeof(string), Required = false, SetName = ProviderConstants.SoCFip)]
    [KustoColumn(Name = ProviderConstants.SoCFip, AdditionalInfo = true, ComposesCacheKey = true)]
    public class SoCFipFilterProvider : NodeSelectionFilter
    {
        /// <inheritdoc/>
        public SoCFipFilterProvider(IServiceCollection services, IConfiguration configuration, ILogger logger)
            : base(services, CacheConstants.SoCTtl, configuration, logger, Properties.Resources.SoCFipQuery)
        {
        }

        internal static class FilterParameters
        {
            internal const string IncludeFirmware = nameof(FilterParameters.IncludeFirmware);
            internal const string ExcludeFirmware = nameof(FilterParameters.ExcludeFirmware);
        }
    }
}
