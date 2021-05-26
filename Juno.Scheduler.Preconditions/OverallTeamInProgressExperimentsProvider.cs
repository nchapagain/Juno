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
    /// A <see cref="PreconditionProvider"/> that evaluates if a target goal 
    /// has reached a maximum number of InProgress experiments for a team.
    /// </summary>
    [SupportedParameter(Name = Parameters.TargetExperimentInstances, Type = typeof(int), Required = true)]
    [SupportedParameter(Name = Parameters.TeamName, Type = typeof(string), Required = true)]
    public class OverallTeamInProgressExperimentsProvider : PreconditionProvider
    {
        /// <summary>
        /// Creates an instance of <see cref="OverallTeamInProgressExperimentsProvider"/>
        /// </summary>
        /// <param name="services">A list of services that can be used for dependency injection.</param>
        public OverallTeamInProgressExperimentsProvider(IServiceCollection services)
            : base(services)
        {
            string query = Properties.Resources.OverallTeamInProgressExperiments;
            query.ThrowIfNullOrWhiteSpace(nameof(query));
            this.Query = query;
        }

        private string Query { get; set; }

        /// <summary>
        /// Evaluates whether the number of in progress experiments for a given team exceeds a threshold.
        /// Condition: 
        ///     False if the number of in progress experiments exceeds the given threshold.
        ///     True if the number of in progress experiments does not exceed the given threshold.
        /// </summary>
        protected override async Task<bool> IsConditionSatisfiedAsync(Precondition component, ScheduleContext scheduleContext, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            component.ThrowIfNull(nameof(component));
            scheduleContext.ThrowIfNull(nameof(scheduleContext));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            bool conditionSatisfied = true;

            if (!cancellationToken.IsCancellationRequested)
            {
                this.ReplaceQueryParameters(component);

                OverallTeamInProgressExperiments overallTeamInProgressExperiments = await this.QueryOverallTeamInProgressExperimentsAsync(cancellationToken)
                    .ConfigureDefaults();

                int targetExperiments = component.Parameters.GetValue<int>(Parameters.TargetExperimentInstances);
                conditionSatisfied = overallTeamInProgressExperiments == null || overallTeamInProgressExperiments.Count < targetExperiments;

                telemetryContext.AddContext(SchedulerEventProperty.TargetExperiments, targetExperiments);
                telemetryContext.AddContext(SchedulerEventProperty.InProgressExperimentsCount, overallTeamInProgressExperiments);
            }

            return conditionSatisfied;
        }

        private void ReplaceQueryParameters(Precondition component)
        {
            string teamName = component.Parameters.GetValue<string>(Parameters.TeamName);
            this.Query = this.Query.Replace(Constants.TeamName, teamName, StringComparison.OrdinalIgnoreCase);
        }

        private async Task<OverallTeamInProgressExperiments> QueryOverallTeamInProgressExperimentsAsync(CancellationToken cancellationToken)
        {
            IExperimentDataManager experimentDataManager = this.Services.GetService<IExperimentDataManager>();
            IEnumerable<JObject> queryResult = await experimentDataManager.QueryExperimentsAsync(this.Query, cancellationToken).ConfigureDefaults();

            List<OverallTeamInProgressExperiments> overallTeamInProgressExperimentsCounts = new List<OverallTeamInProgressExperiments>();
            foreach (var item in queryResult)
            {
                overallTeamInProgressExperimentsCounts.Add(item.ToObject<OverallTeamInProgressExperiments>());
            }

            return overallTeamInProgressExperimentsCounts.FirstOrDefault();
        }

        internal class OverallTeamInProgressExperiments
        {
            [JsonProperty("Count")]
            public int Count { get; set; }
        }

        private class Parameters
        {
            public const string TargetExperimentInstances = nameof(Parameters.TargetExperimentInstances);
            public const string TeamName = nameof(Parameters.TeamName);
        }

        private class Constants
        {
            public const string TeamName = "@teamName";
        }
    }
}
