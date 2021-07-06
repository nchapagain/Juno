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
    using Kusto.Data.Exceptions;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using static Juno.Scheduler.Preconditions.Manager.KustoDataTableExtension;

    /// <summary>
    /// Provider who evaluates whether a Target Goal exceeded the FailureRate
    /// This is designed to be used in a control goal Precondition.
    /// </summary>
    [SupportedParameter(Name = Parameters.MinimumExperimentInstance, Type = typeof(int), Required = true)]
    [SupportedParameter(Name = Parameters.TargetFailureRate, Type = typeof(int), Required = false)]
    [SupportedParameter(Name = Parameters.DaysAgo, Type = typeof(int), Required = false)]
    public class FailureRatePreconditionProvider : PreconditionProvider
    {
        private readonly int defaultDaysAgo = 7;
        private readonly int defaultRailureRate = 20;

        /// <summary>
        /// Creates an instance of <see cref="FailureRatePreconditionProvider"/>
        /// </summary>
        /// <param name="services"></param>
        public FailureRatePreconditionProvider(IServiceCollection services)
            : base(services)
        {
            string query = Properties.Resources.ExperimentFailureRate;
            query.ThrowIfNullOrWhiteSpace(nameof(query));
            this.Query = query;
        }

        /// <summary>
        /// Query which supplies the target goal failure rate.
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
        /// Returns true/false if the target goal has exceeded its failure rate.
        /// Condition:
        ///     False if the failure rate is sctrictly greater than the given threshold.
        ///     True if the failure rate is less than or equal to the given threshold.
        /// </summary>
        protected override async Task<bool> IsConditionSatisfiedAsync(Precondition component, ScheduleContext scheduleContext, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            component.ThrowIfNull(nameof(component));
            scheduleContext.ThrowIfNull(nameof(scheduleContext));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            bool conditionSatisfied = false;
            if (!cancellationToken.IsCancellationRequested)
            {
                int targetFailureRate = component.Parameters.GetValue<int>(Parameters.TargetFailureRate, this.defaultRailureRate);

                IKustoManager kustoManager = this.Services.GetService<IKustoManager>();
                EnvironmentSettings settings = EnvironmentSettings.Initialize(scheduleContext.Configuration);
                KustoSettings kustoSettings = settings.KustoSettings.Get(Setting.AzureCM);

                string resolvedQuery = this.ReplaceQueryParameters(scheduleContext, component, settings.Environment);

                try
                {
                    DataTable response = await kustoManager.GetKustoResponseAsync(
                        string.Concat(CacheKeys.FailureRate, scheduleContext.TargetGoalTrigger.Name),
                        kustoSettings,
                        resolvedQuery).ConfigureDefaults();

                    if (response.Rows.Count != 0)
                    {
                        int failureRate = response.ParseSingleRowSingleKustoColumn(KustoColumn.FailureRate);
                        conditionSatisfied = failureRate > targetFailureRate;

                        telemetryContext.AddContext(SchedulerEventProperty.FailureRate, failureRate);
                        telemetryContext.AddContext(EventProperty.Count, failureRate);
                        telemetryContext.AddContext(SchedulerEventProperty.Threshold, targetFailureRate);
                    }
                }
                catch (KustoRequestThrottledException)
                {
                    // Do not let throttled exceptions prevent execution.
                    this.Logger.LogTelemetry($"{nameof(FailureRatePreconditionProvider)}.ThrottledWarning", LogLevel.Warning, telemetryContext);
                    return false;
                }
            }

            return conditionSatisfied;
        }

        private string ReplaceQueryParameters(ScheduleContext scheduleContext, Precondition component, string environment)
        {
            string targetGoalFilter = scheduleContext.TargetGoalTrigger.Name;
            int daysAgo = component.Parameters.GetValue<int>(Parameters.DaysAgo, this.defaultDaysAgo);
            int minimumExperimentInstance = component.Parameters.GetValue<int>(Parameters.MinimumExperimentInstance);

            string resolvedQuery = string.Copy(this.Query);

            resolvedQuery = resolvedQuery.Replace(Constants.MinimumRuns, $"{minimumExperimentInstance}", StringComparison.Ordinal);
            resolvedQuery = resolvedQuery.Replace(Constants.StartTime, $"now(-{daysAgo}d)", StringComparison.Ordinal);
            resolvedQuery = resolvedQuery.Replace(Constants.Environment, environment, StringComparison.Ordinal);
            resolvedQuery = resolvedQuery.Replace(Constants.TargetGoal, targetGoalFilter, StringComparison.Ordinal);

            return resolvedQuery;
        }

        /// <summary>
        /// Supported parameter string literals
        /// </summary>
        private class Parameters
        {
            public const string MinimumExperimentInstance = nameof(Parameters.MinimumExperimentInstance);
            public const string TargetFailureRate = nameof(Parameters.TargetFailureRate);
            public const string DaysAgo = nameof(Parameters.DaysAgo);
        }

        private class Constants
        {
            public const string MinimumRuns = "$minimumRuns$";
            public const string Environment = "$environment$";
            public const string TargetGoal = "$targetGoal$";
            public const string StartTime = "$startTime$";
        }
    }
}
