namespace Juno.Execution.Providers.AutoTriage
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Execution.ArmIntegration;
    using Juno.Extensions.Telemetry;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Polly;

    /// <summary>
    /// Provides a set of one or more <see cref="IDiagnosticsProvider"/> that can be used to diagnose issues
    /// that have occurred that prevented an experiment workflow from succeeding.
    /// </summary>
    public abstract class DiagnosticsProvider : IDiagnosticsProvider
    {
        private const int DefaultRetryCount = 5;
        private const int DefaultRetryWaitInSecs = 3;

        /// <summary>
        ///  The default retry policy found in each of the diagnostic handlers
        /// </summary>
        protected static readonly IAsyncPolicy DefaultRetryPolicy = Policy.Handle<Exception>()
            .WaitAndRetryAsync(DiagnosticsProvider.DefaultRetryCount, (retries) => TimeSpan.FromSeconds(retries + DiagnosticsProvider.DefaultRetryWaitInSecs));

        /// <summary>
        /// Initializes a new instance of the <see cref="DiagnosticsProvider"/> class.
        /// </summary>
        public DiagnosticsProvider(IServiceCollection services, IAsyncPolicy retryPolicy = null)
        {
            services.ThrowIfNull(nameof(services));

            this.Services = services;
            this.RetryPolicy = retryPolicy ?? DiagnosticsProvider.DefaultRetryPolicy;
        }

        /// <summary>
        /// Gets the service provider/locator for the experiment provider.
        /// </summary>
        public IServiceCollection Services { get; }

        /// <summary>
        /// Represents state information that must be preserved between individual executions of the diagnostics provider.
        /// </summary>
        public DiagnosticState State { get; set; }

        /// <summary>
        /// Represents the retry policy to apply to the execution of the diagnostics task
        /// operation.
        /// </summary>
        protected IAsyncPolicy RetryPolicy { get; set; }

        /// <summary>
        /// Validates required parameters of the diagnostic request that defines the requirements
        /// for the diagnostic provider.
        /// </summary>
        /// <param name="request">The diagnostic request to validate.</param>
        public abstract bool IsHandled(DiagnosticsRequest request);

        /// <inheritdoc/>
        protected abstract Task<ExecutionResult> DiagnoseAsync(DiagnosticsRequest request, EventContext telemetryContext, ILogger logger, CancellationToken cancellationToken);

        /// <summary>
        /// This is the base class that is called when a valid request is made. This passes the information down
        /// to the derived classes to execute their exclusive diagnose async functions.
        /// </summary>
        /// <param name="request">The diagnostic request being processed.</param>
        /// <param name="logger">The logger that is used to capture the telemetry.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        public async Task<ExecutionResult> DiagnoseAsync(DiagnosticsRequest request, ILogger logger, CancellationToken cancellationToken)
        {
            request.ThrowIfNull(nameof(request));

            ExecutionResult result = new ExecutionResult(ExecutionStatus.Succeeded);

            try
            {
                // Persist the activity ID so that it can be used down the callstack.
                EventContext relatedContext = EventContext.Persisted()
                    .AddContext(request);

                if (!cancellationToken.IsCancellationRequested)
                {
                    await logger.LogTelemetryAsync($"{this.GetType().Name}.Diagnostics", relatedContext, async () =>
                    {
                        result = await this.DiagnoseAsync(request, relatedContext, logger, cancellationToken).ConfigureDefaults();
                    }).ConfigureDefaults();
                }
            }
            catch (Exception exc)
            {
                result = new ExecutionResult(ExecutionStatus.Failed, error: exc);
            }

            return !cancellationToken.IsCancellationRequested
                ? result
                : this.Cancelled();
        }
    }

    /// <summary>
    /// Represents state information that must be preserved between individual executions of the diagnostics provider.
    /// </summary>
    public class DiagnosticState
    {
        /// <summary>
        /// The current execution status for the diagnostic provider running the query
        /// for diagnostics.
        /// </summary>
        public ExecutionStatus Status { get; set; }

        /// <summary>
        /// This is a helper function to see if the provider has performed the diagnostics
        /// </summary>
        public bool IsTerminal => ExecutionResult.IsTerminalStatus(this.Status);
    }
}