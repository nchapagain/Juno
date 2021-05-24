namespace Juno.Execution.Providers.AutoTriage
{
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;

    /// <summary>
    /// Stores details of a tip diagnostics.
    /// </summary>
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class TipDiagnosticsInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TipDiagnosticsInfo"/> class.
        /// </summary>
        /// <param name="nodeId">Specifies the tip node id.</param>
        /// <param name="tipSessionId">Specifies the tip session id</param>
        /// <param name="tipSessionChangeId">Specifies the tip session change id</param>
        [JsonConstructor]
        public TipDiagnosticsInfo(string nodeId, string tipSessionId, string tipSessionChangeId = null)
        {
            nodeId.ThrowIfNullOrWhiteSpace(nameof(nodeId));
            tipSessionId.ThrowIfNullOrWhiteSpace(nameof(tipSessionId));

            this.NodeId = nodeId;
            this.TipSessionId = tipSessionId;
            this.TipSessionChangeId = tipSessionChangeId;
        }

        /// <summary>
        /// Specifies the tip node ID.
        /// </summary>
        [JsonProperty("nodeId")]
        public string NodeId { get; }

        /// <summary>
        /// Specifies the tip session ID.
        /// </summary>
        [JsonProperty("tipSessionId")]
        public string TipSessionId { get; }

        /// <summary>
        /// Specifies the tip session change ID.
        /// </summary>
        [JsonProperty("tipSessionChangeId")]
        public string TipSessionChangeId { get; }
    }
}