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
    public class ZeroExecutionClusterSelectionProviderTests
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
            this.provider = new ZeroExecutionClusterSelectionProvider(this.mockFixture.Services, this.mockFixture.Configuration, NullLogger.Instance);
        }

        [Test]
        public async Task ZeroExecutionClusterSelectionProviderSelectsClustersWithZeroExecutionCount()
        {
            using (IMemoryCache<IEnumerable<string>> cache = new MemoryCache<IEnumerable<string>>())
            {
                this.mockFixture.Services.AddSingleton<IMemoryCache<IEnumerable<string>>>(cache);

                IEnumerable<string> experimentNames = this.mockFixture.SelectionSettings.ExperimentNames;
                IEnumerable<string> cpuIds = this.mockFixture.SelectionSettings.CpuIds.Take(2);
                IEnumerable<string> includeRegions = this.mockFixture.SelectionSettings.Regions;
                
                string excludeRegion = includeRegions.Shuffle().First();
                string experimentName = experimentNames.FirstOrDefault(item => item.EqualsOrdinalIgnoreCase("MCU2021.1_Gen7_Candidate"));

                EnvironmentFilter filter = new EnvironmentFilter(typeof(ZeroExecutionClusterSelectionProvider).FullName, new Dictionary<string, IConvertible>()
                {
                    { "experimentName", experimentName },
                    { "cpuId", string.Join(",", cpuIds) },
                    { "includeRegion", string.Join(",", includeRegions) },
                    { "excludeRegion", excludeRegion }
                });

                IDictionary<string, EnvironmentCandidate> candidates = await this.provider.ExecuteAsync(filter, CancellationToken.None);

                // Validate there were results returned from Kusto
                Assert.IsNotNull(candidates);
                Assert.IsNotEmpty(candidates);

                // Validate only the correct regions were returned.
                Assert.IsFalse(candidates.Values.Any(candidate => candidate.Region.Equals(excludeRegion)));
                Assert.IsTrue(candidates.Values.All(candidate => includeRegions.Contains(candidate.Region)));

                // Validate only the correct cpu ids were returned.
                Assert.IsTrue(candidates.Values.All(candidate => cpuIds.Contains(candidate.CpuId)));

                // Validate only correct experiment name were returned
                Assert.IsTrue(candidates.Values.All(candidate => candidate.AdditionalInfo["ExperimentName"].Equals(experimentName)));

                // Verify number of times cacheing functions were called. Although at the end our cacheing can be correct, need to verify that the provider is actually caching the result
                // and then effectively only retrieving values from the cache the second time called.
                this.mockFixture.VerifyExecuteAsync(candidates.Count);

                IDictionary<string, EnvironmentCandidate> candidatesFromCache = await this.provider.ExecuteAsync(filter, CancellationToken.None);

                // Verify that the exact same nodes were returned for the exact same filters.
                Assert.AreEqual(candidates.Count, candidatesFromCache.Count);
                Assert.IsTrue(candidatesFromCache.Keys.All(key => candidates.ContainsKey(key)));

                // Validate only the correct cpu ids were returned.
                Assert.IsTrue(candidates.Values.All(candidate => cpuIds.Contains(candidate.CpuId)));

                // Validate only correct experiment name were returned
                Assert.IsTrue(candidates.Values.All(candidate => candidate.AdditionalInfo["ExperimentName"].Equals(experimentName)));

                // Verify that the values came from the cache and not from kusto.
                this.mockFixture.VerifyCacheExecuteAsync(candidates.Count);
            }
        }
    }
}
