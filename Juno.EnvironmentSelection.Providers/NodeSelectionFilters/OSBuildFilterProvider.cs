namespace Juno.EnvironmentSelection.NodeSelectionFilters
{
    using Juno.Contracts;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Filter that retrieves nodes with the specified OS UBR Build
    /// </summary>
    [SupportedFilter(Name = FilterParameters.IncludeOsUbr, Type = typeof(string), Required = true, SetName = ProviderConstants.OsBuildUbr, CacheLabel = ProviderConstants.OsBuildUbr)]
    [SupportedFilter(Name = FilterParameters.ExcludeOsUbr, Type = typeof(string), Required = false, SetName = ProviderConstants.OsBuildUbr)]
    [KustoColumn(Name = ProviderConstants.OsBuildUbr, AdditionalInfo = true, ComposesCacheKey = true)]
    public class OSBuildFilterProvider : NodeSelectionFilter
    {
        /// <inheritdoc />
        public OSBuildFilterProvider(IServiceCollection services, IConfiguration configuration, ILogger logger)
            : base(services, CacheConstants.OsTtl, configuration, logger, Properties.Resources.OSBuildFilterQuery)
        {
        }

        internal static class FilterParameters
        {
            internal const string IncludeOsUbr = "includeOSBuild";
            internal const string ExcludeOsUbr = "excludeOSBuild";
        }
    }
}
