namespace Juno.EnvironmentSelection.NodeSelectionFilters
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Extensions.AspNetCore;
    using Microsoft.Azure.CRC.Kusto;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.Management.CosmosDB.Fluent.Models;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class HealthyNodeProviderTests
    {
        private HealthyNodeProvider provider;
        private EnvironmentFilter filter;
        private FixtureDependencies mockFixture;
        private Mock<IMemoryCache<bool>> localCache;
        private Mock<IMemoryCache<IEnumerable<string>>> kustoCache;
        private Mock<ICachingFunctions> mockFunctions;
        private Mock<IKustoQueryIssuer> mockKusto;

        [SetUp]
        public void SetupTests()
        {
            this.mockFixture = new FixtureDependencies();
            this.localCache = new Mock<IMemoryCache<bool>>();
            this.kustoCache = new Mock<IMemoryCache<IEnumerable<string>>>();
            this.mockFunctions = new Mock<ICachingFunctions>();
            this.mockKusto = new Mock<IKustoQueryIssuer>();
            this.mockFixture.Services.AddSingleton<IMemoryCache<bool>>(this.localCache.Object);
            this.mockFixture.Services.AddSingleton<IMemoryCache<IEnumerable<string>>>(this.kustoCache.Object);
            this.mockFixture.Services.AddSingleton<ICachingFunctions>(this.mockFunctions.Object);
            this.mockFixture.Services.AddSingleton<IKustoQueryIssuer>(this.mockKusto.Object);

            this.provider = new HealthyNodeProvider(this.mockFixture.Services, this.mockFixture.Configuration, this.mockFixture.Logger.Object);
            this.filter = new EnvironmentFilter(typeof(HealthyNodeProvider).FullName, new Dictionary<string, IConvertible>() { { "includeCluster", "cluster" } });
            this.SetUpDefaultMockBehavior();
        }

        public void SetUpDefaultMockBehavior()
        {
            this.mockFunctions.Setup(f => f.GenerateCacheKeys(It.IsAny<EnvironmentFilter>(), It.IsAny<EventContext>())).Returns(new List<ProviderCacheKey>() { new ProviderCacheKey() });
            this.mockFunctions.Setup(f => f.GenerateCacheKey(It.IsAny<DataRow>(), It.IsAny<IEnumerable<KustoColumnAttribute>>())).Returns(new ProviderCacheKey());
            this.mockFunctions.Setup(f => f.GenerateCacheValue(It.IsAny<EnvironmentCandidate>())).Returns(string.Empty);
            this.mockFunctions.Setup(f => f.PopulateCacheAsync(It.IsAny<IMemoryCache<IEnumerable<string>>>(), It.IsAny<IDictionary<ProviderCacheKey, IList<string>>>(), It.IsAny<TimeSpan>(), It.IsAny<EventContext>())).Returns(Task.CompletedTask);
            this.kustoCache.Setup(cache => cache.Contains(It.IsAny<string>())).Returns(false);
        }

        [Test]
        public void ExecuteFiltersOutPreviouslyReservedNodes()
        {
            this.localCache.SetupSequence(cache => cache.Contains(It.IsAny<string>()))
                .Returns(false)
                .Returns(true);
            this.mockKusto.Setup(kusto => kusto.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult(this.GenerateValidDataTable(2)));

            IDictionary<string, EnvironmentCandidate> result = this.provider.ExecuteAsync(this.filter, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsNotEmpty(result);
            Assert.AreEqual(1, result.Count);
        }

        [Test]
        public void DeleteReservationValidatesParameters()
        {
            Assert.ThrowsAsync<ArgumentException>(() => this.provider.DeleteReservationAsync(null, CancellationToken.None));
        }

        [Test]
        public void DeleteReservationAsyncPostsCorrectEnvironmentCandidate()
        {
            string expectedId = Guid.NewGuid().ToString();
            this.localCache.Setup(cache => cache.Contains(It.IsAny<string>())).Returns(true);
            this.localCache.Setup(cache => cache.RemoveAsync(It.IsAny<string>()))
                .Callback<string>((id) => 
                {
                    Assert.AreEqual(expectedId, id);
                }).Returns(Task.CompletedTask);

            bool result = this.provider.DeleteReservationAsync(new EnvironmentCandidate(node: expectedId), CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(result);

            this.localCache.Verify(cache => cache.RemoveAsync(It.IsAny<string>()), Times.Once());
        }

        [Test]
        public void DeleteReservationAsyncReturnsExpectedResultWhenItemIsNotInCache()
        {
            this.localCache.Setup(cache => cache.Contains(It.IsAny<string>())).Returns(false);
            bool result = this.provider.DeleteReservationAsync(new EnvironmentCandidate(node: Guid.NewGuid().ToString()), CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(result);
            this.localCache.Verify(cache => cache.Contains(It.IsAny<string>()), Times.Once());
        }

        [Test]
        public void DeleteReservationAsyncReturnsExpectedResultWhenExceptionIsThrown()
        {
            this.localCache.Setup(cache => cache.Contains(It.IsAny<string>())).Returns(true);
            this.localCache.Setup(cache => cache.RemoveAsync(It.IsAny<string>()))
                .Throws(new KeyNotFoundException());
            bool result = this.provider.DeleteReservationAsync(new EnvironmentCandidate(node: Guid.NewGuid().ToString()), CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsFalse(result);
            this.localCache.Verify(cache => cache.RemoveAsync(It.IsAny<string>()), Times.Once());
        }

        [Test]
        public void DeleteReservationAsyncReturnsExpectedResultWhenCancellationIsRequested()
        {
            using (CancellationTokenSource source = new CancellationTokenSource())
            {
                CancellationToken token = source.Token;
                source.Cancel();
                bool result = this.provider.DeleteReservationAsync(new EnvironmentCandidate(), token).GetAwaiter().GetResult();
                Assert.IsFalse(result);
            }
        }

        [Test]
        public void ReserveCandidateAsyncValidatesParameters()
        {
            Assert.ThrowsAsync<ArgumentException>(() => this.provider.ReserveCandidateAsync(null, TimeSpan.Zero, CancellationToken.None));
        }

        [Test]
        public void ReserveCandidatesAsyncReturnsExpectedResultWhenGivenDefaultNodeId()
        {
            bool result = this.provider.ReserveCandidateAsync(new EnvironmentCandidate(), TimeSpan.Zero, CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsTrue(result);
        }

        [Test]
        public void ReserveCandidateAsyncReturnsExpectedResultWhenNodeIsInCache()
        {
            string expectedId = Guid.NewGuid().ToString();
            this.localCache.Setup(c => c.Contains(It.IsAny<string>())).Returns(true);
            this.localCache.Setup(c => c.ChangeTimeToLiveAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<bool>()))
                .Callback<string, TimeSpan, bool>((key, token, sliding) =>
                {
                    Assert.AreEqual(expectedId, key);
                }).Returns(Task.CompletedTask);

            bool result = this.provider.ReserveCandidateAsync(new EnvironmentCandidate(node: expectedId), TimeSpan.Zero, CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsTrue(result);
            this.localCache.Verify(c => c.ChangeTimeToLiveAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<bool>()), Times.Once());
        }

        [Test]
        public void ReserveCandidateAsyncReturnsExpectedResultWhenNodeIsInCacheAndThrowsError()
        {
            this.localCache.Setup(c => c.Contains(It.IsAny<string>())).Returns(true);
            this.localCache.Setup(c => c.ChangeTimeToLiveAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<bool>()))
                .Throws(new KeyNotFoundException());

            bool result = this.provider.ReserveCandidateAsync(new EnvironmentCandidate(node: Guid.NewGuid().ToString()), TimeSpan.Zero, CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsFalse(result);
            this.localCache.Verify(c => c.ChangeTimeToLiveAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<bool>()), Times.Once());
        }

        [Test]
        public void ReserveCandidateAsyncReturnsExpectedResultWhenNodeIsNotInCache()
        {
            string expectedId = Guid.NewGuid().ToString();
            this.localCache.Setup(c => c.Contains(It.IsAny<string>())).Returns(false);
            this.localCache.Setup(c => c.AddAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<Func<bool>>(), It.IsAny<bool>()))
                .Callback<string, TimeSpan, Func<bool>, bool>((key, duration, func, sliding) =>
                {
                    Assert.IsFalse(func.Invoke());
                    Assert.AreEqual(expectedId, key);
                }).Returns(Task.FromResult(false));

            bool result = this.provider.ReserveCandidateAsync(new EnvironmentCandidate(node: expectedId), TimeSpan.Zero, CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsTrue(result);

            this.localCache.Verify(c => c.AddAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<Func<bool>>(), It.IsAny<bool>()), Times.Once());
        }

        [Test]
        public void ReserveCandidateAsyncReturnsExpectedResultWhenNodeIsNotInCacheAndExceptionOccurs()
        {
            this.localCache.Setup(c => c.Contains(It.IsAny<string>())).Returns(false);
            this.localCache.Setup(c => c.AddAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<Func<bool>>(), It.IsAny<bool>()))
                .Throws(new KeyNotFoundException());

            bool result = this.provider.ReserveCandidateAsync(new EnvironmentCandidate(node: Guid.NewGuid().ToString()), TimeSpan.Zero, CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsFalse(result);

            this.localCache.Verify(c => c.AddAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<Func<bool>>(), It.IsAny<bool>()), Times.Once());
        }

        [Test]
        public void ReserveCandidateAsyncReturnsExpectedReponseWhenCancellationIsRequested()
        {
            using (CancellationTokenSource source = new CancellationTokenSource())
            {
                CancellationToken token = source.Token;
                source.Cancel();
                bool result = this.provider.ReserveCandidateAsync(new EnvironmentCandidate(), TimeSpan.Zero, token).GetAwaiter().GetResult();
                Assert.IsFalse(result);
            }
        }

        private DataTable GenerateValidDataTable(int rowCount)
        {
            DataTable table = new DataTable();
            table.Columns.Add(ProviderConstants.NodeId);
            table.Columns.Add(ProviderConstants.Rack);
            table.Columns.Add(ProviderConstants.Region);
            table.Columns.Add(ProviderConstants.ClusterId);
            table.Columns.Add(ProviderConstants.MachinePoolName);

            for (int i = 0; i < rowCount; i++)
            {
                DataRow row = table.NewRow();
                row[ProviderConstants.NodeId] = Guid.NewGuid().ToString();
                row[ProviderConstants.Rack] = Guid.NewGuid().ToString();
                row[ProviderConstants.Region] = Guid.NewGuid().ToString();
                row[ProviderConstants.ClusterId] = Guid.NewGuid().ToString();
                row[ProviderConstants.MachinePoolName] = Guid.NewGuid().ToString();
                table.Rows.Add(row);
            }

            return table;
        }
    }
}
