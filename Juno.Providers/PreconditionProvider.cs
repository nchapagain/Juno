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
    /// Provides a base implementation for an <see cref="IPreconditionProvider"/>
    /// for other derived provider instances.
    /// </summary>
    public abstract class PreconditionProvider : GoalComponentProvider, IPreconditionProvider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PreconditionProvider"/>
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider</param>
        protected PreconditionProvider(IServiceCollection services)
            : base(services)
        {
        }

        /// <inheritdoc/>
        public async Task<bool> IsConditionSatisfiedAsync(Precondition component, ScheduleContext scheduleContext, CancellationToken cancellationToken)
        {
            component.ThrowIfNull(nameof(component));
            scheduleContext.ThrowIfNull(nameof(scheduleContext));

            EventContext telemetryContext = EventContext.Persisted();
            telemetryContext.AddContext(scheduleContext, component);

            if (!cancellationToken.IsCancellationRequested)
            {
                return await this.Logger.LogTelemetryAsync($"Precondition.{this.GetType().Name}.Execute", telemetryContext, async () =>
                {
                    this.ValidateParameters(component);
                    await this.ConfigureServicesAsync(component, scheduleContext).ConfigureDefaults();
                    bool result = await this.IsConditionSatisfiedAsync(component, scheduleContext, telemetryContext, cancellationToken)
                        .ConfigureDefaults();

                    telemetryContext.AddContext(nameof(result), result);

                    return result;
                }).ConfigureDefaults();
            }

            return false;
        }

        /// <summary>
        /// Evaluates the Precondition and determines whether or not it is satsified.
        /// Allows derived classes to implement their own respective logic utilizing
        /// the services set up if necessary to evaluate the Precondition.
        /// </summary>
        /// <param name="component">The Precondition to evaluate</param>
        /// <param name="scheduleContext">Allows provider to understand the context in which the provider is being executed</param>
        /// <param name="telemetryContext">Logging context</param>
        /// <param name="cancellationToken">Token used to cancel execution</param>
        /// <returns>An awaitable task that returns True/False if the condition is satisfied.</returns>
        protected abstract Task<bool> IsConditionSatisfiedAsync(Precondition component, ScheduleContext scheduleContext, EventContext telemetryContext, CancellationToken cancellationToken);
    }
}
