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

    /// <summary>
    /// Provides a base implementation for an <see cref="IScheduleActionProvider"/>
    /// for other derived provider instances.
    /// </summary>
    public abstract class ScheduleActionProvider : GoalComponentProvider, IScheduleActionProvider
    {
        /// <summary>
        /// Initializes a new instance of the see <see cref="ScheduleActionProvider"/>
        /// </summary>
        /// <param name="services">A list of services that can be used for dependency injection.</param>
        protected ScheduleActionProvider(IServiceCollection services)
            : base(services)
        {
        }

        /// <see cref="IScheduleActionProvider.ExecuteActionAsync"/>
        public async Task ExecuteActionAsync(ScheduleAction component, ScheduleContext scheduleContext, CancellationToken cancellationToken) 
        {
            component.ThrowIfNull(nameof(component));
            scheduleContext.ThrowIfNull(nameof(scheduleContext));

            EventContext telemetryContext = EventContext.Persisted();
            telemetryContext.AddContext(scheduleContext, component);

            if (!cancellationToken.IsCancellationRequested)
            {
                await this.Logger.LogTelemetryAsync($"ScheduleAction.{this.GetType().Name}.Execute", telemetryContext, async () =>
                {
                    await this.ConfigureServicesAsync(component, scheduleContext).ConfigureDefaults();
                    this.ValidateParameters(component);
                    await this.ExecuteActionAsync(component, scheduleContext, telemetryContext, cancellationToken)
                        .ConfigureDefaults();

                }).ConfigureDefaults();
            }
        }

        /// <summary>
        /// Performs a action. Allows for derived class to implement logic
        /// to perform different actions. 
        /// </summary>
        /// <param name="component">The Schedule Action to execute</param>
        /// <param name="scheduleContext">Context that allows individual providers to know context in which it executes</param>
        /// <param name="telemetryContext">Logging context</param>
        /// <param name="cancellationToken">Token used to cancel execution</param>
        /// <returns>An awaitable task.</returns>
        protected abstract Task ExecuteActionAsync(ScheduleAction component, ScheduleContext scheduleContext, EventContext telemetryContext, CancellationToken cancellationToken);
    }
}
