namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents runtime metadata about an experiment used to supply context
    /// to services and providers in the system operating on the experiment.
    /// </summary>
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class ExperimentMetadata : IEquatable<ExperimentMetadata>
    {
        private int? hashCode;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExperimentMetadata"/> class.
        /// </summary>
        /// <param name="experimentId">The unique ID of the notice.</param>
        /// <param name="metadata">Provides context information that define the specifics of the notice.</param>
        [JsonConstructor]
        public ExperimentMetadata(string experimentId, IDictionary<string, IConvertible> metadata = null)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));

            this.ExperimentId = experimentId;
            this.Metadata = new Dictionary<string, IConvertible>(StringComparer.OrdinalIgnoreCase);

            if (metadata != null)
            {
                this.Metadata.AddRange(metadata);
            }
        }

        /// <summary>
        /// Gets the unique ID of the related experiment.
        /// </summary>
        [JsonProperty(PropertyName = "experimentId", Required = Required.Always)]
        public string ExperimentId { get; }

        /// <summary>
        /// Gets the set of metadata associated with the experiment notice.
        /// </summary>
        [JsonProperty(PropertyName = "metadata", Required = Required.Default, NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(ParameterDictionaryJsonConverter))]
        public IDictionary<string, IConvertible> Metadata { get; }

        /// <summary>
        /// Determines if two objects are equal.
        /// </summary>
        /// <param name="lhs">The left hand side.</param>
        /// <param name="rhs">The right hand side.</param>
        /// <returns>True if the objects are equal. False otherwise.</returns>
        public static bool operator ==(ExperimentMetadata lhs, ExperimentMetadata rhs)
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
        public static bool operator !=(ExperimentMetadata lhs, ExperimentMetadata rhs)
        {
            return !(lhs == rhs);
        }

        /// <summary>
        /// Override method determines if the two objects are equal
        /// </summary>
        /// <param name="obj">Defines the object to compare against the current instance.</param>
        /// <returns>
        /// Type:  System.Boolean
        /// True if the objects are equal or False if not
        /// </returns>
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
                ExperimentMetadata itemDescription = obj as ExperimentMetadata;
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
        /// <param name="other">Defines the object to compare against the current instance.</param>
        /// <returns>
        /// Type:  System.Boolean
        /// True if the objects are equal or False if not
        /// </returns>
        public virtual bool Equals(ExperimentMetadata other)
        {
            return other != null && this.GetHashCode() == other.GetHashCode();
        }

        /// <summary>
        /// Override enables the creation of an accurate hash code for the
        /// object.
        /// </summary>
        public override int GetHashCode()
        {
            if (this.hashCode == null)
            {
                this.hashCode = new StringBuilder(this.ExperimentId)
                    .AppendParameters(this.Metadata)
                    .ToString().GetHashCode(StringComparison.OrdinalIgnoreCase);
            }

            return this.hashCode.Value;
        }
    }
}
