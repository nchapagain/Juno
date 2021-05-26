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
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// A <see cref="PreconditionProvider"/> that evaluates if the overall system
    /// has reached a maximum number of OFRs in the past day.
    /// </summary>
    [SupportedParameter(Name = Parameters.DailyOFRThreshold, Type = typeof(int), Required = true)]
    public class JunoDailyOFRPreconditionProvider : PreconditionProvider
    {
        private readonly int startDateDailyJunoOFR = -1;

        /// <summary>
        /// Initializes a new instance of the<see cref="JunoDailyOFRPreconditionProvider" />
        /// </summary>
        public JunoDailyOFRPreconditionProvider(IServiceCollection services)
            : base(services)
        {
            string query = Properties.Resources.JunoOfr;
            query.ThrowIfNullOrWhiteSpace(nameof(query));
            query = query.Replace(Constants.QueryStartTime, $"now({this.startDateDailyJunoOFR}d)", StringComparison.Ordinal);
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
        /// Evaluates whether the number of OFRs caused by the system as a whole in the past day is greather than the given threshold.
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
                int ofrThreshold = component.Parameters.GetValue<int>(Parameters.DailyOFRThreshold);

                IKustoManager kustoManager = this.Services.GetService<IKustoManager>();

                EnvironmentSettings settings = EnvironmentSettings.Initialize(scheduleContext.Configuration);
                KustoSettings kustoSettings = settings.KustoSettings.Get(Setting.AzureCM);
                DataTable response = await kustoManager.GetKustoResponseAsync(CacheKeys.DailyOFR, kustoSettings, this.Query)
                    .ConfigureDefaults();

                List<JunoOFRNode> junoOfrs = response.ParseOFRNodes();
                IEnumerable<string> tipList = junoOfrs.Select(ofr => ofr.TipSessionId);
                conditionSatisfied = junoOfrs.Count >= ofrThreshold;

                telemetryContext.AddContext(EventProperty.Count, junoOfrs.Count);
                telemetryContext.AddContext(SchedulerEventProperty.Threshold, ofrThreshold);
                telemetryContext.AddContext(SchedulerEventProperty.OffendingTipSessions, tipList);
                telemetryContext.AddContext(SchedulerEventProperty.JunoOfrs, junoOfrs);
            }

            return conditionSatisfied;
        }

        private class Parameters
        {
            public const string DailyOFRThreshold = "dailyOFRThreshold";
        }

        private class Constants
        {
            public const string QueryStartTime = "$startTime$";
        }
    }
}
