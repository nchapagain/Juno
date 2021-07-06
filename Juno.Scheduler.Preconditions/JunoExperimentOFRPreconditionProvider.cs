namespace Juno.Scheduler.Preconditions
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
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
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// A <see cref="PreconditionProvider"/> that evaluates if an experiment
    /// has reached a maximum number of OFRs.
    /// </summary>
    [SupportedParameter(Name = Parameters.ExperimentOFRThreshold, Type = typeof(int), Required = true)]
    public class JunoExperimentGoalOFRPreconditionProvider : PreconditionProvider
    {
        private readonly int startDateExperimentGoalJunoOFR = -7;

        /// <summary>
        /// Initializes a new instance of the<see cref="JunoExperimentGoalOFRPreconditionProvider" />
        /// </summary>
        /// <param name="services">A list of services that can be used for dependency injection.</param>
        public JunoExperimentGoalOFRPreconditionProvider(IServiceCollection services)
            : base(services)
        {
            string query = Properties.Resources.ExperimentOFR;
            query.ThrowIfNullOrWhiteSpace(nameof(query));
            this.Query = query;
        }

        private string Query { get; }

        /// <inheritdoc/>
        public override Task ConfigureServicesAsync(GoalComponent component, ScheduleContext scheduleContext)
        {
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
        /// Evaluates whether the number of OFRs caused by the schedule contexts experiment is greather than the given threshold.
        /// Condition:
        ///     False if the number of actual OFRs is less than the given threshold.
        ///     True if the number of actual OFRs is greather than or equal to the given threshold.
        /// </summary>
        protected override async Task<bool> IsConditionSatisfiedAsync(Precondition component, ScheduleContext scheduleContext, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            component.ThrowIfNull(nameof(component));
            scheduleContext.ThrowIfNull(nameof(scheduleContext));            
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            bool conditionSatisfied = false;

            if (!cancellationToken.IsCancellationRequested)
            {
                TargetGoal targetGoal = scheduleContext.ExecutionGoal.Definition.GetGoal(scheduleContext.TargetGoalTrigger.Name) as TargetGoal;
                string experimentNames = scheduleContext.ExecutionGoal.Definition.ExperimentName;
                int ofrThreshold = component.Parameters.GetValue<int>(Parameters.ExperimentOFRThreshold);
                string resolvedQuery = this.ReplaceQueryParameters(scheduleContext, experimentNames);

                IKustoManager kustoManager = this.Services.GetService<IKustoManager>();

                EnvironmentSettings settings = EnvironmentSettings.Initialize(scheduleContext.Configuration);
                KustoSettings kustoSettings = settings.KustoSettings.Get(Setting.AzureCM);
                try
                {
                    DataTable response = await kustoManager.GetKustoResponseAsync(CacheKeys.ExperimentOFR + experimentNames, kustoSettings, resolvedQuery)
                        .ConfigureDefaults();

                    List<JunoOFRNode> junoOfrs = response.ParseOFRNodes();
                    IEnumerable<string> tipList = junoOfrs.Select(ofr => ofr.TipSessionId);
                    conditionSatisfied = junoOfrs.Count >= ofrThreshold;

                    telemetryContext.AddContext(EventProperty.Count, junoOfrs.Count);
                    telemetryContext.AddContext(SchedulerEventProperty.Threshold, ofrThreshold);
                    telemetryContext.AddContext(SchedulerEventProperty.OffendingTipSessions, tipList);
                    telemetryContext.AddContext(SchedulerEventProperty.JunoOfrs, junoOfrs);
                }
                catch (KustoRequestThrottledException)
                {
                    // Do not let throttled exceptions prevent execution.
                    this.Logger.LogTelemetry($"{nameof(JunoExperimentGoalOFRPreconditionProvider)}.ThrottledWarning", LogLevel.Warning, telemetryContext);
                    return false;
                }
            }

            return conditionSatisfied;
        }

        private string ReplaceQueryParameters(ScheduleContext scheduleContext, string experimentNames)
        {
            string environment = EnvironmentSettings.Initialize(scheduleContext.Configuration).Environment;
            string resolvedQuery = string.Copy(this.Query);
            resolvedQuery = resolvedQuery.Replace(Constants.QueryStartTime, $"now({this.startDateExperimentGoalJunoOFR}d)", StringComparison.Ordinal);
            resolvedQuery = resolvedQuery.Replace(Constants.ExperimentNameForQuery, experimentNames, StringComparison.Ordinal);
            resolvedQuery = resolvedQuery.Replace(Constants.Environment, environment, StringComparison.Ordinal);

            return resolvedQuery;
        }

        private class Parameters
        {
            public const string ExperimentOFRThreshold = nameof(Parameters.ExperimentOFRThreshold);
        }

        private class Constants
        {
            public const string Environment = "$environmentSetting$";
            public const string QueryStartTime = "$startTime$";
            public const string ExperimentNameForQuery = "$JunoExperimentName$";
        }
    }
}
