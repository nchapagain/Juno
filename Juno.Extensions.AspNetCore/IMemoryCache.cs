namespace Juno.Extensions.AspNetCore
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface for Cache that utilizes memory for storage
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IMemoryCache<T> : IDisposable
    {
        /// <summary>
        /// Gets the value associated with the key, otherwise adds a value associated to the key
        /// the value in this context is discoverable via the retrieval function.
        /// </summary>
        /// <param name="key">The key used to associate for the return value</param>
        /// <param name="retrievalFunc">If the value is not populated this is used to retrieve the value</param>
        /// <param name="expiryTime">TTL for the value associated with the key</param>
        /// <param name="sliding">Denotes whether or not to use a sliding window expiration policy.</param>
        /// <returns></returns>
        Task<T> GetOrAddAsync(string key, TimeSpan expiryTime, Func<Task<T>> retrievalFunc, bool sliding = false);

        /// <summary>
        /// Gets the value associated with the key, otherwise adds a value associated to the key
        /// the value in this context is discoverable via the retrieval function.
        /// </summary>
        /// <param name="key">The key used to associate for the return value</param>
        /// <param name="retrievalFunc">If the value is not populated this is used to retrieve the value</param>
        /// <param name="expiryTime">TTL for the value associated with the key</param>
        /// <param name="sliding">Denotes whether or not to use a sliding window expiration policy.</param>
        /// <returns></returns>
        Task<T> GetOrAddAsync(string key, TimeSpan expiryTime, Func<T> retrievalFunc, bool sliding = false);

        /// <summary>
        /// Retrieves the value from the cache corressponding to the given key.
        /// </summary>
        /// <param name="key">the key to map into the cache.</param>
        /// <returns>Object of given type that was retreived from the cache.</returns>
        Task<T> GetAsync(string key);

        /// <summary>
        /// Sets a new value in the cache.
        /// </summary>
        /// <param name="key">The key to use to map into the cache.</param>
        /// <param name="expiryTime">The time to live for the cache.</param>
        /// <param name="retrievalFunc">Function to retreive the cache value.</param>
        /// <param name="sliding">Denotes whether or not to use a sliding window expiration policy.</param>
        /// <returns>If the value was added successfully</returns>
        Task<bool> AddAsync(string key, TimeSpan expiryTime, Func<T> retrievalFunc, bool sliding = false);

        /// <summary>
        /// Removes a value in the cache.
        /// </summary>
        /// <param name="key">The key to use to map into the cache.</param>
        /// <returns></returns>
        Task RemoveAsync(string key);

        /// <summary>
        /// Returns true if the Cache contains a value for the associated key
        /// </summary>
        /// <param name="key">The key to use to discover the associated value in cache</param>
        /// <returns>If the cache contains a value for the given key</returns>
        bool Contains(string key);

        /// <summary>
        /// Changes the TTL for the value associated with the key
        /// if there is no value associated with the key <see cref="ArgumentException"/>
        /// is thrown
        /// </summary>
        /// <param name="key">The key to use to find the associated value</param>
        /// <param name="expiryTime">The new expiration time</param>
        /// <param name="sliding">Denotes whether or not to use a sliding window expiration policy.</param>
        /// <returns></returns>
        Task ChangeTimeToLiveAsync(string key, TimeSpan expiryTime, bool sliding = false);
    }
}
