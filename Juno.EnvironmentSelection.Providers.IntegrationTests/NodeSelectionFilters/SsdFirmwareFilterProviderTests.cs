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
    public class SsdFirmwareFilterProviderTests
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
            this.provider = new SsdFirmwareFilterProvider(this.mockFixture.Services, this.mockFixture.Configuration, NullLogger.Instance);
        }

        [Test]
        public async Task SsdFilterProviderSearchesCorrectNodesWithBothDriveTypes()
        {
            using (IMemoryCache<IEnumerable<string>> cache = new MemoryCache<IEnumerable<string>>())
            {
                this.mockFixture.Services.AddSingleton<IMemoryCache<IEnumerable<string>>>(cache);
                IEnumerable<string> clusterIncludeList = this.mockFixture.SelectionSettings.SsdClusters;
                string clusterExcludeList = clusterIncludeList.Shuffle().First();

                IEnumerable<string> includeFirmware = this.mockFixture.SelectionSettings.SsdFirmwares;
                string excludeFirmware = includeFirmware.Shuffle().First();
                IEnumerable<string> driveTypes = new string[] { SsdDriveType.Data.ToString(), SsdDriveType.System.ToString() };

                EnvironmentFilter filter = new EnvironmentFilter(typeof(SsdFirmwareFilterProvider).FullName, new Dictionary<string, IConvertible>()
                {
                    { "includeCluster", string.Join(",", clusterIncludeList) },
                    { "excludeCluster", clusterExcludeList },
                    { "includeFirmware", string.Join(",", includeFirmware) },
                    { "excludeFirmware", excludeFirmware },
                    { "driveType", string.Join(",", driveTypes) }
                });

                IDictionary<string, EnvironmentCandidate> candidates = await this.provider.ExecuteAsync(filter, CancellationToken.None);

                // Validate that there are results present
                Assert.IsNotNull(candidates);
                Assert.IsNotEmpty(candidates);

                // Validate that the correct Clusters were searched
                Assert.IsFalse(candidates.Values.Any(candidate => candidate.ClusterId.Equals(clusterExcludeList)));
                Assert.IsTrue(candidates.Values.All(candidate => clusterIncludeList.Contains(candidate.ClusterId)));

                // Verify that the correct SSD Model was searched
                Assert.IsTrue(candidates.Values.All(candidate => candidate.AdditionalInfo[ProviderConstants.SsdFirmware].ToList(',').All(sf => includeFirmware.Contains(sf))));
                Assert.False(candidates.Values.All(candidate => candidate.AdditionalInfo[ProviderConstants.SsdFirmware].ToList(',').Any(sf => sf.Equals(excludeFirmware, StringComparison.OrdinalIgnoreCase))));
                Assert.IsTrue(candidates.Values.All(candidate => candidate.AdditionalInfo[ProviderConstants.SsdDriveType].ToList(',').All(dt => driveTypes.Contains(dt))));

                // Verify number of times caching functions were called. Although at the end our cacheing can be correct, need to verify that the provider is actually caching the result
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

        [Test]
        public async Task SsdFilterProviderSearchesCorrectNodesWithOneDriveTypes()
        {
            using (IMemoryCache<IEnumerable<string>> cache = new MemoryCache<IEnumerable<string>>())
            {
                this.mockFixture.Services.AddSingleton<IMemoryCache<IEnumerable<string>>>(cache);
                IEnumerable<string> clusterIncludeList = this.mockFixture.SelectionSettings.SsdClusters;
                string clusterExcludeList = clusterIncludeList.Shuffle().First();

                IEnumerable<string> includeFirmware = this.mockFixture.SelectionSettings.SsdFirmwares;
                string excludeFirmware = includeFirmware.Shuffle().First();
                string driveType = (new string[] { SsdDriveType.Data.ToString(), SsdDriveType.System.ToString() }).Shuffle().First();

                EnvironmentFilter filter = new EnvironmentFilter(typeof(SsdFirmwareFilterProvider).FullName, new Dictionary<string, IConvertible>()
                {
                    { "includeCluster", string.Join(",", clusterIncludeList) },
                    { "excludeCluster", clusterExcludeList },
                    { "includeFirmware", string.Join(",", includeFirmware) },
                    { "excludeFirmware", excludeFirmware },
                    { "driveType", driveType }
                });

                IDictionary<string, EnvironmentCandidate> candidates = await this.provider.ExecuteAsync(filter, CancellationToken.None);

                // Validate that there are results present
                Assert.IsNotNull(candidates);
                Assert.IsNotEmpty(candidates);

                // Validate that the correct Clusters were searched
                Assert.IsFalse(candidates.Values.Any(candidate => candidate.ClusterId.Equals(clusterExcludeList)));
                Assert.IsTrue(candidates.Values.All(candidate => clusterIncludeList.Contains(candidate.ClusterId)));

                // Verify that the correct SSD Model was searched
                Assert.IsTrue(candidates.Values.All(candidate => candidate.AdditionalInfo[ProviderConstants.SsdFirmware].ToList(',').All(sf => includeFirmware.Contains(sf))));
                Assert.False(candidates.Values.All(candidate => candidate.AdditionalInfo[ProviderConstants.SsdFirmware].ToList(',').Any(sf => sf.Equals(excludeFirmware, StringComparison.OrdinalIgnoreCase))));
                Assert.IsTrue(candidates.Values.All(candidate => candidate.AdditionalInfo[ProviderConstants.SsdDriveType].ToList(',').All(dt => driveType.Equals(dt))));

                // Verify number of times caching functions were called. Although at the end our cacheing can be correct, need to verify that the provider is actually caching the result
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