namespace Juno.EnvironmentSelection.ClusterSelectionFilters
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Extensions.AspNetCore;
    using Kusto.Cloud.Platform.Utils;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging.Abstractions;
    using NUnit.Framework;

    [TestFixture]
    [Category("Integration/Live")]
    public class ClusterSkuFilterProviderTests
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
            this.provider = new ClusterSkuFilterProvider(this.mockFixture.Services, this.mockFixture.Configuration, NullLogger.Instance);
        }

        [Test]
        public async Task ClusterSkuFilterProviderSearchesCorrectNodes()
        {
            using (IMemoryCache<IEnumerable<string>> cache = new MemoryCache<IEnumerable<string>>())
            {
                this.mockFixture.Services.AddSingleton<IMemoryCache<IEnumerable<string>>>(cache);
                IEnumerable<string> includeRegion = this.mockFixture.SelectionSettings.Regions;
                string excludeRegion = includeRegion.Shuffle().First();
                IEnumerable<string> includeClusterSkus = this.mockFixture.SelectionSettings.ClusterSkus;
                string excludeClusterSkus = includeClusterSkus.Shuffle().First();

                EnvironmentFilter filter = new EnvironmentFilter(typeof(ClusterSkuFilterProvider).FullName, new Dictionary<string, IConvertible>()
                {
                    { "includeRegion", string.Join(",", includeRegion) },
                    { "excludeRegion", excludeRegion },
                    { "includeClusterSku", string.Join(",", includeClusterSkus) },
                    { "excludeClusterSku", excludeClusterSkus }
                });

                IDictionary<string, EnvironmentCandidate> candidates = await this.provider.ExecuteAsync(filter, CancellationToken.None);

                // Validate there were results returned from Kusto
                Assert.IsNotNull(candidates);
                Assert.IsNotEmpty(candidates);

                // Validate only the correct regions were returned.
                Assert.IsFalse(candidates.Values.Any(candidate => candidate.Region.Equals(excludeRegion)));
                Assert.IsTrue(candidates.Values.All(candidate => includeRegion.Contains(candidate.Region)));

                // Validate only correct ClusterSku Version were returned
                Assert.IsFalse(candidates.Values.Any(candidate => candidate.AdditionalInfo["ClusterSku"].Equals(excludeClusterSkus)));
                Assert.IsTrue(candidates.Values.All(candidate => includeClusterSkus.Contains(candidate.AdditionalInfo["ClusterSku"])));

                // Verify number of times cacheing functions were called. Although at the end our cacheing can be correct, need to verify that the provider is actually caching the result
                // and then effectively only retrieving values from the cache the second time called.
                this.mockFixture.VerifyExecuteAsync(candidates.Count);

                IDictionary<string, EnvironmentCandidate> candidatesFromCache = await this.provider.ExecuteAsync(filter, CancellationToken.None);

                // Verify that the exact same nodes were returned for the exact same filters.
                Assert.AreEqual(candidates.Count, candidatesFromCache.Count);
                Assert.IsTrue(candidatesFromCache.Keys.All(key => candidates.ContainsKey(key)));

                // Verify that the values came from the cache and not from kusto.
                this.mockFixture.VerifyCacheExecuteAsync(candidates.Count);
            }
        }
    }
}
