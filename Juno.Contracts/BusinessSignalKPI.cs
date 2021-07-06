namespace Juno.Contracts
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.Text;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;

    /// <summary>
    /// Light weight version of Business Signals.
    /// </summary>
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class BusinessSignalKPI : IEquatable<BusinessSignalKPI>
    {
        private int? hashCode;

        /// <summary>
        /// Initializes a new instance of the <see cref="BusinessSignalKPI"/> class.
        /// </summary>
        /// <param name="businessSignalKPI">An instance of Business Signal KPI.</param>
        public BusinessSignalKPI(BusinessSignalKPI businessSignalKPI)
            : this(
                 businessSignalKPI?.FriendlyName,
                 businessSignalKPI?.ShortName,
                 businessSignalKPI?.Description,
                 (businessSignalKPI is null) ? 0 : businessSignalKPI.CountA,
                 (businessSignalKPI is null) ? 0 : businessSignalKPI.CountB,
                 businessSignalKPI?.OverallSignal)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BusinessSignalKPI"/> class.
        /// </summary>
        /// <param name="businessSignal">An instance of Business Signal.</param>
        public BusinessSignalKPI(BusinessSignal businessSignal)
            : this(
                 businessSignal?.FriendlyName,
                 businessSignal?.ShortName,
                 businessSignal?.Description,
                 (businessSignal is null) ? 0 : businessSignal.CountA,
                 (businessSignal is null) ? 0 : businessSignal.CountB,
                 businessSignal?.OverallSignal)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BusinessSignalKPI"/> class.
        /// </summary>
        /// <param name="friendlyName">Friendly name of the Business Signal.</param>
        /// <param name="shortName">Short name of the Business Signal.</param>
        /// <param name="description">Description of the Business Signal.</param>
        /// <param name="countA">Total number of instances for A.</param>
        /// <param name="countB">Total number of instances for B.</param>
        /// <param name="overallSignal">Overall color of the Business Signal.</param>
        [JsonConstructor]
        public BusinessSignalKPI(
            string friendlyName,
            string shortName,
            string description,
            int countA,
            int countB,
            string overallSignal)
        {
            friendlyName.ThrowIfNullOrWhiteSpace(nameof(friendlyName));
            this.FriendlyName = friendlyName;

            shortName.ThrowIfNullOrWhiteSpace(nameof(shortName));
            this.ShortName = shortName;

            description.ThrowIfNullOrWhiteSpace(nameof(description));
            this.Description = description;

            this.CountA = (countA >= 0)
                ? countA
                : throw new ArgumentException("The countA parameter should be >= 0.", nameof(countA));

            this.CountB = (countB >= 0)
                ? countB
                : throw new ArgumentException("The countB parameter should be >= 0.", nameof(countB));

            overallSignal.ThrowIfNullOrWhiteSpace(nameof(overallSignal));
            this.OverallSignal = overallSignal;
        }

        /// <summary>
        /// Friendly name of the Business Signal.
        /// </summary>
        [Required]
        [JsonProperty(PropertyName = "friendlyName", Required = Required.Always, NullValueHandling = NullValueHandling.Include)]
        public string FriendlyName { get; }

        /// <summary>
        /// Short name of the Business Signal.
        /// </summary>
        [Required]
        [JsonProperty(PropertyName = "shortName", Required = Required.Always, NullValueHandling = NullValueHandling.Include)]
        public string ShortName { get; }

        /// <summary>
        /// Description of the Business Signal.
        /// </summary>
        [Required]
        [JsonProperty(PropertyName = "description", Required = Required.Always, NullValueHandling = NullValueHandling.Include)]
        public string Description { get; }

        /// <summary>
        /// Total number of instances for A.
        /// </summary>
        [Required]
        [JsonProperty(PropertyName = "countA", Required = Required.Always, NullValueHandling = NullValueHandling.Include)]
        public int CountA { get; }

        /// <summary>
        /// Total number of instances for B.
        /// </summary>
        [Required]
        [JsonProperty(PropertyName = "countB", Required = Required.Always, NullValueHandling = NullValueHandling.Include)]
        public int CountB { get; }

        /// <summary>
        /// Overall color of the Business Signal.
        /// </summary>
        [Required]
        [JsonProperty(PropertyName = "overallSignal", Required = Required.Always, NullValueHandling = NullValueHandling.Include)]
        public string OverallSignal { get; }

        /// <summary>
        /// Determines if two objects are equal.
        /// </summary>
        /// <param name="lhs">The left hand side.</param>
        /// <param name="rhs">The right hand side.</param>
        /// <returns>True if the objects are equal. False otherwise.</returns>
        public static bool operator ==(BusinessSignalKPI lhs, BusinessSignalKPI rhs)
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
        public static bool operator !=(BusinessSignalKPI lhs, BusinessSignalKPI rhs)
        {
            return !(lhs == rhs);
        }

        /// <summary>
        /// Determines if two objects are equal.
        /// </summary>
        /// <param name="obj">Defines the object to compare against the current instance.</param>
        /// <returns>True if the objects are equal. False otherwise.</returns>
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
                if (obj != null)
                {
                    areEqual = this.Equals(obj as BusinessSignalKPI);
                }
            }

            return areEqual;
        }

        /// <summary>
        /// Determines if two objects are equal.
        /// </summary>
        /// <param name="other">Defines the object to compare against the current instance.</param>
        /// <returns>True if the objects are equal. False otherwise.</returns>
        public virtual bool Equals(BusinessSignalKPI other)
        {
            return other != null && this.GetHashCode() == other.GetHashCode();
        }

        /// <summary>
        /// Override method returns a unique integer hash code
        /// identifier for the class instance
        /// </summary>
        /// <returns>
        /// Type:  System.Int32
        /// A unique integer identifier for the class instance
        /// </returns>
        public override int GetHashCode()
        {
            if (this.hashCode == null)
            {
                this.hashCode = new StringBuilder()
                    .AppendProperties(
                    this.FriendlyName,
                    this.ShortName,
                    this.Description,
                    this.CountA.ToString(),
                    this.CountB.ToString(),
                    this.OverallSignal)
                    .ToString().GetHashCode(StringComparison.OrdinalIgnoreCase);
            }

            return this.hashCode.Value;
        }
    }
}