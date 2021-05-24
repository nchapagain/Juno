namespace Juno
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.IO;
    using System.Reflection;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.EnvironmentSelection;
    using Juno.Extensions.AspNetCore;
    using Microsoft.Azure.CRC.Subscriptions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;

    /// <summary>
    /// Provides common mock dependencies used to test Environment Selection Service
    /// </summary>
    public class EnvironmentFilterFixture : FixtureDependencies
    {
        /// <summary>
        /// Initializes a new instance of <see cref="EnvironmentFilter"/>
        /// </summary>
        public EnvironmentFilterFixture(bool integration = true)
            : base()
        {
            this.Setup(integration);
        }

        /// <summary>
        /// A mock Environment Fiilter
        /// </summary>
        public EnvironmentFilter Filter { get; }

        /// <summary>
        /// Mock provider cache
        /// </summary>
        public Mock<IMemoryCache<IEnumerable<string>>> MockCache { get; private set; }

        /// <summary>
        /// Mock Subscription Filter cache.
        /// </summary>
        public Mock<IMemoryCache<IDictionary<string, EnvironmentCandidate>>> SubscriptionFilterCache { get; private set; }

        /// <summary>
        /// Default values for Integration tests
        /// </summary>
        public EnvironmentSelectionSettings SelectionSettings { get; private set; }

        /// <summary>
        /// Mock Cacheing functions
        /// </summary>
        public Mock<ICachingFunctions> MockFunctions { get; private set; }

        /// <summary>
        /// Mock Service Limit Client
        /// </summary>
        public Mock<IServiceLimitClient> MockServiceLimitClient { get; private set; }

        /// <summary>
        /// Wrap Cacheing Functions in a mock to offer traceability.
        /// </summary>
        public void SetUpMockFunctions()
        {
            ICachingFunctions functions = new CachingFunctions(NullLogger.Instance);

            this.MockFunctions.Setup(func => func.GenerateCacheKeys(It.IsAny<EnvironmentFilter>(), It.IsAny<EventContext>()))
                .Returns((EnvironmentFilter filter, EventContext context) => functions.GenerateCacheKeys(filter, context));

            this.MockFunctions.Setup(func => func.RetrieveCacheHitsAsync(It.IsAny<IMemoryCache<IEnumerable<string>>>(), It.IsAny<string>(), It.IsAny<IEnumerable<ProviderCacheKey>>(), It.IsAny<EventContext>()))
                .Returns((IMemoryCache<IEnumerable<string>> cache, string dictKey, IEnumerable<ProviderCacheKey> keys, EventContext context) => functions.RetrieveCacheHitsAsync(cache, dictKey, keys, context));

            this.MockFunctions.Setup(func => func.OverwriteFilterParameters(It.IsAny<EnvironmentFilter>(), It.IsAny<IEnumerable<ProviderCacheKey>>(), It.IsAny<EventContext>()))
                .Callback<EnvironmentFilter, IEnumerable<ProviderCacheKey>, EventContext>((filter, keys, context) => functions.OverwriteFilterParameters(filter, keys, context));

            this.MockFunctions.Setup(func => func.GenerateCacheKey(It.IsAny<DataRow>(), It.IsAny<IEnumerable<KustoColumnAttribute>>()))
                .Returns((DataRow row, IEnumerable<KustoColumnAttribute> attributes) => functions.GenerateCacheKey(row, attributes));

            this.MockFunctions.Setup(func => func.GenerateCacheValue(It.IsAny<EnvironmentCandidate>()))
                .Returns((EnvironmentCandidate candidate) => functions.GenerateCacheValue(candidate));

            this.MockFunctions.Setup(func => func.PopulateCacheAsync(It.IsAny<IMemoryCache<IEnumerable<string>>>(), It.IsAny<IDictionary<ProviderCacheKey, IList<string>>>(), It.IsAny<TimeSpan>(), It.IsAny<EventContext>()))
                .Returns((IMemoryCache<IEnumerable<string>> cache, IDictionary<ProviderCacheKey, IList<string>> entries, TimeSpan ttl, EventContext context) => functions.PopulateCacheAsync(cache, entries, ttl, context));

            // Only called directly from VM Sku filter
            this.MockFunctions.Setup(func => func.MapOnToEnvironmentCandidate(It.IsAny<ProviderCacheKey>(), It.IsAny<string>()))
                .Returns((ProviderCacheKey key, string value) => functions.MapOnToEnvironmentCandidate(key, value));
        }

        /// <summary>
        /// Set up default behvior for commonly used methods for Subscription Filter cache.
        /// </summary>
        public void SetupMockSubscriptionCacheBehavior()
        {
            this.SubscriptionFilterCache.Setup(cache => cache.GetOrAddAsync(
                It.IsAny<string>(), 
                It.IsAny<TimeSpan>(), 
                It.IsAny<Func<Task<IDictionary<string, EnvironmentCandidate>>>>(), 
                It.IsAny<bool>()))
                .Returns((string key, TimeSpan ttl, Func<Task<IDictionary<string, EnvironmentCandidate>>> func, bool sliding) => func.Invoke());

            this.SubscriptionFilterCache.Setup(cache => cache.GetOrAddAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<Func<IDictionary<string, EnvironmentCandidate>>>(),
                It.IsAny<bool>()))
                .ReturnsAsync((string key, TimeSpan ttl, Func<IDictionary<string, EnvironmentCandidate>> func, bool sliding) => func.Invoke());
        }

        /// <summary>
        /// Verify Post kusto call/ pre cache execute async
        /// </summary>
        /// <param name="candidates">Number of candidates returned from Execute Async</param>
        public void VerifyExecuteAsync(int candidates)
        {
            this.MockFunctions.Verify(func => func.GenerateCacheKeys(It.IsAny<EnvironmentFilter>(), It.IsAny<EventContext>()), Times.Once());
            this.MockFunctions.Verify(func => func.RetrieveCacheHitsAsync(It.IsAny<IMemoryCache<IEnumerable<string>>>(), It.IsAny<string>(), It.IsAny<IEnumerable<ProviderCacheKey>>(), It.IsAny<EventContext>()), Times.Never());
            this.MockFunctions.Verify(func => func.OverwriteFilterParameters(It.IsAny<EnvironmentFilter>(), It.IsAny<IEnumerable<ProviderCacheKey>>(), It.IsAny<EventContext>()), Times.Once());
            this.MockFunctions.Verify(func => func.GenerateCacheKey(It.IsAny<DataRow>(), It.IsAny<IEnumerable<KustoColumnAttribute>>()), Times.Exactly(candidates));
            this.MockFunctions.Verify(func => func.GenerateCacheValue(It.IsAny<EnvironmentCandidate>()), Times.Exactly(candidates));
            this.MockFunctions.Verify(func => func.PopulateCacheAsync(It.IsAny<IMemoryCache<IEnumerable<string>>>(), It.IsAny<IDictionary<ProviderCacheKey, IList<string>>>(), It.IsAny<TimeSpan>(), It.IsAny<EventContext>()), Times.Once());
        }

        /// <summary>
        /// Verify post cache execute async
        /// </summary>
        /// <param name="candidates"></param>
        public void VerifyCacheExecuteAsync(int candidates)
        {
            this.MockFunctions.Verify(func => func.GenerateCacheKeys(It.IsAny<EnvironmentFilter>(), It.IsAny<EventContext>()), Times.Exactly(2));
            this.MockFunctions.Verify(func => func.RetrieveCacheHitsAsync(It.IsAny<IMemoryCache<IEnumerable<string>>>(), It.IsAny<string>(), It.IsAny<IEnumerable<ProviderCacheKey>>(), It.IsAny<EventContext>()), Times.Once());
            this.MockFunctions.Verify(func => func.OverwriteFilterParameters(It.IsAny<EnvironmentFilter>(), It.IsAny<IEnumerable<ProviderCacheKey>>(), It.IsAny<EventContext>()), Times.Once());
            this.MockFunctions.Verify(func => func.GenerateCacheKey(It.IsAny<DataRow>(), It.IsAny<IEnumerable<KustoColumnAttribute>>()), Times.Exactly(candidates));
            this.MockFunctions.Verify(func => func.GenerateCacheValue(It.IsAny<EnvironmentCandidate>()), Times.Exactly(candidates));
            this.MockFunctions.Verify(func => func.PopulateCacheAsync(It.IsAny<IMemoryCache<IEnumerable<string>>>(), It.IsAny<IDictionary<ProviderCacheKey, IList<string>>>(), It.IsAny<TimeSpan>(), It.IsAny<EventContext>()), Times.Once());
        }

        private void Setup(bool integration)
        {
            this.SetupEnvironmentSelectionMocks();

            this.MockCache = new Mock<IMemoryCache<IEnumerable<string>>>();
            this.SubscriptionFilterCache = new Mock<IMemoryCache<IDictionary<string, EnvironmentCandidate>>>();
            this.SetupMockSubscriptionCacheBehavior();
            this.MockFunctions = new Mock<ICachingFunctions>();
            this.MockServiceLimitClient = new Mock<IServiceLimitClient>();
            this.Services.AddSingleton<IServiceLimitClient>(this.MockServiceLimitClient.Object);

            if (integration)
            {
                string environmentSettingsPath = Path.Combine(
                    Path.GetDirectoryName(Assembly.GetAssembly(typeof(EnvironmentFilterFixture)).Location),
                    @"Configuration\environmentselection.testsettings.json");
                IConfiguration environmentSettingsConfiguration = new ConfigurationBuilder()
                    .AddJsonFile(environmentSettingsPath)
                    .Build();

                this.SelectionSettings = new EnvironmentSelectionSettings();
                environmentSettingsConfiguration.Bind(nameof(EnvironmentSelectionSettings), this.SelectionSettings);
            }
        }
    }
}
