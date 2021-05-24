namespace Juno.EnvironmentSelection
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Extensions.AspNetCore;
    using Juno.Providers;
    using Kusto.Cloud.Platform.Utils;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Offers implementation of functions defined in <see cref="ICachingFunctions"/>
    /// </summary>
    public class CachingFunctions : ICachingFunctions
    {
        private const char NodeEntrySeparator = '&';
        private static readonly HashSet<string> NonAdditionalInfo = new HashSet<string>()
        {
            nameof(EnvironmentCandidate.Region),
            nameof(EnvironmentCandidate.CpuId),
            nameof(EnvironmentCandidate.VmSku),
            nameof(EnvironmentCandidate.ClusterId)
        };

        private ILogger logger;

        /// <summary>
        /// Initializes an instance of <see cref="CachingFunctions"/>
        /// </summary>
        /// <param name="logger">Logger used for capturing telemetry</param>
        public CachingFunctions(ILogger logger)
        {
            logger.ThrowIfNull(nameof(logger));
            this.logger = logger;
        }

        /// <inheritdoc/>
        public ProviderCacheKey GenerateCacheKey(DataRow row, IEnumerable<KustoColumnAttribute> attributes)
        {
            row.ThrowIfNull(nameof(row));
            attributes.ThrowIfNull(nameof(attributes));

            attributes.ThrowIfInvalid(
                nameof(attributes),
                attrs => attrs.Select(attr => !attr.ComposesCacheKey || (attr.ComposesCacheKey && row.Table.Columns.Contains(attr.Name))).Any(),
                $"{nameof(KustoColumnAttribute)} composes cache key, but the kusto query does not output the expected column");

            ProviderCacheKey result = new ProviderCacheKey();
            foreach (KustoColumnAttribute attribute in attributes)
            {
                if (attribute.ComposesCacheKey)
                {
                    result.Add(attribute.Name, (string)row[attribute.Name]);
                }
            }

            return result;
        }

        /// <inheritdoc/>
        public IEnumerable<ProviderCacheKey> GenerateCacheKeys(EnvironmentFilter filter, EventContext telemetryContext)
        {
            filter.ThrowIfNull(nameof(filter));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));
            EventContext clonedContext = telemetryContext.Clone();

            return this.logger.LogTelemetry($"{nameof(CachingFunctions)}.GenerateCacheKeys", clonedContext, () =>
            {
                IEnumerable<SupportedFilterAttribute> filterAttributes = filter.GetProviderType().GetCustomAttributes<SupportedFilterAttribute>(true);
                IEnumerable<SupportedFilterAttribute> cacheAttributes = filterAttributes.Where(attr => attr?.CacheLabel != null);

                clonedContext.AddContext(nameof(cacheAttributes), string.Join(",", cacheAttributes.Select(attr => attr.Name)));

                IList<ProviderCacheKey> cacheKeys = new List<ProviderCacheKey>();
                foreach (SupportedFilterAttribute attribute in cacheAttributes)
                {
                    // Environment Filter parameters of type string usually hold a list of values. Extracting this information 
                    // is what is being performed here.
                    if (attribute.Type == typeof(string))
                    {
                        IList<string> attributeValues = new List<string>() { attribute.Default ?? string.Empty };
                        if (filter.Parameters.TryGetValue(attribute.Name, out IConvertible paramValue))
                        {
                            attributeValues = paramValue.ToString().ToList(';', ',');
                        }

                        // Exclude from our attribute values the values we want to exclude. This will only be the case if we are working with string parameters
                        if (attribute.Required && attribute.SetName != null)
                        {
                            SupportedFilterAttribute excludeAttribute = filterAttributes.FirstOrDefault(attr => attr.SetName == attribute.SetName && attr != attribute);
                            if (filter.Parameters.TryGetValue(excludeAttribute.Name, out IConvertible excludeString))
                            {
                                IList<string> excludeValues = excludeString.ToString().ToList(',', ';');
                                foreach (string excludeValue in excludeValues)
                                {
                                    attributeValues.Remove(excludeValue);
                                }
                            }
                        }

                        // Project our current attribute onto last set of cache keys.
                        // This ensures that the keys produced have max coverage over the cache, by producing every combination
                        // possible with the attributes that filter is attempting to select.
                        List<ProviderCacheKey> nextCacheKeys = new List<ProviderCacheKey>();
                        foreach (string attributeValue in attributeValues)
                        {
                            IEnumerable<ProviderCacheKey> currentKeys = ProviderCacheKey.ProjectString(cacheKeys, attribute.CacheLabel, attributeValue);
                            nextCacheKeys.AddRange(currentKeys);
                        }

                        // Update our current list of cachekeys
                        cacheKeys = nextCacheKeys;
                    }
                    else
                    {
                        string value = filter.Parameters.GetValue<string>(attribute.Name, attribute.Default);
                        cacheKeys = ProviderCacheKey.ProjectString(cacheKeys, attribute.CacheLabel, value);
                    }
                }

                clonedContext.AddContext(nameof(cacheKeys), cacheKeys.Count);

                return cacheKeys;
            });
        }

        /// <inheritdoc/>
        public string GenerateCacheValue(EnvironmentCandidate candidate)
        {
            candidate.ThrowIfNull(nameof(candidate));

            return $"{candidate.NodeId}{CachingFunctions.NodeEntrySeparator}" +
                $"{candidate.Rack}{CachingFunctions.NodeEntrySeparator}" +
                $"{candidate.ClusterId}{CachingFunctions.NodeEntrySeparator}" +
                $"{candidate.Region}" +
                (!candidate.MachinePoolName.Equals("*", StringComparison.OrdinalIgnoreCase)
                ? $"{CachingFunctions.NodeEntrySeparator}{candidate.MachinePoolName}"
                : string.Empty);
        }

        /// <inheritdoc/>
        public EnvironmentCandidate MapOnToEnvironmentCandidate(ProviderCacheKey cacheKey, string nodeEntry)
        {
            cacheKey.ThrowIfNullOrEmpty(nameof(cacheKey));
            nodeEntry.ThrowIfNullOrWhiteSpace(nameof(nodeEntry));

            string[] values = nodeEntry.Split(CachingFunctions.NodeEntrySeparator);
            EnvironmentCandidate candidate = new EnvironmentCandidate(
                node: values[0],
                rack: values[1],
                cluster: values[2],
                machinePoolName: values.Length == 5 ? values[4] : null,
                region: values[3],
                cpuId: cacheKey.ContainsKey(ProviderConstants.CpuId) ? cacheKey[ProviderConstants.CpuId] : null,
                vmSku: cacheKey.ContainsKey(ProviderConstants.VmSku) ? cacheKey[ProviderConstants.VmSku].ToList(',', ';') : null);

            candidate.AdditionalInfo.AddRange(cacheKey.Where(entry => !CachingFunctions.NonAdditionalInfo.Contains(entry.Key)));
            return candidate;
        }

        /// <inheritdoc/>
        public async Task PopulateCacheAsync(IMemoryCache<IEnumerable<string>> providerCache, IDictionary<ProviderCacheKey, IList<string>> cacheEntries, TimeSpan ttl, EventContext telemetryContext)
        {
            providerCache.ThrowIfNull(nameof(providerCache));
            cacheEntries.ThrowIfNullOrEmpty(nameof(cacheEntries));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            EventContext clonedContext = telemetryContext.Clone();

            await this.logger.LogTelemetryAsync($"{nameof(CachingFunctions)}.PopulateCacheAsync", clonedContext, async () =>
            {
                IDictionary<string, Task<bool>> addTasks = new Dictionary<string, Task<bool>>();
                foreach (KeyValuePair<ProviderCacheKey, IList<string>> entry in cacheEntries)
                {
                    string cacheKey = entry.Key.ToString();

                    // This final check is necessary for the fact that another thread may have populated the cache before
                    // this thread had 
                    if (!providerCache.Contains(cacheKey))
                    {
                        addTasks.Add(cacheKey, providerCache.AddAsync(cacheKey, ttl, () => entry.Value));
                    }
                }

                _ = await Task.WhenAll(addTasks.Values).ConfigureDefaults();

                Dictionary<string, bool> addResults = new Dictionary<string, bool>();
                foreach (KeyValuePair<string, Task<bool>> addTask in addTasks)
                {
                    // Calling await does not block thread. the thread has awaited all "addTasks" with the Task.WhenAll call.
                    addResults.Add(addTask.Key, await addTask.Value.ConfigureDefaults());
                }

                if (!addResults.All(result => result.Value))
                {
                    IEnumerable<string> badKeys = addResults.Where(pair => !pair.Value).Select(pair => pair.Key);
                    throw new EnvironmentSelectionException($"Some values failed to be added to the cache: {string.Join(", ", badKeys)}");
                }
            }).ConfigureDefaults();
        }

        /// <inheritdoc/>
        public void OverwriteFilterParameters(EnvironmentFilter original, IEnumerable<ProviderCacheKey> keys, EventContext telemetryContext)
        {
            original.ThrowIfNull(nameof(original));
            keys.ThrowIfNull(nameof(keys));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            EventContext clonedContext = telemetryContext.Clone();

            this.logger.LogTelemetry($"{nameof(CachingFunctions)}.RestoreEnvironmentFilter", clonedContext, () =>
            {
                clonedContext.AddContext(nameof(original), original)
                    .AddContext(nameof(keys), keys.Count());

                IDictionary<string, HashSet<string>> reconstructedParameters = new Dictionary<string, HashSet<string>>();
                foreach (ProviderCacheKey cacheKey in keys)
                {
                    foreach (KeyValuePair<string, string> pair in cacheKey)
                    {
                        if (reconstructedParameters.ContainsKey(pair.Key))
                        {
                            reconstructedParameters[pair.Key].Add(pair.Value);
                        }
                        else
                        {
                            reconstructedParameters.Add(pair.Key, new HashSet<string>() { pair.Value });
                        }
                    }
                }

                IEnumerable<SupportedFilterAttribute> filterAttributes = original.GetProviderType().GetCustomAttributes<SupportedFilterAttribute>(true);
                IEnumerable<SupportedFilterAttribute> cacheAttributes = filterAttributes.Where(attr => attr?.CacheLabel != null);
                foreach (SupportedFilterAttribute attribute in cacheAttributes)
                {
                    if (original.Parameters.ContainsKey(attribute.Name))
                    {
                        if (attribute.Type == typeof(string))
                        {
                            original.Parameters[attribute.Name] = string.Join(",", reconstructedParameters[attribute.CacheLabel]);
                        }
                        else
                        {
                            original.Parameters[attribute.Name] = original.Parameters[attribute.Name];
                        }
                    }
                }

                clonedContext.AddContext($"{nameof(original)}.Filtered", original);
            });
        }

        /// <inheritdoc/>
        public async Task<IDictionary<string, EnvironmentCandidate>> RetrieveCacheHitsAsync(IMemoryCache<IEnumerable<string>> providerCache, string dictionaryKey, IEnumerable<ProviderCacheKey> cacheHits, EventContext telemetryContext)
        {
            cacheHits.ThrowIfNullOrEmpty(nameof(cacheHits));
            providerCache.ThrowIfNull(nameof(providerCache));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            EventContext clonedContext = telemetryContext.Clone();

            return await this.logger.LogTelemetryAsync($"{nameof(CachingFunctions)}.RetrieveCacheHits", clonedContext, async () => 
            {
                clonedContext.AddContext(nameof(cacheHits), cacheHits.Count());
                IDictionary<string, EnvironmentCandidate> candidates = new Dictionary<string, EnvironmentCandidate>();
                PropertyInfo[] properties = typeof(EnvironmentCandidate).GetPropertiesOrdered();
                PropertyInfo propertyKey = properties.FirstOrDefault(p => p.Name.Equals(dictionaryKey, StringComparison.OrdinalIgnoreCase));
                foreach (ProviderCacheKey cacheHit in cacheHits)
                {
                    IEnumerable<string> nodeList = await providerCache.GetAsync(cacheHit.ToString()).ConfigureDefaults();
                    foreach (string node in nodeList)
                    {
                        EnvironmentCandidate currentCandidate = this.MapOnToEnvironmentCandidate(cacheHit, node);
                        string key = propertyKey.GetValue(currentCandidate).ToString();
                        candidates.Add(key, currentCandidate);
                    }
                }

                clonedContext.AddContext($"{nameof(candidates)}.Count", candidates.Count);

                return candidates;
            }).ConfigureDefaults();
        }
    }
}
