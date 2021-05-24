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
    public class KnownClusterFilterProviderTests
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
            this.provider = new KnownClusterFilterProvider(this.mockFixture.Services, this.mockFixture.Configuration, NullLogger.Instance);
        }

        [Test]
        public async Task KnownClusterProviderSearchesCorrectClusters()
        {
            using (IMemoryCache<IEnumerable<string>> cache = new MemoryCache<IEnumerable<string>>())
            {
                this.mockFixture.Services.AddSingleton<IMemoryCache<IEnumerable<string>>>(cache);

                IEnumerable<string> includeRegion = this.mockFixture.SelectionSettings.Regions;
                IEnumerable<string> includeClusters = this.mockFixture.SelectionSettings.ExampleClusters;
                string excludeCluster = includeClusters.Shuffle().First();

                EnvironmentFilter filter = new EnvironmentFilter(typeof(KnownClusterFilterProvider).FullName, new Dictionary<string, IConvertible>()
                {
                    { "includeRegion", string.Join(",", includeRegion) },
                    { "includeCluster",  string.Join(",", includeClusters) },
                    { "excludeCluster", excludeCluster }
                });

                IDictionary<string, EnvironmentCandidate> candidates = await this.provider.ExecuteAsync(filter, CancellationToken.None);

                // Validate there were results returned from Kusto
                Assert.IsNotNull(candidates);
                Assert.IsNotEmpty(candidates);

                // Validate only the correct regions were returned.
                Assert.IsTrue(candidates.Values.All(candidate => includeRegion.Contains(candidate.Region)));

                // Validate only the correct clusters were returned.
                Assert.IsTrue(candidates.Values.All(candidate => includeClusters.Contains(candidate.ClusterId)));
                Assert.IsFalse(candidates.Values.All(candidate => candidate.ClusterId.Equals(excludeCluster, StringComparison.OrdinalIgnoreCase)));

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