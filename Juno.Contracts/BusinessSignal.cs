namespace Juno.Contracts
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.Text;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;

    /// <summary>
    /// Describes the result of an experiment.
    /// </summary>
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class BusinessSignal : IEquatable<BusinessSignal>
    {
        private int? hashCode;

        /// <summary>
        /// Initializes a new instance of the <see cref="BusinessSignal"/> class.
        /// </summary>
        /// <param name="businessSignal">An instance of Business Signal.</param>
        public BusinessSignal(BusinessSignal businessSignal)
            : this(
                 businessSignal?.ExperimentName,
                 businessSignal?.Revision,
                 businessSignal?.FriendlyName,
                 businessSignal?.ShortName,
                 businessSignal?.Description,
                 (businessSignal is null) ? 0 : businessSignal.TotalCount,
                 (businessSignal is null) ? 0 : businessSignal.CountA,
                 (businessSignal is null) ? 0 : businessSignal.CountB,
                 (businessSignal is null) ? 0 : businessSignal.RedCount,
                 (businessSignal is null) ? 0 : businessSignal.GreenCount,
                 (businessSignal is null) ? 0 : businessSignal.YellowCount,
                 (businessSignal is null) ? 0 : businessSignal.GreyCount,
                 businessSignal?.OverallSignal,
                 (businessSignal is null) ? default : businessSignal.ExperimentDateUtc)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BusinessSignal"/> class.
        /// </summary>
        /// <param name="experimentName">Name of the experiment.</param>
        /// <param name="revision">Revision of the experiment.</param>
        /// <param name="friendlyName">Friendly name of the Business Signal.</param>
        /// <param name="shortName">Short name of the Business Signal.</param>
        /// <param name="description">Description of the Business Signal.</param>
        /// <param name="totalCount">Total number of instances.</param>
        /// <param name="countA">Total number of instances for A.</param>
        /// <param name="countB">Total number of instances for B.</param>
        /// <param name="redCount">Total number of instances which are Red.</param>
        /// <param name="greenCount">Total number of instances which are Green.</param>
        /// <param name="yellowCount">Total number of instances which are Yellow.</param>
        /// <param name="greyCount">Total number of instances which are Grey.</param>
        /// <param name="overallSignal">Overall color of the Business Signal.</param>
        /// <param name="experimentDateUtc">Start date of the experiment.</param>
        [JsonConstructor]
        public BusinessSignal(
            string experimentName,
            string revision,
            string friendlyName,
            string shortName,
            string description,
            int totalCount,
            int countA,
            int countB,
            int redCount,
            int greenCount,
            int yellowCount,
            int greyCount,
            string overallSignal,
            DateTime experimentDateUtc)
        {
            experimentName.ThrowIfNullOrWhiteSpace(nameof(experimentName));
            this.ExperimentName = experimentName;

            revision.ThrowIfNullOrWhiteSpace(nameof(revision));
            this.Revision = revision;

            friendlyName.ThrowIfNullOrWhiteSpace(nameof(friendlyName));
            this.FriendlyName = friendlyName;

            shortName.ThrowIfNullOrWhiteSpace(nameof(shortName));
            this.ShortName = shortName;

            description.ThrowIfNullOrWhiteSpace(nameof(description));
            this.Description = description;

            this.TotalCount = (totalCount >= 0)
                ? totalCount
                : throw new ArgumentException("The totalCount parameter should be >= 0.", nameof(totalCount));

            this.CountA = (countA >= 0)
                ? countA
                : throw new ArgumentException("The countA parameter should be >= 0.", nameof(countA));

            this.CountB = (countB >= 0)
                ? countB
                : throw new ArgumentException("The countB parameter should be >= 0.", nameof(countB));

            this.RedCount = (redCount >= 0)
                ? redCount
                : throw new ArgumentException("The redCount parameter should be >= 0.", nameof(redCount));

            this.GreenCount = (greenCount >= 0)
                ? greenCount
                : throw new ArgumentException("The greenCount parameter should be >= 0.", nameof(greenCount));

            this.YellowCount = (yellowCount >= 0)
                ? yellowCount
                : throw new ArgumentException("The yellowCount parameter should be >= 0.", nameof(yellowCount));

            this.GreyCount = (greyCount >= 0)
                ? greyCount
                : throw new ArgumentException("The greyCount parameter should be >= 0.", nameof(greyCount));

            overallSignal.ThrowIfNullOrWhiteSpace(nameof(overallSignal));
            this.OverallSignal = overallSignal;

            this.ExperimentDateUtc = ((experimentDateUtc > default(DateTime)) && (experimentDateUtc <= DateTime.UtcNow))
                ? experimentDateUtc
                : throw new ArgumentException("The experimentDateUtc parameter has to be in between default and UtcNow.", nameof(experimentDateUtc));
        }

        /// <summary>
        /// Name of the experiment.
        /// </summary>
        [Required]
        [JsonProperty(PropertyName = "experimentName", Required = Required.Always, NullValueHandling = NullValueHandling.Include)]
        public string ExperimentName { get; }

        /// <summary>
        /// Revision of the experiment.
        /// </summary>
        [Required]
        [JsonProperty(PropertyName = "revision", Required = Required.Always, NullValueHandling = NullValueHandling.Include)]
        public string Revision { get; }

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
        /// Total number of instances.
        /// </summary>
        [Required]
        [JsonProperty(PropertyName = "totalCount", Required = Required.Always, NullValueHandling = NullValueHandling.Include)]
        public int TotalCount { get; }

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
        /// Total number of instances which are Red.
        /// </summary>
        [Required]
        [JsonProperty(PropertyName = "redCount", Required = Required.Always, NullValueHandling = NullValueHandling.Include)]
        public int RedCount { get; }

        /// <summary>
        /// Total number of instances which are Green.
        /// </summary>
        [Required]
        [JsonProperty(PropertyName = "greenCount", Required = Required.Always, NullValueHandling = NullValueHandling.Include)]
        public int GreenCount { get; }

        /// <summary>
        /// Total number of instances which are Yellow.
        /// </summary>
        [Required]
        [JsonProperty(PropertyName = "yellowCount", Required = Required.Always, NullValueHandling = NullValueHandling.Include)]
        public int YellowCount { get; }

        /// <summary>
        /// Total number of instances which are Grey.
        /// </summary>
        [Required]
        [JsonProperty(PropertyName = "greyCount", Required = Required.Always, NullValueHandling = NullValueHandling.Include)]
        public int GreyCount { get; }

        /// <summary>
        /// Overall color of the Business Signal.
        /// </summary>
        [Required]
        [JsonProperty(PropertyName = "overallSignal", Required = Required.Always, NullValueHandling = NullValueHandling.Include)]
        public string OverallSignal { get; }

        /// <summary>
        /// Start date of the experiment.
        /// </summary>
        [Required]
        [JsonProperty(PropertyName = "experimentDateUtc", Required = Required.Always, NullValueHandling = NullValueHandling.Include)]
        public DateTime ExperimentDateUtc { get; }

        /// <summary>
        /// Determines if two objects are equal.
        /// </summary>
        /// <param name="lhs">The left hand side.</param>
        /// <param name="rhs">The right hand side.</param>
        /// <returns>True if the objects are equal. False otherwise.</returns>
        public static bool operator ==(BusinessSignal lhs, BusinessSignal rhs)
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
        public static bool operator !=(BusinessSignal lhs, BusinessSignal rhs)
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
                    areEqual = this.Equals(obj as BusinessSignal);
                }
            }

            return areEqual;
        }

        /// <summary>
        /// Determines if two objects are equal.
        /// </summary>
        /// <param name="other">Defines the object to compare against the current instance.</param>
        /// <returns>True if the objects are equal. False otherwise.</returns>
        public virtual bool Equals(BusinessSignal other)
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
                    this.ExperimentName,
                    this.Revision,
                    this.FriendlyName,
                    this.ShortName,
                    this.Description,
                    this.TotalCount.ToString(),
                    this.CountA.ToString(),
                    this.CountB.ToString(),
                    this.RedCount.ToString(),
                    this.GreenCount.ToString(),
                    this.YellowCount.ToString(),
                    this.GreyCount.ToString(),
                    this.OverallSignal,
                    this.ExperimentDateUtc.ToString())
                    .ToString().GetHashCode(StringComparison.OrdinalIgnoreCase);
            }

            return this.hashCode.Value;
        }
    }
}