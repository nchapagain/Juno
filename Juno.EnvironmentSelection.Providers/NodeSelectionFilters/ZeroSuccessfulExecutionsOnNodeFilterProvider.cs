namespace Juno.EnvironmentSelection.NodeSelectionFilters
{
    using Juno.Contracts;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Retrieves nodes that haven't run a given experiment successfully.
    /// </summary>
    [SupportedFilter(Name = FilterParameters.IncludeExperimentName, Type = typeof(string), Required = true, CacheLabel = ProviderConstants.ExperimentName)]
    [KustoColumn(Name = ProviderConstants.ExperimentName, AdditionalInfo = true, ComposesCacheKey = true)]
    public class ZeroSuccessfulExecutionsOnNodeFilterProvider : NodeSelectionFilter
    {
        /// <inheritdoc/>
        public ZeroSuccessfulExecutionsOnNodeFilterProvider(IServiceCollection services, IConfiguration configuration, ILogger logger)
            : base(services, CacheConstants.ZeroSuccessfulExecutionsOnNodeTtl, configuration, logger, Properties.Resources.ZeroSuccessfulExecutionsOnNodeFilterQuery)
        {
        }

        internal static class FilterParameters
        {
            internal const string IncludeExperimentName = "includeExperimentName";
        }
    }
}
