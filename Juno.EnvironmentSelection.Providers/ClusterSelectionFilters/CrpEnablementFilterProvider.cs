namespace Juno.EnvironmentSelection.ClusterSelectionFilters
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Filter that retreives nodes based on if the cluster is CRP enabled.
    /// </summary>
    public class CRPEnablementFilterProvider : ClusterSelectionFilter
    {
        /// <inheritdoc />
        public CRPEnablementFilterProvider(IServiceCollection services, IConfiguration configuration, ILogger logger)
            : base(services, CacheConstants.CRPCheckTtl, configuration, logger, Properties.Resources.CrpEnablementQuery)
        {
        }
    }
}
