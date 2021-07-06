namespace Juno.Scheduler.Preconditions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.DataManagement;
    using Juno.Extensions.Telemetry;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Provider who evaluates whether a Target Goal 
    /// has executed a given amount of successful experiments including in-progress experiments.
    /// This is designed to be used in a target goal Precondition.
    /// CONDITION: Target # of successful experiments less than Acutal # of successful experiments
    /// </summary>
    [SupportedParameter(Name = Parameters.TargetExperiments, Type = typeof(int), Required = true)]
    [SupportedParameter(Name = Parameters.DaysAgo, Type = typeof(int), Required = false)]
    public class SuccessfulExperimentsProvider : PreconditionProvider
    {
        private const int DefaultDaysAgo = 7;

        /// <summary>
        /// Creates an instance of <see cref="SuccessfulExperimentsProvider"/>
        /// </summary>
        /// <param name="services">A list of services that can be used for dependency injection.</param>
        public SuccessfulExperimentsProvider(IServiceCollection services)
            : base(services)
        {
            string query = Properties.Resources.SuccessfulExperiments;
            query.ThrowIfNullOrWhiteSpace(nameof(query));
            this.Query = query;
        }

        /// <summary>
        /// Get the query that can be used to discover the number of successful experiments
        /// have been launched for a target goal.
        /// </summary>
        private string Query { get; }

        /// <summary>
        /// Determines if the given target goal has reached the number of successful completed 
        /// experiments.
        /// Condition:
        ///     False if the number of successful experiments is equal to or exceeds the given threshold.
        ///     True if the number of succssful experiments is less than to the given threshold.
        /// </summary>
        protected override async Task<bool> IsConditionSatisfiedAsync(Precondition component, ScheduleContext scheduleContext, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            component.ThrowIfNull(nameof(component));
            scheduleContext.ThrowIfNull(nameof(scheduleContext));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            bool conditionSatisfied = true;
            if (!cancellationToken.IsCancellationRequested)
            {
                string query = this.ReplaceQueryParameters(scheduleContext, component);
                telemetryContext.AddContext(SchedulerEventProperty.KustoQuery, query);

                SuccessfulExperiments successfulExperiments = await this.GetSuccessfulExperimentsAsync(query, cancellationToken)
                    .ConfigureDefaults();

                int targetExperiments = component.Parameters.GetValue<int>(Parameters.TargetExperiments);
                int successfulExperimentsCount = successfulExperiments == null ? 0 : successfulExperiments.Count;
                conditionSatisfied = successfulExperimentsCount < targetExperiments;

                telemetryContext.AddContext(SchedulerEventProperty.TargetExperiments, targetExperiments);
                telemetryContext.AddContext(SchedulerEventProperty.SuccessfulExperimentsCount, successfulExperimentsCount);
                telemetryContext.AddContext(EventProperty.Count, successfulExperimentsCount);
                telemetryContext.AddContext(SchedulerEventProperty.Threshold, targetExperiments);
            }

            return conditionSatisfied;
        }

        private string ReplaceQueryParameters(ScheduleContext scheduleContext, Precondition component)
        {
            int daysAgo = component.Parameters.GetValue<int>(Parameters.DaysAgo, SuccessfulExperimentsProvider.DefaultDaysAgo);
            string resolvedQuery = string.Copy(this.Query);

            resolvedQuery = resolvedQuery.Replace(Constants.DaysAgo, $"-{daysAgo}", StringComparison.Ordinal);
            resolvedQuery = resolvedQuery.Replace(Constants.TargetGoal, scheduleContext.TargetGoalTrigger.Name, StringComparison.Ordinal);
            resolvedQuery = resolvedQuery.Replace(Constants.ExecutionGoalId, scheduleContext.ExecutionGoal.Id, StringComparison.OrdinalIgnoreCase);
            return resolvedQuery;
        }

        private async Task<SuccessfulExperiments> GetSuccessfulExperimentsAsync(string query, CancellationToken cancellationToken)
        {
            IExperimentDataManager experimentDataManager = this.Services.GetService<IExperimentDataManager>();
            IEnumerable<JObject> queryResult = await experimentDataManager.QueryExperimentsAsync(query, cancellationToken).ConfigureDefaults();

            List<SuccessfulExperiments> successfulExperimentsCounts = new List<SuccessfulExperiments>();
            foreach (var item in queryResult)
            {
                successfulExperimentsCounts.Add(item.ToObject<SuccessfulExperiments>());
            }

            return successfulExperimentsCounts.FirstOrDefault();
        }

        internal class SuccessfulExperiments
        {
            [JsonProperty("Count")]
            public int Count { get; set; }
        }

        /// <summary>
        /// Supported parameter string literals
        /// </summary>
        private class Parameters
        {
            public const string TargetExperiments = "targetExperimentInstances";
            public const string DaysAgo = "daysAgo";
        }

        /// <summary>
        /// Constant string literals for query replacement
        /// </summary>
        private class Constants
        {
            public const string DaysAgo = "@daysAgo";
            public const string TargetGoal = "@targetGoal";
            public const string ExecutionGoalId = "@executionGoalId";
        }
    }
}
