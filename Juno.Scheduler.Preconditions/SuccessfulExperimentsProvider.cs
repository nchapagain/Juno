namespace Juno.Scheduler.Preconditions
{
    using System;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.Extensions.Telemetry;
    using Juno.Providers;
    using Juno.Scheduler.Preconditions.Manager;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;
    using static Juno.Scheduler.Preconditions.Manager.KustoDataTableExtension;

    /// <summary>
    /// Provider who evaluates whether a Target Goal 
    /// has executed a given amount of successful experiments
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
        protected string Query { get; }

        /// <inheritdoc/>
        public override Task ConfigureServicesAsync(GoalComponent component, ScheduleContext scheduleContext)
        {
            component.ThrowIfNull(nameof(component));
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
                int targetExperiments = component.Parameters.GetValue<int>(Parameters.TargetExperiments);

                IKustoManager kustoManager = this.Services.GetService<IKustoManager>();

                EnvironmentSettings settings = EnvironmentSettings.Initialize(scheduleContext.Configuration);
                settings.ThrowIfNull(nameof(settings));

                KustoSettings kustoSettings = settings.KustoSettings.Get(Setting.AzureCM);
                kustoSettings.ThrowIfNull(nameof(kustoSettings));

                string resolvedQuery = this.ReplaceQueryParameters(scheduleContext, component, settings.Environment);
                telemetryContext.AddContext(SchedulerEventProperty.KustoQuery, resolvedQuery);

                DataTable response = await kustoManager.GetKustoResponseAsync(
                    string.Concat(CacheKeys.SuccssfulExperiments, scheduleContext.TargetGoalTrigger.TargetGoal),
                    kustoSettings, 
                    resolvedQuery).ConfigureDefaults();

                if (response.Rows.Count != 0)
                {
                    int successfulExperiments = response.ParseSingleRowSingleKustoColumn(KustoColumn.ExperimentCount);

                    conditionSatisfied = successfulExperiments < targetExperiments;

                    telemetryContext.AddContext(SchedulerEventProperty.TargetExperiments, targetExperiments);
                    telemetryContext.AddContext(SchedulerEventProperty.SuccessfulExperimentsCount, successfulExperiments);
                    telemetryContext.AddContext(EventProperty.Count, successfulExperiments);
                    telemetryContext.AddContext(SchedulerEventProperty.Threshold, targetExperiments);
                }
            }

            return conditionSatisfied;
        }

        private string ReplaceQueryParameters(ScheduleContext scheduleContext, Precondition component, string environment)
        {
            int daysAgo = component.Parameters.GetValue<int>(Parameters.DaysAgo, SuccessfulExperimentsProvider.DefaultDaysAgo);
            string resolvedQuery = string.Copy(this.Query);

            resolvedQuery = resolvedQuery.Replace(Constants.StartTime, $"now(-{daysAgo}d)", StringComparison.Ordinal);
            resolvedQuery = resolvedQuery.Replace(Constants.Environment, environment, StringComparison.Ordinal);
            resolvedQuery = resolvedQuery.Replace(Constants.TargetGoal, scheduleContext.TargetGoalTrigger.TargetGoal, StringComparison.Ordinal);

            return resolvedQuery;
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
            public const string Environment = "$environment$";
            public const string TargetGoal = "$targetGoal$";
            public const string StartTime = "$startTime$";
        }
    }
}
