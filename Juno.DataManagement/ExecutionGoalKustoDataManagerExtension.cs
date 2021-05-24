namespace Juno.DataManagement
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using NCrontab;

    /// <summary>
    /// Extension Methods to Parse Responses from Telemetry
    /// </summary>
    public static class ExecutionGoalKustoDataManagerExtension
    {
        /// <summary>
        /// Method to Parse Kusto Response to meaningful LeakedResources
        /// </summary>
        internal static IList<ExperimentInstanceStatus> ParseExperimentInstanceStatus(this DataTable dataTable)
        {
            IList<ExperimentInstanceStatus> experimentStatuses = new List<ExperimentInstanceStatus>();

            if (dataTable?.Rows != null)
            {
                foreach (DataRow row in dataTable.Rows)
                {
                    if (!DateTime.TryParse((string)row[KustoColumns.ExperimentStartTime], out DateTime experimentStartTime))
                    {
                        throw new FormatException("Unable to parse experimentStartTime from Kusto Response");
                    }

                    if (!DateTime.TryParse((string)row[KustoColumns.LastIngestionTime], out DateTime lastIngestionTime))
                    {
                        throw new FormatException("Unable to parse lastIngestionTime from Kusto Response");
                    }

                    bool impactTypeFound = Enum.TryParse((string)row[KustoColumns.ImpactType], true, out ImpactType impactType);
                    bool experimentStatusFound = Enum.TryParse((string)row[KustoColumns.ExperimentStatus], true, out ExperimentStatus experimentStatus);

                    ExperimentInstanceStatus experimentInstanceStatus = new ExperimentInstanceStatus(
                    experimentId: (string)row[KustoColumns.ExperimentId],
                    experimentName: (string)row[KustoColumns.ExperimentName],
                    experimentStatus: experimentStatusFound ? experimentStatus : ExperimentStatus.Failed, // if Experiment Status can not be parsed, mark as Failed
                    environment: (string)row[KustoColumns.Environment],
                    executionGoal: (string)row[KustoColumns.ExecutionGoal],
                    targetGoal: (string)row[KustoColumns.TargetGoal],
                    impactType: impactTypeFound ? impactType : ImpactType.Impactful, // if Impact Type can not be parsed, mark as Impactful
                    experimentStartTime: experimentStartTime,
                    lastIngestionTime: lastIngestionTime);

                    experimentStatuses.Add(experimentInstanceStatus);
                }
            }

            return experimentStatuses;
        }

        internal static IList<TargetGoalTimeline> ParseExecutionGoalTimeline(this DataTable dataTable, Goal targetGoal, string executionGoalId, string experimentName, string teamName, string environment)
        {
            IList<TargetGoalTimeline> targetGoalTimelines = new List<TargetGoalTimeline>();
            string cronExpression = string.Empty;
            int targetExperimentInstances = -1;

            foreach (Precondition precondition in targetGoal.Preconditions)
            {
                if (precondition.Type == ContractExtension.TimerTriggerType)
                {
                    cronExpression = precondition.Parameters.GetValue<string>(ContractExtension.CronExpression);
                }

                if (precondition.Type == ContractExtension.SuccessfulExperimentsProvider)
                {
                    targetExperimentInstances = precondition.Parameters.GetValue<int>(ContractExtension.TargetExperimentInstances);
                }

                if (precondition.Type == ContractExtension.InProgressExperimentsProvider)
                {
                    targetExperimentInstances = precondition.Parameters.GetValue<int>(ContractExtension.TargetExperimentInstances);
                }
            }

            if (string.IsNullOrEmpty(cronExpression) || targetExperimentInstances == -1)
            {
                IEnumerable<string> preconditionProviders = targetGoal.Preconditions.Select(x => x.Type);
                IEnumerable<string> missingPrecondition = new List<string>()
                {
                    ContractExtension.TimerTriggerType,
                    ContractExtension.SuccessfulExperimentsProvider,
                    ContractExtension.InProgressExperimentsProvider
                }.Except(preconditionProviders);

                throw new ExperimentException($"Preconditon with type '{missingPrecondition.ToJson()}' is not defined for target goal '{targetGoal.Name}'.");
            }

            TargetGoalTimeline targetGoalTimelineWithoutKustoData = new TargetGoalTimeline(
                targetGoal: targetGoal.Name,
                executionGoalId: executionGoalId,
                experimentName: experimentName,
                environment: environment,
                teamName: teamName,
                targetExperimentInstances: targetExperimentInstances,
                successfulExperimentInstances: 0,
                lastModifiedTime: DateTime.MinValue,
                status: ExperimentStatus.Pending,
                estimatedTimeOfCompletion: TargetGoalTimelineExtension.CalculateEstimatedCompletionTime(cronExpression, targetExperimentInstances, 0));

            if (dataTable?.Rows != null)
            {
                foreach (DataRow row in dataTable.Rows)
                {
                    string executionGoal = (string)row[KustoColumns.ExecutionGoal];
                    string nameOfExperiment = (string)row[KustoColumns.ExperimentName];
                    string nameOfTeam = (string)row[KustoColumns.TeamName];

                    // Verifying telemetry is of right configuration
                    if (!(executionGoalId.Equals(executionGoal, StringComparison.OrdinalIgnoreCase)
                        || nameOfExperiment.Equals(experimentName, StringComparison.OrdinalIgnoreCase)
                        || nameOfTeam.Equals(teamName, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    if (!DateTime.TryParse((string)row[KustoColumns.LastIngestionTime], out DateTime lastIngestionTime))
                    {
                        throw new FormatException("Unable to parse lastIngestionTime from Kusto Response");
                    }

                    int successfulExperimentInstances = Convert.ToInt32((string)row[KustoColumns.SucceededExp]);
                    bool targetGoalAccomplished = successfulExperimentInstances >= targetExperimentInstances;

                    TargetGoalTimeline targetGoalTimelineWithKustoData = new TargetGoalTimeline(
                        targetGoal: targetGoal.Name,
                        executionGoalId: executionGoalId,
                        experimentName: experimentName,
                        environment: environment,
                        teamName: teamName,
                        targetExperimentInstances: targetExperimentInstances,
                        successfulExperimentInstances: successfulExperimentInstances,
                        lastModifiedTime: lastIngestionTime,
                        status: targetGoalAccomplished ? ExperimentStatus.Succeeded : ExperimentStatus.InProgress,
                        estimatedTimeOfCompletion: TargetGoalTimelineExtension.CalculateEstimatedCompletionTime(cronExpression, targetExperimentInstances, successfulExperimentInstances));
                    
                    targetGoalTimelines.Add(targetGoalTimelineWithKustoData);
                }
            }
            else
            {
                targetGoalTimelines.Add(targetGoalTimelineWithoutKustoData);
            }

            return targetGoalTimelines;
        }

        private class KustoColumns
        {
            internal const string TeamName = "teamName";
            internal const string ExperimentStartTime = "experimentStartTime";
            internal const string LastIngestionTime = "lastIngestionTime";
            internal const string ExperimentId = "experimentId";
            internal const string ExperimentName = "experimentName";
            internal const string ExperimentStatus = "experimentStatus";
            internal const string Environment = "environment";
            internal const string ExecutionGoal = "executionGoal";
            internal const string TargetGoal = "targetGoal";
            internal const string ImpactType = "impactType";
            internal const string SucceededExp = "SucceededExp";
        }
    }
}
