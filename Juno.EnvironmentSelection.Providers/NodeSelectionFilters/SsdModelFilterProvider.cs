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
    /// Filter provider that returns nodes that have an SSD with a 
    /// particular Model version.
    /// </summary>
    [KustoColumn(Name = ProviderConstants.SsdModel, AdditionalInfo = true, ComposesCacheKey = true)]
    [KustoColumn(Name = ProviderConstants.SsdDriveType, AdditionalInfo = true, ComposesCacheKey = true)]
    [SupportedFilter(Name = FilterParameters.IncludeModel, Type = typeof(string), Required = true, CacheLabel = ProviderConstants.SsdModel, SetName = ProviderConstants.SsdModel)]
    [SupportedFilter(Name = FilterParameters.ExcludeModel, Type = typeof(string), Required = false, SetName = ProviderConstants.SsdModel)]
    [SupportedFilter(Name = FilterParameters.DriveType, Type = typeof(SsdDriveType), Required = true, CacheLabel = ProviderConstants.SsdDriveType)]

    public class SsdModelFilterProvider : NodeSelectionFilter
    {
        /// <inheritdoc/>
        public SsdModelFilterProvider(IServiceCollection services, IConfiguration configuration, ILogger logger)
            : base(services, CacheConstants.SsdTtl, configuration, logger, Properties.Resources.SsdModelNumberQuery)
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

        /// <inheritdoc/>
        protected override Task PopulateCacheAsync(ICachingFunctions functions, IDictionary<ProviderCacheKey, IList<string>> cacheEntries, EventContext telemetryContext)
        {
            functions.ThrowIfNull(nameof(functions));
            cacheEntries.ThrowIfNull(nameof(cacheEntries));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            // Expand each key so that each SSD Model has its own key
            IDictionary<ProviderCacheKey, IList<string>> expandedKeys = new Dictionary<ProviderCacheKey, IList<string>>();
            foreach (KeyValuePair<ProviderCacheKey, IList<string>> entry in cacheEntries)
            {
                IEnumerable<ProviderCacheKey> currentExpandedKeys = entry.Key.ExpandEntry(ProviderConstants.SsdModel, ',');
                foreach (ProviderCacheKey key in currentExpandedKeys)
                {
                    if (expandedKeys.ContainsKey(key))
                    {
                        // If we have the key just add the list
                        IList<string> presentList = expandedKeys[key];
                        foreach (string newValue in entry.Value)
                        {
                            presentList.Add(newValue);
                        }
                    }

                    // Else start a new list.
                    else
                    {
                        expandedKeys.Add(key, new List<string>(entry.Value));
                    }
                }
            }

            return base.PopulateCacheAsync(functions, expandedKeys, telemetryContext);
        }

        /// <inheritdoc/>
        protected async override Task<IDictionary<string, EnvironmentCandidate>> RetrieveCacheHitsAsync(ICachingFunctions functions, EnvironmentFilter filter, IEnumerable<ProviderCacheKey> cacheHits, EventContext telemetryContext)
        {
            functions.ThrowIfNull(nameof(functions));
            filter.ThrowIfNull(nameof(filter));
            cacheHits.ThrowIfNull(nameof(cacheHits));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            IDictionary<string, EnvironmentCandidate> result = new Dictionary<string, EnvironmentCandidate>();
            foreach (ProviderCacheKey key in cacheHits)
            {
                IEnumerable<string> cacheNodes = await this.ProviderCache.GetAsync(key.ToString()).ConfigureDefaults();
                foreach (string cacheNode in cacheNodes)
                {
                    // Create the candidate
                    EnvironmentCandidate candidate = functions.MapOnToEnvironmentCandidate(key, cacheNode);
                    string nodeId = candidate.NodeId;

                    // If we already contain the key then just append the SSD Model to the list
                    if (result.ContainsKey(nodeId))
                    {
                        string model = candidate.AdditionalInfo[ProviderConstants.SsdModel];
                        string originalList = result[nodeId].AdditionalInfo[ProviderConstants.SsdModel];
                        result[nodeId].AdditionalInfo[ProviderConstants.SsdModel] = string.Join(",", model, originalList);
                    }

                    // If we do not contain the key create a new candidate
                    else
                    {
                        result.Add(nodeId, candidate);
                    }
                }
            }

            return result;
        }

        internal static class FilterParameters
        {
            internal const string IncludeModel = nameof(FilterParameters.IncludeModel);
            internal const string ExcludeModel = nameof(FilterParameters.ExcludeModel);
            internal const string DriveType = nameof(FilterParameters.DriveType);
        }
    }
}
