namespace Juno.DataManagement
{
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;

    /// <summary>
    /// Provides methods for managing Juno experiment agent hearthbeat operations.
    /// </summary>
    public interface IAgentHeartbeatManager
    {
        /// <summary>
        /// Creates a new agent heartbeat
        /// </summary>
        /// <param name="agentHeartbeat">Agent heartbeat <see cref="AgentHeartbeat"/>.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// An <see cref="AgentHeartbeatInstance"/> created.
        /// </returns>
        Task<AgentHeartbeatInstance> CreateHeartbeatAsync(AgentHeartbeat agentHeartbeat, CancellationToken cancellationToken);

        /// <summary>
        /// Returns the latest agent heartbeat.
        /// </summary>
        /// <param name="agentIdentification">Define specific agent infromations such as cluster name, node name etc.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// The latest <see cref="AgentHeartbeatInstance"/> in the table.
        /// </returns>
        Task<AgentHeartbeatInstance> GetHeartbeatAsync(AgentIdentification agentIdentification, CancellationToken cancellationToken);
    }
}
