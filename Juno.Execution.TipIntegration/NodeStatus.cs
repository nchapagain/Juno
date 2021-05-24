namespace Juno.Execution.TipIntegration
{
    using System;
    using System.Xml.Serialization;

    /// <summary>
    /// Represents an Azure node's real time status
    /// </summary>
    [Serializable] 
    [XmlRoot(ElementName ="NodeStatus", Namespace = "RD.Fabric.Controller.Data")]
    public class NodeStatus
    {
        /// <summary>
        /// The availability state of the node
        /// </summary>
        public string AvailabilityStateString { get; set; }

        /// <summary>
        /// Number of containers hosted by the node
        /// </summary>
        public string ContainerCount { get; set; }

        /// <summary>
        /// Specifies if the node is in goal state.
        /// Valid values are true or false
        /// </summary>
        public string InGoalState { get; set; }

        /// <summary>
        /// Specifies if the node is in quarantine
        /// </summary>
        public string InQuarantine { get; set; }

        /// <summary>
        /// Specifies if the node is a dedicated bare metal node
        /// </summary>
        public string IsDedicatedBareMetal { get; set; }

        /// <summary>
        /// Specifies if the node is used for dedicated host
        /// </summary>
        public string IsDedicatedHost { get; set; }

        /// <summary>
        /// Specifies if the node is isolated
        /// </summary>
        public string IsIsolated { get; set; }

        /// <summary>
        /// Specifies if the node is offline
        /// </summary>
        public string IsOffline { get; set; }

        /// <summary>
        /// Specifies the node's availability state
        /// </summary>
        public string NodeAvailabilityState { get; set; }

        /// <summary>
        /// Specifies the node's capabilities
        /// </summary>
        public string NodeCapabilityType { get; set; }

        /// <summary>
        /// Specifies the node's state
        /// </summary>
        public string State { get; set; }

        /// <summary>
        /// Specifies the tip session associated with the node
        /// </summary>
        public string TipNodeSessionId { get; set; }
    }
}
