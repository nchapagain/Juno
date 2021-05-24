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
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Repository;
    using Microsoft.Azure.CRC.Repository.Cosmos;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;

    /// <summary>
    /// <see cref="IScheduleTimerDataManager"/>
    /// </summary>
    public class ScheduleTimerDataManager : IScheduleTimerDataManager
    {
        /// <summary>
        /// <see cref="IScheduleTimerDataManager"/>
        /// </summary>
        public ScheduleTimerDataManager(ITableStore<CosmosTableAddress> tableStore, ILogger logger = null)
        {
            tableStore.ThrowIfNull(nameof(tableStore));

            this.TableStore = tableStore;
            this.Logger = logger ?? NullLogger.Instance;
        }

        /// <summary>
        /// Gets the experiment step table store data provider.
        /// </summary>
        protected ITableStore<CosmosTableAddress> TableStore { get; }

        /// <summary>
        /// Gets the logger for capturing operation telemetry.
        /// </summary>
        protected ILogger Logger { get; }

        /// <see cref="IScheduleTimerDataManager.CreateTargetGoalsAsync"/>
        public async Task CreateTargetGoalsAsync(GoalBasedSchedule executionGoal, CancellationToken cancellationToken)
        {
            executionGoal.ThrowIfNull(nameof(executionGoal));

            EventContext telemetryContext = EventContext.Persisted();

            await this.Logger.LogTelemetryAsync($"{nameof(ScheduleTimerDataManager)}.CreateTargetGoals", telemetryContext, async () =>
            {
                IDictionary<CosmosTableAddress, TargetGoalTableEntity> entities = new Dictionary<CosmosTableAddress, TargetGoalTableEntity>();
                foreach (Goal targetGoal in executionGoal.TargetGoals)
                {
                    // Ask if its appropiate to ask the validate method here instead of rewriting the validation criteria.
                    targetGoal.ThrowIfInvalid(nameof(targetGoal), tg =>
                    {
                        Precondition timerTrigger = tg.Preconditions.First((pc) => pc.Type.Equals(ContractExtension.TimerTriggerType, StringComparison.OrdinalIgnoreCase));
                        if (timerTrigger == null)
                        {
                            return false;
                        }

                        return timerTrigger.Parameters.Keys.Any(key => key.Equals(ContractExtension.CronExpression, StringComparison.OrdinalIgnoreCase));
                    });

                    TargetGoalTableEntity entity = targetGoal.ToTableEntity(executionGoal);
                    CosmosTableAddress address = ScheduleAddressFactory.CreateTargetGoalTriggerAddress(executionGoal.Version, entity.RowKey);
                    entities.Add(address, entity);
                }

                await this.TableStore.SaveEntitiesAsync(entities, cancellationToken, false)
                    .ConfigureDefaults();

            }).ConfigureDefaults();
        }

        /// <see cref="IScheduleTimerDataManager.GetTargetGoalTriggersAsync"/>
        public async Task<IEnumerable<TargetGoalTrigger>> GetTargetGoalTriggersAsync(CancellationToken token)
        {
            EventContext telemetryContext = EventContext.Persisted();

            return await this.Logger.LogTelemetryAsync($"{nameof(ScheduleTimerDataManager)}.GetTargetGoalTriggers", telemetryContext, async () =>
            {
                IList<CosmosTableAddress> addresses = ScheduleAddressFactory.CreateAllTargetGoalTriggerAddress();
                List<Task<IEnumerable<TargetGoalTableEntity>>> getTargetGoalTasks = new List<Task<IEnumerable<TargetGoalTableEntity>>>();

                foreach (CosmosTableAddress address in addresses)
                {
                    getTargetGoalTasks.Add(this.TableStore.GetEntitiesAsync<TargetGoalTableEntity>(address, token));
                }

                IEnumerable<TargetGoalTableEntity>[] targetGoalEntitiesArray = await Task.WhenAll(getTargetGoalTasks).ConfigureDefaults();
                IEnumerable<TargetGoalTableEntity> targetGoalEntities = targetGoalEntitiesArray.SelectMany(x => x);

                IList<TargetGoalTrigger> targetGoals = new List<TargetGoalTrigger>();
                foreach (TargetGoalTableEntity entity in targetGoalEntities)
                {
                    targetGoals.Add(entity.ToTargetGoalTrigger());
                }

                targetGoals.ThrowIfNullOrEmpty(nameof(targetGoals));
                telemetryContext.AddContext(nameof(targetGoals), targetGoals);

                return targetGoals;
            }).ConfigureDefaults();
        }

        /// <see cref="IScheduleTimerDataManager.GetTargetGoalTriggerAsync"/>
        public async Task<TargetGoalTrigger> GetTargetGoalTriggerAsync(string version, string rowKey, CancellationToken token)
        {
            rowKey.ThrowIfNullOrWhiteSpace(nameof(rowKey));
            EventContext telemetryContext = EventContext.Persisted();

            return await this.Logger.LogTelemetryAsync($"{nameof(ScheduleTimerDataManager)}.GetTargetGoalTrigger", telemetryContext, async () =>
            {
                CosmosTableAddress address = ScheduleAddressFactory.CreateTargetGoalTriggerAddress(version);

                address.RowKey = rowKey;
                TargetGoalTableEntity targetGoalEntity = await this.TableStore.GetEntityAsync<TargetGoalTableEntity>(address, token)
                    .ConfigureDefaults();

                targetGoalEntity.ThrowIfNull(nameof(targetGoalEntity));

                telemetryContext.AddContext(nameof(targetGoalEntity.ETag), targetGoalEntity.ETag);
                telemetryContext.AddContext(nameof(targetGoalEntity), targetGoalEntity);

                return targetGoalEntity.ToTargetGoalTrigger();
            }).ConfigureDefaults();
        }

        /// <see cref="IScheduleTimerDataManager.UpdateTargetGoalTriggerAsync"/>
        public async Task UpdateTargetGoalTriggerAsync(TargetGoalTrigger trigger, CancellationToken token)
        {
            trigger.ThrowIfNull(nameof(trigger));
            EventContext telemetryContext = EventContext.Persisted();

            await this.Logger.LogTelemetryAsync($"{nameof(ScheduleTimerDataManager)}.UpdateTargetGoalTrigger", telemetryContext, async () =>
            {
                TargetGoalTableEntity entity = trigger.ToTargetGoalTableEntity();
                CosmosTableAddress address = ScheduleAddressFactory.CreateTargetGoalTriggerAddress(entity.PartitionKey, entity.RowKey);

                await this.TableStore.SaveEntityAsync(address, entity, token, replaceIfExists: true)
                    .ConfigureDefaults();

                telemetryContext.AddContext(nameof(entity.ETag), entity.ETag);
                telemetryContext.AddContext(nameof(entity), entity);

            }).ConfigureDefaults();
        }

        /// <inheritdoc/>
        public async Task UpdateTargetGoalTriggersAsync(GoalBasedSchedule executionGoal, CancellationToken token)
        {
            executionGoal.ThrowIfNull(nameof(executionGoal));

            EventContext telemetryContext = EventContext.Persisted();

            await this.Logger.LogTelemetryAsync($"{nameof(ScheduleTimerDataManager)}.CreateTargetGoals", telemetryContext, async () =>
            {
                IEnumerable<TargetGoalTableEntity> originalEntities = await this.TableStore.GetEntitiesAsync<TargetGoalTableEntity>(
                    ScheduleAddressFactory.CreateTargetGoalTriggerAddress(executionGoal.Version), 
                    token).ConfigureDefaults();

                originalEntities = originalEntities.Where(entity => entity.ExecutionGoal == executionGoal.ExecutionGoalId);

                IList<Task> deleteTasks = new List<Task>();
                foreach (TargetGoalTableEntity targetGoal in originalEntities)
                {
                    CosmosTableAddress address = ScheduleAddressFactory.CreateTargetGoalTriggerAddress(executionGoal.Version, targetGoal.RowKey);
                    deleteTasks.Add(this.TableStore.DeleteEntityAsync<TargetGoalTableEntity>(address, token));
                }

                await Task.WhenAll(deleteTasks.ToArray()).ConfigureDefaults();
                IList<TargetGoalTableEntity> newEntities = new List<TargetGoalTableEntity>();
                foreach (Goal targetGoal in executionGoal.TargetGoals)
                {
                    targetGoal.ThrowIfInvalid(nameof(targetGoal), tg =>
                    {
                        Precondition timerTrigger = tg.Preconditions.First((pc) => pc.Type.Equals(ContractExtension.TimerTriggerType, StringComparison.OrdinalIgnoreCase));
                        if (timerTrigger == null)
                        {
                            return false;
                        }

                        return timerTrigger.Parameters.Keys.Any(key => key.Equals(ContractExtension.CronExpression, StringComparison.OrdinalIgnoreCase));
                    });
                    newEntities.Add(targetGoal.ToTableEntity(executionGoal));
                }

                // Create Dictionary and add entitites
                IDictionary<CosmosTableAddress, TargetGoalTableEntity> addDictionry = new Dictionary<CosmosTableAddress, TargetGoalTableEntity>();
                foreach (TargetGoalTableEntity entity in newEntities)
                {
                    addDictionry.Add(ScheduleAddressFactory.CreateTargetGoalTriggerAddress(executionGoal.Version, entity.RowKey), entity);
                }

                if (addDictionry.Any())
                {
                    await this.TableStore.SaveEntitiesAsync(addDictionry, token, false)
                        .ConfigureDefaults();
                }
            }).ConfigureDefaults();
        }

        /// <inheritdoc/>
        public async Task DeleteTargetGoalTriggersAsync(string executionGoalId, CancellationToken token)
        {
            executionGoalId.ThrowIfNull(nameof(executionGoalId));

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(nameof(executionGoalId), executionGoalId);

            await this.Logger.LogTelemetryAsync($"{nameof(ScheduleTimerDataManager)}.DeleteTargetGoals", telemetryContext, async () =>
            {
                IList<CosmosTableAddress> allEntitiesAddress = ScheduleAddressFactory.CreateAllTargetGoalTriggerAddress();
                List<Task<IEnumerable<TargetGoalTableEntity>>> triggersTasks = new List<Task<IEnumerable<TargetGoalTableEntity>>>();
                foreach (CosmosTableAddress entityAddress in allEntitiesAddress)
                {                    
                    triggersTasks.Add(this.TableStore.GetEntitiesAsync<TargetGoalTableEntity>(entityAddress, token));                   
                }

                IEnumerable<TargetGoalTableEntity>[] triggers = await Task.WhenAll(triggersTasks).ConfigureDefaults();
                IEnumerable<TargetGoalTableEntity> triggersList = triggers.SelectMany(x => x);
                IList<TargetGoalTableEntity> targetGoalsDeleted = new List<TargetGoalTableEntity>();
                IList<Task> deleteTasks = new List<Task>();

                foreach (TargetGoalTableEntity trigger in triggersList)
                {
                    if (trigger.ExecutionGoal.Equals(executionGoalId, StringComparison.Ordinal))
                    {
                        targetGoalsDeleted.Add(trigger);
                        CosmosTableAddress address = ScheduleAddressFactory.CreateTargetGoalTriggerAddress(trigger.PartitionKey, trigger.RowKey);
                        deleteTasks.Add(this.TableStore.DeleteEntityAsync<TargetGoalTableEntity>(address, token));
                    }
                }

                telemetryContext.AddContext(nameof(targetGoalsDeleted), targetGoalsDeleted);
                await Task.WhenAll(deleteTasks.ToArray()).ConfigureDefaults();

            }).ConfigureDefaults();
        }
    }
}
