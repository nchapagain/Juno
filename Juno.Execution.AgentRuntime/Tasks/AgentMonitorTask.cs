namespace Juno.Execution.AgentRuntime.Tasks
{
    using System;
    using Juno.Contracts.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Polly;

    /// <summary>
    /// Provides a method for monitoring the host or guest with agent.
    /// </summary>
    public abstract class AgentMonitorTask<TResult> : AgentTask
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AgentMonitorTask{TResult}"/> class.
        /// </summary>
        /// <param name="services">
        /// Services collection provides required dependencies to the host task/operation.
        /// </param>
        /// <param name="settings">Configuration settings for the environment.</param>
        /// <param name="retryPolicy">A retry policy to apply to the execution of the task operation.</param>
        public AgentMonitorTask(IServiceCollection services, EnvironmentSettings settings, IAsyncPolicy retryPolicy = null)
            : base(services, settings, retryPolicy)
        {
        }

        /// <summary>
        /// Event handler executes when the system monitor has data results
        /// to provide.
        /// </summary>
        public event EventHandler<ExecutionEventArgs<TResult>> Results;

        /// <summary>
        /// Invokes the DataReady event.
        /// </summary>
        /// <param name="args">
        /// Event arguments that provide the data results of the monitoring operation execution.
        /// </param>
        protected void OnResults(ExecutionEventArgs<TResult> args)
        {
            this.Results?.Invoke(this, args);
        }
    }
}
