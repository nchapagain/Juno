namespace Juno.Contracts
{
    using System;
    using System.Text;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;

    /// <summary>
    /// An individual reservation against a cluster account.
    /// </summary>
    [JsonObject]
    public class ClusterReservation : IEquatable<ClusterReservation>
    {
        private int? hashCode;

        /// <summary>
        /// Initializes a new instance of the <see cref="ClusterReservation"/> class.
        /// </summary>
        /// <param name="nodeId">the nodeId in which the reservation should be made for.</param>
        /// <param name="expirationTime">The time in which this reservation is no longer valid.</param>
        [JsonConstructor]
        public ClusterReservation(string nodeId, DateTime expirationTime)
        {
            nodeId.ThrowIfNull(nameof(nodeId));

            this.NodeId = nodeId;
            this.ExpirationTime = expirationTime;
        }

        /// <summary>
        /// Get the expiration of the reservation.
        /// </summary>
        [JsonProperty(PropertyName = "expirationTime", Required = Required.Always)]
        public DateTime ExpirationTime { get; }

        /// <summary>
        /// The node id in which the reservation was made on behalf of.
        /// </summary>
        [JsonProperty(PropertyName = "nodeId", Required = Required.Always)]
        public string NodeId { get; }

        /// <summary>
        /// Determines equality between this and an other instance of <see cref="ClusterReservation"/>
        /// </summary>
        /// <param name="other">The other <see cref="ClusterReservation"/></param>
        /// <returns>True/False if the two instances are equal.</returns>
        public bool Equals(ClusterReservation other)
        {
            return other != null && this.GetHashCode() == other.GetHashCode();
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (obj == null) 
            {
                return false;
            }

            ClusterReservation other = obj as ClusterReservation;
            if (other == null)
            {
                return false;
            }

            return this.Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            if (this.hashCode == null)
            {
                this.hashCode = new StringBuilder()
                    .Append(this.ExpirationTime)
                    .Append(this.NodeId)
                    .ToString().GetHashCode(StringComparison.OrdinalIgnoreCase);
            }

            return this.hashCode.Value;
        }
    }
}
