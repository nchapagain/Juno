namespace Juno.DataManagement.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;

    /// <summary>
    /// Extension methods for entity data objects used in Cosmos
    /// data stores.
    /// </summary>
    public static class EntityExtensions
    {
        /// <summary>
        /// Creates an <see cref="ExperimentStepInstance"/> from the properties defined in the
        /// experiment step table entity.
        /// </summary>
        /// <param name="entity">The experiment step table entity.</param>
        /// <returns>
        /// A <see cref="ExperimentStepInstance"/> that contains the properties defined
        /// in the table entity.
        /// </returns>
        internal static ExperimentStepInstance ToStep(this ExperimentStepTableEntity entity)
        {
            entity.ThrowIfNull(nameof(entity));
            entity.ThrowIfInvalid(
                nameof(entity),
                (e) => !string.IsNullOrWhiteSpace(entity.PartitionKey),
                "Invalid experiment step table entity definition. The entity definition must have a partition key defined.");

            entity.ThrowIfInvalid(
                nameof(entity),
                (e) => !string.IsNullOrWhiteSpace(entity.RowKey),
                "Invalid experiment step table entity definition. The entity definition must have a row key defined.");

            ExperimentStepInstance step = new ExperimentStepInstance(
                entity.Id,
                entity.ExperimentId,
                entity.ExperimentGroup,
                (SupportedStepType)Enum.Parse(typeof(SupportedStepType), entity.StepType, true),
                (ExecutionStatus)Enum.Parse(typeof(ExecutionStatus), entity.Status, true),
                entity.Sequence,
                entity.Attempts,
                entity.Definition.FromJson<ExperimentComponent>(),
                // We introduced a 'Created' property after we had experiment step data in the system. In order to support
                // backwards compatibility, we are setting it to a value that is relevant for that previous data. Cosmos Table
                // will set the value to DateTime.UtcNow if the data in the table does not include a 'Created' column.
                created: entity.Created > entity.Timestamp.DateTime ? entity.Timestamp.DateTime : entity.Created,
                lastModified: entity.Timestamp.DateTime,
                agentId: entity.AgentId,
                parentStepId: entity.ParentStepId);

            if (!string.IsNullOrWhiteSpace(entity.ETag))
            {
                step.SetETag(entity.ETag);
            }

            if (!string.IsNullOrWhiteSpace(entity.StartTime))
            {
                step.StartTime = DateTime.Parse(entity.StartTime, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
                    .ToUniversalTime();
            }

            if (!string.IsNullOrWhiteSpace(entity.EndTime))
            {
                step.EndTime = DateTime.Parse(entity.EndTime, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
                    .ToUniversalTime();
            }

            if (!string.IsNullOrWhiteSpace(entity.Error))
            {
                try
                {
                    step.SetError(entity.Error.FromJson<ExperimentException>());
                }
                catch
                {
                    step.SetError(new ExperimentException(entity.Error));
                }
            }

            return step;
        }

        /// <summary>
        /// Creates an <see cref="AgentHeartbeatInstance"/> instance from the properties defined in the
        /// agent heartbeat table entity.
        /// </summary>
        /// <param name="entity">The agent heartbeat table entity.</param>
        /// <returns>
        /// A <see cref="AgentHeartbeatInstance"/> that contains the properties defined
        /// in the table entity.
        /// </returns>
        internal static AgentHeartbeatInstance ToHeartbeat(this AgentHeartbeatTableEntity entity)
        {
            entity.ThrowIfNull(nameof(entity));
            entity.ThrowIfInvalid(
                nameof(entity),
                (e) => !string.IsNullOrWhiteSpace(entity.PartitionKey),
                "Invalid heartbeat table entity definition. The entity definition must have a partition key defined.");

            entity.ThrowIfInvalid(
                nameof(entity),
                (e) => !string.IsNullOrWhiteSpace(entity.RowKey),
                "Invalid heartbeat table entity definition. The entity definition must have a row key defined.");

            var heartbeatInstance = new AgentHeartbeatInstance(
                entity.Id, 
                entity.AgentId, 
                (AgentHeartbeatStatus)Enum.Parse(typeof(AgentHeartbeatStatus), entity.Status, true),
                (AgentType)Enum.Parse(typeof(AgentType), entity.AgentType, true),
                // We introduced a 'Created' property after we had experiment step data in the system. In order to support
                // backwards compatibility, we are setting it to a value that is relevant for that previous data. Cosmos Table
                // will set the value to DateTime.UtcNow if the data in the table does not include a 'Created' column.
                created: entity.Created > entity.Timestamp.DateTime ? entity.Timestamp.DateTime : entity.Created,
                entity.LastModified,
                entity.Message);

            if (!string.IsNullOrWhiteSpace(entity.ETag))
            {
                heartbeatInstance.SetETag(entity.ETag);
            }

            return heartbeatInstance;
        }

        /// <summary>
        /// Creates a Cosmos table entity from the properties defined in the
        /// experiment step.
        /// </summary>
        /// <param name="step">The experiment step.</param>
        /// <returns>
        /// A <see cref="ExperimentStepTableEntity"/> that can be used to store the experiment
        /// step in Cosmos Table.
        /// </returns>
        internal static ExperimentStepTableEntity ToTableEntity(this ExperimentStepInstance step)
        {
            step.ThrowIfNull(nameof(step));

            ExperimentStepTableEntity entity = new ExperimentStepTableEntity
            {
                Name = step.Definition.Name,
                ExperimentGroup = step.ExperimentGroup,
                StepType = step.StepType.ToString(),
                Status = step.Status.ToString(),
                Sequence = step.Sequence,
                Attempts = step.Attempts,
                Created = step.Created,
                Definition = step.Definition.ToJson(),
                PartitionKey = step.ExperimentId,
                RowKey = step.Id,
                ExperimentId = step.ExperimentId,
                AgentId = step.AgentId,
                ParentStepId = step.ParentStepId
            };

            if (step.StartTime != null)
            {
                entity.StartTime = step.StartTime.Value.ToString("o");
            }

            if (step.EndTime != null)
            {
                entity.EndTime = step.EndTime.Value.ToString("o");
            }

            Exception error = step.GetError();
            if (error != null)
            {
                entity.Error = error.ToJson();
            }

            string eTag = step.GetETag();
            if (!string.IsNullOrWhiteSpace(eTag))
            {
                entity.ETag = eTag;
            }

            return entity;
        }

        /// <summary>
        /// Creates a Cosmos table entity from the properties defined in the <see cref="AgentHeartbeatInstance"/>
        /// </summary>
        /// <param name="agentHeartbeat">The agent hearbeat instance.</param>
        /// <returns>
        /// A <see cref="AgentHeartbeatTableEntity"/> that can be used to store agent heartbeat in cosmos table
        /// </returns>
        internal static AgentHeartbeatTableEntity ToTableEntity(this AgentHeartbeatInstance agentHeartbeat)
        {
            agentHeartbeat.ThrowIfNull(nameof(agentHeartbeat));

            AgentHeartbeatTableEntity entity = new AgentHeartbeatTableEntity
            {
                AgentId = agentHeartbeat.AgentId,
                Status = agentHeartbeat.Status.ToString(),
                Created = agentHeartbeat.Created,
                PartitionKey = new AgentIdentification(agentHeartbeat.AgentId).ClusterName,
                RowKey = agentHeartbeat.Id,
                AgentType = agentHeartbeat.AgentType.ToString()
            };

            if (agentHeartbeat.Message != null)
            {
                entity.Message = agentHeartbeat.Message;
            }

            string eTag = agentHeartbeat.GetETag();
            if (!string.IsNullOrWhiteSpace(eTag))
            {
                entity.ETag = eTag;
            }

            return entity;
        }

        /// <summary>
        /// Transfoms a TargetGoalTableEntity to a TargetGoalTrigger
        /// </summary>
        /// <param name="entity">The cosmos entity representation of the target goal.</param>
        /// <returns>The scheduler internal representation of a target goal.</returns>
        internal static TargetGoalTrigger ToTargetGoalTrigger(this TargetGoalTableEntity entity)
        {
            entity.ThrowIfNull(nameof(entity));
            entity.ThrowIfInvalid(
                nameof(entity),
                (e) => !string.IsNullOrWhiteSpace(entity.PartitionKey),
                "Invalid target goal table entity definition. The entity definition must have a partition key defined.");

            entity.ThrowIfInvalid(
                nameof(entity),
                (e) => !string.IsNullOrWhiteSpace(entity.RowKey),
                "Invalid target goal table entity definition. The entity definition must have a row key defined.");

            TargetGoalTrigger result = new TargetGoalTrigger(
                entity.Id,
                entity.ExecutionGoal,
                entity.RowKey,
                entity.CronExpression,
                entity.Enabled,
                entity.ExperimentName,
                entity.TeamName,
                entity.PartitionKey,
                // We introduced a 'Created' property after we had experiment step data in the system. In order to support
                // backwards compatibility, we are setting it to a value that is relevant for that previous data. Cosmos Table
                // will set the value to DateTime.UtcNow if the data in the table does not include a 'Created' column.
                created: entity.Created > entity.Timestamp.DateTime ? entity.Timestamp.DateTime : entity.Created,
                entity.LastModified);

            if (!string.IsNullOrWhiteSpace(entity.ETag))
            {
                result.SetETag(entity.ETag);
            }

            return result;
        }

        /// <summary>
        /// Transforms a TargetGoalTrigger to a TargetGoalTableEntity
        /// </summary>
        /// <param name="trigger">The scheduler internal representation of a target goal.</param>
        /// <returns>The cosmos entity representation of the target goal.</returns>
        internal static TargetGoalTableEntity ToTargetGoalTableEntity(this TargetGoalTrigger trigger)
        {
            trigger.ThrowIfNull(nameof(trigger));

            TargetGoalTableEntity entity = new TargetGoalTableEntity()
            { 
                PartitionKey = trigger.Version,
                RowKey = trigger.TargetGoal,
                CronExpression = trigger.CronExpression,
                Enabled = trigger.Enabled,
                ExecutionGoal = trigger.ExecutionGoal,
                ExperimentName = trigger.ExperimentName,
                TeamName = trigger.TeamName
            };

            string eTag = trigger.GetETag();
            if (!string.IsNullOrWhiteSpace(eTag))
            {
                entity.ETag = eTag;
            }

            return entity;
        }

        internal static TargetGoalTableEntity ToTableEntity(this Goal targetGoal, GoalBasedSchedule executionGoal)
        {
            executionGoal.ThrowIfNull(nameof(executionGoal));

            Precondition timerTrigger = targetGoal.Preconditions.First(precondition => precondition.Type.Equals(ContractExtension.TimerTriggerType, StringComparison.OrdinalIgnoreCase));
            string cronExpression = timerTrigger.Parameters.GetValue<string>(ContractExtension.CronExpression);

            return new TargetGoalTableEntity()
            {
                PartitionKey = executionGoal.Version,
                RowKey = targetGoal.Name,
                Id = targetGoal.Name,
                CronExpression = cronExpression,
                Enabled = executionGoal.Enabled,
                ExperimentName = executionGoal.ExperimentName,
                TeamName = executionGoal.TeamName,
                ExecutionGoal = executionGoal.ExecutionGoalId
            };
        }
    }
}
