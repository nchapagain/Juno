namespace Juno.EnvironmentSelection
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Contracts;
    using Juno.Extensions.AspNetCore;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]

    public class CachingFunctionsTests
    {
        private ICachingFunctions functions;
        private Mock<IMemoryCache<IEnumerable<string>>> mockCache;
        private Fixture fixutre;

        [SetUp]
        public void SetupTests()
        {
            this.functions = new CachingFunctions(NullLogger.Instance);
            this.fixutre = new Fixture();
            this.mockCache = new Mock<IMemoryCache<IEnumerable<string>>>();
            this.fixutre.SetupEnvironmentSelectionMocks();
        }

        [Test]
        public void GenerateCacheKeyValidatesParameters()
        {
            Assert.Throws<ArgumentException>(() => this.functions.GenerateCacheKey(null, new List<KustoColumnAttribute>()));
            Assert.Throws<ArgumentException>(() => this.functions.GenerateCacheKey(this.GenerateDataRow(), null));
        }

        [Test]
        public void GenerateCacheKeyThrowsErrorIfColumnDoesNotExist()
        {
            KustoColumnAttribute attr = new KustoColumnAttribute()
            {
                Name = "not a column",
                ComposesCacheKey = true
            };

            Assert.Throws<ArgumentException>(() => this.functions.GenerateCacheKey(this.GenerateDataRow(), new List<KustoColumnAttribute>() { attr }));
        }

        [Test]
        public void GenerateCacheKeyGeneratesExpectedCacheKey()
        {
            string[] columns = { "foo", "bar" };
            List<KustoColumnAttribute> attributes = new List<KustoColumnAttribute>()
            {
                new KustoColumnAttribute() { ComposesCacheKey = true, Name = columns[0] },
                new KustoColumnAttribute() { ComposesCacheKey = true, Name = columns[1] }
            };

            DataRow row = this.GenerateDataRow(attributes);

            ProviderCacheKey result = this.functions.GenerateCacheKey(row, attributes);

            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(result.All(r => columns.Contains(r.Key)));
        }

        [Test]
        public void GenerateCacheKeyGeneratsExpectedCacheKeyWhenSomeColumnsDontComposeCacheKey()
        {
            string[] columns = { "foo", "bar" };
            List<KustoColumnAttribute> attributes = new List<KustoColumnAttribute>()
            {
                new KustoColumnAttribute() { ComposesCacheKey = true, Name = columns[0] },
                new KustoColumnAttribute() { ComposesCacheKey = true, Name = columns[1] },
                new KustoColumnAttribute() { ComposesCacheKey = false, Name = Guid.NewGuid().ToString() },
                new KustoColumnAttribute() { Name = Guid.NewGuid().ToString() }
            };

            DataRow row = this.GenerateDataRow(attributes);

            ProviderCacheKey result = this.functions.GenerateCacheKey(row, attributes);

            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(result.All(r => columns.Contains(r.Key)));
        }

        [Test]
        public void GenerateCacheKeysValidatesParameters()
        {
            Assert.Throws<ArgumentException>(() => this.functions.GenerateCacheKeys(null, EventContext.None));
            Assert.Throws<ArgumentException>(() => this.functions.GenerateCacheKeys(this.fixutre.Create<EnvironmentFilter>(), null));
        }

        [Test]
        public void GenerateCacheKeysReturnsNoKeysIfNoAttributeHasACacheLabel()
        {
            EnvironmentFilter filter = new EnvironmentFilter(typeof(NoCacheingFilter).FullName);
            IEnumerable<ProviderCacheKey> result = this.functions.GenerateCacheKeys(filter, EventContext.None);

            Assert.IsNotNull(result);
            Assert.IsEmpty(result);
        }

        [Test]
        public void GenerateCacheKeyRetunrsExpectedResultWithFilterWithCacheLabels()
        {
            EnvironmentFilter filter = new EnvironmentFilter(typeof(CacheingFilter).FullName, new Dictionary<string, IConvertible>()
            { { "Attribute1", "v1, v2" }, { "Attribute2", "v3, v4" } });

            List<ProviderCacheKey> expectedResults = new List<ProviderCacheKey>()
            {
                new ProviderCacheKey() { { "label1", "v1" }, { "label2", "v3" } },
                new ProviderCacheKey() { { "label1", "v2" }, { "label2", "v4" } },
                new ProviderCacheKey() { { "label1", "v1" }, { "label2", "v3" } },
                new ProviderCacheKey() { { "label1", "v2" }, { "label2", "v4" } },
            };

            List<ProviderCacheKey> actualResult = this.functions.GenerateCacheKeys(filter, EventContext.None).ToList();

            Assert.IsNotNull(actualResult);
            Assert.AreEqual(expectedResults.Count, actualResult.Count);
            Assert.IsTrue(expectedResults.All(r => actualResult.Contains(r)));
        }

        [Test]
        public void GenerateCacheKeyReturnsExpectedResultWithNonStringCacheLables()
        {
            EnvironmentFilter filter = new EnvironmentFilter(typeof(IntCacheingFilter).FullName, new Dictionary<string, IConvertible>()
            { { "Attribute1", "v1, v2" }, { "Attribute2", 10 } });

            List<ProviderCacheKey> expectedResults = new List<ProviderCacheKey>()
            {
                new ProviderCacheKey() { { "label1", "v1" }, { "label2", "10" } },
                new ProviderCacheKey() { { "label1", "v2" }, { "label2", "10" } },
            };

            List<ProviderCacheKey> actualResult = this.functions.GenerateCacheKeys(filter, EventContext.None).ToList();

            Assert.IsNotNull(actualResult);
            Assert.AreEqual(expectedResults.Count, actualResult.Count);
            Assert.IsTrue(expectedResults.All(r => actualResult.Contains(r)));
        }

        [Test]
        public void GenerateCacheKeyReturnsExpectedResultWithCacheLabelThatBelongsToASet()
        {
            EnvironmentFilter filter = new EnvironmentFilter(typeof(SetCacheingFilter).FullName, new Dictionary<string, IConvertible>()
            { { "Attribute1", "v1, v2, v3, v4" }, { "Attribute2", "v1, v2" } });

            List<ProviderCacheKey> expectedResults = new List<ProviderCacheKey>()
            {
                new ProviderCacheKey() { { "label1", "v3" } },
                new ProviderCacheKey() { { "label1", "v4" } },
            };

            List<ProviderCacheKey> actualResult = this.functions.GenerateCacheKeys(filter, EventContext.None).ToList();

            Assert.IsNotNull(actualResult);
            Assert.AreEqual(expectedResults.Count, actualResult.Count);
            Assert.IsTrue(expectedResults.All(r => actualResult.Contains(r)));
        }

        [Test]
        public void GenerateCacheValueValidatesParameters()
        {
            Assert.Throws<ArgumentException>(() => this.functions.GenerateCacheValue(null));
        }

        [Test]
        public void GenerateCacheValueGeneratesExpectedStringWhenMachinePoolIsNotSupplied()
        {
            string expectedNode = Guid.NewGuid().ToString();
            string expectedRack = Guid.NewGuid().ToString();
            string expectedCluster = Guid.NewGuid().ToString();
            string expectedRegion = Guid.NewGuid().ToString();
            EnvironmentCandidate candidate = new EnvironmentCandidate(node: expectedNode, rack: expectedRack, cluster: expectedCluster, region: expectedRegion);
            string expectedResult = $"{expectedNode}&{expectedRack}&{expectedCluster}&{expectedRegion}";
            string actualResult = this.functions.GenerateCacheValue(candidate);

            Assert.IsNotNull(actualResult);
            Assert.AreEqual(expectedResult, actualResult);
        }

        [Test]
        public void GenerateCacheValueGeneratesExpectedStringWhenMachinePoolIsSupplied()
        {
            string expectedNode = Guid.NewGuid().ToString();
            string expectedRack = Guid.NewGuid().ToString();
            string expectedCluster = Guid.NewGuid().ToString();
            string expectedMachinePool = Guid.NewGuid().ToString();
            string expectedRegion = Guid.NewGuid().ToString();
            EnvironmentCandidate candidate = new EnvironmentCandidate(node: expectedNode, rack: expectedRack, cluster: expectedCluster, machinePoolName: expectedMachinePool, region: expectedRegion);
            string expectedResult = $"{expectedNode}&{expectedRack}&{expectedCluster}&{expectedRegion}&{expectedMachinePool}";
            string actualResult = this.functions.GenerateCacheValue(candidate);

            Assert.IsNotNull(actualResult);
            Assert.AreEqual(expectedResult, actualResult);
        }

        [Test]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase(null)]
        public void MapOnToEnvironmentCandidateValidatesStringParameters(string invalidParameter)
        {
            Assert.Throws<ArgumentException>(() => this.functions.MapOnToEnvironmentCandidate(new ProviderCacheKey() { { "a", "b" } }, invalidParameter));
        }

        [Test]
        public void MapOnToEnvrionmentCandidateValidatesNonStringParameters()
        {
            Assert.Throws<ArgumentException>(() => this.functions.MapOnToEnvironmentCandidate(new ProviderCacheKey(), "I am a string"));
            Assert.Throws<ArgumentNullException>(() => this.functions.MapOnToEnvironmentCandidate(null, "I too am a string"));
        }

        [Test]
        public void MapOnToEnvironmentCanidateReturnsExpectedResultWithNoAdditionalInfoAndNoMachinePool()
        {
            string expectedRegion = Guid.NewGuid().ToString();
            string expectedNodeId = Guid.NewGuid().ToString();
            string expectedRackValue = Guid.NewGuid().ToString();
            string expectedCluster = Guid.NewGuid().ToString();

            string nodeEntry = $"{expectedNodeId}&{expectedRackValue}&{expectedCluster}&{expectedRegion}";
            ProviderCacheKey key = new ProviderCacheKey()
            { { ProviderConstants.Region, expectedRegion } };

            EnvironmentCandidate expectedResult = new EnvironmentCandidate(node: expectedNodeId, rack: expectedRackValue, cluster: expectedCluster, region: expectedRegion);

            EnvironmentCandidate actualResult = this.functions.MapOnToEnvironmentCandidate(key, nodeEntry);
            Assert.IsNotNull(actualResult);
            Assert.IsFalse(actualResult.NodeId == "*");
            Assert.IsFalse(actualResult.Rack == "*");
            Assert.IsFalse(actualResult.ClusterId == "*");
            Assert.IsFalse(actualResult.Region == "*");
            Assert.AreEqual(expectedResult, actualResult);
        }

        [Test]
        public void MapOnToEnvironmentCanidateReturnsExpectedResultWithNoAdditionalInfoAndMachinePool()
        {
            string expectedNodeId = Guid.NewGuid().ToString();
            string expectedRackValue = Guid.NewGuid().ToString();
            string expectedCluster = Guid.NewGuid().ToString();
            string expectedMachinePool = Guid.NewGuid().ToString();
            string expectedRegion = Guid.NewGuid().ToString();

            string nodeEntry = $"{expectedNodeId}&{expectedRackValue}&{expectedCluster}&{expectedRegion}&{expectedMachinePool}";
            ProviderCacheKey key = new ProviderCacheKey()
            { { ProviderConstants.Region, expectedRegion } };

            EnvironmentCandidate expectedResult = new EnvironmentCandidate(node: expectedNodeId, rack: expectedRackValue, cluster: expectedCluster, region: expectedRegion);

            EnvironmentCandidate actualResult = this.functions.MapOnToEnvironmentCandidate(key, nodeEntry);
            Assert.IsNotNull(actualResult);
            Assert.IsFalse(actualResult.NodeId == "*");
            Assert.IsFalse(actualResult.Rack == "*");
            Assert.IsFalse(actualResult.ClusterId == "*");
            Assert.IsFalse(actualResult.MachinePoolName == "*");
            Assert.IsFalse(actualResult.Region == "*");
            Assert.AreEqual(expectedResult, actualResult);
        }

        [Test]
        public void MapOnToEnvironmentCanidateReturnsExpectedResultWithAdditionalInfo()
        {
            string expectedNodeId = Guid.NewGuid().ToString();
            string expectedRackValue = Guid.NewGuid().ToString();
            string expectedCluster = Guid.NewGuid().ToString();
            string expectedMachinePool = Guid.NewGuid().ToString();
            string expectedRegion = Guid.NewGuid().ToString();
            string expectedAdditionalInfo = Guid.NewGuid().ToString();
            string expectedAdditionalInfoValue = Guid.NewGuid().ToString();

            string nodeEntry = $"{expectedNodeId}&{expectedRackValue}&{expectedCluster}&{expectedRegion}&{expectedMachinePool}";
            ProviderCacheKey key = new ProviderCacheKey()
            { { ProviderConstants.Region, expectedRegion }, { expectedAdditionalInfo, expectedAdditionalInfoValue } };

            EnvironmentCandidate expectedResult = new EnvironmentCandidate(node: expectedNodeId, rack: expectedRackValue, cluster: expectedCluster, additionalInfo: new Dictionary<string, string>()
            { { expectedAdditionalInfo, expectedAdditionalInfoValue } });

            EnvironmentCandidate actualResult = this.functions.MapOnToEnvironmentCandidate(key, nodeEntry);
            Assert.IsNotNull(actualResult);
            Assert.IsFalse(actualResult.NodeId == "*");
            Assert.IsFalse(actualResult.Rack == "*");
            Assert.IsFalse(actualResult.ClusterId == "*");
            Assert.IsFalse(actualResult.MachinePoolName == "*");
            Assert.AreEqual(expectedResult, actualResult);

            Assert.IsNotEmpty(actualResult.AdditionalInfo);
            IDictionary<string, string> actualDictionary = actualResult.AdditionalInfo;
            IDictionary<string, string> expectedDictionary = expectedResult.AdditionalInfo;
            Assert.AreEqual(expectedDictionary, actualDictionary);
        }

        [Test]
        public void PopulateCacheAsyncValidatesParameters()
        {
            IDictionary<ProviderCacheKey, IList<string>> mockCacheEntries = new Dictionary<ProviderCacheKey, IList<string>> { { new ProviderCacheKey(), new List<string>() } };
            Assert.ThrowsAsync<ArgumentNullException>(() => this.functions.PopulateCacheAsync(this.mockCache.Object, null, TimeSpan.FromSeconds(1), EventContext.None));
            Assert.ThrowsAsync<ArgumentException>(() => this.functions.PopulateCacheAsync(this.mockCache.Object, mockCacheEntries, TimeSpan.FromSeconds(1), null));
        }

        [Test]
        public void PopulateCacheAsyncPostsCorrectCacheEntiresToCache()
        {
            ProviderCacheKey expectedKey = new ProviderCacheKey() { { "key", "value" } };
            IList<string> expectedValue = new List<string>() { "I am a node" };
            this.mockCache.Setup(cache => cache.AddAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<Func<IEnumerable<string>>>(), It.IsAny<bool>()))
                .Callback<string, TimeSpan, Func<IEnumerable<string>>, bool>((key, timespan, function, sliding) =>
                {
                    Assert.AreEqual(expectedKey.ToString(), key);
                    Assert.AreEqual(expectedValue, function.Invoke());
                }).ReturnsAsync(true);

            this.functions.PopulateCacheAsync(this.mockCache.Object, new Dictionary<ProviderCacheKey, IList<string>>() { { expectedKey, expectedValue } }, TimeSpan.Zero, EventContext.None);

            this.mockCache.Verify(cache => cache.AddAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<Func<IEnumerable<string>>>(), It.IsAny<bool>()), Times.Once());
        }

        [Test]
        public void PopulateCacheAsyncThrowsErrorWhenAnyValuesFailToPopulate()
        {
            ProviderCacheKey expectedKey = new ProviderCacheKey() { { "key", "value" } };
            IList<string> expectedValue = new List<string>() { "I am a node" };
            this.mockCache.Setup(cache => cache.AddAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<Func<IEnumerable<string>>>(), It.IsAny<bool>()))
                .ReturnsAsync(false);

            Assert.ThrowsAsync<EnvironmentSelectionException>(() => this.functions.PopulateCacheAsync(this.mockCache.Object, new Dictionary<ProviderCacheKey, IList<string>>() { { expectedKey, expectedValue } }, TimeSpan.Zero, EventContext.None));

            this.mockCache.Verify(cache => cache.AddAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<Func<IEnumerable<string>>>(), It.IsAny<bool>()), Times.Once());
        }

        [Test]
        public void ReplaceFilterParametersValidatesParameters()
        {
            EnvironmentFilter filter = this.fixutre.Create<EnvironmentFilter>();
            IList<ProviderCacheKey> keys = new List<ProviderCacheKey>();
            Assert.Throws<ArgumentException>(() => this.functions.OverwriteFilterParameters(null, keys, EventContext.None));
            Assert.Throws<ArgumentException>(() => this.functions.OverwriteFilterParameters(filter, null, EventContext.None));
            Assert.Throws<ArgumentException>(() => this.functions.OverwriteFilterParameters(filter, keys, null));
        }

        [Test]
        public void ReplaceFilterParametersReproducesSameFilterIfNoCacheingIsConfigured()
        {
            EnvironmentFilter original = new EnvironmentFilter(typeof(NoCacheingFilter).FullName);
            IList<ProviderCacheKey> keys = new List<ProviderCacheKey>();

            EnvironmentFilter altered = new EnvironmentFilter(original);
            this.functions.OverwriteFilterParameters(altered, keys, EventContext.None);

            Assert.AreEqual(original, altered);
        }

        [Test]
        public void ReplaceFilterParametersReconstructsFilterWithCacheKeyValues()
        {
            EnvironmentFilter actualResult = new EnvironmentFilter(typeof(CacheingFilter).FullName, new Dictionary<string, IConvertible>()
            { { "Attribute1", "replace me" }, { "Attribute2", "replace me too!" } });

            IList<ProviderCacheKey> keys = new List<ProviderCacheKey>()
            { 
                new ProviderCacheKey() { { "label1", "value1" }, { "label2", "value2" } }, 
                new ProviderCacheKey() { { "label1", "value3" }, { "label2", "value4" } } 
            };

            EnvironmentFilter expectedResult = new EnvironmentFilter(typeof(CacheingFilter).FullName, new Dictionary<string, IConvertible>()
            { { "Attribute1", "value1,value3" }, { "Attribute2", "value2,value4" } });

            this.functions.OverwriteFilterParameters(actualResult, keys, EventContext.None);

            Assert.AreEqual(expectedResult, actualResult);
        }

        [Test]
        public void ReplaceFilterParametersReconstructsNonStringParameters()
        {
            EnvironmentFilter actualResult = new EnvironmentFilter(typeof(IntCacheingFilter).FullName, new Dictionary<string, IConvertible>()
            { { "Attribute1", "replace me" }, { "Attribute2", 2 } });

            IList<ProviderCacheKey> keys = new List<ProviderCacheKey>()
            {
                new ProviderCacheKey() { { "label1", "value1" }, { "label2", "2" } },
                new ProviderCacheKey() { { "label1", "value3" }, { "label2", "2" } }
            };

            EnvironmentFilter expectedResult = new EnvironmentFilter(typeof(IntCacheingFilter).FullName, new Dictionary<string, IConvertible>()
            { { "Attribute1", "value1,value3" }, { "Attribute2", 2 } });

            this.functions.OverwriteFilterParameters(actualResult, keys, EventContext.None);

            Assert.AreEqual(expectedResult, actualResult);
        }

        [Test]
        public void RetrieveCacheHitsAsyncValidatesParameters()
        {
            List<ProviderCacheKey> keys = new List<ProviderCacheKey>() { new ProviderCacheKey() };
            Assert.ThrowsAsync<ArgumentException>(() => this.functions.RetrieveCacheHitsAsync(null, ProviderConstants.NodeId, keys, EventContext.None));
            Assert.ThrowsAsync<ArgumentNullException>(() => this.functions.RetrieveCacheHitsAsync(this.mockCache.Object, ProviderConstants.NodeId, null, EventContext.None));
            Assert.ThrowsAsync<ArgumentException>(() => this.functions.RetrieveCacheHitsAsync(this.mockCache.Object, ProviderConstants.NodeId, keys, null));
        }

        [Test]
        public void RetrieveCacheHitsAsyncPostsCorrectCacheKeys()
        {
            string expectedNodeId = Guid.NewGuid().ToString();
            string expectedClusterId = Guid.NewGuid().ToString();
            string expectedRack = Guid.NewGuid().ToString();
            string expectedRegion = Guid.NewGuid().ToString();
            EnvironmentCandidate expectedCandidate = new EnvironmentCandidate(node: expectedNodeId, cluster: expectedClusterId, rack: expectedRack, region: expectedRegion);
            IDictionary<string, EnvironmentCandidate> expectedResult = new Dictionary<string, EnvironmentCandidate>() { { expectedNodeId, expectedCandidate } };
            ProviderCacheKey expectedKey = new ProviderCacheKey() { { ProviderConstants.Region, "region1" } };

            this.mockCache.Setup(cache => cache.GetAsync(It.IsAny<string>()))
                .Callback<string>((key) =>
                {
                    Assert.AreEqual(expectedKey.ToString(), key);
                })
                .ReturnsAsync(new List<string>() { $"{expectedNodeId}&{expectedClusterId}&{expectedRack}&{expectedRegion}" } as IEnumerable<string>);

            _ = this.functions.RetrieveCacheHitsAsync(this.mockCache.Object, ProviderConstants.NodeId, new List<ProviderCacheKey>() { expectedKey }, EventContext.None).GetAwaiter().GetResult();

            this.mockCache.Verify(cache => cache.GetAsync(It.IsAny<string>()), Times.Once());
        }

        [Test]
        public void RetrieveCacheHitsReturnsExpectedValues()
        {
            string expectedNodeId = Guid.NewGuid().ToString();
            string expectedClusterId = Guid.NewGuid().ToString();
            string expectedRack = Guid.NewGuid().ToString();
            string expectedRegion = Guid.NewGuid().ToString();
            EnvironmentCandidate expectedCandidate = new EnvironmentCandidate(node: expectedNodeId, cluster: expectedClusterId, rack: expectedRack);
            IDictionary<string, EnvironmentCandidate> expectedResult = new Dictionary<string, EnvironmentCandidate>() { { expectedNodeId, expectedCandidate } };
            ProviderCacheKey expectedKey = new ProviderCacheKey() { { ProviderConstants.Region, "region1" } };

            this.mockCache.Setup(cache => cache.GetAsync(It.IsAny<string>()))
                .ReturnsAsync(new List<string>() { $"{expectedNodeId}&{expectedClusterId}&{expectedRack}&{expectedRegion}" } as IEnumerable<string>);

            IDictionary<string, EnvironmentCandidate> actualResult = this.functions.RetrieveCacheHitsAsync(this.mockCache.Object, ProviderConstants.NodeId, new List<ProviderCacheKey>() { expectedKey }, EventContext.None).GetAwaiter().GetResult();
            Assert.AreEqual(expectedResult.Count, actualResult.Count);
            Assert.IsTrue(actualResult.ContainsKey(expectedNodeId));

            EnvironmentCandidate actualCandidate = expectedResult[expectedNodeId];
            Assert.AreEqual(expectedCandidate, actualCandidate);
        }

        private DataRow GenerateDataRow(IEnumerable<KustoColumnAttribute> attributes = null)
        {
            DataTable table = new DataTable();
            if (attributes == null)
            {
                table.Columns.Add(new DataColumn("foo"));
                table.Columns.Add(new DataColumn("bar"));

                DataRow row = table.NewRow();
                row["foo"] = "foo";
                row["bar"] = "bar";
                return row;
            }

            foreach (KustoColumnAttribute attr in attributes)
            {    
                table.Columns.Add(new DataColumn(attr.Name));       
            }

            DataRow customRow = table.NewRow();
            foreach (DataColumn column in table.Columns)
            {
                customRow[column.ColumnName] = Guid.NewGuid().ToString();
            }

            return customRow;
        }

        [SupportedFilter(Name = "Attribute", Type = typeof(string), Required = false)]
        private class NoCacheingFilter : EnvironmentSelectionProvider
        {
            public NoCacheingFilter(IServiceCollection services, TimeSpan ttl, IConfiguration configuration, ILogger logger)
                : base(services, ttl, configuration, logger)
            { 
            }

            protected override Task<IDictionary<string, EnvironmentCandidate>> ExecuteAsync(EnvironmentFilter filter, EventContext telemetryContext, CancellationToken token)
            {
                throw new NotImplementedException();
            }
        }

        [SupportedFilter(Name = "Attribute1", Type = typeof(string), Required = true, CacheLabel = "label1")]
        [SupportedFilter(Name = "Attribute2", Type = typeof(string), Required = true, CacheLabel = "label2")]
        private class CacheingFilter : EnvironmentSelectionProvider
        {
            public CacheingFilter(IServiceCollection services, TimeSpan ttl, IConfiguration configuration, ILogger logger)
                : base(services, ttl, configuration, logger)
            {
            }

            protected override Task<IDictionary<string, EnvironmentCandidate>> ExecuteAsync(EnvironmentFilter filter, EventContext telemetryContext, CancellationToken token)
            {
                throw new NotImplementedException();
            }
        }

        [SupportedFilter(Name = "Attribute1", Type = typeof(string), Required = true, CacheLabel = "label1")]
        [SupportedFilter(Name = "Attribute2", Type = typeof(int), Required = true, CacheLabel = "label2")]
        private class IntCacheingFilter : EnvironmentSelectionProvider
        {
            public IntCacheingFilter(IServiceCollection services, TimeSpan ttl, IConfiguration configuration, ILogger logger)
                : base(services, ttl, configuration, logger)
            {
            }

            protected override Task<IDictionary<string, EnvironmentCandidate>> ExecuteAsync(EnvironmentFilter filter, EventContext telemetryContext, CancellationToken token)
            {
                throw new NotImplementedException();
            }
        }

        [SupportedFilter(Name = "Attribute1", Type = typeof(string), Required = true, CacheLabel = "label1", SetName = "foo")]
        [SupportedFilter(Name = "Attribute2", Type = typeof(string), Required = true, SetName = "foo")]
        private class SetCacheingFilter : EnvironmentSelectionProvider
        {
            public SetCacheingFilter(IServiceCollection services, TimeSpan ttl, IConfiguration configuration, ILogger logger)
                : base(services, ttl, configuration, logger)
            {
            }

            protected override Task<IDictionary<string, EnvironmentCandidate>> ExecuteAsync(EnvironmentFilter filter, EventContext telemetryContext, CancellationToken token)
            {
                throw new NotImplementedException();
            }
        }
    }
}
