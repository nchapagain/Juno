namespace Juno.Contracts
{
    using System;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;

    /// <summary>
    /// Encapsulate agent identity information.such as cluster name, node name and virtual machine name
    /// and construction of agentid
    /// </summary>
    public class AgentIdentification : IEquatable<AgentIdentification>
    {
        /// <summary>
        /// Represents the term used to describe an unknown part of the
        /// agent ID (e.g. cluster name, node ID).
        /// </summary>
        public const string UnknownEntity = "unknown";

        private const char Delimiter = ',';
        private int? hashCode;

        /// <summary>
        /// Initializes a new instance of the <see cref="AgentIdentification"/> class.
        /// </summary>
        /// <param name="clusterName">Cluster name of the agent</param>
        /// <param name="nodeName">Node name of the agent</param>
        /// <param name="virtualMachineName">Virtual machine name of the agent</param>
        /// <param name="context">Additional context information to define the agent identification (e.g. TiP session ID).</param>
        [JsonConstructor]
        public AgentIdentification(string clusterName, string nodeName, string virtualMachineName = null, string context = null)
        {
            clusterName.ThrowIfNullOrWhiteSpace(nameof(clusterName));
            nodeName.ThrowIfNullOrWhiteSpace(nameof(nodeName));

            this.ClusterName = clusterName;
            this.NodeName = nodeName;
            this.VirtualMachineName = virtualMachineName;
            this.Context = context;
        }

        /// <summary>
        /// Create a new instance of the <see cref="AgentIdentification"/> class from agent ID string.
        /// </summary>
        public AgentIdentification(string agentId)
        {
            agentId.ThrowIfNullOrWhiteSpace(nameof(agentId));
            var parts = agentId.Split(AgentIdentification.Delimiter);
            agentId.ThrowIfInvalid(
                nameof(agentId),
                (p) => !(parts.Length < 2 || parts.Length > 4),
                $"Invalid formatted agent ID string. An agent ID string can consist of only 4 parts: a cluster name, a node name, a VM name and a single context value (e.g. TiP session ID)");

            this.ClusterName = parts[0];
            this.NodeName = parts[1];

            if (parts.Length > 2)
            {
                this.VirtualMachineName = parts[2];
            }

            if (parts.Length > 3)
            {
                this.Context = parts[3];
            }
        }

        /// <summary>
        /// Gets the cluster name of the agent.
        /// </summary>
        [JsonProperty(PropertyName = "clusterName", Required = Required.Always)]
        public string ClusterName { get; }

        /// <summary>
        /// Gets additional context used to identify the agent.
        /// </summary>
        [JsonProperty(PropertyName = "context", Required = Required.Default, NullValueHandling = NullValueHandling.Ignore)]
        public string Context { get; }

        /// <summary>
        /// Gets the ID of the agent.
        /// </summary>
        [JsonProperty(PropertyName = "nodeName", Required = Required.Always)]
        public string NodeName { get; }

        /// <summary>
        /// Gets the ID of the agent.
        /// </summary>
        [JsonProperty(PropertyName = "virtualMachineName", Required = Required.Default, NullValueHandling = NullValueHandling.Ignore)]
        public string VirtualMachineName { get; }

        /// <summary>
        /// Determines if two objects are equal.
        /// </summary>
        /// <param name="lhs">The left hand side.</param>
        /// <param name="rhs">The right hand side.</param>
        /// <returns>True if the objects are equal. False otherwise.</returns>
        public static bool operator ==(AgentIdentification lhs, AgentIdentification rhs)
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
        public static bool operator !=(AgentIdentification lhs, AgentIdentification rhs)
        {
            return !(lhs == rhs);
        }

        /// <summary>
        /// Creates a unique identifier for an agent targeted to run on a node.
        /// </summary>
        /// <param name="clusterName">The name of the data center cluster on which the agent runs.</param>
        /// <param name="nodeId">The ID/name of the node on which the agent runs.</param>
        /// <param name="contextId">A context identifier (e.g. TiP session ID) that further distinguishes the identity of the host on which the agent runs.</param>
        public static AgentIdentification CreateNodeId(string clusterName = null, string nodeId = null, string contextId = null)
        {
            if (string.IsNullOrWhiteSpace(clusterName)
                && string.IsNullOrWhiteSpace(nodeId)
                && string.IsNullOrWhiteSpace(contextId))
            {
                throw new ArgumentException("Invalid agent identification parameters. At least one identification parameter must be defined in order to create a valid agent ID");
            }

            return new AgentIdentification(
                clusterName: !string.IsNullOrWhiteSpace(clusterName) ? clusterName : AgentIdentification.UnknownEntity,
                nodeName: !string.IsNullOrWhiteSpace(nodeId) ? nodeId : AgentIdentification.UnknownEntity,
                context: contextId);
        }

        /// <summary>
        /// Creates a unique identifier for an agent targeted to run on a node.
        /// </summary>
        /// <param name="clusterName">The name of the data center cluster on which the agent runs.</param>
        /// <param name="nodeId">The ID/name of the node on which the agent runs.</param>
        /// <param name="vmName">The name of the VM on which the agent runs.</param>
        /// <param name="contextId">A context identifier (e.g. TiP session ID) that further distinguishes the identity of the host on which the agent runs.</param>
        public static AgentIdentification CreateVirtualMachineId(string clusterName = null, string nodeId = null, string vmName = null, string contextId = null)
        {
            if (string.IsNullOrWhiteSpace(clusterName)
                && string.IsNullOrWhiteSpace(nodeId)
                && string.IsNullOrWhiteSpace(vmName)
                && string.IsNullOrWhiteSpace(contextId))
            {
                throw new ArgumentException("Invalid agent identification parameters. At least one identification parameter must be defined in order to create a valid ID for an agent that runs on a virtual machine.");
            }

            return new AgentIdentification(
                clusterName: !string.IsNullOrWhiteSpace(clusterName) ? clusterName : AgentIdentification.UnknownEntity,
                nodeName: !string.IsNullOrWhiteSpace(nodeId) ? nodeId : AgentIdentification.UnknownEntity,
                virtualMachineName: !string.IsNullOrWhiteSpace(vmName) ? vmName : AgentIdentification.UnknownEntity,
                context: contextId);
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
                AgentIdentification agentInformation = obj as AgentIdentification;
                if (agentInformation != null)
                {
                    areEqual = this.Equals(agentInformation);
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
        public virtual bool Equals(AgentIdentification other)
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
                this.hashCode = new StringBuilder(this.ToString())
                    .ToString().GetHashCode(StringComparison.OrdinalIgnoreCase);
            }

            return this.hashCode.Value;
        }

        /// <summary>
        /// Overrided provides a string representation of the <see cref="AgentIdentification"/>
        /// instance (e.g. Cluster12,Node01,VM03, Cluster12,Node01,VM03,TiPSession01).
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return string.Join(AgentIdentification.Delimiter.ToString(), new string[]
            {
                this.ClusterName,
                this.NodeName,
                this.VirtualMachineName,
                this.Context
            }.Where(item => !string.IsNullOrWhiteSpace(item))).ToLowerInvariant();
        }
    }
}
