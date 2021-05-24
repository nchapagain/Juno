namespace Juno.EnvironmentSelection.NodeSelectionFilters
{
    using Juno.Contracts;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Filter provider to query based on Microcode version
    /// </summary>
    [SupportedFilter(Name = FilterParameters.IncludeMicrocode, Type = typeof(string), Required = true, SetName = ProviderConstants.Microcode, CacheLabel = ProviderConstants.Microcode)]
    [SupportedFilter(Name = FilterParameters.ExcludeMicrocode, Type = typeof(string), Required = false, SetName = ProviderConstants.Microcode)]
    [KustoColumn(Name = ProviderConstants.Microcode, AdditionalInfo = true, ComposesCacheKey = true)]
    public class MicrocodeFilterProvider : NodeSelectionFilter
    {
        /// <inheritdoc/>
        public MicrocodeFilterProvider(IServiceCollection services, IConfiguration configuration, ILogger logger)
            : base(services, CacheConstants.MicrocodeTtl, configuration, logger, Properties.Resources.MicrocodeFilterQuery)
        {
        }

        internal static class FilterParameters
        {
            internal const string IncludeMicrocode = "includeMicrocode";
            internal const string ExcludeMicrocode = "excludeMicrocode";
        }
    }
}
