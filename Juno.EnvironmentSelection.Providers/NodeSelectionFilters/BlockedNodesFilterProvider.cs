namespace Juno.EnvironmentSelection.NodeSelectionFilters
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Filter out nodes that are in a blocked state.
    /// </summary>
    public class BlockedNodesFilterProvider : NodeSelectionFilter
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BlockedNodesFilterProvider"/> class.
        /// </summary>
        public BlockedNodesFilterProvider(IServiceCollection services, IConfiguration configuration, ILogger logger)
            : base(services, CacheConstants.BlockedNodeTtl, configuration, logger, Properties.Resources.BlockedNodesFilterQuery)
        { 
        }
    }
}
