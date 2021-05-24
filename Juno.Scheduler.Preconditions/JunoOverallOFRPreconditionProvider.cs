namespace Juno.Scheduler.Preconditions
{
    using System;
    using System.Collections.Generic;
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
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Class to manage Control Goal: Juno.Scheduler.Goals.Control.OverallJunoOFR
    /// </summary>
    [SupportedParameter(Name = Parameters.OverallOFRThreshold, Type = typeof(int), Required = true)]
    public class JunoOverallOFRPreconditionProvider : PreconditionProvider
    {
        private readonly int startDateOverallJunoOFR = -7;

        /// <summary>
        /// Initializes a new instance of the<see cref="JunoOverallOFRPreconditionProvider" />
        /// </summary>
        public JunoOverallOFRPreconditionProvider(IServiceCollection services)
            : base(services)
        {
            string query = Properties.Resources.JunoOfr;
            query.ThrowIfNullOrWhiteSpace(nameof(query));
            query = query.Replace(Constants.QueryStartTime, $"now({this.startDateOverallJunoOFR}d)", StringComparison.Ordinal);
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
        /// Determines if the number of Overall OFRs exceeds OFR Threshold
        /// </summary>
        /// <param name="component"> Describe Preconditions Scheduler can take, in this case OverallOFRThreshold</param>
        /// <param name="scheduleContext"><see cref="ScheduleContext"/></param>
        /// <param name="telemetryContext"> Describes the telemetry context this provider is running on, for logging</param>
        /// <param name="cancellationToken"> Propagates notification that operations should be canceled.</param>
        /// <returns> Execution Status and Condition Status <see cref="PreconditionResult"/></returns>
        protected override async Task<PreconditionResult> IsConditionSatisfiedAsync(Precondition component, ScheduleContext scheduleContext, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            component.ThrowIfNull(nameof(component));
            scheduleContext.ThrowIfNull(nameof(scheduleContext));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            ExecutionStatus executionStatus = ExecutionStatus.InProgress;
            bool conditionSatisfied = false;

            if (!cancellationToken.IsCancellationRequested)
            {
                int ofrThreshold = component.Parameters.GetValue<int>(Parameters.OverallOFRThreshold);

                IKustoManager kustoManager = this.Services.GetService<IKustoManager>();
                kustoManager.ThrowIfNull(nameof(kustoManager));

                List<JunoOFRNode> junoOfrs;
                List<string> tipList = new List<string>();

                try
                {
                    EnvironmentSettings settings = EnvironmentSettings.Initialize(scheduleContext.Configuration);
                    settings.ThrowIfNull(nameof(settings));

                    KustoSettings kustoSettings = settings.KustoSettings.Get(Setting.AzureCM);
                    kustoSettings.ThrowIfNull(nameof(kustoSettings));

                    this.ProviderContext.Add(SchedulerEventProperty.KustoQuery, this.Query);
                    DataTable response = await kustoManager.GetKustoResponseAsync(CacheKeys.OverallOFR, kustoSettings, this.Query)
                        .ConfigureDefaults();

                    junoOfrs = response.ParseOFRNodes();
                    junoOfrs.ForEach(node => tipList.Add(node.TipSessionId));

                    if (junoOfrs.Count >= ofrThreshold)
                    {
                        conditionSatisfied = true;
                    }

                    this.ProviderContext.Add(EventProperty.Count, junoOfrs.Count);
                    this.ProviderContext.Add(SchedulerEventProperty.Threshold, ofrThreshold);
                    this.ProviderContext.Add(SchedulerEventProperty.OffendingTipSessions, tipList);
                    this.ProviderContext.Add(SchedulerEventProperty.JunoOfrs, junoOfrs);
                    telemetryContext.AddContext(SchedulerEventProperty.JunoOfrs, junoOfrs);

                    executionStatus = ExecutionStatus.Succeeded;
                }
                catch (Exception exc)
                {
                    telemetryContext.AddError(exc, true);
                    executionStatus = ExecutionStatus.Failed;
                    conditionSatisfied = false;
                }
            }

            return new PreconditionResult(executionStatus, conditionSatisfied);
        }

        private class Parameters
        {
            internal const string OverallOFRThreshold = "overallOFRThreshold";
        }

        internal class Constants
        {
            internal const string QueryStartTime = "$startTime$";
        }
    }
}