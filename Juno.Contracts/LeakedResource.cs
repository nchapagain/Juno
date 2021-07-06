namespace Juno.Contracts
{
    using System;
    using System.Text;
    using Newtonsoft.Json;

    /// <summary>
    /// Describes the properties of Leaked Resource.
    /// </summary>
    public class LeakedResource : IEquatable<LeakedResource>
    {
        private int? hashCode;

        /// <summary>
        /// Json Constructor for juno leaked resources
        /// </summary>
        /// <param name="createdTime"> Timestamp for when first interacted with the resource</param>
        /// <param name="id"> VM name or tipsessionId </param>
        /// <param name="resourceType"> Resource Type Tip or Arm</param>
        /// <param name="tipNodeSessionId">TipSessionId in which Juno leaked</param>
        /// <param name="nodeId">NodeId associated with TipSessionId</param>
        /// <param name="daysLeaked">Number of days resource has been leaked</param>
        /// <param name="experimentId"></param>
        /// <param name="experimentName">Name of the Juno Experiment</param>
        /// <param name="impactType">Impact Type of the Juno Experiment</param>
        /// <param name="cluster">Cluster Name of the resource JunoExperiment Ran on i.e BNZ14PrdApp10</param>
        /// <param name="subscriptionId">Azure Subscription Id associated with the resource</param>
        /// <param name="owner">Owner of the leaked resource</param>
        /// <param name="source">Source of how leaked resource was gathered</param>
        [JsonConstructor]
        public LeakedResource(
            DateTime createdTime,
            string id,
            string resourceType,
            string tipNodeSessionId,
            string nodeId,
            int daysLeaked,
            string experimentId,
            string experimentName,
            ImpactType impactType,
            string cluster,
            string subscriptionId,
            string owner,
            LeakedResourceSource source)
        {
            this.CreatedTime = createdTime;
            this.Id = id;
            this.ResourceType = resourceType;
            this.TipNodeSessionId = tipNodeSessionId;
            this.NodeId = nodeId;
            this.DaysLeaked = daysLeaked;
            this.ExperimentId = experimentId;
            this.ExperimentName = experimentName;
            this.ImpactType = impactType;
            this.Cluster = cluster;
            this.SubscriptionId = subscriptionId;
            this.Owner = owner;
            this.Source = source;
        }

        /// <summary>
        /// Timestamp for tipNodeStartTime
        /// When Juno first interacted with the resource
        /// </summary>
        [JsonProperty(PropertyName = "createdTime", Required = Required.Always, Order = 1)]
        public DateTime CreatedTime { get; }

        /// <summary>
        /// VM name or tipsessionId
        /// </summary>
        [JsonProperty(PropertyName = "id", Required = Required.Always, Order = 2)]
        public string Id { get; }

        /// <summary>
        /// Resource Type Tip or Arm
        /// </summary>
        [JsonProperty(PropertyName = "resourceType", Required = Required.AllowNull, Order = 3)]
        public string ResourceType { get; }

        /// <summary>
        /// TipSessionId in which Juno leaked
        /// </summary>
        [JsonProperty(PropertyName = "tipNodeSessionId", Required = Required.AllowNull, Order = 4)]
        public string TipNodeSessionId { get; }

        /// <summary>
        /// NodeId associated with TipSessionId
        /// </summary>
        [JsonProperty(PropertyName = "nodeId", Required = Required.AllowNull, Order = 5)]
        public string NodeId { get; }

        /// <summary>
        /// Number of days resource has been leaked
        /// </summary>
        [JsonProperty(PropertyName = "daysLeaked", Required = Required.AllowNull, Order = 6)]
        public int DaysLeaked { get; }

        /// <summary>
        /// ID associated with Juno Experiment
        /// </summary>
        [JsonProperty(PropertyName = "experimentId", Required = Required.AllowNull, Order = 7)]
        public string ExperimentId { get; }

        /// <summary>
        /// Name of the Juno Experiment
        /// </summary>
        [JsonProperty(PropertyName = "experimentName", Required = Required.AllowNull, Order = 8)]
        public string ExperimentName { get; }

        /// <summary>
        /// Impact Type of the Juno Experiment
        /// </summary>
        [JsonProperty(PropertyName = "impactType", Required = Required.AllowNull, Order = 9)]
        public ImpactType ImpactType { get; }

        /// <summary>
        /// Cluster Name i.e BNZ14PrdApp10
        /// </summary>
        [JsonProperty(PropertyName = "cluster", Required = Required.AllowNull, Order = 10)]
        public string Cluster { get; }

        /// <summary>
        /// Azure Subscription ID
        /// </summary>
        [JsonProperty(PropertyName = "subscriptionId", Required = Required.AllowNull, Order = 11)]
        public string SubscriptionId { get; }

        /// <summary>
        /// Azure Resource Owner
        /// </summary>
        [JsonProperty(PropertyName = "owner", Required = Required.AllowNull, Order = 12)]
        public string Owner { get; }

        /// <summary>
        /// Source of where leaked resource was found
        /// </summary>
        [JsonProperty(PropertyName = "source", Required = Required.AllowNull, Order = 13)]
        public LeakedResourceSource Source { get; set; }

        /// <summary>
        /// Determines if two objects are equal.
        /// </summary>
        /// <param name="lhs">The left hand side.</param>
        /// <param name="rhs">The right hand side.</param>
        /// <returns>True if the objects are equal. False otherwise.</returns>
        public static bool operator ==(LeakedResource lhs, LeakedResource rhs)
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
        public static bool operator !=(LeakedResource lhs, LeakedResource rhs)
        {
            return !(lhs == rhs);
        }

        /// <inheritdoc/>
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
                LeakedResource itemDescription = obj as LeakedResource;
                if (itemDescription != null)
                {
                    areEqual = this.Equals(itemDescription);
                }
            }

            return areEqual;
        }

        /// <summary>
        /// Method determines if the other object is equal to this instance
        /// </summary>
        /// <param name="other">Defines the other object to compare against</param>
        /// <returns>True if the objects are equal, false otherwise</returns>
        public virtual bool Equals(LeakedResource other)
        {
            return other != null && this.GetHashCode() == other.GetHashCode();
        }

        /// <summary>
        /// Override method returns a unique integer hash code
        /// Calls the base class for now to suppress warnings
        /// </summary>
        /// <returns> 
        /// Type: System.Int32
        /// A unique identifier for the class instance.   
        /// </returns>
        public override int GetHashCode()
        {
            if (this.hashCode == null)
            {
                this.hashCode = new StringBuilder()
                    .AppendProperties(this.Id, this.TipNodeSessionId, this.NodeId, this.ExperimentName, this.ImpactType, this.Cluster, this.SubscriptionId)
                    .ToString().GetHashCode(StringComparison.OrdinalIgnoreCase);
            }

            return this.hashCode.Value;
        }
    }
}