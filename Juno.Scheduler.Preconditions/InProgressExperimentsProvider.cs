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
    /// Provider what evaluates whether a Target Goal 
    /// has executed a given amount of in progress experiments
    /// This is designed to be used in a target goal Precondition.
    /// CONDITION: Target # of in progress experiments less than Actual # of in progress experiments
    /// </summary>
    [SupportedParameter(Name = Parameters.TargetExperimentInstances, Type = typeof(int), Required = true)]
    public class InProgressExperimentsProvider : PreconditionProvider
    {
        /// <summary>
        /// Creates an instance of <see cref="InProgressExperimentsProvider"/>
        /// </summary>
        /// <param name="services"></param>
        public InProgressExperimentsProvider(IServiceCollection services)
            : base(services)
        {
            string query = Properties.Resources.InProgressExperiments;
            query.ThrowIfNullOrWhiteSpace(nameof(query));
            this.Query = query;
        }

        private string Query { get; }

        /// <summary>
        /// Determines if the given target goal has reached the number of InProgress experiments.
        /// </summary>
        protected override async Task<bool> IsConditionSatisfiedAsync(Precondition component, ScheduleContext scheduleContext, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            component.ThrowIfNull(nameof(component));
            scheduleContext.ThrowIfNull(nameof(scheduleContext));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            bool conditionSatisfied = true;

            if (!cancellationToken.IsCancellationRequested)
            {
                string query = this.ReplaceQueryParameters(scheduleContext);
                telemetryContext.AddContext(SchedulerEventProperty.KustoQuery, query);

                InProgressExperiments inProgressExperiments = await this.InProgressExperimentsAsync(query, cancellationToken)
                    .ConfigureDefaults();

                int targetExperimentIntances = component.Parameters.GetValue<int>(Parameters.TargetExperimentInstances);
                conditionSatisfied = inProgressExperiments == null || inProgressExperiments.Count < targetExperimentIntances;

                telemetryContext.AddContext(SchedulerEventProperty.TargetExperiments, targetExperimentIntances);
                telemetryContext.AddContext(SchedulerEventProperty.InProgressExperimentsCount, inProgressExperiments);
            }

            return conditionSatisfied;
        }

        private string ReplaceQueryParameters(ScheduleContext scheduleContext)
        {
            string query = this.Query.Replace(Constants.TargetGoal, scheduleContext.TargetGoalTrigger.Name, StringComparison.Ordinal);
            query = query.Replace(Constants.ExecutionGoalId, scheduleContext.ExecutionGoal.Id, StringComparison.Ordinal);
            return query;
        }

        private async Task<InProgressExperiments> InProgressExperimentsAsync(string query, CancellationToken cancellationToken)
        {
            IExperimentDataManager experimentDataManager = this.Services.GetService<IExperimentDataManager>();
            IEnumerable<JObject> queryResult = await experimentDataManager.QueryExperimentsAsync(query, cancellationToken).ConfigureDefaults();

            List<InProgressExperiments> inProgressExperimentsCounts = new List<InProgressExperiments>();
            foreach (var item in queryResult)
            {
                inProgressExperimentsCounts.Add(item.ToObject<InProgressExperiments>());
            }

            return inProgressExperimentsCounts.FirstOrDefault();
        }

        internal class InProgressExperiments
        {
            [JsonProperty("Count")]
            public int Count { get; set; }
        }

        /// <summary>
        /// Supported parameter string literals
        /// </summary>
        private class Parameters
        {
            public const string TargetExperimentInstances = nameof(Parameters.TargetExperimentInstances);
        }

        /// <summary>
        /// Constant string literals for query replacement
        /// </summary>
        private class Constants
        {
            public const string TargetGoal = "@targetGoal";
            public const string ExecutionGoalId = "@executionGoalId";
        }
    }
}
