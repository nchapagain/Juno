namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Text;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    /// <summary>
    /// An eligible environment resource utilized by the environment selection service.
    /// </summary>
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy), MemberSerialization = MemberSerialization.OptIn)]
    public class EnvironmentCandidate : IEquatable<EnvironmentCandidate>
    {
        private const string WildCard = "*";
        private int? hashCode;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnvironmentCandidate"/> class.
        /// </summary>
        /// <param name="subscription">The subscription used by this instance</param>
        /// <param name="cluster">The cluster assigned to this instance</param>
        /// <param name="region">The region where this instance resides</param>
        /// <param name="machinePoolName">The machine pool where this instance resides</param>
        /// <param name="rack">The rack where the candidate could reside</param>
        /// <param name="node">The node ID of this instance</param>
        /// <param name="vmSku">The VmSku assigned to this instance</param>
        /// <param name="cpuId">Id for the cpu for this instance</param>
        /// <param name="additionalInfo">Any additional info that pertains to this instance</param>
        [JsonConstructor]
        public EnvironmentCandidate(
            string subscription = null, 
            string cluster = null, 
            string region = null, 
            string machinePoolName = null, 
            string rack = null, 
            string node = null, 
            IList<string> vmSku = null, 
            string cpuId = null, 
            IDictionary<string, string> additionalInfo = null)
        {
            this.Subscription = subscription ?? EnvironmentCandidate.WildCard;
            this.ClusterId = cluster ?? EnvironmentCandidate.WildCard;
            this.Region = region ?? EnvironmentCandidate.WildCard;
            this.MachinePoolName = machinePoolName ?? EnvironmentCandidate.WildCard;
            this.Rack = rack ?? EnvironmentCandidate.WildCard;
            this.NodeId = node ?? EnvironmentCandidate.WildCard;
            this.VmSku = vmSku == null ? new List<string>() : new List<string>(vmSku);
            this.CpuId = cpuId ?? EnvironmentCandidate.WildCard;
            
            this.AdditionalInfo = additionalInfo?.Any() == true
                ? this.AdditionalInfo = new Dictionary<string, string>(additionalInfo, StringComparer.OrdinalIgnoreCase)
                : this.AdditionalInfo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The subscription used by this instance
        /// </summary>
        [JsonProperty(PropertyName = "subscription", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(EnvironmentCandidate.WildCard)]
        public string Subscription { get; }

        /// <summary>
        /// The cluster assigned to this instance
        /// </summary>
        [JsonProperty(PropertyName = "cluster", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(EnvironmentCandidate.WildCard)]
        public string ClusterId { get; }

        /// <summary>
        /// The region where this instance resides
        /// </summary>
        [JsonProperty(PropertyName = "region", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(EnvironmentCandidate.WildCard)]
        public string Region { get; }

        /// <summary>
        /// The machine pool where this instance resides
        /// </summary>
        [JsonProperty(PropertyName = "machinePoolName", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(EnvironmentCandidate.WildCard)]
        public string MachinePoolName { get; }

        /// <summary>
        /// The Rack where this candidate resides
        /// </summary>
        [JsonProperty(PropertyName = "rack", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(EnvironmentCandidate.WildCard)]
        public string Rack { get; }

        /// <summary>
        /// The node ID of this instance
        /// </summary>
        [JsonProperty(PropertyName = "node", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(EnvironmentCandidate.WildCard)]
        public string NodeId { get; }

        /// <summary>
        /// The VmSku assigned to this instance
        /// </summary>
        [JsonProperty(PropertyName = "vmSku", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public IList<string> VmSku { get; }

        /// <summary>
        /// The Cpu Id of the current machine
        /// </summary>
        [JsonProperty(PropertyName = "cpuId", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue("*")]
        public string CpuId { get; }

        /// <summary>
        /// Any additional info that pertains to this instance
        /// </summary>
        [JsonProperty(PropertyName = "additionalInfo", Required = Required.Default, NullValueHandling = NullValueHandling.Ignore)]
        public IDictionary<string, string> AdditionalInfo { get; }

        /// <summary>
        /// Determines if two objects are equal
        /// </summary>
        /// <param name="lhs">The left hand side.</param>
        /// <param name="rhs">The right hand side.</param>
        /// <returns>True if the objects are equals. False otherwise.</returns>
        public static bool operator ==(EnvironmentCandidate lhs, EnvironmentCandidate rhs)
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
        /// Determines if the two objects are NOT equal
        /// </summary>
        /// <param name="lhs">The left hand side</param>
        /// <param name="rhs">The right hand side</param>
        /// <returns>True if the objects are not equals. False otherwise.</returns>
        public static bool operator !=(EnvironmentCandidate lhs, EnvironmentCandidate rhs)
        {
            return !(lhs == rhs);
        }

        /// <summary>
        /// Compares the equality of this instance versus another environment candidate.
        /// </summary>
        /// <param name="other">The other Environment candidate to evaluate equality against.</param>
        /// <returns>If the two instances are equal</returns>
        public bool Equals(EnvironmentCandidate other)
        {
            if (other == null)
            {
                return false;
            }

            if (this.GetHashCode() == other.GetHashCode())
            {
                return true;
            }

            return EnvironmentCandidate.EqualsOrWildcard(this.Subscription, other.Subscription)
                && EnvironmentCandidate.EqualsOrWildcard(this.ClusterId, other.ClusterId)
                && EnvironmentCandidate.EqualsOrWildcard(this.Region, other.Region)
                && EnvironmentCandidate.EqualsOrWildcard(this.MachinePoolName, other.MachinePoolName)
                && EnvironmentCandidate.EqualsOrWildcard(this.Rack, other.Rack)
                && EnvironmentCandidate.EqualsOrWildcard(this.NodeId, other.NodeId)
                && EnvironmentCandidate.EqualsOrWildcard(this.VmSku, other.VmSku)
                && EnvironmentCandidate.EqualsOrWildcard(this.CpuId, other.CpuId);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(this, obj))
            {
                return true;
            }

            EnvironmentCandidate itemDescription = obj as EnvironmentCandidate;
            if (itemDescription == null)
            {
                return false;
            }

            return this.Equals(itemDescription);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            if (this.hashCode == null)
            {
                this.hashCode = new StringBuilder(base.GetHashCode().ToString())
                    .AppendProperties(
                    this.Subscription, 
                    this.ClusterId, 
                    this.Region, 
                    this.MachinePoolName,
                    this.Rack,
                    this.NodeId,
                    this.VmSku.ToString(),
                    this.CpuId)
                    .ToString().GetHashCode(StringComparison.OrdinalIgnoreCase);
            }

            return this.hashCode.Value;
        }

        /// <summary>
        /// Returns whether or not the two strings are equal
        /// OR if one/both of the fields contain the wildcard character
        /// </summary>
        /// <param name="value">The first string to compare against</param>
        /// <param name="otherValue">The other string to compare against.</param>
        /// <returns></returns>
        private static bool EqualsOrWildcard(string value, string otherValue)
        {
            return value.Equals(otherValue, StringComparison.Ordinal)
                || value.Equals(EnvironmentCandidate.WildCard, StringComparison.OrdinalIgnoreCase)
                || otherValue.Equals(EnvironmentCandidate.WildCard, StringComparison.OrdinalIgnoreCase);
        }

        private static bool EqualsOrWildcard(IList<string> left, IList<string> right)
        {
            return left.Count == 0 || right.Count == 0 || left.SequenceEqual(right);
        }
    }
}
