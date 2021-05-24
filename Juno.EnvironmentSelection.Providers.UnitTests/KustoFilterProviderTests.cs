namespace Juno.EnvironmentSelection.NodeSelectionFilters
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.IO;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Extensions.AspNetCore;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Kusto;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Newtonsoft.Json;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class KustoFilterProviderTests
    {
        private KustoFilterProvider provider;
        private KustoFilterProvider scaleProvider;
        private IServiceCollection mockServices;
        private IConfiguration mockConfiguration;
        private ILogger mockLogger;
        private Mock<IKustoQueryIssuer> mockQueryIssuer;
        private string mockQuery;
        private string scaleQuery;
        private EnvironmentFilter mockFilter;
        private Mock<ICachingFunctions> mockFunctions;
        private Mock<IMemoryCache<IEnumerable<string>>> mockCache;

        [SetUp]
        public void Setup()
        {
            this.mockConfiguration = new ConfigurationBuilder().SetBasePath(Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                @"Configuration"))
                .AddJsonFile($"juno-dev01.environmentsettings.json")
                .Build();
            this.mockFunctions = new Mock<ICachingFunctions>();
            this.mockCache = new Mock<IMemoryCache<IEnumerable<string>>>();
            this.mockServices = new ServiceCollection();
            this.mockLogger = NullLogger.Instance;
            this.mockQueryIssuer = new Mock<IKustoQueryIssuer>();
            this.mockServices.AddSingleton<IKustoQueryIssuer>(this.mockQueryIssuer.Object);
            this.mockServices.AddSingleton<ICachingFunctions>(this.mockFunctions.Object);
            this.mockServices.AddSingleton<IMemoryCache<IEnumerable<string>>>(this.mockCache.Object);
            this.mockQuery = "A $mock$ query that has $booleanValue$ parameters to $replace$";
            this.scaleQuery = "$includeRegion$";

            this.provider = new TestKustoProvider(this.mockServices, this.mockConfiguration, this.mockLogger, this.mockQuery);
            this.scaleProvider = new TestKustoProvider(this.mockServices, this.mockConfiguration, this.mockLogger, this.scaleQuery);
            
            this.mockFilter = new EnvironmentFilter(type: typeof(TestKustoProvider).FullName, new Dictionary<string, IConvertible>()
            { 
                ["includeRegion"] = "region1, region2, region3"
            });

            this.SetupCacheingFunctions();
        }

        public void SetupCacheingFunctions()
        {
            // Default behavior should be exclusively all cache misses.
            this.mockFunctions.Setup(func => func.GenerateCacheKeys(It.IsAny<EnvironmentFilter>(), It.IsAny<EventContext>()))
                .Returns(new List<ProviderCacheKey>() { new ProviderCacheKey() });

            this.mockFunctions.Setup(func => func.OverwriteFilterParameters(It.IsAny<EnvironmentFilter>(), It.IsAny<IEnumerable<ProviderCacheKey>>(), It.IsAny<EventContext>()));

            this.mockFunctions.Setup(func => func.GenerateCacheKey(It.IsAny<DataRow>(), It.IsAny<IEnumerable<KustoColumnAttribute>>()))
                .Returns(new ProviderCacheKey());

            this.mockFunctions.Setup(func => func.GenerateCacheValue(It.IsAny<EnvironmentCandidate>()))
                .Returns(Guid.NewGuid().ToString());

            this.mockFunctions.Setup(func => func.PopulateCacheAsync(
                It.IsAny<IMemoryCache<IEnumerable<string>>>(),
                It.IsAny<IDictionary<ProviderCacheKey, IList<string>>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<EventContext>()))
                .Returns(Task.CompletedTask);

            this.mockCache.Setup(cache => cache.Contains(It.IsAny<string>()))
                .Returns(false);
        }

        [Test]
        public void ExecuteAsyncReturnsExpectedResult()
        {
            DataTable table = this.CreateValidDataTable();
            this.mockQueryIssuer.Setup(mgr => mgr.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult(table));

            IDictionary<string, EnvironmentCandidate> expectedResult = new Dictionary<string, EnvironmentCandidate>()
            {
                { "node", this.CreateValidEnvironmentCandidate() }
            };
            IDictionary<string, EnvironmentCandidate> actualResult = this.provider.ExecuteAsync(this.mockFilter, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsNotNull(actualResult);
            Assert.AreEqual(actualResult.Count, 1);
            Assert.AreEqual(expectedResult, actualResult);
        }

        [Test]
        public void ExecuteAsyncReturnsExpectedResultWhenDataTableFieldsAreOmitted()
        {
            DataTable table = new DataTable();
            table.Columns.Add(ProviderConstants.NodeId);
            table.Columns.Add(ProviderConstants.Rack);
            table.Columns.Add(ProviderConstants.Region);
            table.Columns.Add(ProviderConstants.ClusterId);
            table.Columns.Add("Column");
            DataRow row = table.NewRow();
            row[ProviderConstants.NodeId] = "node";
            row[ProviderConstants.Region] = "region";
            row[ProviderConstants.Rack] = "rack";
            row["Column"] = "additonalInfo";
            row[ProviderConstants.ClusterId] = "cluster";

            table.Rows.Add(row);

            this.mockQueryIssuer.Setup(issuer => issuer.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult(table));

            IDictionary<string, EnvironmentCandidate> expectedResult = new Dictionary<string, EnvironmentCandidate>()
            {
                { 
                    "node", new EnvironmentCandidate(null, "cluster", "region", null, "rack", "node", null, null, new Dictionary<string, string>()
                    {
                        { "Column", "additonalInfo" }
                    }) 
                }
            };
            IDictionary<string, EnvironmentCandidate> actualResult = this.provider.ExecuteAsync(this.mockFilter, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsNotNull(actualResult);
            Assert.AreEqual(actualResult.Count, 1);
            Assert.AreEqual(expectedResult, actualResult);
        }

        [Test]
        public void ReplaceQueryParametersReplacesExpectedParameters()
        {
            EnvironmentFilter filter = new EnvironmentFilter(type: typeof(TestQueryReplaceProvider).FullName, new Dictionary<string, IConvertible>()
            {
                { "mock", 10 },
                { "replace", "this, is, a, list" },
                { "booleanValue", true }
            });

            string expectedResult = "A 10 query that has True parameters to dynamic(['this', 'is', 'a', 'list'])";
            this.mockQueryIssuer.Setup(issuer => issuer.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string, string>((cluster, database, query) =>
                {
                    Assert.AreEqual(expectedResult, query);
                })
                .Returns(Task.FromResult(this.CreateValidDataTable()));

            TestQueryReplaceProvider provider = new TestQueryReplaceProvider(this.mockServices, this.mockConfiguration, this.mockLogger, this.mockQuery);
            provider.ExecuteAsync(filter, CancellationToken.None).GetAwaiter().GetResult();
        }

        [Test]
        public void KustoFilterProviderReturnsAllExpectedNodes()
        {
            this.mockFilter.Parameters["includeRegion"] = "region1, region2, region3, region4, region5, region6, region7";
            this.mockQueryIssuer.Setup(issuer => issuer.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(() => Task.FromResult(this.CreateValidDataTable(Guid.NewGuid().ToString())));

            IDictionary<string, EnvironmentCandidate> result = this.scaleProvider.ExecuteAsync(this.mockFilter, CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
        }

        [Test]
        public void KustoFilterProviderRetrieveCacheHitsValidatesParameters()
        {
            TestKustoProvider provider = new TestKustoProvider(this.mockServices, this.mockConfiguration, this.mockLogger, this.mockQuery);
            ProviderCacheKey key = new ProviderCacheKey();
            EnvironmentFilter filter = new EnvironmentFilter(typeof(TestKustoProvider).FullName);

            Assert.ThrowsAsync<ArgumentException>(() => provider.OnRetreiveCacheHits(null, filter, new List<ProviderCacheKey>() { key }, EventContext.None));
            Assert.ThrowsAsync<ArgumentException>(() => provider.OnRetreiveCacheHits(this.mockFunctions.Object, null, new List<ProviderCacheKey>() { key }, EventContext.None));
            Assert.ThrowsAsync<ArgumentException>(() => provider.OnRetreiveCacheHits(this.mockFunctions.Object, filter, null, EventContext.None));
            Assert.ThrowsAsync<ArgumentException>(() => provider.OnRetreiveCacheHits(this.mockFunctions.Object, filter, new List<ProviderCacheKey>() { key }, null));
        }

        [Test]
        public void KustoFilterProviderRetrieveCacheHitsPostsCorrectValues()
        {
            TestKustoProvider provider = new TestKustoProvider(this.mockServices, this.mockConfiguration, this.mockLogger, this.mockQuery);
            List<ProviderCacheKey> expectedKeys = new List<ProviderCacheKey>() { new ProviderCacheKey() };
            EnvironmentFilter filter = new EnvironmentFilter(typeof(TestKustoProvider).FullName);
            this.mockFunctions.Setup(func => func.RetrieveCacheHitsAsync(
                It.IsAny<IMemoryCache<IEnumerable<string>>>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<ProviderCacheKey>>(),
                It.IsAny<EventContext>()))
                .Callback<IMemoryCache<IEnumerable<string>>, string, IEnumerable<ProviderCacheKey>, EventContext>((cache, dictKey, keys, context) =>
                {
                    Assert.AreSame(expectedKeys, keys);
                }).ReturnsAsync(new Dictionary<string, EnvironmentCandidate>());

            _ = provider.OnRetreiveCacheHits(this.mockFunctions.Object, filter, expectedKeys, EventContext.None).GetAwaiter().GetResult();

            this.mockFunctions.Verify(
                func => func.RetrieveCacheHitsAsync(It.IsAny<IMemoryCache<IEnumerable<string>>>(), It.IsAny<string>(), It.IsAny<IEnumerable<ProviderCacheKey>>(), It.IsAny<EventContext>()),
                Times.Once());
        }

        [Test]
        public void KustoFilterProviderPopulateCacheValidatesParameters()
        {
            TestKustoProvider provider = new TestKustoProvider(this.mockServices, this.mockConfiguration, this.mockLogger, this.mockQuery);
            Dictionary<ProviderCacheKey, IList<string>> map = new Dictionary<ProviderCacheKey, IList<string>>() { { new ProviderCacheKey(), new List<string>() } };
            EnvironmentFilter filter = new EnvironmentFilter(typeof(TestKustoProvider).FullName);

            Assert.ThrowsAsync<ArgumentException>(() => provider.OnPopulateCacheAsync(null, map, EventContext.None));
            Assert.ThrowsAsync<ArgumentException>(() => provider.OnPopulateCacheAsync(this.mockFunctions.Object, null, EventContext.None));
            Assert.ThrowsAsync<ArgumentException>(() => provider.OnPopulateCacheAsync(this.mockFunctions.Object, map, null));
        }

        [Test]
        public void KustoFilterProviderPopulateCachePostsCorrectParameters()
        {
            TestKustoProvider provider = new TestKustoProvider(this.mockServices, this.mockConfiguration, this.mockLogger, this.mockQuery);
            Dictionary<ProviderCacheKey, IList<string>> map = new Dictionary<ProviderCacheKey, IList<string>>() { { new ProviderCacheKey(), new List<string>() } };
            EnvironmentFilter filter = new EnvironmentFilter(typeof(TestKustoProvider).FullName);
            this.mockFunctions.Setup(cache => cache.PopulateCacheAsync(
                It.IsAny<IMemoryCache<IEnumerable<string>>>(),
                It.IsAny<IDictionary<ProviderCacheKey, IList<string>>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<EventContext>()))
                .Callback<IMemoryCache<IEnumerable<string>>, IDictionary<ProviderCacheKey, IList<string>>, TimeSpan, EventContext>((cache, entries, ttl, context) =>
                {
                    Assert.AreSame(map, entries);
                });

            provider.OnPopulateCacheAsync(this.mockFunctions.Object, map, EventContext.None).GetAwaiter().GetResult();

            this.mockFunctions.Verify(
                cache => cache.PopulateCacheAsync(
                It.IsAny<IMemoryCache<IEnumerable<string>>>(),
                It.IsAny<IDictionary<ProviderCacheKey, IList<string>>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<EventContext>()), 
                Times.Once());
        }

        private EnvironmentCandidate CreateValidEnvironmentCandidate()
        {
            return new EnvironmentCandidate("subscription", "cluster", "region", "machinePool", "rack", "node", new List<string>() { "vmsku" }, "cpuId", new Dictionary<string, string>
            {
                { "Column", "additionalInfo" }
            });
        }

        private DataTable CreateValidDataTable(string nodeId = null)
        {
            DataTable table = new DataTable();
            table.Columns.Add(new DataColumn(ProviderConstants.NodeId));
            table.Columns.Add(new DataColumn(ProviderConstants.Subscription));
            table.Columns.Add(new DataColumn(ProviderConstants.ClusterId));
            table.Columns.Add(new DataColumn(ProviderConstants.MachinePoolName));
            table.Columns.Add(new DataColumn(ProviderConstants.Rack));
            table.Columns.Add(new DataColumn(ProviderConstants.Region));
            table.Columns.Add(new DataColumn(ProviderConstants.VmSku));
            table.Columns.Add(new DataColumn(ProviderConstants.CpuId));
            table.Columns.Add(new DataColumn("Column"));

            DataRow row = table.NewRow();
            row[ProviderConstants.NodeId] = nodeId ?? "node";
            row[ProviderConstants.Subscription] = "subscription";
            row[ProviderConstants.ClusterId] = "cluster";
            row[ProviderConstants.MachinePoolName] = "machinePool";
            row[ProviderConstants.Rack] = "rack";
            row[ProviderConstants.Region] = "region";
            row[ProviderConstants.VmSku] = JsonConvert.SerializeObject(new List<string>() { "vmsku" });
            row[ProviderConstants.CpuId] = "cpuId";
            row["Column"] = "additionInfo";

            table.Rows.Add(row);
            return table;
        }

        [KustoColumn(Name = "Column", AdditionalInfo = true)]
        private class TestKustoProvider : KustoFilterProvider
        {
            public TestKustoProvider(IServiceCollection services, IConfiguration configuration, ILogger logger, string query)
                : base(services, TimeSpan.FromMinutes(15), configuration, logger, query)
            {
                this.DictionaryKey = ProviderConstants.NodeId;
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

        [SupportedFilter(Name = "mock", Type = typeof(int), Required = true)]
        [SupportedFilter(Name = "replace", Type = typeof(string), Required = true)]
        [SupportedFilter(Name = "booleanValue", Type = typeof(bool), Required = true)]
        private class TestQueryReplaceProvider : KustoFilterProvider
        { 
            public TestQueryReplaceProvider(IServiceCollection services, IConfiguration configuration, ILogger logger, string query)
                : base(services, TimeSpan.FromMinutes(15), configuration, logger, query)
            {
                this.DictionaryKey = ProviderConstants.NodeId;
            }
        }
    }
}