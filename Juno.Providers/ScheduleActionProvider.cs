namespace Juno.Providers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Extensions.Telemetry;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Provides a base implementation for an <see cref="IScheduleActionProvider"/>
    /// for other derived provider instances.
    /// </summary>
    public abstract class ScheduleActionProvider : GoalComponentProvider, IScheduleActionProvider
    {
        /// <summary>
        /// Initializes a new instance of the see <see cref="ScheduleActionProvider"/>
        /// </summary>
        /// <param name="services"></param>
        protected ScheduleActionProvider(IServiceCollection services)
            : base(services)
        {
        }

        /// <see cref="IScheduleActionProvider.ExecuteActionAsync"/>
        public async Task<ExecutionResult> ExecuteActionAsync(ScheduleAction component, ScheduleContext scheduleContext, CancellationToken cancellationToken) 
        {
            component.ThrowIfNull(nameof(component));
            scheduleContext.ThrowIfNull(nameof(scheduleContext));

            ExecutionResult scheduleActionResult = new ExecutionResult(ExecutionStatus.Pending);

            // Persist the activity ID so that it can be used down the callstack.
            EventContext telemetryContext = EventContext.Persisted();
            telemetryContext.AddContext(scheduleContext, component);

            try
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    scheduleActionResult = await this.Logger.LogTelemetryAsync($"ScheduleAction.{this.GetType().Name}.Execute", telemetryContext, async () =>
                    {
                        await this.ConfigureServicesAsync(component, scheduleContext).ConfigureDefaults();
                        this.ValidateParameters(component);
                        return await this.ExecuteActionAsync(component, scheduleContext, telemetryContext, cancellationToken)
                            .ConfigureDefaults();

                    }).ConfigureDefaults();
                }
            }
            catch (TaskCanceledException)
            {
                scheduleActionResult = new ExecutionResult(ExecutionStatus.Cancelled);
            }
            catch (Exception exc)
            {
                scheduleActionResult = new ExecutionResult(ExecutionStatus.Failed, error: exc);
            }

            telemetryContext.AddContext(SchedulerEventProperty.ScheduleActionResult, scheduleActionResult);
            telemetryContext.AddContext(EventProperty.MetaData, this.ProviderContext);
            telemetryContext.AddContext(this.ProviderContext);  // remove this in future
            telemetryContext.AddContext(SchedulerEventProperty.ScheduleContext, component);

            this.Logger.LogTelemetry($"ScheduleAction.{this.GetType().Name}.Result", LogLevel.Information, telemetryContext);

            return cancellationToken.IsCancellationRequested 
                ? new ExecutionResult(ExecutionStatus.Cancelled) 
                : scheduleActionResult;
        }

        /// <summary>
        /// Performs a action. Allows for derived class to implement logic
        /// to perform different actions. 
        /// </summary>
        /// <param name="component">The Schedule Action to execute</param>
        /// <param name="scheduleContext">Context that allows individual providers to know context in which it executes</param>
        /// <param name="telemetryContext">Logging context</param>
        /// <param name="cancellationToken">Token used to cancel execution</param>
        /// <returns></returns>
        protected abstract Task<ExecutionResult> ExecuteActionAsync(ScheduleAction component, ScheduleContext scheduleContext, EventContext telemetryContext, CancellationToken cancellationToken);
    }
}
