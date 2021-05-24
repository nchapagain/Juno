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

        /// <summary>
        /// <see cref="IPreconditionProvider.IsConditionSatisfiedAsync"/>
        /// </summary>
        public async Task<PreconditionResult> IsConditionSatisfiedAsync(Precondition component, ScheduleContext scheduleContext, CancellationToken cancellationToken)
        {
            component.ThrowIfNull(nameof(component));
            scheduleContext.ThrowIfNull(nameof(scheduleContext));

            PreconditionResult preconditionResult = new PreconditionResult(ExecutionStatus.Pending);

            // Persist the activity ID so that it can be used down the callstack.
            EventContext telemetryContext = EventContext.Persisted();
            telemetryContext.AddContext(scheduleContext, component);

            try
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    preconditionResult = await this.Logger.LogTelemetryAsync($"Precondition.{this.GetType().Name}.Execute", telemetryContext, async () =>
                    {
                        this.ValidateParameters(component);
                        await this.ConfigureServicesAsync(component, scheduleContext).ConfigureDefaults();
                        return await this.IsConditionSatisfiedAsync(component, scheduleContext, telemetryContext, cancellationToken)
                            .ConfigureDefaults();

                    }).ConfigureDefaults();
                }
            }
            catch (TaskCanceledException)
            {
                preconditionResult = new PreconditionResult(ExecutionStatus.Cancelled);
            }
            catch (Exception exc)
            {
                preconditionResult = new PreconditionResult(ExecutionStatus.Failed, error: exc);
            }

            telemetryContext.AddContext(SchedulerEventProperty.PreconditionResult, preconditionResult);
            telemetryContext.AddContext(EventProperty.MetaData, this.ProviderContext);
            telemetryContext.AddContext(this.ProviderContext); // remove this in future
            telemetryContext.AddContext(SchedulerEventProperty.ScheduleContext, component);
            this.Logger.LogTelemetry($"Precondition.{this.GetType().Name}.Result", LogLevel.Information, telemetryContext);

            return cancellationToken.IsCancellationRequested 
                ? new PreconditionResult(ExecutionStatus.Cancelled)
                : preconditionResult;
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
        /// <returns></returns>
        protected abstract Task<PreconditionResult> IsConditionSatisfiedAsync(Precondition component, ScheduleContext scheduleContext, EventContext telemetryContext, CancellationToken cancellationToken);
    }
}
