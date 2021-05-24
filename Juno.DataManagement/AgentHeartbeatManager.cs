namespace Juno.DataManagement
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.DataManagement.Cosmos;
    using Juno.Extensions.Telemetry;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Repository;
    using Microsoft.Azure.CRC.Repository.Cosmos;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;

    /// <inheritdoc/>
    public class AgentHeartbeatManager : IAgentHeartbeatManager
    {
        /// <summary>
        /// Initializes and instance of the <see cref="AgentHeartbeatManager"/> class.
        /// </summary>
        /// <param name="tableStore">Provides methods to agent heartbeat.</param>
        /// <param name="logger">A logger to use for capturing telemetry data.</param>
        public AgentHeartbeatManager(ITableStore<CosmosTableAddress> tableStore, ILogger logger = null)
        {
            tableStore.ThrowIfNull(nameof(tableStore));
            this.TableStore = tableStore;
            this.Logger = logger ?? NullLogger.Instance;
        }

        /// <summary>
        /// Gets the logger for capturing operation telemetry.
        /// </summary>
        protected ILogger Logger { get; }

        /// <summary>
        /// Gets the agent heartbeat table to store agent status.
        /// </summary>
        protected ITableStore<CosmosTableAddress> TableStore { get; }

        /// <inheritdoc/>
        public async Task<AgentHeartbeatInstance> CreateHeartbeatAsync(AgentHeartbeat agentHeartbeat, CancellationToken cancellationToken)
        {
            agentHeartbeat.ThrowIfNull(nameof(agentHeartbeat));
            agentHeartbeat.AgentIdentification.ThrowIfNull(nameof(agentHeartbeat));

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(agentHeartbeat);

            return await this.Logger.LogTelemetryAsync(EventNames.CreateHeartbeat, telemetryContext, async () =>
            {
                // Cosmos Table is case-sensitive. We are lowercasing the agent ID to ensure consistency
                // with queries.
                string agentId = agentHeartbeat.AgentIdentification.ToString();

                AgentHeartbeatInstance instance = new AgentHeartbeatInstance(
                    Guid.NewGuid().ToString(),
                    agentId.ToLowerInvariant(),
                    agentHeartbeat.Status,
                    agentHeartbeat.AgentType,
                    DateTime.UtcNow,
                    DateTime.UtcNow,
                    agentHeartbeat.Message);

                CosmosTableAddress address = ExperimentAddressFactory.CreateAgentHeartbeatAddress(DateTime.UtcNow, agentId, instance.Id);
                AgentHeartbeatTableEntity tableEntity = instance.ToTableEntity();

                await this.TableStore.SaveEntityAsync(address, tableEntity, cancellationToken, replaceIfExists: false)
                    .ConfigureAwait(false);
                
                instance.SetETag(tableEntity.ETag);
                telemetryContext.AddContext(instance);

                return instance;
            }).ConfigureDefaults();
        }

        /// <inheritdoc/>
        public async Task<AgentHeartbeatInstance> GetHeartbeatAsync(AgentIdentification agentIdentification, CancellationToken cancellationToken)
        {
            agentIdentification.ThrowIfNull(nameof(agentIdentification));

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(nameof(agentIdentification), agentIdentification);

            return await this.Logger.LogTelemetryAsync(EventNames.GetHeartbeat, telemetryContext, async () =>
            {
                string agentId = agentIdentification.ToString();
                CosmosTableAddress address = ExperimentAddressFactory.CreateAgentHeartbeatAddress(DateTime.UtcNow, agentId);

                var entities = await this.TableStore.GetEntitiesAsync<AgentHeartbeatTableEntity>(address, cancellationToken)
                    .ConfigureAwait(false);

                // Get the agent heartbeat data by agentid, sort by descending order and return the first entry.
                var entity = entities?.OrderByDescending(e => e.Timestamp).FirstOrDefault();

                AgentHeartbeatInstance instance = entity?.ToHeartbeat();
                telemetryContext.AddContext(instance);

                return instance;
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Provides the names of telemetry events emitted by the data manager.
        /// </summary>
        private static class EventNames
        {
            public static readonly string CreateHeartbeat = EventContext.GetEventName(nameof(AgentHeartbeatManager), "CreateHeartbeat");
            public static readonly string GetHeartbeat = EventContext.GetEventName(nameof(AgentHeartbeatManager), "GetHeartbeat");
        }
    }
}
