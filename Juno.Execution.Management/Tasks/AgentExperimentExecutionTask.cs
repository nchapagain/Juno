namespace Juno.Execution.Management.Tasks
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.Execution.AgentRuntime.Tasks;
    using Juno.Extensions.Telemetry;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Executes logic in the background to process Juno experiment steps
    /// on the agent.
    /// </summary>
    public class AgentExperimentExecutionTask : AgentTask
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AgentExperimentExecutionTask"/> class.
        /// </summary>
        /// <param name="services">
        /// Services collection provides required dependencies to the host task/operation.
        /// </param>
        /// <param name="settings">Configuration settings for the environment.</param>
        /// <param name="agentId">Defines the ID of the agent in which the task if running.</param>
        /// <param name="agentType">Defines the type of agent in which the task is running (e.g. Guest, Host).</param>
        public AgentExperimentExecutionTask(IServiceCollection services, EnvironmentSettings settings, AgentIdentification agentId, AgentType agentType)
            : base(services, settings)
        {
            agentId.ThrowIfNull(nameof(agentId));
            agentType.ThrowIfNull(nameof(agentType));

            this.AgentId = agentId;
            this.AgentType = agentType;
        }

        /// <summary>
        /// Gets the identifier for the agent in which the task is running.
        /// </summary>
        protected AgentIdentification AgentId { get; }

        /// <summary>
        /// Gets the type of agent in which the task is running (e.g. GuestAgent, HostAgent).
        /// </summary>
        protected AgentType AgentType { get; }

        /// <summary>
        /// Executes logic in the background to process steps for the agent that are part of
        /// a Juno experiment.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// A task that can be used to execute the task asynchronously.
        /// </returns>
        public override Task ExecuteAsync(CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                EventContext telemetryContext = EventContext.Persist(Guid.NewGuid())
                    .AddContext("pollingInterval", this.Settings.AgentSettings.WorkPollingInterval.ToString());

                try
                {
                    await this.Logger.LogTelemetryAsync($"{this.AgentType}.ExperimentExecution", telemetryContext, async () =>
                    {
                        AgentExecutionManager executionManager = this.Services.GetService<AgentExecutionManager>();
                        await this.ProcessExperimentAsync(executionManager, cancellationToken).ConfigureDefaults();
                    }).ConfigureDefaults();
                }
                catch
                {
                    // We do not want exceptions that happen in the processing of experiment steps to
                    // crash the agent nor exit the runtime task. We capture telemetry for the error
                    // cases.
                }
            });
        }

        /// <summary>
        /// Executes the experiment step processing operations.
        /// </summary>
        protected virtual Task ProcessExperimentAsync(AgentExecutionManager executionManager, CancellationToken cancellationToken)
        {
            executionManager.ThrowIfNull(nameof(executionManager));
            return executionManager.ExecuteAsync(cancellationToken);
        }
    }
}
