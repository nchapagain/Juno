namespace Juno.EnvironmentSelection
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Extensions.AspNetCore;
    using Microsoft.Azure.CRC.Telemetry;

    /// <summary>
    /// Interface to supply transformation functions to bring values in and out of the cache.
    /// </summary>
    public interface ICachingFunctions
    {
        /// <summary>
        /// Overwrite the parameters belonging to the given <see cref="EnvironmentFilter"/> 
        /// such that the parameters correspond with the values in the keys given.
        /// </summary>
        /// <param name="original">The <see cref="EnvironmentFilter"/> whose parameters should be overwritten</param>
        /// <param name="keys">The keys that offer the new parameters</param>
        /// <param name="telemetryContext"><see cref="EventContext"/> to capture telemetry</param>
        void OverwriteFilterParameters(EnvironmentFilter original, IEnumerable<ProviderCacheKey> keys, EventContext telemetryContext);

        /// <summary>
        /// Function that defines the 1 to 1 transformation of an <see cref="EnvironmentFilter"/>
        /// to a set of <see cref="ProviderCacheKey"/>
        /// </summary>
        /// <param name="filter">The input <see cref="EnvironmentFilter"/></param>
        /// <param name="telemetryContext"><see cref="EventContext"/> to capture telemetry</param>
        /// <returns>A set of <see cref="ProviderCacheKey"/></returns>
        IEnumerable<ProviderCacheKey> GenerateCacheKeys(EnvironmentFilter filter, EventContext telemetryContext);

        /// <summary>
        /// Generates a cache key given the row and instructions on how to parse the row
        /// given via the <see cref="IEnumerable{KustoColumnAttribute}"/>
        /// </summary>
        /// <param name="row">The data row to parse</param>
        /// <param name="attributes">List of <see cref="KustoColumnAttribute"/> disctating how the datarow should be parsed</param>
        /// <returns>A <see cref="ProviderCacheKey"/></returns>
        ProviderCacheKey GenerateCacheKey(DataRow row, IEnumerable<KustoColumnAttribute> attributes);

        /// <summary>
        /// Generates a cache value given a <see cref="EnvironmentCandidate"/>
        /// The Cache representation is dependent on the implementation of the interface.
        /// </summary>
        /// <param name="candidate">The candidate to derivce the cache value from.</param>
        /// <returns>A <see cref="string"/> that represents the candidate in cache value form.</returns>
        string GenerateCacheValue(EnvironmentCandidate candidate);

        /// <summary>
        /// Retrieves cache values with the given cache keys from the given cache.
        /// </summary>
        /// <param name="cacheHits"><see cref="IEnumerable{ProviderCacheKey}"/> that define values contained in the cache.</param>
        /// <param name="telemetryContext"><see cref="EventContext"/> to capture telemetry</param>
        /// <param name="providerCache">The cache to retreive values from.</param>
        /// <param name="dictionaryKey"></param>
        /// <returns>Result from the cache.</returns>
        Task<IDictionary<string, EnvironmentCandidate>> RetrieveCacheHitsAsync(IMemoryCache<IEnumerable<string>> providerCache, string dictionaryKey, IEnumerable<ProviderCacheKey> cacheHits, EventContext telemetryContext);

        /// <summary>
        /// Populates cache values with the given cache keys and cache values.
        /// </summary>
        /// <param name="providerCache">The cache to populate</param>
        /// <param name="cacheEntries">The key value pairs that should map into the cache.</param>
        /// <param name="ttl">How long the cache entries should live.</param>
        /// <param name="telemetryContext"><see cref="EventContext"/> to capture telemetry</param>
        /// <returns>an awaitable <see cref="Task"/></returns>
        Task PopulateCacheAsync(IMemoryCache<IEnumerable<string>> providerCache, IDictionary<ProviderCacheKey, IList<string>> cacheEntries, TimeSpan ttl, EventContext telemetryContext);

        /// <summary>
        /// Maps the cache key and node entry on to an Environment Candidate. Both objects are neccessary to
        /// give enough sufficient context to the Environment candidate.
        /// </summary>
        /// <param name="cacheKey">The cachekey that contains contextual information for the Environment Candidate.</param>
        /// <param name="nodeEntry">The cache value that contains contextual infomation for the Environment Candidate.</param>
        /// <returns>A <see cref="EnvironmentCandidate"/> given context from the cache key and value.</returns>
        EnvironmentCandidate MapOnToEnvironmentCandidate(ProviderCacheKey cacheKey, string nodeEntry);
    }
}
