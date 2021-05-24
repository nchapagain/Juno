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
    /// Diagnoses the ARM VM deployment failure.
    /// </summary>
    public class ArmVmDeploymentFailureKustoDiagnostics : DiagnosticsProvider
    {
        private readonly IKustoQueryIssuer queryIssuer;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArmVmDeploymentFailureKustoDiagnostics"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the diagnostic handler.</param>
        /// <param name="retryPolicy">The optional retry policy for the diagnostic handler.</param>
        public ArmVmDeploymentFailureKustoDiagnostics(IServiceCollection services, IAsyncPolicy retryPolicy = null)
            : base(services, retryPolicy)
        {
            this.queryIssuer = services.GetService<IKustoQueryIssuer>();
        }

        /// <inheritdoc/>
        public override bool IsHandled(DiagnosticsRequest request)
        {
            request.ThrowIfNull(nameof(request));

            return request.Context.ContainsKey(DiagnosticsParameter.TipSessionId) &&
                request.Context.ContainsKey(DiagnosticsParameter.ResourceGroupName) &&
                request.IssueType.Equals(DiagnosticsIssueType.ArmVmCreationFailure);
        }

        /// <summary>
        /// When implemented executes this diagnostics handler's operation.
        /// </summary>
        /// <param name="request">The diagnostic request being processed.</param>
        /// <param name="telemetryContext">Event Context used for capturing telemetry.</param>
        /// <param name="logger">The logger that is used to capture the telemetry.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// The execution result from performing the diagnostics
        /// </returns>
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
                try
                {
                    IAsyncPolicy wrappedPolicy = Policy.WrapAsync(ArmVmDeploymentFailureKustoDiagnostics.PolicyWithFallBack(logger, relatedContext), this.RetryPolicy);

                    await logger.LogTelemetryAsync($"{nameof(ArmVmDeploymentFailureKustoDiagnostics)}.Diagnose", relatedContext, async () =>
                    {
                        string tipSessionId = request.Context.GetValue<string>(DiagnosticsParameter.TipSessionId);
                        string resourceGroupName = request.Context.GetValue<string>(DiagnosticsParameter.ResourceGroupName);

                        IEnumerable<DiagnosticsEntry> entries = await this.queryIssuer.GetArmVmDeploymentFailureKustoDiagnosticsAsync(
                            tipSessionId, 
                            resourceGroupName, 
                            request.TimeRangeBegin, 
                            request.TimeRangeEnd, 
                            wrappedPolicy)
                        .ConfigureDefaults();
                        if (entries?.Any() == true)
                        {
                            await logger.LogTelemetryAsync($"{nameof(ArmVmDeploymentFailureKustoDiagnostics)}.DiagnosticsResults", relatedContext, entries).ConfigureDefaults();
                        }
                    }).ConfigureDefaults();

                    this.State.Status = executionResult.Status;
                }
                finally
                {
                    await this.SaveStateAsync(request, this.State, cancellationToken).ConfigureDefaults();
                }
            }

            return executionResult;
        }

        private static IAsyncPolicy PolicyWithFallBack(ILogger logger, EventContext eventContext)
        {
            return Policy.Handle<Exception>()
                       .FallbackAsync(async (token) => await Task.CompletedTask.ConfigureDefaults(), async (exception) => 
                       await ArmVmDeploymentFailureKustoDiagnostics.LogFallbackErrorAsync(logger, eventContext, exception).ConfigureDefaults());
        }

        private static Task LogFallbackErrorAsync(ILogger logger, EventContext eventContext, Exception exception)
        {
            string eventName = $"{nameof(ArmVmDeploymentFailureKustoDiagnostics)}.Diagnose{EventNameSuffix.Error}";
            eventContext.AddError(exception, withCallStack: true);
            return logger.LogTelemetryAsync(eventName, LogLevel.Error, eventContext);
        }
    }
}