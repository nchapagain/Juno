namespace Juno.Execution.AgentRuntime.Tasks
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.Extensions.Telemetry;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using Polly;

    /// <summary>
    /// Executes logic in the background to send heartbeats to the
    /// Juno system.
    /// </summary>
    public class AgentHeartbeatTask : AgentTask
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AgentHeartbeatTask"/> class.
        /// </summary>
        /// <param name="services">
        /// Services collection provides required dependencies to the host task/operation.
        /// </param>
        /// <param name="settings">Configuration settings for the environment.</param>
        /// <param name="agentId">Defines the ID of the agent in which the task if running.</param>
        /// <param name="agentType">Defines the type of agent in which the task is running (e.g. Guest, Host).</param>
        /// <param name="retryPolicy">A retry policy to apply to API calls.</param>
        public AgentHeartbeatTask(IServiceCollection services, EnvironmentSettings settings, AgentIdentification agentId, AgentType agentType, IAsyncPolicy retryPolicy = null)
            : base(services, settings, retryPolicy)
        {
            agentId.ThrowIfNull(nameof(agentId));
            agentType.ThrowIfNull(nameof(agentType));

            this.AgentId = agentId;
            this.AgentType = agentType;
        }

        /// <summary>
        /// Gets the identifier for the agent in which the heartbeat task is running.
        /// </summary>
        protected AgentIdentification AgentId { get; }

        /// <summary>
        /// Gets the type of agent in which the task is running (e.g. GuestAgent, HostAgent).
        /// </summary>
        protected AgentType AgentType { get; }

        /// <summary>
        /// Gets the sampling options to apply to telemetry event data written.
        /// </summary>
        protected SamplingOptions TelemetrySamplingOptions { get; }

        /// <summary>
        /// Executes logic in the background to send heartbeats to the Juno system.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// A task that can be used to execute the task asynchronously.
        /// </returns>
        public override Task ExecuteAsync(CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                try
                {
                    EventContext telemetryContext = EventContext.Persisted()
                        .AddContext(this.AgentId)
                        .AddContext("heartbeatInterval", this.Settings.AgentSettings.HeartbeatInterval.ToString());

                    await this.Logger.LogTelemetryAsync($"{this.AgentType}.SendHeartbeat", telemetryContext, async () =>
                    {
                        int attempts = 0;
                        List<HttpResponseMessage> responses = new List<HttpResponseMessage>();

                        try
                        {
                            ClientPool<AgentClient> clientPool = this.Services.GetService<ClientPool<AgentClient>>();
                            AgentClient apiClient = clientPool.GetClient(ApiClientType.AgentHeartbeatApi);

                            await this.RetryPolicy.ExecuteAsync(async () =>
                            {
                                attempts++;
                                AgentHeartbeat agentHeartbeat = new AgentHeartbeat(this.AgentId, AgentHeartbeatStatus.Running, this.AgentType);
                                HttpResponseMessage response = await apiClient.CreateHeartbeatAsync(agentHeartbeat, cancellationToken)
                                    .ConfigureDefaults();

                                responses.Add(response);
                                response.ThrowOnError<ExperimentException>();
                            }).ConfigureDefaults();
                        }
                        finally
                        {
                            telemetryContext.AddContext(responses);
                            telemetryContext.AddContext(nameof(attempts), attempts);
                        }
                    }).ConfigureDefaults();
                }
                catch
                {
                    // We do not want exceptions that happen in the process of sending heartbeats to
                    // crash the agent nor exit the heartbeat task. We capture telemetry for the error
                    // cases.
                }
            });
        }
    }
}
