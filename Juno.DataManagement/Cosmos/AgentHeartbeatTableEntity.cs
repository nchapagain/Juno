namespace Juno.DataManagement.Cosmos
{
    using System;
    using Microsoft.Azure.CRC.Repository.Cosmos;

    /// <summary>
    /// Represents an agent heartbeat
    /// </summary>
    internal class AgentHeartbeatTableEntity : ItemEntity
    {
        public AgentHeartbeatTableEntity()
        {
            this.Created = DateTime.UtcNow;
            this.Timestamp = DateTime.UtcNow;
        }

        /// <summary>
        /// Gets the ID of the agent.
        /// </summary>
        public string AgentId { get; set; }

        /// <summary>
        /// Gets or sets the current status of agent.
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Gets or sets the agent type.
        /// </summary>
        public string AgentType { get; set; }

        /// <summary>
        /// Gets or sets any message.
        /// </summary>
        public string Message { get; set; }
    }
}