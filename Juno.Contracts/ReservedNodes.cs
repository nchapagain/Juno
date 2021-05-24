namespace Juno.Contracts
{
    using System.Collections.Generic;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;

    /// <summary>
    /// Nodes that are reserved by the system using TiP.
    /// </summary>
    public class ReservedNodes
    {
        /// <summary>
        /// Initializes a new instance of <see cref="ReservedNodes"/>
        /// </summary>
        /// <param name="nodes"></param>
        [JsonConstructor]
        public ReservedNodes(IEnumerable<EnvironmentCandidate> nodes)
        {
            nodes.ThrowIfNull(nameof(nodes));

            this.Nodes = nodes;
        }

        /// <summary>
        /// List of reserved nodes.
        /// </summary>
        [JsonProperty(PropertyName = "nodes", Required = Required.Always)]
        public IEnumerable<EnvironmentCandidate> Nodes { get; }
    }
}
