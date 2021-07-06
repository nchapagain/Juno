namespace Juno.DataManagement
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.DataManagement.Cosmos;
    using Juno.Extensions.Telemetry;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Repository;
    using Microsoft.Azure.CRC.Repository.Cosmos;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;

    /// <summary>
    /// <see cref="IScheduleDataManager"/>
    /// </summary>
    public class ScheduleDataManager : IScheduleDataManager
    {
        /// <summary>
        /// <see cref="IScheduleDataManager"/>
        /// </summary>
        public ScheduleDataManager(IDocumentStore<CosmosAddress> documentStore, ILogger logger = null)
        {
            documentStore.ThrowIfNull(nameof(documentStore));

            this.DocumentStore = documentStore;
            this.Logger = logger ?? NullLogger.Instance;
        }

        /// <summary>
        /// Gets the experiment document store data provider.
        /// </summary>
        protected IDocumentStore<CosmosAddress> DocumentStore { get; }

        /// <summary>
        /// Gets the logger for capturing operation telemetry.
        /// </summary>
        protected ILogger Logger { get; }

        /// <see cref="IScheduleDataManager.CreateExecutionGoalAsync"/>
        public async Task<Item<GoalBasedSchedule>> CreateExecutionGoalAsync(Item<GoalBasedSchedule> executionGoal, CancellationToken cancellationToken)
        {
            executionGoal.ThrowIfNull(nameof(executionGoal));

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(nameof(executionGoal), executionGoal);

            var executionGoalId = executionGoal.Id;

            return await this.Logger.LogTelemetryAsync($"{nameof(ScheduleDataManager)}.CreateExecutionGoal", telemetryContext, async () =>
            {
                Item<GoalBasedSchedule> executionGoalItem = new Item<GoalBasedSchedule>(executionGoalId, executionGoal.Definition);
                CosmosAddress address = ScheduleAddressFactory.CreateExecutionGoalAddress(executionGoal.Definition.TeamName, executionGoal.Id);
                await this.DocumentStore.SaveDocumentAsync(address, executionGoalItem, cancellationToken)
                    .ConfigureDefaults();

                telemetryContext.AddContext(nameof(executionGoalItem), executionGoalItem);
                return executionGoalItem;

            }).ConfigureDefaults();
        }

        /// <inheritdoc/>
        public async Task DeleteExecutionGoalAsync(string executionGoalId, string teamName, CancellationToken cancellationToken)
        {
            executionGoalId.ThrowIfNullOrWhiteSpace(nameof(executionGoalId));
            teamName.ThrowIfNullOrWhiteSpace(nameof(teamName));

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(nameof(executionGoalId), executionGoalId)
                .AddContext(nameof(teamName), teamName);

            await this.Logger.LogTelemetryAsync($"{nameof(ScheduleDataManager)}.DeleteExecutionGoal", telemetryContext, async () =>
            {
                CosmosAddress address = ScheduleAddressFactory.CreateExecutionGoalAddress(teamName, executionGoalId);
                await this.DocumentStore.DeleteDocumentAsync(address, cancellationToken)
                    .ConfigureDefaults();

            }).ConfigureDefaults();
        }

        /// <see cref="IScheduleDataManager.GetExecutionGoalAsync"/>
        public async Task<Item<GoalBasedSchedule>> GetExecutionGoalAsync(string executionGoalId, string teamName, CancellationToken cancellationToken)
        {
            executionGoalId.ThrowIfNullOrWhiteSpace(nameof(executionGoalId));
            teamName.ThrowIfNullOrWhiteSpace(nameof(teamName));

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(nameof(executionGoalId), executionGoalId);

            return await this.Logger.LogTelemetryAsync($"{nameof(ScheduleDataManager)}.GetExecutionGoal", telemetryContext, async () =>
            {
                CosmosAddress address = ScheduleAddressFactory.CreateExecutionGoalAddress(teamName, executionGoalId);
                Item<GoalBasedSchedule> executionGoalItem = await this.DocumentStore.GetDocumentAsync<Item<GoalBasedSchedule>>(address, cancellationToken)
                    .ConfigureDefaults();
                telemetryContext.AddContext(nameof(GoalBasedSchedule), executionGoalItem);
                return executionGoalItem;

            }).ConfigureDefaults();
        }

        /// <see cref="IScheduleDataManager.GetExecutionGoalAsync"/>
        public async Task<IEnumerable<ExecutionGoalSummary>> GetExecutionGoalsInfoAsync(CancellationToken cancellationToken, string teamName = null, string executionGoalId = null)
        {
            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(nameof(teamName), teamName)
                .AddContext(nameof(executionGoalId), executionGoalId);

            return await this.Logger.LogTelemetryAsync($"{nameof(ScheduleDataManager)}.GetExecutionGoalInfo", telemetryContext, async () =>
            {
                IList<ExecutionGoalSummary> templateList = new List<ExecutionGoalSummary>();
                CosmosAddress address = ScheduleAddressFactory.CreateExecutionGoalAddress(teamName, executionGoalId);
                IEnumerable<Item<GoalBasedSchedule>> items = executionGoalId == null
                    ? items = await this.DocumentStore.GetDocumentsAsync<Item<GoalBasedSchedule>>(address, cancellationToken).ConfigureDefaults()
                    : items = new List<Item<GoalBasedSchedule>>() { await this.DocumentStore.GetDocumentAsync<Item<GoalBasedSchedule>>(address, cancellationToken).ConfigureDefaults() };

                foreach (var item in items)
                {
                    GoalBasedSchedule template = item.Definition;
                    templateList.Add(new ExecutionGoalSummary(item.Id, template.Description, template.TeamName, template.GetParametersFromTemplate(), template.Metadata));
                }

                telemetryContext.AddContext(nameof(templateList), templateList);

                return templateList;

            }).ConfigureDefaults();
        }

        /// <see cref="IScheduleDataManager.GetExecutionGoalsAsync"/>
        public async Task<IEnumerable<Item<GoalBasedSchedule>>> GetExecutionGoalsAsync(CancellationToken cancellationToken, string teamName = null)
        {
            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(nameof(teamName), teamName);
            IList<GoalBasedSchedule> executionGoalDefinitions = new List<GoalBasedSchedule>();
            IEnumerable<Item<GoalBasedSchedule>> executionGoalItems = await this.Logger.LogTelemetryAsync($"{nameof(ScheduleDataManager)}.GetExecutionGoals", telemetryContext, async () =>
            {
                CosmosAddress cosmosAddress = ScheduleAddressFactory.CreateExecutionGoalAddress(teamName);
                return await this.DocumentStore.GetDocumentsAsync<Item<GoalBasedSchedule>>(cosmosAddress, cancellationToken)
                    .ConfigureDefaults();

            }).ConfigureDefaults();

            telemetryContext.AddContext(nameof(executionGoalItems), executionGoalItems);
            return executionGoalItems;
        }

        /// <see cref="IScheduleDataManager.UpdateExecutionGoalAsync"/>
        public async Task<Item<GoalBasedSchedule>> UpdateExecutionGoalAsync(Item<GoalBasedSchedule> executionGoal, CancellationToken cancellationToken)
        {
            executionGoal.ThrowIfNull(nameof(executionGoal));

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(nameof(executionGoal), executionGoal);

            return await this.Logger.LogTelemetryAsync($"{nameof(ScheduleDataManager)}.UpdateExecutionGoal", telemetryContext, async () =>
            {
                executionGoal.LastModified = DateTime.UtcNow;
                CosmosAddress address = ScheduleAddressFactory.CreateExecutionGoalAddress(executionGoal.Definition.TeamName, executionGoal.Id);
                Item<GoalBasedSchedule> originalDocument = await this.DocumentStore.GetDocumentAsync<Item<GoalBasedSchedule>>(address, cancellationToken)
                    .ConfigureDefaults();

                originalDocument.ThrowIfNull(nameof(originalDocument));

                Item<GoalBasedSchedule> updatedDocument = new Item<GoalBasedSchedule>(executionGoal.Id, executionGoal.Definition);
                updatedDocument.SetETag(originalDocument.GetETag());
                await this.DocumentStore.SaveDocumentAsync(address, updatedDocument, cancellationToken, true)
                    .ConfigureDefaults();

                telemetryContext.AddContext(nameof(updatedDocument), updatedDocument);
                return updatedDocument;

            }).ConfigureDefaults();
        }

        /// <see cref="IScheduleDataManager.CreateExecutionGoalTemplateAsync"/>
        public async Task<Item<GoalBasedSchedule>> CreateExecutionGoalTemplateAsync(Item<GoalBasedSchedule> executionGoalItem, bool replaceIfExists, CancellationToken cancellationToken)
        {
            executionGoalItem.ThrowIfNull(nameof(executionGoalItem));

            string teamName = executionGoalItem.Definition.TeamName;
            string executionGoalTemplateId = executionGoalItem.Id;

            teamName.ThrowIfNullOrEmpty(nameof(teamName));
            executionGoalTemplateId.ThrowIfNullOrEmpty(nameof(executionGoalTemplateId));

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(nameof(executionGoalItem), executionGoalItem)
                .AddContext(nameof(teamName), teamName)
                .AddContext(nameof(executionGoalTemplateId), executionGoalTemplateId);

            return await this.Logger.LogTelemetryAsync($"{nameof(ScheduleDataManager)}.CreateExecutionGoalTemplate", telemetryContext, async () =>
            {
                executionGoalItem.LastModified = DateTime.UtcNow;
                CosmosAddress cosmosAddress = ScheduleAddressFactory.CreateExecutionGoalTemplateAddress(teamName, executionGoalTemplateId);
                await this.DocumentStore.SaveDocumentAsync(cosmosAddress, executionGoalItem, cancellationToken, replaceIfExists)
                    .ConfigureDefaults();

                telemetryContext.AddContext("updatedDocument", executionGoalItem);
                return executionGoalItem;

            }).ConfigureDefaults();
        }

        /// <see cref="IScheduleDataManager.GetExecutionGoalTemplatesAsync(CancellationToken, string)"/>
        public async Task<IEnumerable<Item<GoalBasedSchedule>>> GetExecutionGoalTemplatesAsync(CancellationToken cancellationToken, string teamName = null)
        {
            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(nameof(teamName), teamName);

            return await this.Logger.LogTelemetryAsync($"{nameof(ScheduleDataManager)}.GetExecutionGoalTemplates", telemetryContext, async () =>
            {
                CosmosAddress cosmosAddress = ScheduleAddressFactory.CreateExecutionGoalTemplateAddress(teamName);
                IEnumerable<Item<GoalBasedSchedule>> executionGoalItems = await this.DocumentStore.GetDocumentsAsync<Item<GoalBasedSchedule>>(cosmosAddress, cancellationToken)
                    .ConfigureDefaults();

                telemetryContext.AddContext(nameof(executionGoalItems), executionGoalItems);
                return executionGoalItems;

            }).ConfigureDefaults();
        }

        /// <inheritdoc />
        public async Task<Item<GoalBasedSchedule>> GetExecutionGoalTemplateAsync(string executionGoalTemplateId, string teamName, CancellationToken cancellationToken)
        {
            executionGoalTemplateId.ThrowIfNullOrWhiteSpace(nameof(executionGoalTemplateId));
            teamName.ThrowIfNullOrWhiteSpace(nameof(teamName));

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(nameof(executionGoalTemplateId), executionGoalTemplateId)
                .AddContext(nameof(teamName), teamName);

            return await this.Logger.LogTelemetryAsync($"{nameof(ScheduleDataManager)}.GetExecutionGoalTemplate", telemetryContext, async () =>
            {
                CosmosAddress address = ScheduleAddressFactory.CreateExecutionGoalTemplateAddress(teamName, executionGoalTemplateId);
                Item<GoalBasedSchedule> goalBasedScheduleItem = await this.DocumentStore.GetDocumentAsync<Item<GoalBasedSchedule>>(address, cancellationToken)
                    .ConfigureDefaults();

                telemetryContext.AddContext(nameof(goalBasedScheduleItem), goalBasedScheduleItem);
                return goalBasedScheduleItem;

            }).ConfigureDefaults();
        }

        /// <inheritdoc/>
        public async Task DeleteExecutionGoalTemplateAsync(string executionGoalId, string teamName, CancellationToken cancellationToken)
        {
            executionGoalId.ThrowIfNullOrWhiteSpace(nameof(executionGoalId));
            teamName.ThrowIfNullOrWhiteSpace(nameof(teamName));

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(nameof(executionGoalId), executionGoalId)
                .AddContext(nameof(teamName), teamName);

            await this.Logger.LogTelemetryAsync($"{nameof(ScheduleDataManager)}.DeleteExecutionGoalTemplate", telemetryContext, async () =>
            {
                CosmosAddress address = ScheduleAddressFactory.CreateExecutionGoalTemplateAddress(teamName, executionGoalId);
                await this.DocumentStore.DeleteDocumentAsync(address, cancellationToken).ConfigureDefaults();
            }).ConfigureDefaults();
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ExecutionGoalSummary>> GetExecutionGoalTemplateInfoAsync(CancellationToken cancellationToken, string teamName = null, string templateId = null)
        {
            EventContext telemetryContext = EventContext.Persisted();

            return await this.Logger.LogTelemetryAsync($"{nameof(ScheduleDataManager)}.GetExecutionGoalMetaInformation", telemetryContext, async () =>
            {
                IList<ExecutionGoalSummary> templateList = new List<ExecutionGoalSummary>();
                CosmosAddress address = ScheduleAddressFactory.CreateExecutionGoalTemplateAddress(teamName, templateId);
                IEnumerable<Item<GoalBasedSchedule>> items = templateId == null
                    ? items = await this.DocumentStore.GetDocumentsAsync<Item<GoalBasedSchedule>>(address, cancellationToken).ConfigureDefaults()
                    : items = new List<Item<GoalBasedSchedule>>() { await this.DocumentStore.GetDocumentAsync<Item<GoalBasedSchedule>>(address, cancellationToken).ConfigureDefaults() };

                foreach (var item in items)
                {
                    GoalBasedSchedule template = item.Definition;
                    templateList.Add(new ExecutionGoalSummary(item.Id, template.Description, template.TeamName, template.GetParametersFromTemplate(), template.Metadata));
                }

                telemetryContext.AddContext(nameof(templateList), templateList);

                return templateList;
            }).ConfigureDefaults();
        }
    }
}
