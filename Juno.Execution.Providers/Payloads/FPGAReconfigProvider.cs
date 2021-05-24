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
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Provider that requests a reconfiguration of the FPGA card
    /// </summary>
    [ExecutionConstraints(SupportedStepType.Payload, SupportedStepTarget.ExecuteOnNode)]
    [ProviderInfo(Name = "Reconfigures the FPGA", Description = "Reconfigures the FPGA associated with the nodes/blades in the experiment group", FullDescription = "Step to reconfigure the FPGA associated with the nodes/blades in the experiment group.")]
    public class FPGAReconfigProvider : ExperimentProvider
    {
        // Default timeout for the reconfig to finish.
        internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(20);

        /// <summary>
        /// Initializes a new instance of the <see cref="FPGAReconfigProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public FPGAReconfigProvider(IServiceCollection services)
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
                FPGAReconfigProviderState state = await this.GetStateAsync<FPGAReconfigProviderState>(context, cancellationToken).ConfigureDefaults()
                    ?? new FPGAReconfigProviderState()
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

                if (!state.ReconfigRequested)
                {
                    this.RequestReconfig(state, telemetryContext, cancellationToken);
                    await this.SaveStateAsync(context, state, cancellationToken).ConfigureDefaults();
                }

                telemetryContext.AddContext("reconfigResult", state);
                if (state.ReconfigCompleted)
                {
                    result = new ExecutionResult(ExecutionStatus.Succeeded);
                }

                if (!state.ReconfigCompleted)
                {
                    result = new ExecutionResult(ExecutionStatus.Failed, new ProviderException(state.ReconfigOutput, ErrorReason.ProviderStateInvalid));
                }
            }
            else
            {
                result = new ExecutionResult(ExecutionStatus.Cancelled);
            }

            return result;
        }

        private void RequestReconfig(FPGAReconfigProviderState state, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                this.Logger.LogTelemetry($"{nameof(FPGAReconfigProvider)}.RequestReconfig", telemetryContext, () =>
                {
                    var fpgaManager = this.Services.GetService<IFPGAManager>();
                    var processExecution = this.Services.GetService<IProcessExecution>();

                    var reconfigResult = fpgaManager.ReconfigFPGA(processExecution);
                    state.ReconfigRequested = true;
                    state.ReconfigCompleted = reconfigResult.Succeeded;
                    state.ReconfigOutput = reconfigResult.ExecutionResult;
                });
            }
        }

        internal class FPGAReconfigProviderState
        {
            /// <summary>
            /// True if the provider has successfully requested the reconfiguration
            /// </summary>
            public bool ReconfigRequested { get; set; }

            /// <summary>
            /// True if the provider has successfully completed the reconfiguration
            /// </summary>
            public bool ReconfigCompleted { get; set; }

            /// <summary>
            /// Timeout for the step
            /// </summary>
            public DateTime StepTimeout { get; set; }

            /// <summary>
            /// Raw output of the flash command
            /// </summary>
            public string ReconfigOutput { get; set; }
        }

    }
}
