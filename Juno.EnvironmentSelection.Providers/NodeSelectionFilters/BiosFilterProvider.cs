namespace Juno.EnvironmentSelection.NodeSelectionFilters
{
    using Juno.Contracts;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Filter that retrieves nodes with the specified Bios Version
    /// </summary>
    [SupportedFilter(Name = FilterParameters.IncludeBios, Type = typeof(string), Required = true, SetName = ProviderConstants.BiosVersion, CacheLabel = ProviderConstants.BiosVersion)]
    [SupportedFilter(Name = FilterParameters.ExcludeBios, Type = typeof(string), Required = false, SetName = ProviderConstants.BiosVersion)]
    [KustoColumn(Name = ProviderConstants.BiosVersion, AdditionalInfo = true, ComposesCacheKey = true)]
    public class BiosFilterProvider : NodeSelectionFilter
    {
        /// <inheritdoc/>
        public BiosFilterProvider(IServiceCollection services, IConfiguration configuration, ILogger logger)
            : base(services, CacheConstants.BiosTtl, configuration, logger, Properties.Resources.BiosFilterQuery)
        {
        }

        internal static class FilterParameters
        {
            internal const string IncludeBios = "includeBiosVersion";
            internal const string ExcludeBios = "excludeBiosVersion";
        }
    }
}
