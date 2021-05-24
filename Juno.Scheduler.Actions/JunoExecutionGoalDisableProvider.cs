namespace Juno.Scheduler.Actions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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
    /// Class to manage Control Action: Juno.Scheduler.Goals.Control.Action.EditTargetGoal
    /// </summary>
    public class JunoExecutionGoalDisableProvider : ScheduleActionProvider
    {
        /// <summary>
        /// Function to disable scheduler
        /// </summary>
        /// /// <param name="services"><see cref="IServiceCollection"/></param>
        public JunoExecutionGoalDisableProvider(IServiceCollection services)
            : base(services)
        {
        }

        /// <summary>
        ///  Disabled scheduler trigger
        /// </summary>
        /// <param name="component"><see cref="Precondition"/></param>
        /// <param name="scheduleContext"><see cref="ScheduleContext"/></param>
        /// <param name="telemetryContext"> <see cref="EventContext"/></param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns></returns>
        protected override async Task<ExecutionResult> ExecuteActionAsync(ScheduleAction component, ScheduleContext scheduleContext, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            component.ThrowIfNull(nameof(component));
            scheduleContext.ThrowIfNull(nameof(scheduleContext));
            scheduleContext.ThrowIfInvalid(nameof(scheduleContext), (sc) =>
            {
                return (scheduleContext.TargetGoalTrigger != null);
            });
            telemetryContext.ThrowIfNull(nameof(telemetryContext));
            ExecutionResult result = new ExecutionResult(ExecutionStatus.InProgress);

            if (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    IScheduleTimerDataManager timerDataManager = this.Services.GetService<IScheduleTimerDataManager>();
                    timerDataManager.ThrowIfNull(nameof(timerDataManager));
                    TargetGoalTrigger targetGoal = scheduleContext.TargetGoalTrigger;
                    targetGoal.Enabled = false;
                    
                    await timerDataManager.UpdateTargetGoalTriggerAsync(targetGoal, cancellationToken).ConfigureDefaults();
                    this.ProviderContext.Add(SchedulerEventProperty.TargetGoal, targetGoal.TargetGoal);

                    result = new ExecutionResult(ExecutionStatus.Succeeded);
                }
                catch (Exception err)
                {
                    telemetryContext.AddError(err);
                    result = new ExecutionResult(ExecutionStatus.Failed);
                }
            }

            return cancellationToken.IsCancellationRequested 
                ? new ExecutionResult(ExecutionStatus.Cancelled)
                : result;
        }
    }
}
