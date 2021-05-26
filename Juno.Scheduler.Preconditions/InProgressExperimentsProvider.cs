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
    [SupportedParameter(Name = Parameters.TargetExperimentsInstances, Type = typeof(int), Required = true)]
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

        private string Query { get; set; }

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
                this.ReplaceQueryParameters(scheduleContext);
                telemetryContext.AddContext(SchedulerEventProperty.KustoQuery, this.Query);

                InProgressExperiments inProgressExperiments = await this.InProgressExperimentsAsync(cancellationToken)
                    .ConfigureDefaults();

                int targetExperimentIntances = component.Parameters.GetValue<int>(Parameters.TargetExperimentsInstances);
                conditionSatisfied = inProgressExperiments == null || inProgressExperiments.Count < targetExperimentIntances;

                telemetryContext.AddContext(SchedulerEventProperty.TargetExperiments, targetExperimentIntances);
                telemetryContext.AddContext(SchedulerEventProperty.InProgressExperimentsCount, inProgressExperiments);
            }

            return conditionSatisfied;
        }

        private void ReplaceQueryParameters(ScheduleContext scheduleContext)
        {
            this.Query = this.Query.Replace(Constants.TargetGoal, scheduleContext.TargetGoalTrigger.TargetGoal, StringComparison.Ordinal);
        }

        private async Task<InProgressExperiments> InProgressExperimentsAsync(CancellationToken cancellationToken)
        {
            IExperimentDataManager experimentDataManager = this.Services.GetService<IExperimentDataManager>();
            IEnumerable<JObject> queryResult = await experimentDataManager.QueryExperimentsAsync(this.Query, cancellationToken).ConfigureDefaults();

            List<InProgressExperiments> inProgressExperimentsCounts = new List<InProgressExperiments>();
            foreach (var item in queryResult)
            {
                inProgressExperimentsCounts.Add(item.ToObject<InProgressExperiments>());
            }

            return inProgressExperimentsCounts.FirstOrDefault();
        }

        /// <summary>
        /// Supported parameter string literals
        /// </summary>
        internal class Parameters
        {
            public const string TargetExperimentsInstances = nameof(Parameters.TargetExperimentsInstances);
        }

        internal class InProgressExperiments
        {
            [JsonProperty("Count")]
            public int Count { get; set; }
        }

        /// <summary>
        /// Constant string literals for query replacement
        /// </summary>
        private class Constants
        {
            public const string TargetGoal = "@targetGoal";
        }
    }
}
