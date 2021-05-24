namespace Juno.Execution.Providers.AutoTriage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Execution.ArmIntegration;
    using Juno.Extensions.Telemetry;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Polly;

    /// <summary>
    /// Diagnoses the ARM VM deployment failure activity logs.
    /// </summary>
    public class ArmVmDeploymentFailureActivityLogDiagnostics : DiagnosticsProvider
    {
        private IArmClient armClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArmVmDeploymentFailureActivityLogDiagnostics"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        /// <param name="retryPolicy">The optional retry policy for the diagnostic handler.</param>
        public ArmVmDeploymentFailureActivityLogDiagnostics(IServiceCollection services, IAsyncPolicy retryPolicy = null)
            : base(services, retryPolicy)
        {
            this.armClient = services.GetService<IArmClient>();
        }

        /// <inheritdoc/>
        public override bool IsHandled(DiagnosticsRequest request)
        {
            request.ThrowIfNull(nameof(request));

            return (request.Context.ContainsKey(DiagnosticsParameter.SubscriptionId) &&
                request.Context.ContainsKey(DiagnosticsParameter.ResourceGroupName)) &&
                request.IssueType == DiagnosticsIssueType.ArmVmCreationFailure;
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
                try
                {
                    await logger.LogTelemetryAsync($"{nameof(ArmVmDeploymentFailureActivityLogDiagnostics)}.Diagnose", relatedContext, async () =>
                    {
                        IEnumerable<DiagnosticsEntry> entries = await this.GetDiagnosticEntriesAsync(request, cancellationToken).ConfigureDefaults();

                        if (entries?.Any() == true)
                        {
                            await logger.LogTelemetryAsync($"{nameof(ArmVmDeploymentFailureActivityLogDiagnostics)}.DiagnosticsResults", relatedContext, entries).ConfigureDefaults();
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

        private async Task<IEnumerable<DiagnosticsEntry>> GetDiagnosticEntriesAsync(DiagnosticsRequest request, CancellationToken cancellationToken)
        {
            IEnumerable<DiagnosticsEntry> errorLog = null;
            string subscriptionId = request.Context.GetValue<string>(DiagnosticsParameter.SubscriptionId);
            string resourceGroupName = request.Context.GetValue<string>(DiagnosticsParameter.ResourceGroupName);

            await this.RetryPolicy.ExecuteAsync(async () =>
            {
                HttpResponseMessage response = await this.GetActivityLogsAsync(subscriptionId, resourceGroupName, request.TimeRangeBegin, request.TimeRangeEnd, cancellationToken).ConfigureAwait(false);
                // if this returns the error logs should be the content returned
                if (response.IsSuccessStatusCode && response.Content != null)
                {
                    string jsonString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    ArmActivityLogEntry result = jsonString.FromJson<ArmActivityLogEntry>();
                    if (result?.Value?.Any() == true)
                    {
                        List<EventLogDataValues> errors = new List<EventLogDataValues>();
                        // filter the results by Error/Critical logs
                        foreach (EventLogDataValues log in result.Value)
                        {
                            if (log.Level.Contains("Error", StringComparison.OrdinalIgnoreCase) || log.Level.Contains("Critical", StringComparison.OrdinalIgnoreCase))
                            {
                                errors.Add(log);
                            }
                        }

                        if (errors?.Any() == true)
                        {
                            errorLog = errors.Select(errorDict =>
                            {
                                return new DiagnosticsEntry($"{nameof(ArmVmDeploymentFailureActivityLogDiagnostics)}.Diagnose{EventNameSuffix.Error}", new Dictionary<string, IConvertible>()
                                {
                                        { "timestamp", errorDict.EventTimestamp },
                                        { "correlationId", errorDict.CorrelationId },
                                        { "level", errorDict.Level },
                                        { "operationName", errorDict.OperationName.ToString() },
                                        { "resourceGroupName", errorDict.ResourceGroupName },
                                        { "resourceType", errorDict.ResourceType.ToString() },
                                        { "properties", errorDict.Properties.ToString() },
                                        { "status", errorDict.Status.ToString() }
                                });
                            });
                        }
                    }
                }
            }).ConfigureDefaults();

            // ensure that a non-null value is returned
            return errorLog = (errorLog != null) ? errorLog : new List<DiagnosticsEntry>();
        }

        private Task<HttpResponseMessage> GetActivityLogsAsync(string subscriptionId, string resourceGroupName, DateTime timeRangeBegin, DateTime timeRangeEnd, CancellationToken cancellationToken)
        {
            string filter = $"eventTimestamp ge '{timeRangeBegin.ToUniversalTime().ToString("o")}' and eventTimestamp le '{timeRangeEnd.ToUniversalTime().ToString("o")}' and resourceGroupName eq '{resourceGroupName}'";
            IEnumerable<string> fields = new List<string>()
            {
                "eventTimestamp",
                "correlationId",
                "level",
                "operationName",
                "resourceGroupName",
                "resourceType",
                "properties",
                "status"
            };

            return this.armClient.GetSubscriptionActivityLogsAsync(subscriptionId, filter, cancellationToken, fields);
        }
    }
}