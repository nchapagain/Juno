namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents the result of a Precondition Provider execution.
    /// </summary>
    public class PreconditionResult : ExecutionResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PreconditionResult"/>
        /// </summary>
        /// <param name="status">The outcome status of the provider execution</param>
        /// <param name="satisfied">The outcome of evaluation of the Precondition provider</param>
        /// <param name="error">Any error that associated with the provider execution. </param>
        /// <param name="extensionTimeout">
        /// A timespan that indicates the amount of time the provider is requesting before a follow up execution
        /// This is for providers whose operation requires reentrency in order to determine a final outcome.
        /// </param>
        public PreconditionResult(ExecutionStatus status, bool? satisfied = null, Exception error = null, TimeSpan? extensionTimeout = null)
            : base(status, error, extensionTimeout)
        {
            this.Satisfied = satisfied;
        }

        /// <summary>
        /// Gets whether or not the Precondition is satisfied or not.
        /// </summary>
        public bool? Satisfied { get; }
    }
}
