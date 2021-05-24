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
    /// Provider who evaluates whether a Target Goal exceeded the FailureRate
    /// This is designed to be used in a control goal Precondition.
    //// CONDITION: Failure rate threshold < Acutal failure rate 
    /// </summary>
    [SupportedParameter(Name = Parameters.MinimumRuns, Type = typeof(int), Required = true)]
    [SupportedParameter(Name = Parameters.FailureRate, Type = typeof(int), Required = false)]
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

        private string Query { get; set; }

        private void ReplaceQueryParameters(ScheduleContext scheduleContext, Precondition component, string environment)
        {
            string targetGoalFilter = scheduleContext.TargetGoalTrigger.TargetGoal;
            int daysAgo = component.Parameters.GetValue<int>(Parameters.DaysAgo, this.defaultDaysAgo);
            int minimumExperimentInstance = component.Parameters.GetValue<int>(Parameters.MinimumRuns);

            this.Query = this.Query.Replace(Constants.MinimumRuns, $"{minimumExperimentInstance}", StringComparison.Ordinal);
            this.Query = this.Query.Replace(Constants.StartTime, $"now(-{daysAgo}d)", StringComparison.Ordinal);
            this.Query = this.Query.Replace(Constants.Environment, environment, StringComparison.Ordinal);
            this.Query = this.Query.Replace(Constants.TargetGoal, targetGoalFilter, StringComparison.Ordinal);
        }

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
        /// Determines if the given control goal has exceeded Failure Rate.
        /// </summary>
        /// <param name="component"><see cref="Precondition"/></param>
        /// <param name="scheduleContext"><see cref="ScheduleContext"/></param>
        /// <param name="telemetryContext"><see cref="EventContext"/></param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns>
        /// A <see cref="PreconditionResult"/> where the condition is satisfied if the experiment Failure Rate
        /// is less than the target failure Rate.
        /// </returns>
        protected override async Task<PreconditionResult> IsConditionSatisfiedAsync(Precondition component, ScheduleContext scheduleContext, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            component.ThrowIfNull(nameof(component));
            scheduleContext.ThrowIfNull(nameof(scheduleContext));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            bool conditionSatisfied = false;
            if (!cancellationToken.IsCancellationRequested)
            {
                int targetFailureRate = component.Parameters.GetValue<int>(Parameters.FailureRate, this.defaultRailureRate);

                IKustoManager kustoManager = this.Services.GetService<IKustoManager>();
                kustoManager.ThrowIfNull(nameof(kustoManager));

                try
                {
                    EnvironmentSettings settings = EnvironmentSettings.Initialize(scheduleContext.Configuration);
                    settings.ThrowIfNull(nameof(settings));

                    KustoSettings kustoSettings = settings.KustoSettings.Get(Setting.AzureCM);
                    kustoSettings.ThrowIfNull(nameof(kustoSettings));

                    this.ReplaceQueryParameters(scheduleContext, component, settings.Environment);
                    this.ProviderContext.Add(SchedulerEventProperty.KustoQuery, this.Query);

                    DataTable response = await kustoManager.GetKustoResponseAsync(
                        string.Concat(CacheKeys.FailureRate, scheduleContext.TargetGoalTrigger.TargetGoal),
                        kustoSettings,
                        this.Query).ConfigureDefaults();

                    if (response.Rows.Count != 0)
                    {
                        int failureRate = response.ParseSingleRowSingleKustoColumn(KustoColumn.FailureRate);
                        conditionSatisfied = failureRate > targetFailureRate;

                        this.ProviderContext.Add(SchedulerEventProperty.FailureRate, failureRate);
                        this.ProviderContext.Add(EventProperty.Count, failureRate);
                        this.ProviderContext.Add(SchedulerEventProperty.Threshold, targetFailureRate);
                    }
                    else
                    {
                        this.ProviderContext.Add(EventProperty.Response, "No Experiments Failed yet");
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

        /// <summary>
        /// Supported parameter string literals
        /// </summary>
        internal class Parameters
        {
            internal const string MinimumRuns = "minimumExperimentInstance";
            internal const string FailureRate = "targetFailureRate";
            internal const string DaysAgo = "daysAgo";
        }

        /// <summary>
        /// Constant string literals for query replacement
        /// </summary>
        internal class Constants
        {
            internal const string MinimumRuns = "$minimumRuns$";
            internal const string Environment = "$environment$";
            internal const string TargetGoal = "$targetGoal$";
            internal const string StartTime = "$startTime$";
        }
    }
}
