namespace Juno.Execution.Providers.Diagnostics
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
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Provider enables to sleep for a duration.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.Diagnostics, SupportedStepTarget.ExecuteRemotely)]
    [SupportedParameter(Name = StepParameters.Duration, Type = typeof(TimeSpan), Required = true)]
    [SupportedParameter(Name = Parameters.Option, Type = typeof(SleepProviderOption), Required = false)]
    [ProviderInfo(Name = "Wait for a period of time", Description = "Waits/sleeps for a period of time before allowing the experiment workflow/steps to continue", FullDescription = "Step to wait/sleep for a period of time before allowing the experiment workflow/steps to continue. The following 'parameters' will be used creating experiment step: duration, option.")]
    public class SleepProvider : ExperimentProvider
    {
        private static readonly TimeSpan ReevaluationExtension = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Initializes a new instance of the <see cref="SleepProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public SleepProvider(IServiceCollection services)
            : base(services)
        {
        }

        internal enum SleepProviderOption
        {
            Always,
            WhenAnyPreviousStepsFailed,
            WhenNoPreviousStepsFailed
        }

        /// <inheritdoc />
        protected override async Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            ExecutionResult result = new ExecutionResult(ExecutionStatus.InProgress, extensionTimeout: SleepProvider.ReevaluationExtension);

            if (!cancellationToken.IsCancellationRequested)
            {
                TimeSpan duration = component.Parameters.GetTimeSpanValue(StepParameters.Duration);

                State state = await this.GetStateAsync<State>(context, cancellationToken, sharedState: false).ConfigureDefaults()
                    ?? new State
                    {
                        SleepStartTime = DateTime.UtcNow,
                        SleepEndTime = DateTime.UtcNow + duration
                    };

                // The default behavior of the sleep provider is to perform a 'sleep' for the duration of time defined.
                SleepProviderOption sleepOption = component.Parameters.GetEnumValue<SleepProviderOption>(Parameters.Option, SleepProviderOption.Always);

                if (DateTime.UtcNow >= state.SleepEndTime)
                {
                    result = new ExecutionResult(ExecutionStatus.Succeeded);
                }
                else
                {
                    IEnumerable<ExperimentStepInstance> experimentSteps = await this.GetExperimentStepsAsync(context, telemetryContext, cancellationToken)
                        .ConfigureDefaults();

                    if (experimentSteps?.Any() == true)
                    {
                        switch (sleepOption)
                        {
                            case SleepProviderOption.WhenAnyPreviousStepsFailed:
                                if (!experimentSteps.AnyPreviousStepsInStatus(context.ExperimentStep, ExecutionStatus.Failed))
                                {
                                    result = new ExecutionResult(ExecutionStatus.Succeeded);
                                }

                                break;

                            case SleepProviderOption.WhenNoPreviousStepsFailed:
                                if (experimentSteps.AnyPreviousStepsInStatus(context.ExperimentStep, ExecutionStatus.Failed))
                                {
                                    result = new ExecutionResult(ExecutionStatus.Succeeded);
                                }

                                break;
                        }
                    }
                }

                await this.SaveStateAsync(context, state, cancellationToken, sharedState: false).ConfigureDefaults();
            }

            return result;
        }

        private async Task<IEnumerable<ExperimentStepInstance>> GetExperimentStepsAsync(ExperimentContext context, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            IEnumerable<ExperimentStepInstance> steps = null;
            try
            {
                IProviderDataClient apiClient = this.Services.GetService<IProviderDataClient>();
                steps = await apiClient.GetExperimentStepsAsync(context.Experiment, cancellationToken).ConfigureDefaults();
            }
            catch (Exception exc)
            {
                // We aren't going to fail out if the call to get the steps fails. We will retry the next time around until we
                // satisfy the conditions of the sleep provider.
                EventContext relatedContext = telemetryContext.Clone()
                    .AddError(exc);

                await this.Logger.LogTelemetryAsync($"{nameof(SleepProvider)}.GetExperimentStepsError", LogLevel.Warning, relatedContext)
                    .ConfigureDefaults();
            }

            return steps;
        }

        internal class State
        {
            /// <summary>
            /// When the sleep should end.
            /// </summary>
            public DateTime SleepEndTime { get; set; }

            /// <summary>
            /// The sleep start time.
            /// </summary>
            public DateTime SleepStartTime { get; set; }
        }

        private static class Parameters
        {
            public const string Option = "option";
        }
    }
}