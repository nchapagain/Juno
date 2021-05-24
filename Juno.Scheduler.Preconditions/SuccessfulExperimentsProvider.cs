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
    /// CONDITION: Target # of successful experiments < Acutal # of successful experiments
    /// </summary>
    [SupportedParameter(Name = Parameters.TargetExperiments, Type = typeof(int), Required = true)]
    [SupportedParameter(Name = Parameters.DaysAgo, Type = typeof(int), Required = false)]
    public class SuccessfulExperimentsProvider : PreconditionProvider
    {
        private const int DefaultDaysAgo = 7;

        /// <summary>
        /// Creates an instance of <see cref="SuccessfulExperimentsProvider"/>
        /// </summary>
        /// <param name="services"></param>
        public SuccessfulExperimentsProvider(IServiceCollection services)
            : base(services)
        {
            string query = Properties.Resources.SuccessfulExperiments;
            query.ThrowIfNullOrWhiteSpace(nameof(query));
            this.Query = query;
        }

        private string Query { get; set; }

        private void ReplaceQueryParameters(ScheduleContext scheduleContext, Precondition component, string environment)
        {
            int daysAgo = component.Parameters.GetValue<int>(Parameters.DaysAgo, SuccessfulExperimentsProvider.DefaultDaysAgo);

            this.Query = this.Query.Replace(Constants.StartTime, $"now(-{daysAgo}d)", StringComparison.Ordinal);
            this.Query = this.Query.Replace(Constants.Environment, environment, StringComparison.Ordinal);
            this.Query = this.Query.Replace(Constants.TargetGoal, scheduleContext.TargetGoalTrigger.TargetGoal, StringComparison.Ordinal);
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
        /// Determines if the given target goal has reached the number of successful completed 
        /// experiments.
        /// </summary>
        /// <param name="component">Precondition contract with valid parameters</param>
        /// <param name="scheduleContext">Context in which the provider is executing</param>
        /// <param name="telemetryContext"><see cref="EventContext"/></param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns>
        /// A <see cref="PreconditionResult"/> where the condition is satisfied if the actual number
        /// of successful experiments is less than the target number of successful experiments
        /// </returns>
        protected override async Task<PreconditionResult> IsConditionSatisfiedAsync(Precondition component, ScheduleContext scheduleContext, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            component.ThrowIfNull(nameof(component));
            scheduleContext.ThrowIfNull(nameof(scheduleContext));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            bool conditionSatisfied = true;
            if (!cancellationToken.IsCancellationRequested)
            {
                int targetExperiments = component.Parameters.GetValue<int>(Parameters.TargetExperiments);

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
                        string.Concat(CacheKeys.SuccssfulExperiments, scheduleContext.TargetGoalTrigger.TargetGoal),
                        kustoSettings, 
                        this.Query).ConfigureDefaults();

                    if (response.Rows.Count != 0)
                    {
                        int successfulExperiments = response.ParseSingleRowSingleKustoColumn(KustoColumn.ExperimentCount);

                        conditionSatisfied = successfulExperiments < targetExperiments;

                        this.ProviderContext.Add(SchedulerEventProperty.TargetExperiments, targetExperiments);
                        this.ProviderContext.Add(SchedulerEventProperty.SuccessfulExperimentsCount, successfulExperiments);
                        this.ProviderContext.Add(EventProperty.Count, successfulExperiments);
                        this.ProviderContext.Add(SchedulerEventProperty.Threshold, targetExperiments);
                    }
                    else
                    {
                        this.ProviderContext.Add(EventProperty.Response, "No Experiments Succeeded yet");
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
            internal const string TargetExperiments = "targetExperimentInstances";
            internal const string DaysAgo = "daysAgo";
        }

        /// <summary>
        /// Constant string literals for query replacement
        /// </summary>
        internal class Constants
        {
            internal const string Environment = "$environment$";
            internal const string TargetGoal = "$targetGoal$";
            internal const string StartTime = "$startTime$";
        }
    }
}
