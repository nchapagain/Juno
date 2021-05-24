namespace Juno.Providers
{
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Provides methods for handling the runtime requirements of an experiment
    /// workflow step/execution component.
    /// </summary>
    public interface IDiagnosticsProvider
    {
        /// Executes the logic to validate the diagnostic request data before executing
        /// the diagnostic provider's DiagnoseAsync method.
        /// </summary>
        /// <param name="request">The diagnostic request being processed.</param>
        /// /// <returns>
        /// A boolean for whether or not the request has the diagnostic data to
        /// be processed by the diagnostic provider.
        /// </returns>
        bool IsHandled(DiagnosticsRequest request);

        /// Executes the logic required to handle the runtime requirements of the
        /// experiment diagnostic workflow execution.
        /// </summary>
        /// <param name="request">The diagnostic request being processed.</param>
        /// <param name="logger">The logger that is used to capture the telemetry.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// /// <returns>
        /// A task that can be used to execute the diagnostics for the specified diagnostic handler.
        /// asynchronously.
        /// </returns>
        Task<ExecutionResult> DiagnoseAsync(DiagnosticsRequest request, ILogger logger, CancellationToken cancellationToken);
    }
}