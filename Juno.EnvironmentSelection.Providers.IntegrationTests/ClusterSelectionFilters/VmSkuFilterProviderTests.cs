namespace Juno.EnvironmentSelection.ClusterSelectionFilters
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Extensions.AspNetCore;
    using Kusto.Cloud.Platform.Utils;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Integration/Live")]
    public class VmSkuFilterProviderTests
    {
        private EnvironmentFilterFixture mockFixture;
        private ClusterSelectionFilter provider;

        [SetUp]
        public void SetupTests()
        {
            TestDependencies.Initialize();
            this.mockFixture = new EnvironmentFilterFixture();
            this.mockFixture.SetUpMockFunctions();
            this.mockFixture.Services.AddSingleton(TestDependencies.KeyVaultClient);
            this.mockFixture.Services.AddSingleton<ICachingFunctions>(this.mockFixture.MockFunctions.Object);
            this.provider = new VmSkuFilterProvider(this.mockFixture.Services, this.mockFixture.Configuration, NullLogger.Instance);
        }

        [Test]
        public async Task VmSkuFilterProviderSearchesCorrectNodes()
        {
            using (IMemoryCache<IEnumerable<string>> cache = new MemoryCache<IEnumerable<string>>())
            {
                using (IMemoryCache<string> localCache = new MemoryCache<string>())
                {
                    this.mockFixture.Services.AddSingleton<IMemoryCache<IEnumerable<string>>>(cache);
                    this.mockFixture.Services.AddSingleton<IMemoryCache<string>>(localCache);

                    IEnumerable<string> includeRegion = this.mockFixture.SelectionSettings.Regions;
                    string excludeRegion = includeRegion.Shuffle().First();

                    IEnumerable<string> includeVmSku = this.mockFixture.SelectionSettings.VmSkus;
                    string excludeVmSku = includeVmSku.Shuffle().First();
                    EnvironmentFilter filter = new EnvironmentFilter(typeof(VmSkuFilterProvider).FullName, new Dictionary<string, IConvertible>()
                    {
                        { "includeRegion", string.Join(",", includeRegion) },
                        { "excludeRegion", excludeRegion },
                        { "includeVmSku", string.Join(",", includeVmSku) },
                        { "excludeVmSku", excludeVmSku }
                    });

                    IDictionary<string, EnvironmentCandidate> candidates = await this.provider.ExecuteAsync(filter, CancellationToken.None);

                    // Validate there were results returned from Kusto
                    Assert.IsNotNull(candidates);
                    Assert.IsNotEmpty(candidates);

                    // Validate only the correct regions were returned.
                    Assert.IsFalse(candidates.Values.Any(candidate => candidate.Region.Equals(excludeRegion)));
                    Assert.IsTrue(candidates.Values.All(candidate => includeRegion.Contains(candidate.Region)));

                    // Validate only the correct VmSkus were returned.
                    Assert.IsFalse(candidates.Values.Any(candidate => candidate.VmSku.Contains(excludeVmSku)));
                    Assert.IsTrue(candidates.Values.All(candidate => candidate.VmSku.All(vm => includeVmSku.Contains(vm))));

                    // Verify number of times cacheing functions were called. Although at the end our cacheing can be correct, need to verify that the provider is actually caching the result
                    // and then effectively only retrieving values from the cache the second time called.
                    this.mockFixture.VerifyExecuteAsync(candidates.Count);

                    IDictionary<string, EnvironmentCandidate> candidatesFromCache = await this.provider.ExecuteAsync(filter, CancellationToken.None);

                    // Verify that the exact same nodes were returned for the exact same filters.
                    Assert.AreEqual(candidates.Count, candidatesFromCache.Count);
                    Assert.IsTrue(candidatesFromCache.Keys.All(key => candidates.ContainsKey(key)));

                    // Verify that the values came from the cache and not from kusto.
                    this.mockFixture.MockFunctions.Verify(func => func.GenerateCacheKeys(It.IsAny<EnvironmentFilter>(), It.IsAny<EventContext>()), Times.Exactly(2));
                    this.mockFixture.MockFunctions.Verify(func => func.RetrieveCacheHitsAsync(It.IsAny<IMemoryCache<IEnumerable<string>>>(), It.IsAny<string>(), It.IsAny<IEnumerable<ProviderCacheKey>>(), It.IsAny<EventContext>()), Times.Never());
                    this.mockFixture.MockFunctions.Verify(func => func.OverwriteFilterParameters(It.IsAny<EnvironmentFilter>(), It.IsAny<IEnumerable<ProviderCacheKey>>(), It.IsAny<EventContext>()), Times.Once());
                    this.mockFixture.MockFunctions.Verify(func => func.GenerateCacheKey(It.IsAny<DataRow>(), It.IsAny<IEnumerable<KustoColumnAttribute>>()), Times.Exactly(candidates.Count));
                    this.mockFixture.MockFunctions.Verify(func => func.GenerateCacheValue(It.IsAny<EnvironmentCandidate>()), Times.Exactly(candidates.Count));
                    this.mockFixture.MockFunctions.Verify(func => func.PopulateCacheAsync(It.IsAny<IMemoryCache<IEnumerable<string>>>(), It.IsAny<IDictionary<ProviderCacheKey, IList<string>>>(), It.IsAny<TimeSpan>(), It.IsAny<EventContext>()), Times.Once());
                }
            }
        }
    }
}
