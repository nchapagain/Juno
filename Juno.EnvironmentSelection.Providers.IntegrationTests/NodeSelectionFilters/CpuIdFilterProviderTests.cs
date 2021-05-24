namespace Juno.EnvironmentSelection.NodeSelectionFilters
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
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging.Abstractions;
    using NUnit.Framework;

    [TestFixture]
    [Category("Integration/Live")]
    public class CpuIdFilterProviderTests 
    {
        private EnvironmentFilterFixture mockFixture;
        private NodeSelectionFilter provider;

        [SetUp]
        public void SetupTests()
        {
            TestDependencies.Initialize();
            this.mockFixture = new EnvironmentFilterFixture();
            this.mockFixture.SetUpMockFunctions();
            this.mockFixture.Services.AddSingleton(TestDependencies.KeyVaultClient);
            this.mockFixture.Services.AddSingleton<ICachingFunctions>(this.mockFixture.MockFunctions.Object);
            this.provider = new CpuIdFilterProvider(this.mockFixture.Services, this.mockFixture.Configuration, NullLogger.Instance);
        }

        [Test]
        public async Task CpuIdFilterProviderSearchesCorrectNodes()
        {
            using (IMemoryCache<IEnumerable<string>> cache = new MemoryCache<IEnumerable<string>>())
            {
                this.mockFixture.Services.AddSingleton<IMemoryCache<IEnumerable<string>>>(cache);
                IEnumerable<string> includeCluster = this.mockFixture.SelectionSettings.ExampleClusters;
                string excludeCluster = includeCluster.Shuffle().First();

                IEnumerable<string> includeCpuId = this.mockFixture.SelectionSettings.CpuIds;
                string excludeCpuId = includeCpuId.Shuffle().First();

                EnvironmentFilter filter = new EnvironmentFilter(typeof(CpuIdFilterProvider).FullName, new Dictionary<string, IConvertible>()
                {
                    { "includeCluster", string.Join(",", includeCluster) },
                    { "excludeCluster", excludeCluster },
                    { "includeCpuId", string.Join(",", includeCpuId) },
                    { "excludeCpuId", excludeCpuId }
                });

                IDictionary<string, EnvironmentCandidate> candidates = await this.provider.ExecuteAsync(filter, CancellationToken.None);

                // Validate there were results returned from Kusto
                Assert.IsNotNull(candidates);
                Assert.IsNotEmpty(candidates);

                // Validate only the correct regions were returned.
                Assert.IsFalse(candidates.Values.Any(candidate => candidate.ClusterId.Equals(excludeCluster)));
                Assert.IsTrue(candidates.Values.All(candidate => includeCluster.Contains(candidate.ClusterId)));

                // Validate only correct Cpu IDs were returned
                Assert.IsFalse(candidates.Values.Any(candidate => candidate.CpuId.Equals(excludeCpuId)));
                Assert.IsTrue(candidates.Values.All(candidate => includeCpuId.Contains(candidate.CpuId)));

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
