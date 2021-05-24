namespace Juno.Execution.AgentRuntime.Contract
{
    using System;
    
    /// <summary>
    /// Defines Node properties.
    /// </summary>
    public class NodeIdentifier
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NodeIdentifier"/> class.
        /// </summary>
        public NodeIdentifier(string tipSessionId, string nodeId, string clusterId)
        {
            this.TipSessionId = tipSessionId;
            this.NodeId = nodeId;
            this.ClusterId = clusterId;
        }

        /// <summary>
        /// Id setup by TIP service to identify 
        /// the node is part of TIP session.
        /// </summary>
        public string TipSessionId { get; }

        /// <summary>
        /// Id setup by Fabric allocator to identify the node.
        /// </summary>
        public string NodeId { get; }

        /// <summary>
        /// Azure cluster Id.
        /// </summary>
        public string ClusterId { get; }
    }

    internal static class NodeConstants
    {
        internal const string TipSessionKey = @"SOFTWARE\Microsoft\AzureTipNode";
        internal const string AzNodeKey = @"SOFTWARE\AzureHL\NodeProperties";
    }
}
