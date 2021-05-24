namespace Juno.Execution.Providers.AutoTriage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Extensions.Telemetry;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Kusto;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Polly;

    /// <summary>
    /// Diagnoses the tip deployment failures.
    /// </summary>
    public class TipDeploymentFailureKustoDiagnostics : DiagnosticsProvider
    {
        private readonly IKustoQueryIssuer queryIssuer;

        /// <summary>
        /// Initializes a new instance of the <see cref="TipDeploymentFailureKustoDiagnostics"/> class.
        /// </summary>
        /// <param name="services">Specifies query issuer object.</param>
        /// <param name="retryPolicy"></param>
        public TipDeploymentFailureKustoDiagnostics(IServiceCollection services, IAsyncPolicy retryPolicy = null)
            : base(services, retryPolicy)
        {
            this.queryIssuer = services.GetService<IKustoQueryIssuer>();
        }

        /// <inheritdoc/>
        public override bool IsHandled(DiagnosticsRequest request)
        {
            request.ThrowIfNull(nameof(request));

            return request.Context.ContainsKey(DiagnosticsParameter.TipSessionId) &&
                request.IssueType.Equals(DiagnosticsIssueType.TipPilotfishPackageDeploymentFailure);
        }

        /// <inheritdoc/>
        protected override async Task<ExecutionResult> DiagnoseAsync(DiagnosticsRequest request, EventContext telemetryContext, ILogger logger, CancellationToken cancellationToken)
        {
            request.ThrowIfNull(nameof(request));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            ExecutionResult executionResult = new ExecutionResult(ExecutionStatus.Succeeded);

            this.State = await this.GetStateAsync<DiagnosticState>(request, cancellationToken).ConfigureDefaults()
                ?? new DiagnosticState()
                {
                    Status = ExecutionStatus.InProgress
                };

            if (!this.State.IsTerminal && this.IsHandled(request))
            {
                EventContext relatedContext = telemetryContext.Clone();
                if (!this.State.IsTerminal)
                {
                    try
                    {
                        await logger.LogTelemetryAsync($"{nameof(TipDeploymentFailureKustoDiagnostics)}.Diagnose", relatedContext, async () =>
                        {
                            string tipSessionId = request.Context.GetValue<string>(DiagnosticsParameter.TipSessionId);
                            IEnumerable<DiagnosticsEntry> entries = await this.queryIssuer.GetTipDeploymentFailureDiagnosticsAsync(tipSessionId, this.RetryPolicy).ConfigureDefaults();
                            if (entries?.Any() == true)
                            {
                                await logger.LogTelemetryAsync($"{nameof(TipDeploymentFailureKustoDiagnostics)}.DiagnosticsResults", relatedContext, entries).ConfigureDefaults();
                            }
                        }).ConfigureDefaults();

                        this.State.Status = executionResult.Status;
                    }
                    finally
                    {
                        await this.SaveStateAsync(request, this.State, cancellationToken).ConfigureDefaults();
                    }
                }
            }

            return executionResult;
        }
    }
}