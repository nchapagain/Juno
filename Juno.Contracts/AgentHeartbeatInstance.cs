namespace Juno.Contracts
{
    using System;
    using System.Text;
    using Microsoft.Azure.CRC.Contracts;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Represents agent hearthbeat in relation to a Juno experiment agent.
    /// </summary>
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class AgentHeartbeatInstance : ItemBase
    {
        private int? hashCode;

        /// <summary>
        /// create new instance of <see cref="AgentHeartbeatInstance"/>
        /// </summary>
        /// <param name="id">The ID of the hearthbeat.</param>
        /// <param name="agentId">ID of the agent.</param>
        /// <param name="status">The status of the agent</param>
        /// <param name="agentType">Type of agent. Guest or Host</param>
        /// <param name="message">Any message sent by the agent</param>
        public AgentHeartbeatInstance(string id, string agentId, AgentHeartbeatStatus status, AgentType agentType, string message = null)
            : this(id, agentId, status, agentType, DateTime.UtcNow, DateTime.UtcNow, message)
        {
            this.AgentId = agentId;
            this.Status = status;
            this.AgentType = agentType;
            this.Message = message;
        }

        /// <summary>
        /// create new instance of <see cref="AgentHeartbeatInstance"/>
        /// </summary>
        /// <param name="id">The ID of the hearthbeat.</param>
        /// <param name="agentId">ID of the agent.</param>
        /// <param name="status">The status of the agent</param>
        /// <param name="agentType">Type of agent. Guest or Host</param>
        /// <param name="created">The date at which the agent heartbeat was created.</param>
        /// <param name="lastModified">The date at which the agent heartbeat was last modified.</param>
        /// <param name="message">Any message sent by the agent</param>
        [JsonConstructor]
        public AgentHeartbeatInstance(string id, string agentId, AgentHeartbeatStatus status, AgentType agentType, DateTime created, DateTime lastModified, string message = null)
            : base(id, created, lastModified)
        {
            this.AgentId = agentId;
            this.Status = status;
            this.AgentType = agentType;
            this.Message = message;
        }

        /// <summary>
        /// Gets the ID of the agent.
        /// </summary>
        [JsonProperty(PropertyName = "agentId", Required = Required.Always)]
        public string AgentId { get; set; }

        /// <summary>
        /// Gets or sets the current status of the agent.
        /// </summary>
        [JsonProperty(PropertyName = "status", Required = Required.Always)]
        [JsonConverter(typeof(StringEnumConverter))]
        public AgentHeartbeatStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the agent type
        /// </summary>
        [JsonProperty(PropertyName = "agentType", Required = Required.Default)]
        [JsonConverter(typeof(StringEnumConverter))]
        public AgentType AgentType { get; set; }

        /// <summary>
        /// Gets or sets any message .
        /// </summary>
        [JsonProperty(PropertyName = "message", Required = Required.Default)]
        public string Message { get; set; }

        /// <summary>
        /// Override enables the creation of an accurate hash code for the
        /// object.
        /// </summary>
        public override int GetHashCode()
        {
            if (this.hashCode == null)
            {
                this.hashCode = new StringBuilder(base.GetHashCode().ToString())
                    .AppendProperties(this.AgentId, this.Status, this.AgentType, this.Message)
                    .ToString().GetHashCode(StringComparison.OrdinalIgnoreCase);
            }

            return this.hashCode.Value;
        }
    }
}
