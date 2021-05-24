namespace Juno.Scheduler.Preconditions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Extensions.Telemetry;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;
    using NCrontab;

    /// <summary>
    /// Evaluates whether the Cron Expression is Satisfied or not
    /// </summary>
    [SupportedParameter(Name = Parameters.CronExpression, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.StartTime, Type = typeof(DateTime), Required = true)]
    [SupportedParameter(Name = Parameters.EndTime, Type = typeof(DateTime), Required = true)]
    public class TimerTriggerProvider : PreconditionProvider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TimerTriggerProvider"/> class.
        /// </summary>
        /// <param name="services"></param>
        public TimerTriggerProvider(IServiceCollection services)
            : base(services)
        {
        }

        /// <summary>
        /// Evaluates whether the Cron Expression is Satisfied or not
        /// </summary>
        /// <param name="component"><see cref="Precondition"/></param>
        /// <param name="scheduleContext"><see cref="ScheduleContext"/></param>
        /// <param name="telemetryContext"><see cref="EventContext"/></param>
        /// <param name="token"><see cref="CancellationToken"/></param>
        /// <returns><see cref="PreconditionResult"/></returns>
        protected async override Task<PreconditionResult> IsConditionSatisfiedAsync(Precondition component, ScheduleContext scheduleContext, EventContext telemetryContext, CancellationToken token)
        {
            component.ThrowIfNull(nameof(component));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));
            token.ThrowIfNull(nameof(token));

            if (!token.IsCancellationRequested)
            {
                DateTime startTime = component.Parameters.GetValue<DateTime>(Parameters.StartTime);
                DateTime endTime = component.Parameters.GetValue<DateTime>(Parameters.EndTime);
                string cronExpression = component.Parameters.GetValue<string>(Parameters.CronExpression);
                try
                {
                    var parseOptions = new CrontabSchedule.ParseOptions()
                    {
                        IncludingSeconds = cronExpression.Trim().Count(char.IsWhiteSpace) == 4 ? false : true
                    };
                    CrontabSchedule crontabSchedule = CrontabSchedule.Parse(cronExpression, parseOptions);
                    IEnumerable<DateTime> nextOccurence = crontabSchedule.GetNextOccurrences(startTime, endTime);

                    this.ProviderContext.Add(SchedulerEventProperty.CronExpression, cronExpression);
                    this.ProviderContext.Add(EventProperty.StartTime, startTime);
                    this.ProviderContext.Add(EventProperty.EndTime, endTime);
                    this.ProviderContext.Add(SchedulerEventProperty.NextOccurence, nextOccurence);

                    bool conditionSatisfied = nextOccurence.Any();

                    return new PreconditionResult(conditionSatisfied ? ExecutionStatus.Succeeded : ExecutionStatus.Failed, conditionSatisfied);
                }
                catch (CrontabException exc)
                {
                    telemetryContext.AddError(exc, true);
                    return new PreconditionResult(ExecutionStatus.Failed, error: exc);
                }            
            }

            return new PreconditionResult(ExecutionStatus.Cancelled);
        }

        internal class Parameters
        {
            internal const string StartTime = "startTime";
            internal const string EndTime = "endTime";
            internal const string CronExpression = "cronExpression";
        }
    }
}
