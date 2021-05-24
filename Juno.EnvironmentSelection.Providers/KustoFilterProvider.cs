namespace Juno.EnvironmentSelection
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.Contracts.Validation;
    using Juno.Extensions.AspNetCore;
    using Juno.Providers;
    using Kusto.Data.Exceptions;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Kusto;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Polly;

    /// <summary>
    /// Intermediate implementation of <see cref="EnvironmentSelectionProvider"/>
    /// for Providers that run Kusto Queries
    /// </summary>
    public abstract class KustoFilterProvider : EnvironmentSelectionProvider
    {
        private IAsyncPolicy retryPolicy;
        private IDictionary<Type, Func<string, IConvertible, string, string>> parameterReplacers = new Dictionary<Type, Func<string, IConvertible, string, string>>()
        {
            [typeof(int)] = (key, value, query) =>
            {
                return query.Replace($"${key}$", $"{(value == null ? -1 : value)}", StringComparison.OrdinalIgnoreCase);
            },
            [typeof(string)] = (key, value, query) =>
            {
                if (value == null)
                {
                    return query.Replace($"${key}$", "\"\"", StringComparison.OrdinalIgnoreCase);
                }

                string[] parameters = ((string)value).Split(',', ';');
                return query.Replace($"${key}$", $"dynamic([{string.Join(", ", parameters.Select(param => $"'{param.Trim()}'"))}])", StringComparison.Ordinal);
            },
            [typeof(bool)] = (key, value, query) =>
            {
                return query.Replace($"${key}$", $"{value?.ToString() ?? "true"}", StringComparison.OrdinalIgnoreCase);
            }
        };

        /// <summary>
        /// Instantiates a <see cref="KustoFilterProvider"/>
        /// </summary>
        /// <param name="services">List of services used for dependency injection</param>
        /// <param name="ttl">Time to live for the results in the cache.</param>
        /// <param name="configuration">The current configuration of the execution environment</param>
        /// <param name="logger"><see cref="ILogger"/></param>
        /// <param name="query">The kusto query to be executed.</param>
        public KustoFilterProvider(IServiceCollection services, TimeSpan ttl, IConfiguration configuration, ILogger logger, string query)
            : base(services, ttl, configuration, logger)
        {
            query.ThrowIfNullOrWhiteSpace(nameof(query));

            this.BaseQuery = query;

            EnvironmentSettings settings = EnvironmentSettings.Initialize(configuration);
            this.KustoSettings = settings.KustoSettings.Get(Setting.AzureCM);
        }

        /// <summary>
        /// BaseQuery Supplied by derived provider: must return a datatable in the pre-designated format.
        /// </summary>
        protected string BaseQuery { get; }

        /// <summary>
        /// The key to index the keys of the dictionary by
        /// </summary>
        protected string DictionaryKey { get; set; }

        /// <summary>
        /// Kusto settings required for issuing queries
        /// </summary>
        protected KustoSettings KustoSettings { get; }

        /// <summary>
        /// Extensible Dictionary allowing for derived member to define how to replace parameters in the query.
        /// </summary>
        protected virtual IDictionary<Type, Func<string, IConvertible, string, string>> ParameterReplacers => this.parameterReplacers;

        /// <summary>
        /// Cache that allows providers to store their results in memory
        /// </summary>
        protected IMemoryCache<IEnumerable<string>> ProviderCache { get; private set; }

        /// <inheritdoc/>
        public override Task ConfigureServicesAsync()
        {
            if (!this.Services.TryGetService<IKustoQueryIssuer>(out IKustoQueryIssuer issuer))
            {
                AadPrincipalSettings principalSettings = this.KustoSettings.AadPrincipals.Get(Setting.Default);
                this.Services.AddSingleton<IKustoQueryIssuer>(new KustoQueryIssuer(
                    principalSettings.PrincipalId,
                    principalSettings.PrincipalCertificateThumbprint,
                    principalSettings.TenantId));
            }

            // Retry Policies:
            // 1) Handle InternalServiceError issues differently than others. The Kusto cluster can get overloaded at times and
            //    we just need to backoff a bit more before retries.
            //
            // 2) Handle other exceptions that do not indicate the Kusto cluster itself is having load issues.
            this.retryPolicy = Policy.WrapAsync(
                Policy.Handle<KustoClientException>().Or<Kusto.Cloud.Platform.Utils.UtilsException>().Or<AggregateException>()
                .WaitAndRetryAsync(retryCount: 5, (retries) =>
                {
                    return TimeSpan.FromSeconds(Math.Pow(2, retries));
                }),
                Policy.Handle<KustoServiceException>().WaitAndRetryAsync(retryCount: 5, (retries) =>
                {
                    return TimeSpan.FromSeconds(Math.Pow(2, retries));
                }));

            IMemoryCache<IEnumerable<string>> cache;
            if (!this.Services.TryGetService<IMemoryCache<IEnumerable<string>>>(out cache))
            {
                cache = new MemoryCache<IEnumerable<string>>();
                this.Services.AddSingleton(cache);
            }

            this.ProviderCache = cache;

            if (!this.Services.TryGetService<ICachingFunctions>(out ICachingFunctions functions))
            {
                this.Services.AddSingleton<ICachingFunctions>(new CachingFunctions(this.Logger));
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Applies the environment filter as a kusto query
        /// </summary>
        /// <param name="filter">Filter to apply</param>
        /// <param name="telemetryContext">Event context used to capture telemetry</param>
        /// <param name="token"><see cref="CancellationToken"/></param>
        /// <returns></returns>
        protected async override Task<IDictionary<string, EnvironmentCandidate>> ExecuteAsync(EnvironmentFilter filter, EventContext telemetryContext, CancellationToken token)
        {
            filter.ThrowIfNull(nameof(filter));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            IDictionary<string, EnvironmentCandidate> result = new Dictionary<string, EnvironmentCandidate>();

            if (token.IsCancellationRequested)
            {
                return result;
            }

            IKustoQueryIssuer queryIssuer = this.Services.GetService<IKustoQueryIssuer>();
            ICachingFunctions cacheingFunctions = this.Services.GetService<ICachingFunctions>();

            AadPrincipalSettings principalSettings = this.KustoSettings.AadPrincipals.Get(Setting.Default);

            IEnumerable<ProviderCacheKey> cacheKeys = cacheingFunctions.GenerateCacheKeys(filter, telemetryContext);
            IEnumerable<ProviderCacheKey> cacheHits = cacheKeys.Where(key => this.ProviderCache.Contains(key.ToString()));
            IEnumerable<ProviderCacheKey> cacheMisses = cacheKeys.Where(key => !cacheHits?.Contains(key) == true);

            telemetryContext.AddContext(nameof(cacheKeys), cacheKeys.Count());
            telemetryContext.AddContext(nameof(cacheHits), cacheHits.Count());
            telemetryContext.AddContext(nameof(cacheMisses), cacheMisses.Count());

            // Combine the results from the cache hits and cache misses.
            if (cacheHits?.Any() == true)
            {
                result.AddRange(await this.RetrieveCacheHitsAsync(cacheingFunctions, filter, cacheHits, telemetryContext).ConfigureDefaults());
            }

            if (cacheMisses?.Any() == true)
            {
                cacheingFunctions.OverwriteFilterParameters(filter, cacheMisses, telemetryContext);
                string populatedQuery = this.ReplaceQueryParameters(filter);
                telemetryContext.AddContext(nameof(populatedQuery), populatedQuery);

                DataTable table = await this.Logger.LogTelemetryAsync($"{this.GetType().Name}.KustoExecution", telemetryContext, async () =>
                {
                    return await this.retryPolicy.ExecuteAsync(async () =>
                    {
                        return await queryIssuer.IssueAsync(this.KustoSettings.ClusterUri.AbsoluteUri, this.KustoSettings.ClusterDatabase, populatedQuery)
                            .ConfigureDefaults();
                    }).ConfigureDefaults();
                }).ConfigureDefaults();

                ValidationResult validationResult = this.ValidateResult(table);
                if (!validationResult.IsValid)
                {
                    throw new ProviderException($"Errors occured while parsing kusto response from filter: {this.GetType()} " +
                        $"{string.Join(", ", validationResult.ValidationErrors)}");
                }

                var expectedColumns = this.GetType().GetCustomAttributes<KustoColumnAttribute>(true);
                IDictionary<ProviderCacheKey, IList<string>> cacheValues = new Dictionary<ProviderCacheKey, IList<string>>();

                IDictionary<string, EnvironmentCandidate> currentResults = table.ToEnvironmentCandidate(cacheingFunctions, expectedColumns, this.DictionaryKey, out cacheValues);
                foreach (KeyValuePair<string, EnvironmentCandidate> currentResult in currentResults)
                {
                    // If the node has been retrieved from the cache, it can also be retrieved from source, only under the circumstance
                    // the node was stored in cache with VmSku1 but was retrieved from source with VmSku2
                    if (result.ContainsKey(currentResult.Key))
                    {
                        // Append the newfound vmskus to the original result retrieved from the cache.
                        IList<string> vmSkus = result[currentResult.Key].VmSku;
                        foreach (string newSku in currentResult.Value.VmSku)
                        {
                            vmSkus.Add(newSku);
                        }
                    }
                    else
                    {
                        result.Add(currentResult.Key, currentResult.Value);
                    }
                }

                // Its possible for no results to be produced:
                // i.e. includeRegion: US West
                //      excludeRegion: US West
                if (cacheValues.Any())
                {
                    // If we ask kusto for bad combinations, still want to record the bad combination, but put an empty list.
                    // so we dont continually ask kusto for these bad combinations 
                    IEnumerable<ProviderCacheKey> badCombinations = cacheMisses.Where(miss => !cacheValues.ContainsKey(miss)).ToList();
                    foreach (ProviderCacheKey badCombination in badCombinations)
                    {
                        cacheValues.Add(badCombination, new List<string>());
                    }

                    // Populate the cache with the values retreived from the cachemisses.
                    await this.PopulateCacheAsync(cacheingFunctions, cacheValues, telemetryContext).ConfigureDefaults();
                }
            }

            telemetryContext.AddContext(Constants.ResultCount, result.Count);

            return result;
        }

        /// <summary>
        /// Populates kusto query values into the cache
        /// </summary>
        /// <param name="functions">Functions that allow for mapping into and out of the cache.</param>
        /// <param name="cacheEntries">Entries to place in the cache.</param>
        /// <param name="telemetryContext">Event Context used for capturing telemetry</param>
        /// <returns>an awaitable <see cref="Task"/></returns>
        protected virtual Task PopulateCacheAsync(ICachingFunctions functions, IDictionary<ProviderCacheKey, IList<string>> cacheEntries, EventContext telemetryContext)
        {
            functions.ThrowIfNull(nameof(functions));
            cacheEntries.ThrowIfNull(nameof(cacheEntries));
            telemetryContext.ThrowIfNull(nameof(telemetryContext)); 
            return functions.PopulateCacheAsync(this.ProviderCache, cacheEntries, this.TTL, telemetryContext);
        }

        /// <summary>
        /// Retreives values from the cache.
        /// </summary>
        /// <param name="functions">Functions that allow for mapping into and out of the cache.</param>
        /// <param name="filter">EnvironmentFilter to give context to the retrieve.</param>
        /// <param name="cacheHits">Keys to retreive the cache.</param>
        /// <param name="telemetryContext">Event context used for capturing telemetry</param>
        /// <returns>The dictionary of cache values retreived.</returns>
        protected virtual Task<IDictionary<string, EnvironmentCandidate>> RetrieveCacheHitsAsync(ICachingFunctions functions, EnvironmentFilter filter, IEnumerable<ProviderCacheKey> cacheHits, EventContext telemetryContext)
        {
            functions.ThrowIfNull(nameof(functions));
            filter.ThrowIfNull(nameof(filter));
            cacheHits.ThrowIfNull(nameof(cacheHits));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));
            return functions.RetrieveCacheHitsAsync(this.ProviderCache, this.DictionaryKey, cacheHits, telemetryContext);
        }

        /// <inheritdoc/>
        protected virtual string ReplaceQueryParameters(EnvironmentFilter filter)
        {
            filter.ThrowIfNull(nameof(filter));
            string result = this.BaseQuery;
            IEnumerable<SupportedFilterAttribute> attributes = this.GetType().GetCustomAttributes<SupportedFilterAttribute>();
            foreach (SupportedFilterAttribute attribute in attributes)
            {
                // Enums should display exact behavior as strings.
                Type attrType = attribute.Type.IsEnum ? typeof(string) : attribute.Type;
                if (!this.ParameterReplacers.TryGetValue(attrType, out var replacer))
                {
                    throw new ProviderException($"Replacement function for type: {attribute.Type} in provider: {filter.Type} is not defined.");
                }

                string key = attribute.Name;
                IConvertible value = null;
                if ((attribute.Default != null && attribute.Type != typeof(string)) || filter.Parameters.ContainsKey(key))
                {
                    value = filter.Parameters.GetValue<string>(key, attribute.Default);
                }

                result = replacer.Invoke(key, value, result);
            }

            return result;
        }

        internal static class Constants
        {
            internal const string ResultCount = "queryResultCount";
        }
    }
}
