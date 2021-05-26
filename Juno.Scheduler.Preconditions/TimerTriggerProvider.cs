namespace Juno.Scheduler.Preconditions
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;
    using NCrontab;

    /// <summary>
    /// A <see cref="PreconditionProvider"/> that evaluates if a cron expression'
    /// has an occurence in the next minute.
    /// </summary>
    [SupportedParameter(Name = Parameters.CronExpression, Type = typeof(string), Required = true)]
    public class TimerTriggerProvider : PreconditionProvider
    {
        private const int GracePeriodSeconds = 10;

        /// <summary>
        /// Initializes a new instance of the <see cref="TimerTriggerProvider"/> class.
        /// </summary>
        /// <param name="services">A list of services that can be used for dependency injection.</param>
        public TimerTriggerProvider(IServiceCollection services)
            : base(services)
        {
        }

        /// <summary>
        /// Evaluates whether the cron expression given has any occurences in the next minute.
        /// Conditon:
        ///     true if there is an occurence in the next minute. (+- some grace period)
        ///     false if there is no occurence in the next minute. (+- some grace period)
        /// </summary>
        protected override Task<bool> IsConditionSatisfiedAsync(Precondition component, ScheduleContext scheduleContext, EventContext telemetryContext, CancellationToken token)
        {
            component.ThrowIfNull(nameof(component));
            scheduleContext.ThrowIfNull(nameof(scheduleContext));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            DateTime startTime = DateTime.UtcNow.AddSeconds(-TimerTriggerProvider.GracePeriodSeconds);
            DateTime endTime = DateTime.UtcNow.AddMinutes(1).AddSeconds(TimerTriggerProvider.GracePeriodSeconds);

            string cronExpression = component.Parameters.GetValue<string>(Parameters.CronExpression);
            CrontabSchedule schedule = CrontabSchedule.Parse(cronExpression);
            return Task.FromResult(schedule.GetNextOccurrences(startTime, endTime).Any());
        }

        private class Parameters
        {
            public const string CronExpression = nameof(Parameters.CronExpression);
        }
    }
}
