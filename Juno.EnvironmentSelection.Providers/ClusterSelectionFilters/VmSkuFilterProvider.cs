namespace Juno.EnvironmentSelection.ClusterSelectionFilters
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Filter that retrieves nodes based on VMSku
    /// </summary>
    [SupportedFilter(Name = FilterParameters.IncludeVmSku, Type = typeof(string), Required = true, SetName = ProviderConstants.VmSku, CacheLabel = ProviderConstants.VmSku)]
    [SupportedFilter(Name = FilterParameters.ExcludeVmSku, Type = typeof(string), Required = false, SetName = ProviderConstants.VmSku)]
    [SupportedFilter(Name = FilterParameters.AllocableVmCount, Type = typeof(int), Required = false, CacheLabel = ProviderConstants.MinVmCount, Default = "10")]
    [KustoColumn(Name = ProviderConstants.VmSku, AdditionalInfo = false, ComposesCacheKey = true)]
    [KustoColumn(Name = ProviderConstants.MinVmCount, AdditionalInfo = true, ComposesCacheKey = true)]
    public class VmSkuFilterProvider : ClusterSelectionFilter
    {
        /// <inheritdoc/>
        public VmSkuFilterProvider(IServiceCollection services, IConfiguration configuration, ILogger logger)
            : base(services, CacheConstants.VmSkuTtl, configuration, logger, Properties.Resources.VMSkuFilterQuery)
        {
        }

        /// <inheritdoc/>
        protected override Task PopulateCacheAsync(ICachingFunctions functions, IDictionary<ProviderCacheKey, IList<string>> cacheEntries, EventContext telemetryContext)
        {
            functions.ThrowIfNull(nameof(functions));
            cacheEntries.ThrowIfNull(nameof(cacheEntries));
            telemetryContext.ThrowIfNull(nameof(telemetryContext)); 

            // Expand each key so that each VM has its own key
            IDictionary<ProviderCacheKey, IList<string>> expandedKeys = new Dictionary<ProviderCacheKey, IList<string>>();
            foreach (KeyValuePair<ProviderCacheKey, IList<string>> entry in cacheEntries)
            {
                IEnumerable<ProviderCacheKey> currentExpandedKeys = entry.Key.ExpandEntry(ProviderConstants.VmSku, ',');
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
                    string clusterId = candidate.ClusterId;

                    // If we already contain the key then just append the VmSku to the list
                    if (result.ContainsKey(clusterId))
                    {
                        string vmSku = candidate.VmSku[0];
                        result[clusterId].VmSku.Add(vmSku);
                    }

                    // If we do not contain the key create a new candidate
                    else
                    {
                        result.Add(clusterId, candidate);
                    }
                }
            }

            return result;
        }

        internal static class FilterParameters
        {
            internal const string IncludeVmSku = "includeVmSku";
            internal const string ExcludeVmSku = "excludeVmSku";
            internal const string AllocableVmCount = "allocableVmCount";
        }
    }
}
