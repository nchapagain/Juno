using System;
using System.Threading;
using System.Threading.Tasks;
using Juno.Contracts;
using Juno.Providers;
using Microsoft.Azure.CRC.Extensions;

namespace Juno.Execution.Providers.AutoTriage
{
    /// <summary>
    /// Extension methods for Experiment Diagnostics
    /// </summary>
    public static class ExperimentDiagnosticExtensions
    {
        /// <summary>
        /// Extension returns a result that indicates the diagnostics provider operation was cancelled.
        /// </summary>
        /// <param name="provider">The diagnostics provider itself.</param>
        /// <param name="error">An optional parameter that provides an error that caused the cancellation.</param>
        /// <returns>
        /// An <see cref="ExecutionResult"/> having a canceled status.
        /// </returns>
        public static ExecutionResult Cancelled(this DiagnosticsProvider provider, Exception error = null)
        {
            return new ExecutionResult(ExecutionStatus.Cancelled, error: error);
        }

        /// <summary>
        /// Gets the state object for the diagnostics provider.
        /// </summary>
        /// <param name="handler">The diagnostics provider itself.</param>
        /// <param name="request">Provides the request for the specific experiment for which the diagnostics are requested.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// A task that can be used to save the diagnostics provider state object asynchronously.
        /// </returns>
        public static async Task<TState> GetStateAsync<TState>(this DiagnosticsProvider handler, DiagnosticsRequest request, CancellationToken cancellationToken)
        {
            handler.ThrowIfNull(nameof(handler));
            request.ThrowIfNull(nameof(request));

            TState state = default(TState);
            if (!cancellationToken.IsCancellationRequested)
            {
                IProviderDataClient apiClient = handler.Services.GetService<IProviderDataClient>();
                string stateKey = ExperimentDiagnosticExtensions.GetStateKey(request);
                string stateId = ExperimentDiagnosticExtensions.GetStateId(request);
                state = await apiClient.GetOrCreateStateAsync<TState>(request.ExperimentId, stateKey, cancellationToken, stateId)
                    .ConfigureAwait(false);
            }

            return state;
        }

        /// <summary>
        /// Saves the state object for the diagnostics provider.
        /// </summary>
        /// <param name="handler">The diagnostics provider itself.</param>
        /// <param name="request">Provides the request for the specific experiment for which the diagnostics provider is related.</param>
        /// <param name="state">The state object itself.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// True to look for the state definition in the shared/global state/request object. False to look for the state definition in a provider-specific
        /// state/request object.
        /// </param>
        /// <returns>
        /// A task that can be used to save the diagnostics provider state object asynchronously.
        /// </returns>
        public static Task SaveStateAsync<TState>(this DiagnosticsProvider handler, DiagnosticsRequest request, TState state, CancellationToken cancellationToken)
        {
            handler.ThrowIfNull(nameof(handler));
            request.ThrowIfNull(nameof(request));
            state.ThrowIfNull(nameof(state));

            string stateKey = ExperimentDiagnosticExtensions.GetStateKey(request);
            string stateId = ExperimentDiagnosticExtensions.GetStateId(request);

            return handler.SaveStateAsync(request, stateKey, state, cancellationToken, stateId);
        }

        /// <summary>
        /// Saves the state object for the diagnostics provider.
        /// </summary>
        /// <param name="handler">The diagnostics provider itself.</param>
        /// <param name="request">Provides the request for the specific experiment for which the diagnostics provider is related.</param>
        /// <param name="key">The key to which the state will be saved.</param>
        /// <param name="state">The state object itself.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="requestId">Optionally defines the specific ID of the request to retrieve. When not defined, returns the global request for the experiment.</param>
        /// <returns>
        /// A task that can be used to save the diagnostics provider state object asynchronously.
        /// </returns>
        public static async Task SaveStateAsync<TState>(this DiagnosticsProvider handler, DiagnosticsRequest request, string key, TState state, CancellationToken cancellationToken, string requestId = null)
        {
            handler.ThrowIfNull(nameof(handler));
            request.ThrowIfNull(nameof(request));
            key.ThrowIfNullOrWhiteSpace(nameof(key));
            state.ThrowIfNull(nameof(state));

            if (!cancellationToken.IsCancellationRequested)
            {
                IProviderDataClient apiClient = handler.Services.GetService<IProviderDataClient>();
                await apiClient.SaveStateAsync(request.ExperimentId, key, state, cancellationToken, requestId)
                    .ConfigureAwait(false);
            }
        }

        internal static string GetStateId(DiagnosticsRequest request)
        {
            request.ThrowIfNull(nameof(request));
            return $"{request.Id}-diagnostics-state";
        }

        internal static string GetStateKey(DiagnosticsRequest request)
        {
            request.ThrowIfNull(nameof(request));
            return $"{request.ExperimentId}-diagnostics";
        }
    }
}