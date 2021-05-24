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
    [SupportedParameter(Name = Parameters.TargetExperiments, Type = typeof(int), Required = true)]
    [SupportedParameter(Name = Parameters.TeamName, Type = typeof(string), Required = true)]
    public class OverallTeamInProgressExperimentsProvider : PreconditionProvider
    {
        /// <summary>
        /// Creates an instance of <see cref="OverallTeamInProgressExperimentsProvider"/>
        /// </summary>
        /// <param name="services"></param>
        public OverallTeamInProgressExperimentsProvider(IServiceCollection services)
            : base(services)
        {
            string query = Properties.Resources.OverallTeamInProgressExperiments;
            query.ThrowIfNullOrWhiteSpace(nameof(query));
            this.Query = query;
        }

        private string Query { get; set; }

        /// <inheritdoc/>
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
                    this.ReplaceQueryParameters(component);

                    OverallTeamInProgressExperiments overallTeamInProgressExperiments = await this.QueryOverallTeamInProgressExperimentsAsync(cancellationToken)
                        .ConfigureDefaults();

                    if (overallTeamInProgressExperiments?.Count > 0)
                    {
                        int targetExperiments = component.Parameters.GetValue<int>(Parameters.TargetExperiments);
                        conditionSatisfied = overallTeamInProgressExperiments.Count < targetExperiments;

                        this.ProviderContext.Add(SchedulerEventProperty.TargetExperiments, targetExperiments);
                        this.ProviderContext.Add(SchedulerEventProperty.InProgressExperimentsCount, overallTeamInProgressExperiments);
                    }
                    else
                    {
                        this.ProviderContext.Add(EventProperty.Response, "No Experiments InProgress");
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

        internal class Parameters
        {
            internal const string TargetExperiments = "targetExperimentInstances";
            internal const string TeamName = "teamName";
        }

        internal class Constants
        {
            internal const string TeamName = "@teamName";
        }

        internal class OverallTeamInProgressExperiments
        {
            [JsonProperty("Count")]
            public int Count { get; set; }
        }
    }
}
