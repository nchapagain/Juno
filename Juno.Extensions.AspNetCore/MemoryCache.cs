namespace Juno.Extensions.AspNetCore
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Extensions.Caching.Memory;

    /// <summary>
    /// Reader/Writer Cache, allows multiple readers but blcoks on any given writer
    /// </summary>
    /// <typeparam name="T">User defined type</typeparam>
    public class MemoryCache<T> : IMemoryCache<T>
    {
        private readonly MemoryCache cache;

        private readonly ConcurrentDictionary<string, SemaphoreSlim> locks;

        private bool disposedValue;

        /// <summary>
        /// Instantiates a <see cref="MemoryCache{T}"/>
        /// </summary>
        public MemoryCache()
        {
            this.cache = new MemoryCache(new MemoryCacheOptions());
            this.locks = new ConcurrentDictionary<string, SemaphoreSlim>();
            this.disposedValue = false;
        }

        /// <inheritdoc/>
        public bool Contains(string key)
        {
            key.ThrowIfNullOrWhiteSpace(nameof(key));

            return this.cache.TryGetValue(key, out _);
        }

        /// <inheritdoc/>
        public async Task<T> GetOrAddAsync(string key, TimeSpan expiryTime, Func<Task<T>> retrievalFunc, bool sliding = false)
        {
            key.ThrowIfNullOrWhiteSpace(nameof(key));
            retrievalFunc.ThrowIfNull(nameof(retrievalFunc));

            // 1. Try to read value
            T cacheEntry;
            SemaphoreSlim currentLock = this.locks.GetOrAdd(key, k => new SemaphoreSlim(1, 1));
            await currentLock.WaitAsync().ConfigureDefaults();

            try
            {
                if (!this.cache.TryGetValue(key, out cacheEntry))
                {
                    // Key not in cache, so get data.
                    MemoryCacheEntryOptions cacheEntryOptions = sliding 
                        ? new MemoryCacheEntryOptions().SetSlidingExpiration(expiryTime)
                        : new MemoryCacheEntryOptions().SetAbsoluteExpiration(expiryTime);
                    cacheEntry = await retrievalFunc().ConfigureAwait(false);
                    this.cache.Set(key, cacheEntry, cacheEntryOptions);
                }
            }
            finally
            {
                currentLock.Release();
            }

            return cacheEntry;
        }

        /// <inheritdoc/>
        public async Task<T> GetOrAddAsync(string key, TimeSpan expiryTime, Func<T> retrievalFunc, bool sliding = false)
        {
            key.ThrowIfNullOrWhiteSpace(nameof(key));
            retrievalFunc.ThrowIfNull(nameof(retrievalFunc));

            // Try to read value
            T cacheEntry;
            SemaphoreSlim currentLock = this.locks.GetOrAdd(key, k => new SemaphoreSlim(1, 1));
            await currentLock.WaitAsync().ConfigureDefaults();

            try
            {
                if (!this.cache.TryGetValue(key, out cacheEntry))
                {
                    MemoryCacheEntryOptions cacheEntryOptions = sliding
                        ? new MemoryCacheEntryOptions().SetSlidingExpiration(expiryTime)
                        : new MemoryCacheEntryOptions().SetAbsoluteExpiration(expiryTime);
                    cacheEntry = retrievalFunc.Invoke();
                    this.cache.Set(key, cacheEntry, cacheEntryOptions);
                }
            }
            finally
            {
                currentLock.Release();
            }

            return cacheEntry;
        }

        /// <inheritdoc />
        public async Task<T> GetAsync(string key)
        {
            key.ThrowIfNullOrWhiteSpace(nameof(key));

            // Try to read value
            T cacheEntry;
            bool lockPresent = this.locks.TryGetValue(key, out SemaphoreSlim currentLock);
            if (!lockPresent)
            {
                throw new KeyNotFoundException($"The key: {key} does not exist in the cache");
            }

            await currentLock.WaitAsync().ConfigureDefaults();

            try
            {
                if (this.cache.TryGetValue(key, out cacheEntry))
                {
                    return cacheEntry;
                }

                throw new ArgumentException($"The key: {key} TTL has expired");
            }
            finally
            {
                currentLock.Release();
            }
        }

        /// <inheritdoc/>
        public async Task<bool> AddAsync(string key, TimeSpan expiryTime, Func<T> retrievalFunc, bool sliding = false)
        {
            key.ThrowIfNullOrWhiteSpace(nameof(key));
            retrievalFunc.ThrowIfNull(nameof(retrievalFunc));

            // Try to read value
            T cacheEntry;
            if (this.cache.TryGetValue(key, out T value))
            {
                throw new ArgumentException("Cache key already exists");
            }

            SemaphoreSlim currentLock = this.locks.GetOrAdd(key, k => new SemaphoreSlim(1, 1));

            await currentLock.WaitAsync().ConfigureDefaults();

            try
            {
                if (!this.cache.TryGetValue(key, out cacheEntry))
                {
                    MemoryCacheEntryOptions cacheEntryOptions = sliding
                        ? new MemoryCacheEntryOptions().SetSlidingExpiration(expiryTime)
                        : new MemoryCacheEntryOptions().SetAbsoluteExpiration(expiryTime);

                    cacheEntry = retrievalFunc.Invoke();
                    this.cache.Set(key, cacheEntry, cacheEntryOptions);
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                currentLock.Release();
            }

            return true;
        }

        /// <inheritdoc/>
        public async Task RemoveAsync(string key)
        {
            key.ThrowIfNullOrWhiteSpace(nameof(key));

            bool lockPresent = this.locks.TryRemove(key, out SemaphoreSlim currentLock);
            if (!lockPresent)
            {
                throw new KeyNotFoundException($"The key: {key} does not exist in the cache");
            }

            await currentLock.WaitAsync().ConfigureDefaults();

            try
            {
                this.cache.Remove(key);
            }
            finally
            {
                currentLock.Release();
                currentLock.Dispose();
            }
        }

        /// <inheritdoc/>
        public async Task ChangeTimeToLiveAsync(string key, TimeSpan expiryTime, bool sliding = false)
        {
            key.ThrowIfNullOrWhiteSpace(nameof(key));

            if (!this.locks.TryGetValue(key, out SemaphoreSlim currentLock))
            {
                throw new KeyNotFoundException($"The key: {key} does not exist in the cache");
            }

            await currentLock.WaitAsync().ConfigureDefaults();

            try
            {
                if (!this.cache.TryGetValue(key, out T cacheEntry))
                {
                    throw new KeyNotFoundException($"The key: {key} does not have an entry in the current cache");
                }

                MemoryCacheEntryOptions cacheEntryOptions = sliding
                    ? new MemoryCacheEntryOptions().SetSlidingExpiration(expiryTime)
                    : new MemoryCacheEntryOptions().SetAbsoluteExpiration(expiryTime);
                this.cache.Set(key, cacheEntry, cacheEntryOptions);
            }
            finally
            {
                currentLock.Release();
            }
        }

        /// <summary>
        /// Tears down Disposable resources
        /// </summary>
        public void Dispose()
        {
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes of resources used by the cache
        /// </summary>
        /// <param name="disposing">If the cache has been disposed</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this.cache.Dispose();
                }

                this.disposedValue = true;
            }
        }
    }
}