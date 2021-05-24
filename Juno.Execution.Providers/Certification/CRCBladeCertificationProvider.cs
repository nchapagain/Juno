namespace Juno.Execution.Providers.Certification
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Execution.AgentRuntime.Windows;
    using Juno.Execution.ArmIntegration;
    using Juno.Execution.Providers.Payloads;
    using Juno.Execution.TipIntegration;
    using Juno.Extensions.Telemetry;
    using Juno.Providers;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Newtonsoft.Json;
    using TipGateway.Entities;

    /// <summary>
    /// Provider that requests a certification of the blade
    /// </summary>
    [ExecutionConstraints(SupportedStepType.Certification, SupportedStepTarget.ExecuteOnNode)]
    [ProviderInfo(Name = "Certify blade", Description = "Certifies the physical node/blade used in the experiment to ensure it is ready before returning it to the Azure production fleet", FullDescription = "Step to certify the physical node/blade used in the experiment to ensure it is ready before returning it to the Azure production fleet.")]    
    public class CRCBladeCertificationProvider : ExperimentProvider
    {
        // Default timeout for the reconfig to finish.
        internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(20);

        /// <summary>
        /// Initializes a new instance of the <see cref="FPGAReconfigProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public CRCBladeCertificationProvider(IServiceCollection services)
            : base(services)
        {
        }

        /// <inheritdoc/>
        public override Task ConfigureServicesAsync(ExperimentContext context, ExperimentComponent component)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));

            ICertificationManager certificationMgr;
            if (!this.Services.TryGetService<ICertificationManager>(out certificationMgr))
            {
                this.Services.AddTransient<ICertificationManager>((provider) => new CertificationManager());
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
                CRCBladeCertificationProviderState state = await this.GetStateAsync<CRCBladeCertificationProviderState>(context, cancellationToken).ConfigureDefaults()
                    ?? new CRCBladeCertificationProviderState()
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

                if (!state.CertificationRequested)
                {
                    this.RequestCertification(state, telemetryContext, cancellationToken);
                    await this.SaveStateAsync(context, state, cancellationToken).ConfigureDefaults();
                }

                telemetryContext.AddContext("certificationResult", state);
                if (state.CertificationCompleted)
                {
                    result = new ExecutionResult(ExecutionStatus.Succeeded);
                }

                if (!state.CertificationCompleted)
                {
                    result = new ExecutionResult(ExecutionStatus.Failed, new ProviderException(state.CertificationOutput, ErrorReason.ProviderStateInvalid));
                }
            }
            else
            {
                result = new ExecutionResult(ExecutionStatus.Cancelled);
            }

            return result;
        }

        private void RequestCertification(CRCBladeCertificationProviderState state, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                this.Logger.LogTelemetry($"{nameof(FPGAReconfigProvider)}.RequestReconfig", telemetryContext, () =>
                {
                    var certificationManager = this.Services.GetService<ICertificationManager>();
                    var processExecution = this.Services.GetService<IProcessExecution>();

                    var certificationResult = certificationManager.Certify(processExecution, out var result);
                    state.CertificationRequested = true;
                    state.CertificationCompleted = certificationResult;
                    state.CertificationOutput = result;
                });
            }
        }

        internal class CRCBladeCertificationProviderState
        {
            /// <summary>
            /// True if the provider has successfully requested the certification
            /// </summary>
            public bool CertificationRequested { get; set; }

            /// <summary>
            /// True if the provider has successfully completed the certification
            /// </summary>
            public bool CertificationCompleted { get; set; }
            
            /// <summary>
            /// Raw output of the certification command
            /// </summary>
            public string CertificationOutput { get; set; }

            /// <summary>
            /// Timeout for the step
            /// </summary>
            public DateTime StepTimeout { get; set; }
        }

    }
}
