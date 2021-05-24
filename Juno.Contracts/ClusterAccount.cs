namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    /// <summary>
    /// Account for a cluster. Keeps tracks of transactions made to the cluster account.
    /// </summary>
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class ClusterAccount : IEquatable<ClusterAccount>
    {
        private static readonly Func<List<ClusterReservation>, List<ClusterReservation>> PruningFunction = (list) =>
        {
            return list.Where(dt => DateTime.Compare(dt.ExpirationTime, DateTime.UtcNow) > 0).ToList();
        };

        private int? hashCode;

        /// <summary>
        /// Initialize a <see cref="ClusterAccount"/>
        /// </summary>
        /// <param name="tipSessionSource">Number of tip sessions according to source.</param>
        public ClusterAccount(int tipSessionSource)
        {
            this.TipSessionsSource = tipSessionSource;
            this.Reservations = new List<ClusterReservation>();
        }

        /// <summary>
        /// Initialize a <see cref="ClusterAccount"/>
        /// </summary>
        /// <param name="tipSessionSource">Number of tip sessions according to source.</param>
        /// <param name="reservations">List of reservations made to this cluster.</param>
        [JsonConstructor]
        public ClusterAccount(int tipSessionSource, List<ClusterReservation> reservations)
        {
            this.TipSessionsSource = tipSessionSource;
            this.Reservations = new List<ClusterReservation>(reservations);
        }

        /// <summary>
        /// Get List of Reservations made to this cluster.
        /// </summary>
        [JsonProperty(PropertyName = "reservations", Required = Required.Always)]
        public List<ClusterReservation> Reservations { get; }

        /// <summary>
        /// Get Number of tip sessions allowed on this cluster.
        /// </summary>
        public int TipSessionsAllowed => this.TipSessionsSource -
                    ClusterAccount.PruningFunction.Invoke(this.Reservations).Count;

        [JsonProperty(PropertyName = "tipSessionsSource", Required = Required.Always)]
        private int TipSessionsSource { get; }

        /// <summary>
        /// Remove expired reservation/deletions
        /// </summary>
        /// <returns>A Cluster Account with the pruned lists.</returns>
        public ClusterAccount PruneAccount()
        {
            return new ClusterAccount(this.TipSessionsSource, ClusterAccount.PruningFunction.Invoke(this.Reservations));
        }

        /// <summary>
        /// Determines equality with this and another instance of <see cref="ClusterAccount"/>
        /// </summary>
        /// <param name="other">The other instance to use in the determination of equality.</param>
        /// <returns>True/False if this and the other Cluster Account are equal.</returns>
        public bool Equals(ClusterAccount other)
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

            ClusterAccount other = obj as ClusterAccount;
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
                    .Append(this.TipSessionsAllowed)
                    .Append(string.Join(string.Empty, this.Reservations))
                    .ToString().GetHashCode(StringComparison.OrdinalIgnoreCase);
            }

            return this.hashCode.Value;
        }
    }
}
