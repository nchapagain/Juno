namespace Juno.Execution.AgentRuntime
{
    using System;

    /// <summary>
    /// Represents data or results from the execution of a host runtime
    /// task.
    /// </summary>
    public class ExecutionEventArgs<TResult> : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExecutionEventArgs{TData}"/> class.
        /// </summary>
        /// <param name="results">The results/data of the runtime task execution.</param>
        public ExecutionEventArgs(TResult results)
        {
            this.Results = results;
        }

        /// <summary>
        /// Gets the data results of the runtime execution.
        /// </summary>
        public TResult Results { get; }
    }
}
