namespace Juno.Execution.Providers.AutoTriage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Execution.TipIntegration;
    using Juno.Extensions.Telemetry;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Azure.Management.ContainerRegistry.Fluent.Models;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Polly;
    using TipGateway.Entities;

    /// <summary>
    /// Diagnoses the TIP Api service failures.
    /// </summary>
    public class TipApiServiceFailureDiagnostics : DiagnosticsProvider
    {
        private readonly ITipClient tipClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="TipApiServiceFailureDiagnostics"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        /// <param name="retryPolicy"></param>
        public TipApiServiceFailureDiagnostics(IServiceCollection services, IAsyncPolicy retryPolicy = null)
            : base(services, retryPolicy)
        {
            this.tipClient = services.GetService<ITipClient>();
        }

        /// <inheritdoc/>
        public override bool IsHandled(DiagnosticsRequest request)
        {
            request.ThrowIfNull(nameof(request));

            return request.Context.ContainsKey(DiagnosticsParameter.TipSessionId) &&
                request.Context.ContainsKey(DiagnosticsParameter.TipSessionChangeId) &&
                request.IssueType.Equals(DiagnosticsIssueType.TipPilotfishPackageDeploymentFailure);
        }

        /// <inheritdoc/>
        protected override async Task<ExecutionResult> DiagnoseAsync(DiagnosticsRequest request, EventContext telemetryContext, ILogger logger, CancellationToken cancellationToken)
        {
            request.ThrowIfNull(nameof(request));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            ExecutionResult executionResult = new ExecutionResult(ExecutionStatus.Succeeded);

            // get the execution state for the current diagnostic handler
            this.State = await this.GetStateAsync<DiagnosticState>(request, cancellationToken).ConfigureDefaults()
                ?? new DiagnosticState()
                {
                    Status = ExecutionStatus.InProgress
                };

            if (!this.State.IsTerminal && this.IsHandled(request))
            {
                string tipSessionId = request.Context.GetValue<string>(DiagnosticsParameter.TipSessionId);
                string tipSessionChangeId = request.Context.GetValue<string>(DiagnosticsParameter.TipSessionChangeId);

                if (!string.IsNullOrWhiteSpace(tipSessionChangeId))
                {
                    EventContext relatedContext = telemetryContext.Clone();
                    try
                    {
                        await logger.LogTelemetryAsync($"{nameof(TipApiServiceFailureDiagnostics)}.Diagnose", relatedContext, async () =>
                        {
                            IEnumerable<DiagnosticsEntry> entries = await this.GetTipSessionChangeDetailsAsync(tipSessionId, tipSessionChangeId, cancellationToken)
                                .ConfigureDefaults();

                            if (entries?.Any() == true)
                            {
                                await logger.LogTelemetryAsync($"{nameof(TipApiServiceFailureDiagnostics)}.DiagnosticsResults", relatedContext, entries)
                                    .ConfigureDefaults();
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

        private async Task<IEnumerable<DiagnosticsEntry>> GetTipSessionChangeDetailsAsync(string tipSessionId, string tipSessionChangeId, CancellationToken cancellationToken)
        {
            List<DiagnosticsEntry> entries = new List<DiagnosticsEntry>();
            await this.RetryPolicy.ExecuteAsync((Func<Task>)(async () =>
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    TipNodeSessionChangeDetails tipSessionChangeResult = await this.tipClient.GetTipSessionChangeAsync(tipSessionId, tipSessionChangeId, cancellationToken)
                    .ConfigureDefaults();
                    if (tipSessionChangeResult != null)
                    {
                        Dictionary<string, IConvertible> diagnosticsMessage = new Dictionary<string, IConvertible>()
                        {
                            { "tipSessionId", tipSessionId },
                            { "tipSessionChangeId", tipSessionChangeId },
                            { "errorMessage", tipSessionChangeResult.ErrorMessage ?? string.Empty }
                        };

                        entries.Add(new DiagnosticsEntry($"{nameof(TipApiServiceFailureDiagnostics)}.TipGateway.TipNodeSessionChangeDetails", diagnosticsMessage));
                    }
                }
            })).ConfigureAwait(false);

            return entries;
        }
    }
}