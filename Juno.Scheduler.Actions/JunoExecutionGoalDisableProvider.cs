namespace Juno.Scheduler.Actions
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.DataManagement;
    using Juno.Extensions.Telemetry;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// A <see cref="ScheduleActionProvider"/> that disables the current
    /// schedule contexts target goal.
    /// </summary>
    public class JunoExecutionGoalDisableProvider : ScheduleActionProvider
    {
        /// <summary>
        /// Function to disable scheduler
        /// </summary>
        /// /// <param name="services">A list of services that can be used for dependency injection.S</param>
        public JunoExecutionGoalDisableProvider(IServiceCollection services)
            : base(services)
        {
        }

        /// <summary>
        /// Disable the current target goal.
        /// </summary>
        protected override async Task ExecuteActionAsync(ScheduleAction component, ScheduleContext scheduleContext, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            component.ThrowIfNull(nameof(component));
            scheduleContext.ThrowIfNull(nameof(scheduleContext));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            if (!cancellationToken.IsCancellationRequested)
            {
                IScheduleTimerDataManager timerDataManager = this.Services.GetService<IScheduleTimerDataManager>();
                TargetGoalTrigger targetGoal = scheduleContext.TargetGoalTrigger;
                targetGoal.Enabled = false;
                    
                await timerDataManager.UpdateTargetGoalTriggerAsync(targetGoal, cancellationToken).ConfigureDefaults();
                telemetryContext.AddContext(SchedulerEventProperty.TargetGoal, targetGoal.Name);
            }
        }
    }
}
