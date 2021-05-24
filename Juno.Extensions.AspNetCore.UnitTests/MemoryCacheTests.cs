namespace Juno.Extensions.AspNetCore
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class MemoryCacheTests
    {
        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void GetOrAddAsyncValidatesStringParamaters(string invalidParameter)
        {
            using (IMemoryCache<int> component = new MemoryCache<int>())
            {
                Assert.ThrowsAsync<ArgumentException>(async () => await component.GetOrAddAsync(invalidParameter, TimeSpan.Zero, () => Task.FromResult(1)));
            }
        }

        [Test]
        public void GetOrAddAsyncValidatesFunctionParamaters()
        {
            using (IMemoryCache<int> component = new MemoryCache<int>())
            {
                Assert.ThrowsAsync<ArgumentException>(async () => await component.GetOrAddAsync("Key", TimeSpan.Zero, (Func<Task<int>>)null));
            }
        }

        [Test]
        public async Task GetOrAddAsyncAddsNewValueToCache()
        {
            using (IMemoryCache<int> component = new MemoryCache<int>())
            {
                int expectedValue = 5;
                string key = "five";
                int actualValue = await component.GetOrAddAsync(key, TimeSpan.FromSeconds(2), () => Task.FromResult(expectedValue))
                    .ConfigureAwait(false);
                Assert.AreEqual(expectedValue, actualValue);
            }
        }

        [Test]
        public async Task GetOrAddRetrievesAlreadyAddedValueFromCache()
        {
            using (IMemoryCache<int> component = new MemoryCache<int>())
            {
                int expectedValue = 5;
                string key = "five";

                int actualValue = await component.GetOrAddAsync(key, TimeSpan.FromSeconds(2), () => Task.FromResult(expectedValue)).ConfigureAwait(false);
                Assert.AreEqual(expectedValue, actualValue);

                actualValue = await component.GetOrAddAsync(key, TimeSpan.FromSeconds(1), () => Task.FromResult(expectedValue + 1)).ConfigureAwait(false);
                Assert.AreEqual(expectedValue, actualValue);
            }
        }

        [Test]
        public async Task GetOrAddEvictsKeyAfterTimeOut()
        {
            using (IMemoryCache<int> component = new MemoryCache<int>())
            {
                int expectedValue = 5;
                string key = "five";
                TimeSpan ttl = TimeSpan.FromTicks(1);

                int actualValue = await component.GetOrAddAsync(key, ttl, () => Task.FromResult(expectedValue)).ConfigureAwait(false);

                Assert.AreEqual(expectedValue, actualValue);

                expectedValue = 6;
                actualValue = await component.GetOrAddAsync(key, ttl, () => Task.FromResult(expectedValue)).ConfigureAwait(false);
                Assert.AreEqual(expectedValue, actualValue);
            }        
        }

        [Test]
        public async Task GetOrAddAllowsMultipleReaders()
        {
            using (IMemoryCache<int> component = new MemoryCache<int>())
            {
                // Initial write
                string key = "five";
                int value = 5;
                TimeSpan ttl = TimeSpan.FromSeconds(10);
                int actualValue = await component.GetOrAddAsync(key, ttl, () => Task.FromResult(value)).ConfigureAwait(false);

                Func<Task<int>> readerFunc = async () =>
                {
                    return await component.GetOrAddAsync(key, ttl, () => Task.FromResult(value)).ConfigureAwait(false);
                };

                IList<Task<int>> tasks = new List<Task<int>>();

                const int readers = 10;
                for (int i = 0; i < readers; i++)
                {
                    tasks.Add(readerFunc.Invoke());
                }

                int[] task = await Task.WhenAll(tasks.ToArray());
                Assert.IsTrue(task.All(val => val == value));
            }
        }

        [Test]
        public async Task GetOrAddOnlyWritesOnceWithMultipleReadersWithinExpiry()
        {
            const int readers = 10;
            const int value = 5;
            using (IMemoryCache<int> component = new MemoryCache<int>())
            {
                // Reload Function
                Mock<Func<Task<int>>> reloadFunc = new Mock<Func<Task<int>>>();
                reloadFunc.Setup(s => s()).Returns(Task.FromResult(value));

                Func<Task<int>> readerFunc = async () => await component.GetOrAddAsync("key", TimeSpan.FromDays(1), reloadFunc.Object);

                IList<Task<int>> readerThreads = new List<Task<int>>();
                for (int i = 0; i < readers; i++)
                {
                    readerThreads.Add(readerFunc.Invoke());
                }

                int[] values = await Task.WhenAll(readerThreads.ToArray()).ConfigureAwait(false);
                Assert.IsTrue(values.All(val => val == value));
                reloadFunc.Verify(s => s(), Times.Once());
            }
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void GetOrAddAsyncNonAsyncRetrievalValidatesStringParamaters(string invalidParameter)
        {
            using (IMemoryCache<int> component = new MemoryCache<int>())
            {
                Assert.ThrowsAsync<ArgumentException>(async () => await component.GetOrAddAsync(invalidParameter, TimeSpan.Zero, () => 1));
            }
        }

        [Test]
        public void GetOrAddAsyncNonAsycRetrievalValidatesFunctionParamaters()
        {
            using (IMemoryCache<int> component = new MemoryCache<int>())
            {
                Assert.ThrowsAsync<ArgumentException>(async () => await component.GetOrAddAsync("Key", TimeSpan.Zero, (Func<int>)null));
            }
        }

        [Test]
        public async Task GetOrAddAsyncNonAsyncRetrievalAddsNewValueToCache()
        {
            using (IMemoryCache<int> component = new MemoryCache<int>())
            {
                int expectedValue = 5;
                string key = "five";
                int actualValue = await component.GetOrAddAsync(key, TimeSpan.FromSeconds(2), () => expectedValue)
                    .ConfigureAwait(false);
                Assert.AreEqual(expectedValue, actualValue);
            }            
        }

        [Test]
        public async Task GetOrAddNonAsyncRetrievalRetrievesAlreadyAddedValueFromCache()
        {
            using (IMemoryCache<int> component = new MemoryCache<int>())
            {
                int expectedValue = 5;
                string key = "five";

                int actualValue = await component.GetOrAddAsync(key, TimeSpan.FromSeconds(2), () => expectedValue).ConfigureAwait(false);
                Assert.AreEqual(expectedValue, actualValue);

                actualValue = await component.GetOrAddAsync(key, TimeSpan.FromSeconds(1), () => expectedValue + 1).ConfigureAwait(false);
                Assert.AreEqual(expectedValue, actualValue);
            }
        }

        [Test]
        public async Task GetOrAddNonAsyncRetrievalEvictsKeyAfterTimeOut()
        {
            using (IMemoryCache<int> component = new MemoryCache<int>())
            {
                int expectedValue = 5;
                string key = "five";
                TimeSpan ttl = TimeSpan.FromTicks(1);

                int actualValue = await component.GetOrAddAsync(key, ttl, () => expectedValue).ConfigureAwait(false);

                Assert.AreEqual(expectedValue, actualValue);

                expectedValue = 6;
                actualValue = await component.GetOrAddAsync(key, ttl, () => expectedValue).ConfigureAwait(false);
                Assert.AreEqual(expectedValue, actualValue);
            }
        }

        [Test]
        public async Task GetOrAddNonAsyncRetrievalAllowsMultipleReaders()
        {
            using (IMemoryCache<int> component = new MemoryCache<int>())
            {
                // Initial write
                string key = "five";
                int value = 5;
                TimeSpan ttl = TimeSpan.FromSeconds(10);
                int actualValue = await component.GetOrAddAsync(key, ttl, () => value).ConfigureAwait(false);

                Func<Task<int>> readerFunc = async () =>
                {
                    return await component.GetOrAddAsync(key, ttl, () => value).ConfigureAwait(false);
                };

                IList<Task<int>> tasks = new List<Task<int>>();

                const int readers = 10;
                for (int i = 0; i < readers; i++)
                {
                    tasks.Add(readerFunc.Invoke());
                }

                int[] task = await Task.WhenAll(tasks.ToArray());
                Assert.IsTrue(task.All(val => val == value));
            }
        }

        [Test]
        public async Task GetOrAddNonAsyncRetrievalOnlyWritesOnceWithMultipleReadersWithinExpiry()
        {
            const int readers = 10;
            const int value = 5;
            using (IMemoryCache<int> component = new MemoryCache<int>())
            {
                // Reload Function
                Mock<Func<int>> reloadFunc = new Mock<Func<int>>();
                reloadFunc.Setup(s => s()).Returns(value);

                Func<Task<int>> readerFunc = async () => await component.GetOrAddAsync("key", TimeSpan.FromDays(1), reloadFunc.Object);

                IList<Task<int>> readerThreads = new List<Task<int>>();
                for (int i = 0; i < readers; i++)
                {
                    readerThreads.Add(readerFunc.Invoke());
                }

                int[] values = await Task.WhenAll(readerThreads.ToArray()).ConfigureAwait(false);
                Assert.IsTrue(values.All(val => val == value));

                reloadFunc.Verify(s => s(), Times.Once());
            }
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void GetAsyncValidatesParameters(string invalidParameter)
        {
            using (IMemoryCache<int> cache = new MemoryCache<int>())
            {
                Assert.ThrowsAsync<ArgumentException>(() => cache.GetAsync(invalidParameter));
            }
        }

        [Test]
        public void GetAsyncThrowsExceptionWhenThereIsNoLockEntry()
        {
            using (IMemoryCache<int> cache = new MemoryCache<int>())
            {
                Assert.ThrowsAsync<KeyNotFoundException>(() => cache.GetAsync("not there"));
            }
        }

        [Test]
        public async Task GetAsyncReturnsExpectedValue()
        {
            using (IMemoryCache<int> cache = new MemoryCache<int>())
            {
                string key = Guid.NewGuid().ToString();
                const int expectedValue = 10;
                bool success = await cache.AddAsync(key, TimeSpan.FromMinutes(1), () => expectedValue);
                Assert.IsTrue(success);

                int actualValue = await cache.GetAsync(key);
                Assert.AreEqual(expectedValue, actualValue);
            }
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void AddAsyncValidatesStringParameters(string invalidParameter)
        {
            using (IMemoryCache<int> cache = new MemoryCache<int>())
            {
                Assert.ThrowsAsync<ArgumentException>(() => cache.AddAsync(invalidParameter, TimeSpan.FromMilliseconds(100), () => 1));
            }
        }

        [Test]
        public void AddAsyncValidatesParameters()
        {
            using (IMemoryCache<int> cache = new MemoryCache<int>())
            {
                Assert.ThrowsAsync<ArgumentException>(() => cache.AddAsync("stringy string", TimeSpan.FromMilliseconds(100), null));
            }
        }

        [Test]
        public async Task AddAsyncThrowsErrorIfKeyAlreadyExists()
        {
            using (IMemoryCache<int> cache = new MemoryCache<int>())
            {
                string key = "string";
                int value = 5;
                bool success = await cache.AddAsync(key, TimeSpan.FromMinutes(1), () => value);
                Assert.IsTrue(success);

                Assert.ThrowsAsync<ArgumentException>(() => cache.AddAsync(key, TimeSpan.FromMinutes(1), () => value));
            }
        }

        [Test]
        public async Task AddAsyncReturnsTrueOnCacheAddition()
        {
            using (IMemoryCache<int> cache = new MemoryCache<int>())
            {
                string key = "string";
                int value = 5;
                bool success = await cache.AddAsync(key, TimeSpan.FromMinutes(1), () => value);
                Assert.IsTrue(success);
            }
        }

        [Test]
        public async Task ContainsReturnsExpectedResultWhenEntryIsPresent()
        {
            using (IMemoryCache<int> cache = new MemoryCache<int>())
            {
                const string key = "key";
                const int value = 5;
                _ = await cache.GetOrAddAsync(key, TimeSpan.FromMinutes(5), () => value);

                bool contains = cache.Contains(key);

                Assert.IsTrue(contains);
            }
        }

        [Test]
        public async Task ContainsReturnsExpectedResultWhenValueIsEvicted()
        {
            using (IMemoryCache<int> cache = new MemoryCache<int>())
            {
                const string key = "key";
                const int value = 5;
                _ = await cache.GetOrAddAsync(key, TimeSpan.FromTicks(1), () => value);

                await Task.Delay(TimeSpan.FromTicks(2));

                bool contains = cache.Contains(key);

                Assert.IsFalse(contains);
            }
        }

        [Test]
        public void ContainsReturnsFalseWhenTheValueWasNeverAdded()
        {
            using (IMemoryCache<int> cache = new MemoryCache<int>())
            {
                const string key = "key";

                bool contains = cache.Contains(key);

                Assert.IsFalse(contains);
            }
        }

        [Test]
        public void ChangeTimeToLiveThrowsExceptionWhenKeyDoesNotExist()
        {
            using (IMemoryCache<int> cache = new MemoryCache<int>())
            {
                Assert.ThrowsAsync<KeyNotFoundException>(() => cache.ChangeTimeToLiveAsync("key", TimeSpan.FromSeconds(2)));
            }
        }

        [Test]
        public async Task ChangeTimeToLiveThrowsExceptionWhenValueIsEvicted()
        {
            using (IMemoryCache<int> cache = new MemoryCache<int>())
            {
                const string key = "key";
                const int value = 5;
                await cache.GetOrAddAsync(key, TimeSpan.FromTicks(1), () => value);

                await Task.Delay(TimeSpan.FromTicks(2));

                Assert.ThrowsAsync<KeyNotFoundException>(() => cache.ChangeTimeToLiveAsync(key, TimeSpan.FromSeconds(2)));
            }
        }

        [Test]
        public async Task ChangeTimeToLiveDoesNotThrowExceptionWhenCacheEntryIsValid()
        {
            using (IMemoryCache<int> cache = new MemoryCache<int>())
            {
                const string key = "key";
                const int value = 5;

                await cache.GetOrAddAsync(key, TimeSpan.FromSeconds(30), () => value);
                try
                {
                    await cache.ChangeTimeToLiveAsync(key, TimeSpan.FromSeconds(20));
                    Assert.Pass();
                }
                catch (ArgumentException)
                {
                    Assert.Fail();
                }
            }
        }

        [Test]
        public void RemoveAsyncThrowsExceptionWhenKeyDoesNotExist()
        {
            using (IMemoryCache<int> cache = new MemoryCache<int>())
            {
                Assert.ThrowsAsync<KeyNotFoundException>(() => cache.RemoveAsync("key"));
            }
        }
    }
}
