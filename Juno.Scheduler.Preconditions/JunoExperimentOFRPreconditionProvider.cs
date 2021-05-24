namespace Juno.Scheduler.Preconditions
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.Extensions.Telemetry;
    using Juno.Providers;
    using Juno.Scheduler.Preconditions.Manager;
    using Kusto.Cloud.Platform.Utils;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Class to manage Control Goal: Juno.Scheduler.Goals.Control.TargetGoalOFR
    /// </summary>
    [SupportedParameter(Name = Parameters.ExperimentOFRThreshold, Type = typeof(int), Required = true)]
    public class JunoExperimentGoalOFRPreconditionProvider : PreconditionProvider
    {
        private readonly int startDateExperimentGoalJunoOFR = -7;

        /// <summary>
        /// Initializes a new instance of the<see cref="JunoExperimentGoalOFRPreconditionProvider" />
        /// </summary>
        public JunoExperimentGoalOFRPreconditionProvider(IServiceCollection services)
            : base(services)
        {
            string query = Properties.Resources.ExperimentOFR;
            query.ThrowIfNullOrWhiteSpace(nameof(query));
            this.Query = query;
        }

        private string Query { get; set; }

        private void ReplaceQueryParameters(ScheduleContext scheduleContext, string experimentNames)
        { 
            string environment = EnvironmentSettings.Initialize(scheduleContext.Configuration).Environment;
            this.Query = this.Query.Replace(Constants.QueryStartTime, $"now({this.startDateExperimentGoalJunoOFR}d)", StringComparison.Ordinal);
            this.Query = this.Query.Replace(Constants.ExperimentNameForQuery, $"{experimentNames}", StringComparison.Ordinal);
            this.Query = this.Query.Replace(Constants.Environment, environment, StringComparison.Ordinal);
        }

        /// <inheritdoc/>
        public override Task ConfigureServicesAsync(GoalComponent component, ScheduleContext scheduleContext)
        {
            scheduleContext.ThrowIfNull(nameof(scheduleContext));
            if (!this.Services.TryGetService<IKustoManager>(out IKustoManager kustoManager))
            {
                kustoManager = KustoManager.Instance;
                kustoManager.SetUp(scheduleContext.Configuration);

                this.Services.AddSingleton<IKustoManager>(kustoManager);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Determines if the number of Experiment OFRs exceeds OFR Threshold
        /// </summary>
        /// <param name="component"> Describe Preconditions Scheduler can take, in this case OverallOFRThreshold</param>
        /// <param name="scheduleContext"><see cref="ScheduleContext"/></param>
        /// <param name="telemetryContext"> Describes the telemetry context this provider is running on, for logging</param>
        /// <param name="cancellationToken"> Propagates notification that operations should be canceled.</param>
        /// <returns> Execution Status and Condition Status <see cref="PreconditionResult"/></returns>
        protected override async Task<PreconditionResult> IsConditionSatisfiedAsync(Precondition component, ScheduleContext scheduleContext, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            component.ThrowIfNull(nameof(component));
            scheduleContext.ThrowIfNull(nameof(scheduleContext));
            scheduleContext.ThrowIfInvalid(nameof(scheduleContext), (sc) =>
            {
                if (sc.ExecutionGoal == null)
                {
                    return false;
                }

                return !string.IsNullOrEmpty(sc.ExecutionGoal.ExperimentName);
            });
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            ExecutionStatus executionStatus = ExecutionStatus.InProgress;
            bool conditionSatisfied = false;

            string experimentNames = this.GetExperimentNames(scheduleContext);
            this.ReplaceQueryParameters(scheduleContext, experimentNames);

            if (!cancellationToken.IsCancellationRequested)
            {
                int ofrThreshold = component.Parameters.GetValue<int>(Parameters.ExperimentOFRThreshold);

                IKustoManager kustoManager = this.Services.GetService<IKustoManager>();
                kustoManager.ThrowIfNull(nameof(kustoManager));

                List<JunoOFRNode> junoOfrs;
                List<string> tipList = new List<string>();

                try
                {
                    EnvironmentSettings settings = EnvironmentSettings.Initialize(scheduleContext.Configuration);
                    settings.ThrowIfNull(nameof(settings));

                    KustoSettings kustoSettings = settings.KustoSettings.Get(Setting.AzureCM);
                    kustoSettings.ThrowIfNull(nameof(kustoSettings));
                    this.ProviderContext.Add(SchedulerEventProperty.KustoQuery, this.Query);

                    DataTable response = await kustoManager.GetKustoResponseAsync(CacheKeys.ExperimentOFR + experimentNames, kustoSettings, this.Query)
                        .ConfigureDefaults();

                    junoOfrs = response.ParseOFRNodes();
                    junoOfrs.ForEach(node => tipList.Add(node.TipSessionId));

                    if (junoOfrs.Count >= ofrThreshold)
                    {
                        conditionSatisfied = true;
                    }

                    this.ProviderContext.Add(EventProperty.Count, junoOfrs.Count);
                    this.ProviderContext.Add(SchedulerEventProperty.Threshold, ofrThreshold);
                    this.ProviderContext.Add(SchedulerEventProperty.OffendingTipSessions, tipList);
                    this.ProviderContext.Add(SchedulerEventProperty.JunoOfrs, junoOfrs);
                    telemetryContext.AddContext(SchedulerEventProperty.JunoOfrs, junoOfrs);

                    executionStatus = ExecutionStatus.Succeeded;
                }
                catch (Exception exc)
                {
                    telemetryContext.AddError(exc, true);
                    executionStatus = ExecutionStatus.Failed;
                    conditionSatisfied = false;
                }
            }

            return new PreconditionResult(executionStatus, conditionSatisfied);
        }

        private string GetExperimentNames(ScheduleContext scheduleContext)
        {
            string parameterkey = "experiment.name";
            string targetGoalName = scheduleContext.TargetGoalTrigger.TargetGoal;
            // trimming "-teamName" from targetGoal to match it with what is there in execution goal.
            targetGoalName = targetGoalName.Replace(string.Concat("-", scheduleContext.TargetGoalTrigger.TeamName), string.Empty, StringComparison.Ordinal);

            List<string> experimentNames = new List<string>();
            List<KeyValuePair<string, IConvertible>> targetGoalExperimentNames = new List<KeyValuePair<string, IConvertible>>();

            // getting all parameters from execution goal
            Goal targetGoal = scheduleContext.ExecutionGoal.TargetGoals.Where(targetGoals => targetGoals.Name == targetGoalName).FirstOrDefault();
            targetGoal?.Actions.ForEach(scheduleAction => scheduleAction.Parameters.ForEach(parameters => targetGoalExperimentNames.Add(parameters)));

            experimentNames = targetGoalExperimentNames.Where(x => x.Key == parameterkey).Select(y => y.Value.ToString()).ToList();
            experimentNames.Add(scheduleContext.ExecutionGoal.ExperimentName);
            return string.Join(",", experimentNames.Distinct().Select(x => x.DoubleQuote()));
        }

        private class Parameters
        {
            internal const string ExperimentOFRThreshold = "experimentOFRThreshold";
        }

        internal class Constants
        {
            internal const string Environment = "$environmentSetting$";
            internal const string QueryStartTime = "$startTime$";
            internal const string ExperimentNameForQuery = "$JunoExperimentName$";
        }
    }
}
