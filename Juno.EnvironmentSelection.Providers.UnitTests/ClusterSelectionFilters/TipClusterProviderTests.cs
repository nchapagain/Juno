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
    using Microsoft.Azure.CRC.Kusto;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using Moq.Language.Flow;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class TipClusterProviderTests
    {
        private TipClusterProvider provider;
        private EnvironmentFilter filter;
        private FixtureDependencies mockFixture;
        private Mock<IMemoryCache<ClusterAccount>> localCache;
        private Mock<IMemoryCache<IEnumerable<string>>> kustoCache;
        private Mock<ICachingFunctions> mockFunctions;
        private Mock<IKustoQueryIssuer> mockKusto;
        private IReturnsResult<IMemoryCache<ClusterAccount>> addMock;

        private delegate void CallbackFunc(object key, out object value);

        [SetUp]
        public void SetupTests()
        {
            this.mockFixture = new FixtureDependencies();
            this.kustoCache = new Mock<IMemoryCache<IEnumerable<string>>>();
            this.localCache = new Mock<IMemoryCache<ClusterAccount>>();
            this.mockFunctions = new Mock<ICachingFunctions>();
            this.mockKusto = new Mock<IKustoQueryIssuer>();
            this.mockFixture.Services.AddSingleton<IMemoryCache<ClusterAccount>>(this.localCache.Object);
            this.mockFixture.Services.AddSingleton<IMemoryCache<IEnumerable<string>>>(this.kustoCache.Object);
            this.mockFixture.Services.AddSingleton<ICachingFunctions>(this.mockFunctions.Object);
            this.mockFixture.Services.AddSingleton<IKustoQueryIssuer>(this.mockKusto.Object);

            this.provider = new TipClusterProvider(this.mockFixture.Services, this.mockFixture.Configuration, this.mockFixture.Logger.Object);
            this.filter = new EnvironmentFilter(typeof(TipClusterProvider).FullName, new Dictionary<string, IConvertible>() { { "includeRegion", "region" } });

            this.SetUpDefaultMockBehavior();
        }

        public void SetUpDefaultMockBehavior()
        {
            this.mockFunctions.Setup(f => f.GenerateCacheKeys(It.IsAny<EnvironmentFilter>(), It.IsAny<EventContext>())).Returns(new List<ProviderCacheKey>() { new ProviderCacheKey() });
            this.mockFunctions.Setup(f => f.GenerateCacheKey(It.IsAny<DataRow>(), It.IsAny<IEnumerable<KustoColumnAttribute>>())).Returns(new ProviderCacheKey());
            this.mockFunctions.Setup(f => f.GenerateCacheValue(It.IsAny<EnvironmentCandidate>())).Returns(string.Empty);
            this.mockFunctions.Setup(f => f.PopulateCacheAsync(It.IsAny<IMemoryCache<IEnumerable<string>>>(), It.IsAny<IDictionary<ProviderCacheKey, IList<string>>>(), It.IsAny<TimeSpan>(), It.IsAny<EventContext>())).Returns(Task.CompletedTask);
            this.kustoCache.Setup(cache => cache.Contains(It.IsAny<string>())).Returns(false);

            this.localCache.Setup(c => c.Contains(It.IsAny<string>())).Returns(false);
            this.addMock = this.localCache.Setup(c => c.AddAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<Func<ClusterAccount>>(), It.IsAny<bool>())).Returns(Task.FromResult(true));
        }

        [Test]
        public void ExecuteAsyncPostsCorrectValuesToCacheWhenCacheIsEmpty()
        {
            DataTable table = this.GenerateValidDataTable(rowCount: 10);
            List<ClusterAccount> expectedAccounts = new List<ClusterAccount>();
            List<string> expectedKeys = new List<string>();
            foreach (DataRow row in table.Rows)
            {
                expectedAccounts.Add(new ClusterAccount(int.Parse(row[ProviderConstants.RemainingTipSessions].ToString())));
                expectedKeys.Add(row[ProviderConstants.ClusterId].ToString());
            }

            this.mockKusto.Setup(kusto => kusto.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult(table));

            this.addMock.Callback<string, TimeSpan, Func<ClusterAccount>, bool>((key, ttl, func, sliding) =>
            {
                ClusterAccount value = func.Invoke();
                Assert.IsTrue(expectedAccounts.Any(a => a.Equals(value)));
                Assert.IsTrue(expectedKeys.Any(k => k.Equals(key.ToString(), StringComparison.OrdinalIgnoreCase)));
            });

            _ = this.provider.ExecuteAsync(this.filter, CancellationToken.None).GetAwaiter().GetResult();

            this.mockKusto.Verify(kusto => kusto.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once());
            this.localCache.Verify(c => c.AddAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<Func<ClusterAccount>>(), It.IsAny<bool>()), Times.Exactly(table.Rows.Count));
        }

        [Test]
        public void ExecuteAsyncPostsCorrectValuesToCacheWhenCacheIsNotEmpty()
        {
            DataTable table = this.GenerateValidDataTable(rowCount: 10);
            ClusterAccount outValue = new ClusterAccount(5);
            List<string> expectedKeys = new List<string>();
            foreach (DataRow row in table.Rows)
            {
                expectedKeys.Add(row[ProviderConstants.ClusterId].ToString());
            }

            this.mockKusto.Setup(kusto => kusto.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult(table));
            this.localCache.Setup(c => c.Contains(It.IsAny<string>())).Returns(true);
            this.localCache.Setup(c => c.GetAsync(It.IsAny<string>())).ReturnsAsync(outValue);
            this.addMock.Callback<string, TimeSpan, Func<ClusterAccount>, bool>((key, ttl, func, sliding) =>
            {
                ClusterAccount value = func.Invoke();
                Assert.AreEqual(outValue, func.Invoke());
                Assert.IsTrue(expectedKeys.Any(k => k.Equals(key.ToString(), StringComparison.OrdinalIgnoreCase)));
            });

            _ = this.provider.ExecuteAsync(this.filter, CancellationToken.None).GetAwaiter().GetResult();

            this.mockKusto.Verify(kusto => kusto.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once());
            this.localCache.Verify(c => c.AddAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<Func<ClusterAccount>>(), It.IsAny<bool>()), Times.Exactly(table.Rows.Count));
        }

        [Test]
        public void ExecuteAsyncFiltersOutCorrectClusters()
        {
            const int totalRows = 10;
            DataTable table = this.GenerateValidDataTable(5, totalRows / 2);
            table.Merge(this.GenerateValidDataTable(0, totalRows / 2));
            Dictionary<string, ClusterAccount> expectedAccounts = new Dictionary<string, ClusterAccount>();
            foreach (DataRow row in table.Rows)
            {
                expectedAccounts.Add(row[ProviderConstants.ClusterId].ToString(), new ClusterAccount(Convert.ToInt32(row[ProviderConstants.RemainingTipSessions])));
            }

            this.mockKusto.Setup(kusto => kusto.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult(table));
            this.localCache.Setup(c => c.Contains(It.IsAny<string>())).Returns(true);
            this.localCache.Setup(c => c.GetAsync(It.IsAny<string>()))
                .ReturnsAsync((string key) => expectedAccounts[key]);

            IDictionary<string, EnvironmentCandidate> actualResult = this.provider.ExecuteAsync(this.filter, CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsNotNull(actualResult);
            Assert.IsNotEmpty(actualResult);
            Assert.IsTrue(actualResult.Values.All(r => int.Parse(r.AdditionalInfo[ProviderConstants.RemainingTipSessions]) > 0));
        }

        [Test]
        public void DeleteReservationValidatesParameters()
        {
            Assert.ThrowsAsync<ArgumentException>(() => this.provider.DeleteReservationAsync(null, CancellationToken.None));
        }

        [Test]
        public void ReserveCandidateValidatesParameters()
        {
            Assert.ThrowsAsync<ArgumentException>(() => this.provider.ReserveCandidateAsync(null, TimeSpan.Zero, CancellationToken.None));
        }

        [Test]
        public void ReserveCandidateUpdatesClusterWhenClusterIsInCache()
        {
            ClusterAccount clusterAccount = new ClusterAccount(5);
            string expectedClusterId = Guid.NewGuid().ToString();
            string expectedNodeId = Guid.NewGuid().ToString();
            TimeSpan offset = TimeSpan.FromHours(1);
            ClusterAccount expectedAccount = new ClusterAccount(5, new List<ClusterReservation>() { new ClusterReservation(expectedNodeId, DateTime.UtcNow + TimeSpan.FromHours(1)) });
            EnvironmentCandidate candidate = new EnvironmentCandidate(cluster: expectedClusterId, node: expectedNodeId);
            this.localCache.Setup(c => c.Contains(It.IsAny<string>())).Returns(true);
            this.localCache.Setup(c => c.GetAsync(It.IsAny<string>()))
                .ReturnsAsync(clusterAccount);
            this.addMock.Callback<string, TimeSpan, Func<ClusterAccount>, bool>((key, ttl, func, sliding) =>
            {
                ClusterAccount actualCluster = func.Invoke();
                Assert.AreEqual(expectedAccount.TipSessionsAllowed, actualCluster.TipSessionsAllowed);
                Assert.IsTrue(actualCluster.Reservations.All(r => r.NodeId.Equals(expectedNodeId, StringComparison.OrdinalIgnoreCase)));
            });

            bool result = this.provider.ReserveCandidateAsync(candidate, offset, CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsTrue(result);

            this.localCache.Verify(cache => cache.Contains(It.IsAny<string>()), Times.Once());
            this.localCache.Verify(cache => cache.AddAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<Func<ClusterAccount>>(), It.IsAny<bool>()), Times.Once());
        }

        [Test]
        public void ReserveCandidateUpdatesClusterWhenClusterIsNotInCache()
        {
            string expectedClusterId = Guid.NewGuid().ToString();
            string expectedNodeId = Guid.NewGuid().ToString();
            TimeSpan offset = TimeSpan.FromHours(1);
            ClusterAccount expectedAccount = new ClusterAccount(0, new List<ClusterReservation>() { new ClusterReservation(expectedNodeId, DateTime.UtcNow + offset) });
            EnvironmentCandidate candidate = new EnvironmentCandidate(cluster: expectedClusterId, node: expectedNodeId);
            this.localCache.Setup(c => c.Contains(It.IsAny<string>())).Returns(false);

            this.addMock.Callback<string, TimeSpan, Func<ClusterAccount>, bool>((key, ttl, func, sliding) =>
            {
                ClusterAccount value = func.Invoke();
                Assert.AreEqual(expectedAccount, value);
                Assert.AreEqual(expectedClusterId, key);
            });

            bool result = this.provider.ReserveCandidateAsync(candidate, offset, CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsTrue(result);

            this.localCache.Verify(cache => cache.Contains(It.IsAny<string>()), Times.Once());
            this.localCache.Verify(cache => cache.AddAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<Func<ClusterAccount>>(), It.IsAny<bool>()), Times.Once());
        }

        [Test]
        public void ReserveCandidateReturnsExpectedResultWhenGivenDefaultCandidate()
        {
            bool result = this.provider.ReserveCandidateAsync(new EnvironmentCandidate(), TimeSpan.Zero, CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsTrue(result);
            this.localCache.Verify(c => c.Contains(It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void DeleteCandidateUpdatesClusterWhenClusterIsInCacheAndContainsNodeIdReservation()
        {
            string nodeId = Guid.NewGuid().ToString();
            string clusterId = Guid.NewGuid().ToString();
            TimeSpan offset = TimeSpan.FromHours(1);
            ClusterAccount clusterAccount = new ClusterAccount(5, new List<ClusterReservation>() { new ClusterReservation(nodeId, DateTime.UtcNow + offset) });
            EnvironmentCandidate candidate = new EnvironmentCandidate(node: nodeId, cluster: clusterId);

            this.localCache.Setup(c => c.Contains(It.IsAny<string>())).Returns(true);
            this.localCache.Setup(c => c.GetAsync(It.IsAny<string>()))
                .ReturnsAsync(clusterAccount);

            this.addMock.Callback<string, TimeSpan, Func<ClusterAccount>, bool>((key, ttl, func, sliding) =>
            {
                ClusterAccount actualAccount = func.Invoke();
                Assert.AreEqual(5, actualAccount.TipSessionsAllowed, $"The updated {nameof(ClusterAccount)} does not match the expected: {nameof(ClusterAccount.TipSessionsAllowed)}");
                Assert.IsEmpty(actualAccount.Reservations, $"the updated {nameof(ClusterAccount.Reservations)} is not empty");
            });

            bool result = this.provider.DeleteReservationAsync(candidate, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(result);
            this.localCache.Verify(cache => cache.Contains(It.IsAny<string>()), Times.Once());
            this.localCache.Verify(cache => cache.AddAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<Func<ClusterAccount>>(), It.IsAny<bool>()), Times.Once());
        }

        [Test]
        public void DeleteCandidateUpdatesClusterWhenClusterIsInCache()
        {
            string nodeId = Guid.NewGuid().ToString();
            string clusterId = Guid.NewGuid().ToString();
            TimeSpan offset = TimeSpan.FromHours(1);
            ClusterAccount clusterAccount = new ClusterAccount(5, new List<ClusterReservation>());
            EnvironmentCandidate candidate = new EnvironmentCandidate(node: nodeId, cluster: clusterId);

            this.localCache.Setup(c => c.Contains(It.IsAny<string>())).Returns(true);
            this.localCache.Setup(c => c.GetAsync(It.IsAny<string>()))
                .Returns(Task.FromResult(clusterAccount));

            this.addMock.Callback<string, TimeSpan, Func<ClusterAccount>, bool>((key, ttl, func, sliding) =>
            {
                ClusterAccount actualAccount = func.Invoke();
                Assert.AreEqual(5, actualAccount.TipSessionsAllowed, $"The updated {nameof(ClusterAccount)} does not match the expected: {nameof(ClusterAccount.TipSessionsAllowed)}");
                Assert.IsEmpty(actualAccount.Reservations, $"the updated {nameof(ClusterAccount.Reservations)} is not empty");
            });

            bool result = this.provider.DeleteReservationAsync(candidate, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(result);
            this.localCache.Verify(cache => cache.Contains(It.IsAny<string>()), Times.Once());
            this.localCache.Verify(cache => cache.AddAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<Func<ClusterAccount>>(), It.IsAny<bool>()), Times.Once());
        }

        [Test]
        public void DeleteCandidateReturnsExpectedResultWhenClusterIsNotInCache()
        {
            string nodeId = Guid.NewGuid().ToString();
            string clusterId = Guid.NewGuid().ToString();
            TimeSpan offset = TimeSpan.FromHours(1);
            object clusterAccount = new ClusterAccount(0, new List<ClusterReservation>());
            EnvironmentCandidate candidate = new EnvironmentCandidate(node: nodeId, cluster: clusterId);
            this.localCache.Setup(c => c.Contains(It.IsAny<string>())).Returns(false);

            this.addMock.Callback<string, TimeSpan, Func<ClusterAccount>, bool>((key, ttl, func, sliding) =>
            {
                ClusterAccount actualAccount = func.Invoke();
                Assert.AreEqual(0, actualAccount.TipSessionsAllowed, $"The updated {nameof(ClusterAccount)} does not match the expected: {nameof(ClusterAccount.TipSessionsAllowed)}");
                Assert.IsEmpty(actualAccount.Reservations, $"the updated {nameof(ClusterAccount.Reservations)} is not empty");
            });

            bool result = this.provider.DeleteReservationAsync(candidate, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(result);
            this.localCache.Verify(cache => cache.Contains(It.IsAny<string>()), Times.Once());
            this.localCache.Verify(cache => cache.AddAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<Func<ClusterAccount>>(), It.IsAny<bool>()), Times.Once());
        }

        [Test]
        public void DeleteCandidateReturnsExpectedResultWhenGivenDefaultCandidate()
        {
            bool result = this.provider.DeleteReservationAsync(new EnvironmentCandidate(), CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsTrue(result);
            this.localCache.Verify(c => c.Contains(It.IsAny<string>()), Times.Never());
        }

        private DataTable GenerateValidDataTable(int tipRemaining = 5, int rowCount = 5)
        {
            DataTable table = new DataTable();
            table.Columns.Add(ProviderConstants.ClusterId);
            table.Columns.Add(ProviderConstants.Region);
            table.Columns.Add(ProviderConstants.RemainingTipSessions);
            table.Columns.Add(ProviderConstants.TipSessionLowerBound);

            for (int i = 0; i < rowCount; i++)
            {
                DataRow row = table.NewRow();
                row[ProviderConstants.ClusterId] = Guid.NewGuid().ToString();
                row[ProviderConstants.Region] = Guid.NewGuid().ToString();
                row[ProviderConstants.RemainingTipSessions] = tipRemaining;
                row[ProviderConstants.TipSessionLowerBound] = tipRemaining;

                table.Rows.Add(row);
            }

            return table;
        }
    }
}
