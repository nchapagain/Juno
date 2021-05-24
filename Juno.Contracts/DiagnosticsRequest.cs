namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;

    /// <summary>
    /// Class holds the details about the diagnostics the provider requested for.
    /// </summary>
    [DebuggerDisplay("{IssueType }: {Id}")]
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class DiagnosticsRequest : IEquatable<DiagnosticsRequest>, IIdentifiable
    {
        private int? hashCode;

        /// <summary>
        /// Initializes a new instance of the <see cref="DiagnosticsRequest"/> class.
        /// </summary>
        /// <param name="experimentId">The experiment in which the diagnostics are being performed</param>
        /// <param name="id">Specifies an unique id for this request.</param>
        /// <param name="issueType">Specifies the issue type.</param>
        /// <param name="timeRangeBegin">Specifies the time range begin.</param>
        /// <param name="timeRangeEnd">Specifies the time range end.</param>
        /// <param name="context">Specifies the context details.</param>
        [JsonConstructor]
        public DiagnosticsRequest(string experimentId, string id, DiagnosticsIssueType issueType, DateTime timeRangeBegin, DateTime timeRangeEnd, IDictionary<string, IConvertible> context)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));
            id.ThrowIfNullOrWhiteSpace(nameof(id));
            context.ThrowIfNull(nameof(context));

            this.ExperimentId = experimentId;
            this.Id = id;
            this.IssueType = issueType;
            this.TimeRangeBegin = timeRangeBegin;
            this.TimeRangeEnd = timeRangeEnd;
            this.Context = context;
        }

        /// <summary>
        /// Gets the Experiment ID.
        /// </summary>
        [JsonProperty(PropertyName = "experimentId", Required = Required.Always)]
        public string ExperimentId { get; }

        /// <summary>
        /// Gets the ID of the diagnostics request.
        /// </summary>
        [JsonProperty(PropertyName = "id", Required = Required.Always)]
        public string Id { get; }

        /// <summary>
        /// Gets the type of issue.
        /// </summary>
        [JsonProperty(PropertyName = "issueType", Required = Required.Always)]
        public DiagnosticsIssueType IssueType { get; }

        /// <summary>
        /// Gets the diagnostics start time.
        /// </summary>
        [JsonProperty(PropertyName = "timeRangeBegin", Required = Required.Always)]
        public DateTime TimeRangeBegin { get; }

        /// <summary>
        /// Gets the diagnostics end time.
        /// </summary>
        [JsonProperty(PropertyName = "timeRangeEnd", Required = Required.Always)]
        public DateTime TimeRangeEnd { get; }

        /// <summary>
        /// Gets the context describing additional information about the diagnostics.
        /// </summary>
        [JsonProperty(PropertyName = "context", Required = Required.Default, NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(ParameterDictionaryJsonConverter))]
        public IDictionary<string, IConvertible> Context { get; }

        /// <summary>
        /// Determines if two objects are equal.
        /// </summary>
        /// <param name="lhs">The left hand side.</param>
        /// <param name="rhs">The right hand side.</param>
        /// <returns>True if the objects are equal. False otherwise.</returns>
        public static bool operator ==(DiagnosticsRequest lhs, DiagnosticsRequest rhs)
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
        public static bool operator !=(DiagnosticsRequest lhs, DiagnosticsRequest rhs)
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
                // Apply value-type semantics to determine the equality of the instances
                DiagnosticsRequest itemDescription = obj as DiagnosticsRequest;
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
        public virtual bool Equals(DiagnosticsRequest other)
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
                StringBuilder hashBuilder = new StringBuilder($"{this.Id},{this.IssueType},{this.TimeRangeBegin},{this.TimeRangeEnd}");
                if (this.Context?.Any() == true)
                {
                    hashBuilder.Append($"{string.Join(",", this.Context.Select(entry => $"{entry.Key}={entry.Value}"))}");
                }

                this.hashCode = hashBuilder.ToString().GetHashCode(StringComparison.OrdinalIgnoreCase);
            }

            return this.hashCode.Value;
        }
    }
}
