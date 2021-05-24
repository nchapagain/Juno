namespace Juno.DataManagement
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Extensions.Telemetry;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Repository;
    using Microsoft.Azure.CRC.Repository.Cosmos;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;

    /// <summary>
    /// <see cref="IExperimentTemplateDataManager"/>
    /// </summary>
    public class ExperimentTemplateDataManager : IExperimentTemplateDataManager
    {
        /// <summary>
        /// <see cref="IExperimentTemplateDataManager"/>
        /// </summary>
        /// <param name="documentStore">Provides methods to manage experiment JSON documents/instances.</param>
        /// <param name="cosmosClient">Provides methods to manage COSMOS data.</param>
        /// <param name="logger">A logger to use for capturing telemetry data.</param>
        public ExperimentTemplateDataManager(IDocumentStore<CosmosAddress> documentStore, CosmosClient cosmosClient = null, ILogger logger = null)
        {
            documentStore.ThrowIfNull(nameof(documentStore));

            this.DocumentStore = documentStore;
            this.CosmosClient = cosmosClient;
            this.Logger = logger ?? NullLogger.Instance;
        }

        /// <summary>
        /// Gets the logger for capturing operation telemetry.
        /// </summary>
        protected ILogger Logger { get; }

        /// <summary>
        /// Gets the logger for capturing operation telemetry.
        /// </summary>
        protected CosmosClient CosmosClient { get; }

        /// <summary>
        /// Gets the experiment document store data provider.
        /// </summary>
        protected IDocumentStore<CosmosAddress> DocumentStore { get; }

        /// <summary>
        /// <see cref="IExperimentTemplateDataManager.CreateExperimentTemplateAsync"/>
        /// </summary>
        public async Task<ExperimentItem> CreateExperimentTemplateAsync(ExperimentItem experiment, string documentId, bool replaceIfExists, CancellationToken cancellationToken)
        {
            documentId.ThrowIfNullOrEmpty(nameof(documentId));
            experiment.ThrowIfNull(nameof(experiment));
            experiment.ThrowIfNull(experiment.Definition.Metadata["teamName"].ToString());

            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(nameof(experiment), experiment);
            return await this.Logger.LogTelemetryAsync($"{nameof(ExperimentTemplateDataManager)}.CreateExperimentTemplate", telemetryContext, async () =>
            {
                experiment.LastModified = DateTime.UtcNow;
                CosmosAddress address = ExperimentTemplateAddressFactory.CreateExperimentTemplateAddress(experiment.Definition.Metadata["teamName"].ToString(), documentId);
                await this.DocumentStore.SaveDocumentAsync<ExperimentItem>(address, experiment, cancellationToken, replaceIfExists)
                    .ConfigureDefaults();

                return experiment;

            }).ConfigureDefaults();
        }

        /// <summary>
        /// <see cref="IExperimentTemplateDataManager.DeleteExperimentTemplateAsync"/>
        /// </summary>
        public async Task<string> DeleteExperimentTemplateAsync(string documentId, string teamName, CancellationToken cancellationToken)
        {
            documentId.ThrowIfNullOrEmpty(nameof(documentId));
            teamName.ThrowIfNullOrEmpty(nameof(teamName));

            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(nameof(documentId), documentId);

            return await this.Logger.LogTelemetryAsync($"{nameof(ExperimentTemplateDataManager)}.DeleteExperimentTemplate", telemetryContext, async () =>
            {
                CosmosAddress address = ExperimentTemplateAddressFactory.CreateExperimentTemplateAddress(teamName, documentId);
                await this.DocumentStore.DeleteDocumentAsync(address, cancellationToken).ConfigureDefaults();
                return address.DocumentId;

            }).ConfigureDefaults();
        }

        /// <summary>
        /// <see cref="IExperimentTemplateDataManager.GetExperimentTemplatesListAsync(CancellationToken)"/>
        /// </summary>
        public async Task<IEnumerable<ExperimentItem>> GetExperimentTemplatesListAsync(CancellationToken cancellationToken)
        {
            EventContext telemetryContext = EventContext.Persisted();
            return await this.Logger.LogTelemetryAsync($"{nameof(ExperimentTemplateDataManager)}.GetExperimentTemplatesList", telemetryContext, async () =>
            {
                List<ExperimentItem> templateList = new List<ExperimentItem>();
                Microsoft.Azure.Cosmos.Container container = this.CosmosClient.GetContainer("experimentDefinitions", "experiments");
                QueryDefinition queryDefinition = new QueryDefinition("SELECT * FROM c");
                FeedIterator<ExperimentItem> feedIterator = container.GetItemQueryIterator<ExperimentItem>(queryDefinition);
                while (feedIterator.HasMoreResults)
                {
                    foreach (var item in await feedIterator.ReadNextAsync().ConfigureDefaults())
                    {
                        templateList.Add(item);
                    }
                }

                return templateList;
            }).ConfigureDefaults();
        }

        /// <summary>
        /// <see cref="IExperimentTemplateDataManager.GetExperimentTemplateAsync"/>
        /// </summary>
        public async Task<ExperimentItem> GetExperimentTemplateAsync(string definitionId, string teamName, CancellationToken cancellationToken, string experimentName = null)
        {
            definitionId.ThrowIfNullOrWhiteSpace(nameof(definitionId));

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(nameof(definitionId), definitionId)
                .AddContext(nameof(teamName), teamName);

            return await this.Logger.LogTelemetryAsync($"{nameof(ExperimentTemplateDataManager)}.GetExperimentTemplate", telemetryContext, async () =>
            {
                CosmosAddress address = ExperimentTemplateAddressFactory.CreateExperimentTemplateAddress(teamName, definitionId);
                ExperimentItem item = null;
                try
                {
                    item = await this.DocumentStore.GetDocumentAsync<ExperimentItem>(address, cancellationToken)
                        .ConfigureDefaults();
                }
                finally
                {
                    telemetryContext.AddContext(item);
                }

                return item;

            }).ConfigureDefaults();
        }

        /// <summary>
        /// <see cref="IExperimentTemplateDataManager.GetExperimentTemplatesListAsync(string, CancellationToken)"/>
        /// </summary>       
        public async Task<IEnumerable<ExperimentItem>> GetExperimentTemplatesListAsync(string teamName, CancellationToken cancellationToken)
        {
            teamName.ThrowIfNull(nameof(teamName));

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(nameof(teamName), teamName);

            return await this.Logger.LogTelemetryAsync($"{nameof(ExperimentTemplateDataManager)}.GetExperimentTemplatesList", telemetryContext, async () =>
            {
                CosmosAddress cosmosAddress = ExperimentTemplateAddressFactory.CreateExperimentTemplateAddress(teamName);
                return await this.DocumentStore.GetDocumentsAsync<ExperimentItem>(cosmosAddress, cancellationToken)
                    .ConfigureDefaults();

            }).ConfigureDefaults();
        }
    }
}
