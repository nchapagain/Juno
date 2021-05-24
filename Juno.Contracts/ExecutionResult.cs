namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    /// <summary>
    /// Represents the result of a provider execution.
    /// </summary>
    public class ExecutionResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExecutionResult"/> class.
        /// </summary>
        /// <param name="status">The outcome status of the provider execution.</param>
        /// <param name="error">Any error associated with the provider execution.</param>
        /// <param name="extensionTimeout">
        /// A timespan that indicates the amount of time the provider is requesting before a follow up execution.
        /// This is for providers whose operation requires reentrancy in order to determine a final outcome/result.
        /// </param>
        public ExecutionResult(ExecutionStatus status, Exception error = null, TimeSpan? extensionTimeout = null)
        {
            this.Status = status;
            this.Error = error;
            this.Extension = extensionTimeout;
        }

        /// <summary>
        /// Statuses that represent a completed result whether it is successful or not 
        /// (e.g. Succeeded, Failed, Cancelled).
        /// </summary>
        public static IEnumerable<ExecutionStatus> CompletedStatuses { get; } = new List<ExecutionStatus>
        {
            ExecutionStatus.Cancelled,
            ExecutionStatus.Failed,
            ExecutionStatus.Succeeded,
            ExecutionStatus.SystemCancelled
        };

        /// <summary>
        /// Statuses that represent a result that is not completed, failed or cancelled
        /// (e.g. InProgress, InProgressContinue, Pending).
        /// </summary>
        public static IEnumerable<ExecutionStatus> NonCompletedStatuses { get; } = new List<ExecutionStatus>
        {
            ExecutionStatus.InProgress,
            ExecutionStatus.InProgressContinue,
            ExecutionStatus.Pending
        };

        /// <summary>
        /// Statuses that represent a result that is not successful but is a final/terminal
        /// state (e.g. Failed, Cancelled).
        /// </summary>
        public static IEnumerable<ExecutionStatus> TerminalStatuses { get; } = new List<ExecutionStatus>
        {
            ExecutionStatus.Cancelled,
            ExecutionStatus.Failed,
            ExecutionStatus.SystemCancelled
        };

        /// <summary>
        /// Gets any error that occurred during the execution of the
        /// provider operations.
        /// </summary>
        public Exception Error { get; }

        /// <summary>
        /// Gets an amount of time for which the provider is requesting
        /// for extension before its results are evaluated as final.
        /// </summary>
        public TimeSpan? Extension { get; set; }

        /// <summary>
        /// Gets the status of the execution/result.
        /// </summary>
        public ExecutionStatus Status { get; }

        /// <summary>
        /// Given a set of one or more execution results, returns a time extension relative to
        /// the extension requested in each of the results.
        /// </summary>
        /// <param name="results">A set of execution results.</param>
        /// <returns>
        /// A time span value representing the extension in time relative to the set of all execution results.
        /// </returns>
        public static TimeSpan GetRelativeTimeExtension(IEnumerable<ExecutionResult> results)
        {
            results.ThrowIfNullOrEmpty(nameof(results));

            TimeSpan extension = TimeSpan.FromSeconds(1);
            TimeSpan? requestedExtension = results.Where(r => r.Extension != null)
                ?.OrderByDescending(r => r.Extension)
                ?.FirstOrDefault()
                ?.Extension;

            if (requestedExtension != null)
            {
                extension = requestedExtension.Value;
            }

            return extension;
        }

        /// <summary>
        /// Determines if the current execution status is in any status indicating completion
        /// (e.g. Succeeded, Failed, Cancelled, SystemCancelled).
        /// </summary>
        /// <param name="status"> The current execution status</param>
        public static bool IsCompletedStatus(ExecutionStatus status)
        {
            return ExecutionResult.CompletedStatuses.Contains(status);
        }

        /// <summary>
        /// Determines if the current execution status is a terminal status
        /// (e.g. Failed, Cancelled, SystemCancelled).
        /// </summary>
        /// <param name="status"> The current execution status</param>
        public static bool IsTerminalStatus(ExecutionStatus status)
        {
            return ExecutionResult.TerminalStatuses.Contains(status);
        }
    }
}
