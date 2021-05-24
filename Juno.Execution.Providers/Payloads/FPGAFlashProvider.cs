namespace Juno.Execution.Providers.Payloads
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Execution.AgentRuntime.Windows;
    using Juno.Execution.ArmIntegration;
    using Juno.Execution.TipIntegration;
    using Juno.Extensions.Telemetry;
    using Juno.Providers;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Provider that requests a reconfiguration of the FPGA card
    /// </summary>
    [ExecutionConstraints(SupportedStepType.Payload, SupportedStepTarget.ExecuteOnNode)]
    [SupportedParameter(Name = Parameters.ImageFileName, Type = typeof(string), Required = true)]
    [ProviderInfo(Name = "Flash the FPGA", Description = "Flashes/re-images the FPGA associated with the nodes/blades in the experiment group", FullDescription = "Step to flash/re-image the FPGA associated with the nodes/blades in the experiment group.")]
    public class FPGAFlashProvider : ExperimentProvider
    {
        // Default timeout for the reconfig to finish.
        internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(20);

        /// <summary>
        /// Initializes a new instance of the <see cref="FPGAFlashProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public FPGAFlashProvider(IServiceCollection services)
            : base(services)
        {
        }

        /// <inheritdoc/>
        public override Task ConfigureServicesAsync(ExperimentContext context, ExperimentComponent component)
        {
            IFPGAManager fpgaManager;
            if (!this.Services.TryGetService<IFPGAManager>(out fpgaManager))
            {
                this.Services.AddTransient<IFPGAManager>((provider) => new FPGAManager());
            }

            if (!this.Services.TryGetService<IProcessExecution>(out var processExec))
            {
                this.Services.AddTransient<IProcessExecution>((provider) => new ProcessExecution());
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        protected override async Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            ExecutionResult result = new ExecutionResult(ExecutionStatus.InProgress);

            if (!cancellationToken.IsCancellationRequested)
            {
                TimeSpan timeout = component.Parameters.GetTimeSpanValue(StepParameters.Timeout, FPGAReconfigProvider.DefaultTimeout);
                FPGAFlashProviderState state = await this.GetStateAsync<FPGAFlashProviderState>(context, cancellationToken).ConfigureDefaults()
                    ?? new FPGAFlashProviderState()
                    {
                        StepTimeout = DateTime.UtcNow.Add(timeout)
                    };

                if (DateTime.UtcNow > state.StepTimeout)
                {
                    throw new ProviderException(
                       $"Timeout expired. The reconfig and verification process did not complete within the time range " +
                       $"allowed (timeout = '{state.StepTimeout}')",
                       ErrorReason.Timeout);
                }

                if (!state.FlashRequested)
                {
                    this.RequestFlash(component, state, telemetryContext, cancellationToken);
                    await this.SaveStateAsync(context, state, cancellationToken).ConfigureDefaults();
                }

                telemetryContext.AddContext("flashResult", state);

                if (state.FlashCompleted)
                {
                    result = new ExecutionResult(ExecutionStatus.Succeeded);
                }

                if (!state.FlashCompleted)
                {
                    result = new ExecutionResult(ExecutionStatus.Failed, new ProviderException(state.FlashOutput, ErrorReason.ProviderStateInvalid));
                }
            }
            else
            {
                result = new ExecutionResult(ExecutionStatus.Cancelled);
            }

            return result;
        }

        private void RequestFlash(ExperimentComponent component, FPGAFlashProviderState state, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                this.Logger.LogTelemetry($"{nameof(FPGAReconfigProvider)}.RequestFlash", telemetryContext, () =>
                {
                    var fpgaManager = this.Services.GetService<IFPGAManager>();
                    var processExecution = this.Services.GetService<IProcessExecution>();
                    var fileName = component.Parameters.GetValue<string>(Parameters.ImageFileName);

                    var flashResult = fpgaManager.FlashFPGA(processExecution, fileName);
                    state.FlashRequested = true;
                    state.FlashCompleted = flashResult.Succeeded;
                    state.FlashOutput = flashResult.ExecutionResult;
                });
            }
        }

        internal class FPGAFlashProviderState
        {
            /// <summary>
            /// True if the provider has successfully requested the reconfiguration
            /// </summary>
            public bool FlashRequested { get; set; }

            /// <summary>
            /// True if the provider has successfully completed the reconfiguration
            /// </summary>
            public bool FlashCompleted { get; set; }

            /// <summary>
            /// Raw output of the flash command
            /// </summary>
            public string FlashOutput { get; set; }

            /// <summary>
            /// Timeout for the step
            /// </summary>
            public DateTime StepTimeout { get; set; }

        }

        private class Parameters
        {
            internal const string ImageFileName = "imageFileName";
        }

    }
}
