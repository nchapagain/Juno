namespace Juno.DataManagement
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Extensions.Telemetry;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Repository;
    using Microsoft.Azure.CRC.Repository.Cosmos;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;

    /// <summary>
    /// Provides methods to manage analysis cache.
    /// </summary>
    public class AnalysisCacheManager : IAnalysisCacheManager
    {
        /// <summary>
        /// Initializes and instance of the <see cref="AnalysisCacheManager"/> class.
        /// </summary>
        /// <param name="analysisCacheStore">Provides methods to manage analysis JSON documents/instances.</param>
        /// <param name="logger">A logger to use for capturing telemetry data.</param>
        public AnalysisCacheManager(IDocumentStore<CosmosAddress> analysisCacheStore, ILogger logger = null)
        {
            analysisCacheStore.ThrowIfNull(nameof(analysisCacheStore));

            this.AnalysisCacheStore = analysisCacheStore;
            this.Logger = logger ?? NullLogger.Instance;
        }

        /// <summary>
        /// Gets the store for analysis cache.
        /// </summary>
        protected IDocumentStore<CosmosAddress> AnalysisCacheStore { get; }

        /// <summary>
        /// Gets the logger for capturing operation telemetry.
        /// </summary>
        protected ILogger Logger { get; }

        /// <summary>
        /// Returns a collection of Business Signals for Juno experiments.
        /// </summary>
        /// <param name="query">A Cosmos SQL API query to retrieve Business Signals for Juno experiments.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// A collection of <see cref="BusinessSignal"/> Business Signals for Juno experiments.
        /// </returns>
        public async Task<IEnumerable<BusinessSignal>> GetBusinessSignalsAsync(string query, CancellationToken cancellationToken)
        {
            query.ThrowIfNullOrWhiteSpace(nameof(query));

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(nameof(query), query);

            return await this.Logger.LogTelemetryAsync(EventNames.GetBusinessSignals, telemetryContext, async () =>
            {
                CosmosAddress cosmosAddress = AnalysisCacheAddressFactory.GetAnalysisCacheCosmosAddress();
                return await this.AnalysisCacheStore.QueryDocumentsAsync<BusinessSignal>(cosmosAddress, new QueryFilter(query), cancellationToken).ConfigureDefaults();
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Returns a collection of progress indicators for Juno experiments.
        /// </summary>
        /// <param name="query">A Cosmos SQL API query to retrieve progress indicators for Juno experiments.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// A collection of <see cref="ExperimentProgress"/> progress indicators for Juno experiments.
        /// </returns>
        public async Task<IEnumerable<ExperimentProgress>> GetExperimentsProgressAsync(string query, CancellationToken cancellationToken)
        {
            query.ThrowIfNullOrWhiteSpace(nameof(query));

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(nameof(query), query);

            return await this.Logger.LogTelemetryAsync(EventNames.GetExperimentsProgress, telemetryContext, async () =>
            {
                CosmosAddress cosmosAddress = AnalysisCacheAddressFactory.GetAnalysisCacheCosmosAddress();
                return await this.AnalysisCacheStore.QueryDocumentsAsync<ExperimentProgress>(cosmosAddress, new QueryFilter(query), cancellationToken).ConfigureDefaults();
            }).ConfigureDefaults();
        }

        private static class EventNames
        {
            public static readonly string GetBusinessSignals = EventContext.GetEventName(nameof(AnalysisCacheManager), "GetBusinessSignals");
            public static readonly string GetExperimentsProgress = EventContext.GetEventName(nameof(AnalysisCacheManager), "GetExperimentsProgress");
        }
    }
}