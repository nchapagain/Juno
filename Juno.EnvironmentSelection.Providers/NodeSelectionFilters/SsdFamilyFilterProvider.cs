namespace Juno.EnvironmentSelection.NodeSelectionFilters
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Filter provider that returns certain SSDs that belong to a family AND
    /// are a Data drive or a system Drive
    /// </summary>
    [KustoColumn(Name = ProviderConstants.SsdFamily, AdditionalInfo = true, ComposesCacheKey = true)]
    [KustoColumn(Name = ProviderConstants.SsdDriveType, AdditionalInfo = true, ComposesCacheKey = true)]
    [SupportedFilter(Name = FilterParameters.IncludeFamily, Type = typeof(string), Required = true, CacheLabel = ProviderConstants.SsdFamily)]
    [SupportedFilter(Name = FilterParameters.DriveType, Type = typeof(SsdDriveType), Required = true, CacheLabel = ProviderConstants.SsdDriveType)]
    public class SsdFamilyFilterProvider : NodeSelectionFilter
    {
        /// <inheritdoc/>
        public SsdFamilyFilterProvider(IServiceCollection services, IConfiguration configuration, ILogger logger)
            : base(services, CacheConstants.SsdTtl, configuration, logger, Properties.Resources.SsdFilterQuery)
        { 
        }

        /// <inheritdoc/>
        protected override Task<IDictionary<string, EnvironmentCandidate>> ExecuteAsync(EnvironmentFilter filter, EventContext telemetryContext, CancellationToken token)
        {
            filter.ThrowIfNull(nameof(filter));

            // Confirm the list is sorted in asc order.
            IEnumerable<string> driveTypes = filter.Parameters[FilterParameters.DriveType].ToString().ToList(',', ';').OrderBy(d => d);
            filter.Parameters[FilterParameters.DriveType] = string.Join(",", driveTypes);
            
            return base.ExecuteAsync(filter, telemetryContext, token);
        }

        internal static class FilterParameters
        {
            internal const string IncludeFamily = "includeFamily";
            internal const string DriveType = "driveType";
        }
    }
}
