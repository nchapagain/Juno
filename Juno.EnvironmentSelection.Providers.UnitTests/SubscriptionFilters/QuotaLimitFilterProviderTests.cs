namespace Juno.EnvironmentSelection.SubscriptionFilters
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Contracts;
    using Juno.Extensions.AspNetCore;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Subscriptions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class QuotaLimitFilterProviderTests
    {
        private Fixture mockFixture;
        private IEnvironmentSelectionProvider provider;
        private Mock<IServiceLimitClient> mockClient;
        private Mock<IMemoryCache<IDictionary<string, EnvironmentCandidate>>> mockCache;
        private Mock<ISelectionStrategy> mockStrategy;
        private string mockRegion;

        [SetUp]
        public void SetupTests()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupEnvironmentSelectionMocks();
            this.mockClient = new Mock<IServiceLimitClient>();
            this.mockStrategy = new Mock<ISelectionStrategy>();
            IServiceCollection services = new ServiceCollection();
            services.AddSingleton(this.mockStrategy.Object);
            services.AddSingleton<IServiceLimitClient>(this.mockClient.Object);
            this.mockCache = new Mock<IMemoryCache<IDictionary<string, EnvironmentCandidate>>>();
            this.mockCache.Setup(cache => cache.GetOrAddAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<Func<Task<IDictionary<string, EnvironmentCandidate>>>>(), It.IsAny<bool>()))
                .Returns((string key, TimeSpan ttl, Func<Task<IDictionary<string, EnvironmentCandidate>>> retrievalFunction, bool sliding) => retrievalFunction.Invoke());
            services.AddSingleton<IMemoryCache<IDictionary<string, EnvironmentCandidate>>>(this.mockCache.Object);
            this.provider = new QuotaLimitFilterProvider(services, new Mock<IConfiguration>().Object, NullLogger.Instance);
            this.mockRegion = Guid.NewGuid().ToString();
            this.mockClient.Setup(client => client.GetAllSupportedRegionsAsync(It.IsAny<CancellationToken>(), It.IsAny<IList<string>>()))
                .Returns(Task.FromResult(new List<AzureRegion>() { new AzureRegion(Guid.NewGuid().ToString(), this.mockRegion, this.mockRegion) } as IEnumerable<AzureRegion>));
            this.mockStrategy.Setup(s => s.GetEnvironmentCandidates(It.IsAny<IList<ServiceLimitAvailibility>>(), It.IsAny<int>(), It.IsAny<EventContext>()))
                .Returns(new Dictionary<string, EnvironmentCandidate>() { ["subid"] = FixtureExtensions.CreateEnvironmentCandidateInstance() });
        }

        [Test]
        public void ExecuteAsyncThrowsErrorWhenStrategyTypeIsNotValid()
        {
            EnvironmentFilter filter = new EnvironmentFilter(typeof(QuotaLimitFilterProvider).FullName, new Dictionary<string, IConvertible>()
            {
                ["includeVmSku"] = "VMSKU1",
                ["includeSubscription"] = "Anysub",
                ["strategy"] = "Not a valid strategy"
            });

            IServiceCollection services = new ServiceCollection();
            services.AddSingleton<IMemoryCache<IDictionary<string, EnvironmentCandidate>>>(this.mockCache.Object);
            services.AddSingleton<IServiceLimitClient>(this.mockClient.Object);
            IEnvironmentSelectionProvider provider = new QuotaLimitFilterProvider(services, new Mock<IConfiguration>().Object, NullLogger.Instance);

            ServiceLimitAvailibility returnValue = this.mockFixture.Create<ServiceLimitAvailibility>();
            returnValue.Region = this.mockRegion;
            this.mockClient.Setup(client => client.GetServiceLimitAvailabilityForVmSkuAsync(
                It.IsAny<EventContext>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<IList<string>>(),
                It.IsAny<IList<string>>(),
                It.IsAny<IList<string>>(),
                It.IsAny<int>()))
                .Returns(Task.FromResult(new List<ServiceLimitAvailibility>() { returnValue } as IList<ServiceLimitAvailibility>));

            Assert.ThrowsAsync<TypeLoadException>(() => provider.ExecuteAsync(filter, CancellationToken.None));
        }

        [Test]
        public void ExecuteAsyncSubmitsExpectedListOfVmSkus()
        {
            string expectedSkuName = "VMSKU1";
            EnvironmentFilter filter = new EnvironmentFilter(typeof(QuotaLimitFilterProvider).FullName, new Dictionary<string, IConvertible>()
            {
                ["includeVmSku"] = "VMSKU1",
                ["includeSubscription"] = "Anysub"
            });

            ServiceLimitAvailibility returnValue = this.mockFixture.Create<ServiceLimitAvailibility>();
            returnValue.Region = this.mockRegion;
            returnValue.Limit = 1000;
            returnValue.Usage = 0;

            this.mockClient.Setup(client => client.GetServiceLimitAvailabilityForVmSkuAsync(
                It.IsAny<EventContext>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<IList<string>>(),
                It.IsAny<IList<string>>(),
                It.IsAny<IList<string>>(),
                It.IsAny<int>()))
                .Callback<EventContext, CancellationToken, IList<string>, IList<string>, IList<string>, int>((context, token, subs, regions, skus, quota) =>
                {
                    Assert.IsNotNull(skus);
                    Assert.AreEqual(1, skus.Count);
                    string actualSkuName = skus[0];
                    Assert.AreEqual(expectedSkuName, actualSkuName);
                }).Returns(Task.FromResult(new List<ServiceLimitAvailibility>() { returnValue } as IList<ServiceLimitAvailibility>));

            _ = this.provider.ExecuteAsync(filter, CancellationToken.None).GetAwaiter().GetResult();

            this.mockClient.Verify(
                client => client.GetServiceLimitAvailabilityForVmSkuAsync(
                It.IsAny<EventContext>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<IList<string>>(),
                It.IsAny<IList<string>>(),
                It.IsAny<IList<string>>(),
                It.IsAny<int>()),
                Times.Once());
        }

        [Test]
        public void ExecuteAsyncReturnsExpectedResultFromSelectionStrategy()
        {
            EnvironmentFilter filter = new EnvironmentFilter(typeof(QuotaLimitFilterProvider).FullName, new Dictionary<string, IConvertible>()
            {
                ["includeVmSku"] = "VMSKU1",
                ["includeSubscription"] = "Anysub"
            });

            ServiceLimitAvailibility returnValue = this.mockFixture.Create<ServiceLimitAvailibility>();
            returnValue.Region = this.mockRegion;

            this.mockClient.Setup(client => client.GetServiceLimitAvailabilityForVmSkuAsync(
                It.IsAny<EventContext>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<IList<string>>(),
                It.IsAny<IList<string>>(),
                It.IsAny<IList<string>>(),
                It.IsAny<int>()))
                .Returns(Task.FromResult(new List<ServiceLimitAvailibility>() { returnValue } as IList<ServiceLimitAvailibility>));

            IDictionary<string, EnvironmentCandidate> expectedResult = new Dictionary<string, EnvironmentCandidate>()
            { ["subid"] = this.mockFixture.Create<EnvironmentCandidate>() };

            IDictionary<string, EnvironmentCandidate> actualResult = this.provider.ExecuteAsync(filter, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(expectedResult, actualResult);
        }

        [Test]
        public void ExecuteAsyncReturnsExpectedWhenNoDataIsReturnedFromServiceLimitClient()
        { 
            EnvironmentFilter filter = new EnvironmentFilter(typeof(QuotaLimitFilterProvider).FullName, new Dictionary<string, IConvertible>()
            {
                ["includeVmSku"] = "VMSKU1",
                ["includeSubscription"] = "Anysub"
            });

            this.mockClient.Setup(client => client.GetServiceLimitAvailabilityForVmSkuAsync(
                It.IsAny<EventContext>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<IList<string>>(),
                It.IsAny<IList<string>>(),
                It.IsAny<IList<string>>(),
                It.IsAny<int>()))
                .Returns(Task.FromResult(new List<ServiceLimitAvailibility>() as IList<ServiceLimitAvailibility>));

            IDictionary<string, EnvironmentCandidate> actualResult = this.provider.ExecuteAsync(filter, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsNotNull(actualResult);
            Assert.IsEmpty(actualResult);
        }

        [Test]
        public void ExecuteAsyncPassesCorrectRegionsWhenRegionsAreGiven()
        {
            EnvironmentFilter filter = new EnvironmentFilter(typeof(QuotaLimitFilterProvider).FullName, new Dictionary<string, IConvertible>()
            {
                ["includeVmSku"] = "VMSKU1",
                ["includeSubscription"] = "Anysub",
                ["includeRegion"] = this.mockRegion
            });
            this.mockClient.Setup(client => client.GetServiceLimitAvailabilityForVmSkuAsync(
                It.IsAny<EventContext>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<IList<string>>(),
                It.IsAny<IList<string>>(),
                It.IsAny<IList<string>>(),
                It.IsAny<int>()))
                .Callback<EventContext, CancellationToken, IList<string>, IList<string>, IList<string>, int>((context, token, subs, regions, vms, limit) => 
                {
                    Assert.IsTrue(regions.All(r => r.Equals(this.mockRegion, StringComparison.OrdinalIgnoreCase)));
                })
                .Returns(Task.FromResult(new List<ServiceLimitAvailibility>() as IList<ServiceLimitAvailibility>));

            _ = this.provider.ExecuteAsync(filter, CancellationToken.None).GetAwaiter().GetResult();

            this.mockClient.Verify(
                client => client.GetServiceLimitAvailabilityForVmSkuAsync(
                It.IsAny<EventContext>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<IList<string>>(),
                It.IsAny<IList<string>>(),
                It.IsAny<IList<string>>(),
                It.IsAny<int>()), 
                Times.Once());
        }

        [Test]
        public void ExecuteAsyncThrowsErrorWhenRegionGivenDoesNotMap()
        {
            EnvironmentFilter filter = new EnvironmentFilter(typeof(QuotaLimitFilterProvider).FullName, new Dictionary<string, IConvertible>()
            {
                ["includeVmSku"] = "VMSKU1",
                ["includeSubscription"] = "Anysub",
                ["includeRegion"] = Guid.NewGuid().ToString()
            });

            Assert.ThrowsAsync<ArgumentException>(() => this.provider.ExecuteAsync(filter, CancellationToken.None));
        }

        [Test]
        public void ExecuteAsyncReturnsExpectedResultWhenExcludeRegionIsGiven()
        {
            string expectedRegion = Guid.NewGuid().ToString();
            IEnumerable<AzureRegion> regions = new List<AzureRegion>()
            { new AzureRegion(Guid.NewGuid().ToString(), this.mockRegion, this.mockRegion), new AzureRegion(Guid.NewGuid().ToString(), expectedRegion, expectedRegion) };

            EnvironmentFilter filter = new EnvironmentFilter(typeof(QuotaLimitFilterProvider).FullName, new Dictionary<string, IConvertible>()
            {
                ["includeVmSku"] = "VMSKU1",
                ["includeSubscription"] = "Anysub",
                ["excludeRegion"] = this.mockRegion
            });

            this.mockClient.Setup(client => client.GetAllSupportedRegionsAsync(It.IsAny<CancellationToken>(), It.IsAny<IList<string>>()))
                .Returns(Task.FromResult(regions));

            IList<ServiceLimitAvailibility> limits = new List<ServiceLimitAvailibility>()
            {
                new ServiceLimitAvailibility() { Region = this.mockRegion },
                new ServiceLimitAvailibility() { Region = expectedRegion },
            };

            this.mockStrategy.Setup(s => s.GetEnvironmentCandidates(It.IsAny<IList<ServiceLimitAvailibility>>(), It.IsAny<int>(), It.IsAny<EventContext>()))
                .Returns(new Dictionary<string, EnvironmentCandidate>() { ["subid"] = FixtureExtensions.CreateEnvironmentCandidateInstance() })
                .Callback<IList<ServiceLimitAvailibility>, int, EventContext>((serviceLimits, limit, context) => 
                {
                    Assert.IsTrue(serviceLimits.All(sl => sl.Region.Equals(expectedRegion)), "Service Limits with unexpected region were posted.");
                });

            this.mockClient.Setup(client => client.GetServiceLimitAvailabilityForVmSkuAsync(
                It.IsAny<EventContext>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<IList<string>>(),
                It.IsAny<IList<string>>(),
                It.IsAny<IList<string>>(),
                It.IsAny<int>()))
                .Returns(Task.FromResult(limits));

            _ = this.provider.ExecuteAsync(filter, CancellationToken.None).GetAwaiter().GetResult();

            this.mockStrategy.Verify(s => s.GetEnvironmentCandidates(It.IsAny<IList<ServiceLimitAvailibility>>(), It.IsAny<int>(), It.IsAny<EventContext>()), Times.Once());
        }
    }
}
