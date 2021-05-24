namespace Juno.EnvironmentSelection.NodeSelectionFilters
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
    public class SoCNitroFilterProviderTests
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
            this.provider = new SoCNitroFilterProvider(this.mockFixture.Services, this.mockFixture.Configuration, NullLogger.Instance);
        }

        [Test]
        public async Task SoCNitroFilterProviderSearchesCorrectNodes()
        {
            using (IMemoryCache<IEnumerable<string>> cache = new MemoryCache<IEnumerable<string>>())
            {
                this.mockFixture.Services.AddSingleton<IMemoryCache<IEnumerable<string>>>(cache);

                IEnumerable<string> includeCluster = this.mockFixture.SelectionSettings.ExampleClusters;
                string excludeCluster = includeCluster.Shuffle().First();
                IEnumerable<string> includeNitro = this.mockFixture.SelectionSettings.SoCNitroFirmwares;
                string excludeNitro = includeNitro.Shuffle().First();

                EnvironmentFilter filter = new EnvironmentFilter(typeof(SoCNitroFilterProvider).FullName, new Dictionary<string, IConvertible>()
                {
                    { "includeCluster", "dsm06prdapp18" },
                    { "excludeCluster", excludeCluster },
                    { "includeFirmware", string.Join(",", includeNitro) },
                    { "excludeFirmware", excludeNitro }
                });

                IDictionary<string, EnvironmentCandidate> candidates = await this.provider.ExecuteAsync(filter, CancellationToken.None);

                // Validate that there are results present
                Assert.IsNotNull(candidates);
                Assert.IsNotEmpty(candidates);

                // Validate that the correct clusters were searched
                Assert.IsFalse(candidates.Values.Any(candidate => candidate.ClusterId.Equals(excludeCluster)), "Clusters that were excluded were included.");
                Assert.IsTrue(candidates.Values.Any(candidate => candidate.ClusterId.Equals("dsm06prdapp18", StringComparison.OrdinalIgnoreCase)), "Not all clusters belong to the include list.");

                // Validate that the correct OS Builds were searched
                Assert.IsFalse(candidates.Values.Any(candidate => candidate.AdditionalInfo["SoCNitro"].Equals(excludeNitro)), "Some firmwares excluded have been included in results.");
                Assert.IsTrue(candidates.Values.All(candidate => includeNitro.Contains(candidate.AdditionalInfo["SoCNitro"])), "Not all SoCNitro firmwares belong to the include list.");

                // Verify number of times cacheing functions were called. Although at the end our cacheing can be correct, need to verify that the provider is actually caching the result
                // and then effectively only retrieving values from the cache the second time called.
                this.mockFixture.VerifyExecuteAsync(candidates.Count);

                IDictionary<string, EnvironmentCandidate> candidatesFromCache = await this.provider.ExecuteAsync(filter, CancellationToken.None);

                // Verify that the exact same nodes were returned for the exact same filters.
                Assert.AreEqual(candidates.Count, candidatesFromCache.Count, $"Counts from cache: {candidatesFromCache.Count} Counts from source: {candidates.Count}");
                Assert.IsTrue(candidatesFromCache.Keys.All(key => candidates.ContainsKey(key)), "Not all candidates from source were returned from candidates from cache.");

                // Verify that the values came from the cache and not from kusto.
                this.mockFixture.VerifyCacheExecuteAsync(candidates.Count);
            }
        }
    }
}
