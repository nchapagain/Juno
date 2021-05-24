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
    /// CONDITION: Target # of in progress experiments < Actual # of in progress experiments
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
        /// <param name="component">Precondition contract with valid parameters</param>
        /// <param name="scheduleContext">Context in which the provider is executing</param>
        /// <param name="telemetryContext"><see cref="EventContext"/></param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns>
        /// A <see cref="PreconditionResult"/> where the condition is satisfied if the actual number
        /// of in progress experiments is less than the target number of in progress experiments
        /// </returns>
        protected override async Task<PreconditionResult> IsConditionSatisfiedAsync(Precondition component, ScheduleContext scheduleContext, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            component.ThrowIfNull(nameof(component));
            scheduleContext.ThrowIfNull(nameof(scheduleContext));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            bool conditionSatisfied = true;

            if (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    this.ProviderContext.Add(SchedulerEventProperty.KustoQuery, this.Query);
                    this.ReplaceQueryParameters(scheduleContext);

                    InProgressExperiments inProgressExperiments = await this.InProgressExperimentsAsync(cancellationToken)
                        .ConfigureDefaults();

                    if (inProgressExperiments?.Count > 0)
                    {
                        int targetExperimentIntances = component.Parameters.GetValue<int>(Parameters.TargetExperimentsInstances);
                        conditionSatisfied = inProgressExperiments.Count < targetExperimentIntances;

                        this.ProviderContext.Add(SchedulerEventProperty.TargetExperiments, targetExperimentIntances);
                        this.ProviderContext.Add(SchedulerEventProperty.InProgressExperimentsCount, inProgressExperiments);
                    }
                    else
                    {
                        this.ProviderContext.Add(EventProperty.Response, "No Experiments InProgress.");
                    }
                }
                catch (Exception exc)
                {
                    telemetryContext.AddError(exc, true);
                    return new PreconditionResult(ExecutionStatus.Failed, false);
                }
            }

            return new PreconditionResult(ExecutionStatus.Succeeded, conditionSatisfied);
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
            internal const string TargetExperimentsInstances = "targetExperimentInstances";
        }

        /// <summary>
        /// Constant string literals for query replacement
        /// </summary>
        internal class Constants
        {
            internal const string TargetGoal = "@targetGoal";
        }

        internal class InProgressExperiments
        {
            [JsonProperty("Count")]
            public int Count { get; set; }
        }
    }
}
