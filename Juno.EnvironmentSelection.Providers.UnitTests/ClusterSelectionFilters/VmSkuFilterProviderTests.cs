namespace Juno.EnvironmentSelection.ClusterSelectionFilters
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Contracts;
    using Juno.Extensions.AspNetCore;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class VmSkuFilterProviderTests
    {
        private TestVmSkuFilter provider;
        private Fixture fixture;
        private Mock<ICachingFunctions> mockFunctions;
        private Mock<IMemoryCache<IEnumerable<string>>> mockProviderCache;

        [SetUp]
        public void SetupTests()
        {
            ServiceCollection mockServices = new ServiceCollection();
            IConfiguration mockConfiguration = new ConfigurationBuilder().SetBasePath(Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                @"Configuration"))
                .AddJsonFile($"juno-dev01.environmentsettings.json")
                .Build();
            this.fixture = new Fixture();
            this.fixture.SetupEnvironmentSelectionMocks();
            this.mockFunctions = new Mock<ICachingFunctions>();

            this.mockProviderCache = new Mock<IMemoryCache<IEnumerable<string>>>();
            mockServices.AddSingleton<IMemoryCache<IEnumerable<string>>>(this.mockProviderCache.Object);
            
            this.provider = new TestVmSkuFilter(mockServices, mockConfiguration, NullLogger.Instance);
            this.provider.ConfigureServicesAsync();
            this.mockFunctions.Setup(function => function.PopulateCacheAsync(
                It.IsAny<IMemoryCache<IEnumerable<string>>>(), 
                It.IsAny<IDictionary<ProviderCacheKey, IList<string>>>(), 
                It.IsAny<TimeSpan>(), 
                It.IsAny<EventContext>()))
                .Returns(Task.CompletedTask);

            this.mockFunctions.Setup(function => function.MapOnToEnvironmentCandidate(It.IsAny<ProviderCacheKey>(), It.IsAny<string>()))
                .Returns(this.fixture.Create<EnvironmentCandidate>());

            this.mockProviderCache.Setup(cache => cache.GetAsync(It.IsAny<string>()))
                .ReturnsAsync(new List<string>() { Guid.NewGuid().ToString() });
        }

        [Test]
        public void PopulateCacheAsyncValidatesParameters()
        {
            Dictionary<ProviderCacheKey, IList<string>> map = new Dictionary<ProviderCacheKey, IList<string>>() { { new ProviderCacheKey(), new List<string>() } };
            EnvironmentFilter filter = new EnvironmentFilter(typeof(TestVmSkuFilter).FullName);

            Assert.ThrowsAsync<ArgumentException>(() => this.provider.OnPopulateCacheAsync(null, map, EventContext.None));
            Assert.ThrowsAsync<ArgumentException>(() => this.provider.OnPopulateCacheAsync(this.mockFunctions.Object, null, EventContext.None));
            Assert.ThrowsAsync<ArgumentException>(() => this.provider.OnPopulateCacheAsync(this.mockFunctions.Object, map, null));
        }

        [Test]
        public void RetreiveCacheHitsAsyncValidatesParameters()
        {
            ProviderCacheKey key = new ProviderCacheKey();
            EnvironmentFilter filter = new EnvironmentFilter(typeof(TestVmSkuFilter).FullName);

            Assert.ThrowsAsync<ArgumentException>(() => this.provider.OnRetreiveCacheHits(null, filter, new List<ProviderCacheKey>() { key }, EventContext.None));
            Assert.ThrowsAsync<ArgumentException>(() => this.provider.OnRetreiveCacheHits(this.mockFunctions.Object, null, new List<ProviderCacheKey>() { key }, EventContext.None));
            Assert.ThrowsAsync<ArgumentException>(() => this.provider.OnRetreiveCacheHits(this.mockFunctions.Object, filter, null, EventContext.None));
            Assert.ThrowsAsync<ArgumentException>(() => this.provider.OnRetreiveCacheHits(this.mockFunctions.Object, filter, new List<ProviderCacheKey>() { key }, null));
        }

        [Test]
        public void PopulateCacheAsyncPostsExpectedCacheKeys()
        {
            IList<string> expectedValues = new List<string>() { Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString() };

            IDictionary<ProviderCacheKey, IList<string>> originalEntry = new Dictionary<ProviderCacheKey, IList<string>>()
            {
                { new ProviderCacheKey() { { ProviderConstants.VmSku, "vm1,vm2,vm3, vm4" } },  expectedValues }
            };

            IDictionary<ProviderCacheKey, IList<string>> expectedKeys = new Dictionary<ProviderCacheKey, IList<string>>()
            {
                { new ProviderCacheKey() { { ProviderConstants.VmSku, "vm1" } }, expectedValues },
                { new ProviderCacheKey() { { ProviderConstants.VmSku, "vm2" } }, expectedValues },
                { new ProviderCacheKey() { { ProviderConstants.VmSku, "vm3" } }, expectedValues },
                { new ProviderCacheKey() { { ProviderConstants.VmSku, "vm4" } }, expectedValues },
            };

            this.mockFunctions.Setup(function => function.PopulateCacheAsync(
                It.IsAny<IMemoryCache<IEnumerable<string>>>(),
                It.IsAny<IDictionary<ProviderCacheKey, IList<string>>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<EventContext>()))
                .Callback<IMemoryCache<IEnumerable<string>>, IDictionary<ProviderCacheKey, IList<string>>, TimeSpan, EventContext>((cache, keys, ttl, context) =>
                {
                    Assert.IsTrue(keys.Keys.All(key => expectedKeys.Keys.Contains(key)));
                    Assert.IsTrue(keys.Values.All(value => value.All(v => expectedValues.Contains(v))));
                })
                .Returns(Task.CompletedTask);

            this.provider.OnPopulateCacheAsync(this.mockFunctions.Object, originalEntry, EventContext.None).GetAwaiter().GetResult();

            this.mockFunctions.Verify(
                function => function.PopulateCacheAsync(
                It.IsAny<IMemoryCache<IEnumerable<string>>>(),
                It.IsAny<IDictionary<ProviderCacheKey, IList<string>>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<EventContext>()),
                Times.Once());
        }

        [Test]
        public void RetrieveCacheHitsAsyncRequestsCorrectNodeReferencesFromProviderCache()
        {
            ProviderCacheKey expectedKey = new ProviderCacheKey() { { ProviderConstants.VmSku, "vm1" } };
            IEnumerable<string> result = new List<string>() { "foo", "bar", "baz" };
            this.mockProviderCache.Setup(cache => cache.GetAsync(It.IsAny<string>()))
                .Callback<string>((key) =>
                {
                    Assert.IsTrue(key.Equals(expectedKey.ToString()));
                })
                .ReturnsAsync(result);

            _ = this.provider.OnRetreiveCacheHits(this.mockFunctions.Object, this.fixture.Create<EnvironmentFilter>(), new List<ProviderCacheKey>() { expectedKey }, EventContext.None).GetAwaiter().GetResult();

            this.mockProviderCache.Verify(cache => cache.GetAsync(It.IsAny<string>()), Times.Once());
        }

        [Test]
        public void RetrieveCacheHitAsyncReturnsExpectedResult()
        {
            ProviderCacheKey expectedKey = new ProviderCacheKey() { { ProviderConstants.VmSku, "vm1" } };
            string expectedNodeId = Guid.NewGuid().ToString();
            EnvironmentCandidate expectedCandidate = new EnvironmentCandidate(cluster: expectedNodeId, vmSku: new List<string>() { Guid.NewGuid().ToString() });

            this.mockFunctions.Setup(functions => functions.MapOnToEnvironmentCandidate(It.IsAny<ProviderCacheKey>(), It.IsAny<string>()))
                .Returns(expectedCandidate);

            IDictionary<string, EnvironmentCandidate> actualResult = this.provider.OnRetreiveCacheHits(this.mockFunctions.Object, this.fixture.Create<EnvironmentFilter>(), new List<ProviderCacheKey>() { expectedKey }, EventContext.None).GetAwaiter().GetResult();
            Assert.IsNotNull(actualResult);
            Assert.AreEqual(1, actualResult.Count);
            Assert.IsTrue(actualResult.ContainsKey(expectedNodeId));
            Assert.AreEqual(expectedCandidate, actualResult[expectedNodeId]);
        }

        [Test]
        public void RetrieveCacheHitAsyncReturnsExpectedResultWhenMultipleKeysReferenceSameNode()
        {
            List<ProviderCacheKey> expectedKeys = new List<ProviderCacheKey>() { new ProviderCacheKey(), new ProviderCacheKey() };
            string expectedNodeId = Guid.NewGuid().ToString();
            List<string> expectedVmSkus = new List<string>() { Guid.NewGuid().ToString(), Guid.NewGuid().ToString() };

            EnvironmentCandidate expectedCandidate = new EnvironmentCandidate(cluster: expectedNodeId, vmSku: new List<string>() { expectedVmSkus[0] });
            EnvironmentCandidate expectedCandidate2 = new EnvironmentCandidate(cluster: expectedNodeId, vmSku: new List<string>() { expectedVmSkus[1] });

            this.mockFunctions.SetupSequence(func => func.MapOnToEnvironmentCandidate(It.IsAny<ProviderCacheKey>(), It.IsAny<string>()))
                .Returns(expectedCandidate)
                .Returns(expectedCandidate2);

            IDictionary<string, EnvironmentCandidate> actualResult = this.provider.OnRetreiveCacheHits(this.mockFunctions.Object, this.fixture.Create<EnvironmentFilter>(), expectedKeys, EventContext.None).GetAwaiter().GetResult();
            
            Assert.IsNotNull(actualResult);
            Assert.AreEqual(1, actualResult.Count);
            Assert.IsTrue(actualResult.ContainsKey(expectedNodeId));
            Assert.AreEqual(2, actualResult[expectedNodeId].VmSku.Count);
            Assert.IsTrue(actualResult[expectedNodeId].VmSku.All(sku => expectedVmSkus.Contains(sku)));
        }

        private class TestVmSkuFilter : VmSkuFilterProvider
        {
            public TestVmSkuFilter(IServiceCollection collection, IConfiguration configuration, ILogger logger)
                : base(collection, configuration, logger)
            {
                this.DictionaryKey = ProviderConstants.ClusterId;
            }

            public Func<ICachingFunctions, IDictionary<ProviderCacheKey, IList<string>>, EventContext, Task> OnPopulateCacheAsync
            {
                get { return this.PopulateCacheAsync; }
            }

            public Func<ICachingFunctions, EnvironmentFilter, IEnumerable<ProviderCacheKey>, EventContext, Task<IDictionary<string, EnvironmentCandidate>>> OnRetreiveCacheHits
            {
                get { return this.RetrieveCacheHitsAsync; }
            }
        }
    }
}
