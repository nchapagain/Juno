namespace Juno.Execution.Providers.Payloads
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Extensions.Telemetry;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Provider to activate CPU Microcode.
    /// Internal-only provider
    /// </summary>
    [ExecutionConstraints(SupportedStepType.Payload, SupportedStepTarget.ExecuteOnNode)]
    [SupportedParameter(Name = Parameters.MicrocodeVersion, Type = typeof(string), Required = true)]
    public class MicrocodeActivationProvider : ExperimentProvider
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Initializes a new instance of the <see cref="MicrocodeActivationProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public MicrocodeActivationProvider(IServiceCollection services)
           : base(services)
        {
        }

        /// <inheritdoc />
        protected override async Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));

            ExecutionResult result = new ExecutionResult(ExecutionStatus.InProgress);

            if (!cancellationToken.IsCancellationRequested)
            {
                MicrocodeActivationProviderState state = await this.GetStateAsync<MicrocodeActivationProviderState>(context, cancellationToken, sharedState: false).ConfigureDefaults()
                    ?? new MicrocodeActivationProviderState();

                if (!cancellationToken.IsCancellationRequested)
                {
                    string expectedMicrocodeVersion = component.Parameters.GetValue<string>(Parameters.MicrocodeVersion);
                    TimeSpan timeout = component.Parameters.GetTimeSpanValue(StepParameters.Timeout, MicrocodeActivationProvider.DefaultTimeout);

                    if (!state.ActivationBegan)
                    {
                        state.ActivationBegan = true;
                        state.Timeout = timeout;
                        state.StepTimeout = DateTime.UtcNow.Add(timeout);

                        await this.SaveStateAsync(context, state, cancellationToken, sharedState: false).ConfigureDefaults();
                    }

                    if (DateTime.UtcNow > state.StepTimeout)
                    {
                        throw new ProviderException(
                           $"Timeout expired. The microcode activation/verification process did not complete within the time range " +
                           $"allowed (timeout = '{state.Timeout}')",
                           ErrorReason.Timeout);
                    }

                    IPayloadActivator payloadActivator;
                    if (!this.Services.TryGetService<IPayloadActivator>(out payloadActivator))
                    {
                        payloadActivator = new MicrocodeActivator(expectedMicrocodeVersion, TimeSpan.FromSeconds(30), logger: this.Logger);
                    }

                    ActivationResult activationResult = await payloadActivator.ActivateAsync(cancellationToken).ConfigureAwait(false);
                    telemetryContext.AddContext(nameof(activationResult), activationResult);
                    telemetryContext.AddContext("isUpdated", activationResult.IsActivated);
                    telemetryContext.AddContext("updatedTime", activationResult.ActivationTime);

                    if (activationResult.IsActivated)
                    {
                        result = new ExecutionResult(ExecutionStatus.Succeeded);
                    }
                }
            }

            return result;
        }

        internal class MicrocodeActivationProviderState
        {
            public bool ActivationBegan { get; set; }

            public TimeSpan Timeout { get; set; }

            public DateTime StepTimeout { get; set; }
        }

        private class Parameters
        {
            internal const string MicrocodeVersion = "MicrocodeVersion";
        }
    }
}