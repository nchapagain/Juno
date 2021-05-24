namespace Juno.Contracts
{
    using System;
    using System.Text;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Represents agent hearthbeat data
    /// </summary>
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class AgentHeartbeat : IEquatable<AgentHeartbeat>
    {
        private int? hashCode;

        /// <summary>
        /// Initializes a new instance of the <see cref="AgentHeartbeat"/> class.
        /// </summary>
        /// <param name="agentIdentification">Encapsulate agent identifications.</param>
        /// <param name="status">The agent heatbeat status</param>
        /// <param name="agentType">The agent type (Host/Guest)</param>
        /// <param name="message">Any message related to this agent hearthbeat</param>
        [JsonConstructor]
        public AgentHeartbeat(AgentIdentification agentIdentification, AgentHeartbeatStatus status, AgentType agentType, string message = null)
        {
            agentIdentification.ThrowIfNull(nameof(agentIdentification));

            this.AgentIdentification = agentIdentification;
            this.Status = status;
            this.AgentType = agentType;
            this.Message = message;
        }

        /// <summary>
        /// Gets the ID of the agent.
        /// </summary>
        [JsonProperty(PropertyName = "agentInformation", Required = Required.Always)]
        public AgentIdentification AgentIdentification { get; }

        /// <summary>
        /// Gets or sets the current status of the agent.
        /// </summary>
        [JsonProperty(PropertyName = "status", Required = Required.Always)]
        [JsonConverter(typeof(StringEnumConverter))]
        public AgentHeartbeatStatus Status { get; set; }

        /// <summary>
        /// Gets or sets agent type
        /// </summary>
        [JsonProperty(PropertyName = "agenttype", Required = Required.Default)]
        [JsonConverter(typeof(StringEnumConverter))]
        public AgentType AgentType { get; set; }

        /// <summary>
        /// Gets or sets any message
        /// </summary>
        [JsonProperty(PropertyName = "message", Required = Required.Default)]
        public string Message { get; set; }

        /// <summary>
        /// Determines if two objects are equal.
        /// </summary>
        /// <param name="lhs">The left hand side.</param>
        /// <param name="rhs">The right hand side.</param>
        /// <returns>True if the objects are equal. False otherwise.</returns>
        public static bool operator ==(AgentHeartbeat lhs, AgentHeartbeat rhs)
        {
            if (object.ReferenceEquals(lhs, rhs))
            {
                return true;
            }

            if (object.ReferenceEquals(null, lhs) || object.ReferenceEquals(null, rhs))
            {
                return false;
            }

            return lhs.Equals(rhs);
        }

        /// <summary>
        /// Determines if two objects are NOT equal.
        /// </summary>
        /// <param name="lhs">The left hand side.</param>
        /// <param name="rhs">The right hand side.</param>
        /// <returns>True if the objects are NOT equal. False otherwise.</returns>
        public static bool operator !=(AgentHeartbeat lhs, AgentHeartbeat rhs)
        {
            return !(lhs == rhs);
        }

        /// <summary>
        /// Override method determines if the two objects are equal
        /// </summary>
        /// <param name="obj">Defines the object to compare against the current instance.</param>
        /// <returns>
        /// Type:  System.Boolean
        /// True if the objects are equal or False if not
        /// </returns>
        public override bool Equals(object obj)
        {
            bool areEqual = false;

            if (object.ReferenceEquals(this, obj))
            {
                areEqual = true;
            }
            else
            {
                // Apply value-type semantics to determine
                // the equality of the instances
                AgentHeartbeat agentHearthbeat = obj as AgentHeartbeat;
                if (agentHearthbeat != null)
                {
                    areEqual = this.Equals(agentHearthbeat);
                }
            }

            return areEqual;
        }

        /// <summary>
        /// Method determines if the other object is equal to this instance
        /// </summary>
        /// <param name="other">Defines the object to compare against the current instance.</param>
        /// <returns>
        /// Type:  System.Boolean
        /// True if the objects are equal or False if not
        /// </returns>
        public virtual bool Equals(AgentHeartbeat other)
        {
            return other != null && this.GetHashCode() == other.GetHashCode();
        }

        /// <summary>
        /// Override enables the creation of an accurate hash code for the
        /// object.
        /// </summary>
        public override int GetHashCode()
        {
            if (this.hashCode == null)
            {
                this.hashCode = new StringBuilder(this.AgentIdentification.GetHashCode().ToString())
                    .AppendProperties(this.Status, this.AgentType, this.Message)
                    .ToString().GetHashCode(StringComparison.OrdinalIgnoreCase);
            }

            return this.hashCode.Value;
        }
    }
}
