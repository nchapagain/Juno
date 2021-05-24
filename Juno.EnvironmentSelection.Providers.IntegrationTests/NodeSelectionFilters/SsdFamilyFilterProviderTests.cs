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
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Integration/Live")]
    public class SsdFamilyFilterProviderTests
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
            this.provider = new SsdFamilyFilterProvider(this.mockFixture.Services, this.mockFixture.Configuration, NullLogger.Instance);
        }

        [Test]
        public async Task SsdFilterProviderSearchesCorrectNodesWithBothDriveTypes()
        {
            using (IMemoryCache<IEnumerable<string>> cache = new MemoryCache<IEnumerable<string>>())
            {
                this.mockFixture.Services.AddSingleton<IMemoryCache<IEnumerable<string>>>(cache);
                IEnumerable<string> clusterIncludeList = this.mockFixture.SelectionSettings.SsdClusters;
                string clusterExcludeList = clusterIncludeList.Shuffle().First();

                string includeFamily = this.mockFixture.SelectionSettings.SsdFamilies.Shuffle().First();
                IEnumerable<string> driveTypes = new string[] { SsdDriveType.Data.ToString(), SsdDriveType.System.ToString() };

                EnvironmentFilter filter = new EnvironmentFilter(typeof(SsdFamilyFilterProvider).FullName, new Dictionary<string, IConvertible>()
                {
                    { "includeCluster", string.Join(",", clusterIncludeList) },
                    { "excludeCluster", clusterExcludeList },
                    { "includeFamily", includeFamily },
                    { "driveType", string.Join(",", driveTypes) }
                });

                IDictionary<string, EnvironmentCandidate> candidates = await this.provider.ExecuteAsync(filter, CancellationToken.None);

                // Validate that there are results present
                Assert.IsNotNull(candidates);
                Assert.IsNotEmpty(candidates);

                // Validate that the correct Clusters were searched
                Assert.IsFalse(candidates.Values.Any(candidate => candidate.ClusterId.Equals(clusterExcludeList)));
                Assert.IsTrue(candidates.Values.All(candidate => clusterIncludeList.Contains(candidate.ClusterId)));

                // Verify that the correct SSD Family was searched
                Assert.IsTrue(candidates.Values.All(candidate => candidate.AdditionalInfo[ProviderConstants.SsdFamily].Equals(includeFamily, StringComparison.OrdinalIgnoreCase)));
                Assert.IsTrue(candidates.Values.All(candidate => driveTypes.Contains(candidate.AdditionalInfo[ProviderConstants.SsdDriveType])));

                // Verify number of times caching functions were called. Although at the end our cacheing can be correct, need to verify that the provider is actually caching the result
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

        [Test]
        public async Task SsdFilterProviderSearchesCorrectNodesWithOneDriveTypes()
        {
            using (IMemoryCache<IEnumerable<string>> cache = new MemoryCache<IEnumerable<string>>())
            {
                this.mockFixture.Services.AddSingleton<IMemoryCache<IEnumerable<string>>>(cache);
                IEnumerable<string> clusterIncludeList = this.mockFixture.SelectionSettings.SsdClusters;
                string clusterExcludeList = clusterIncludeList.Shuffle().First();

                string includeFamily = this.mockFixture.SelectionSettings.SsdFamilies.Shuffle().First();
                string driveType = (new string[] { SsdDriveType.Data.ToString(), SsdDriveType.System.ToString() }).Shuffle().First();

                EnvironmentFilter filter = new EnvironmentFilter(typeof(SsdFamilyFilterProvider).FullName, new Dictionary<string, IConvertible>()
                {
                    { "includeCluster", string.Join(",", clusterIncludeList) },
                    { "excludeCluster", clusterExcludeList },
                    { "includeFamily", includeFamily },
                    { "driveType", driveType }
                });

                IDictionary<string, EnvironmentCandidate> candidates = await this.provider.ExecuteAsync(filter, CancellationToken.None);

                // Validate that there are results present
                Assert.IsNotNull(candidates);
                Assert.IsNotEmpty(candidates);

                // Validate that the correct Clusters were searched
                Assert.IsFalse(candidates.Values.Any(candidate => candidate.ClusterId.Equals(clusterExcludeList)));
                Assert.IsTrue(candidates.Values.All(candidate => clusterIncludeList.Contains(candidate.ClusterId)));

                // Verify that the correct SSD Family was searched
                Assert.IsTrue(candidates.Values.All(candidate => candidate.AdditionalInfo[ProviderConstants.SsdFamily].Equals(includeFamily, StringComparison.OrdinalIgnoreCase)));
                Assert.IsTrue(candidates.Values.All(candidate => candidate.AdditionalInfo[ProviderConstants.SsdDriveType].Equals(driveType)));

                // Verify number of times caching functions were called. Although at the end our cacheing can be correct, need to verify that the provider is actually caching the result
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
