namespace Juno.Execution.AgentRuntime.Contract
{
    using System;
    using System.Text;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;

    /// <summary>
    /// Attribute for a SATA device
    /// </summary>
    public class SataSmartAttribute : IEquatable<SataSmartAttribute>
    {
        private int? hashCode;

        /// <summary>
        /// Initializes a new instance of the <see cref="SataSmartAttribute"/> class.
        /// </summary>
        /// <param name="id">Id of the attribute</param>
        /// <param name="name">Name of the attribute</param>
        /// <param name="value">Value of the attribute</param>
        /// <param name="worst">Worst value of the attribute</param>
        /// <param name="thresh">threshold of the value of the attribute</param>
        /// <param name="when_failed">When that threshold was exceeded</param>
        [JsonConstructor]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Parameter names must match JSON property names.")]
        public SataSmartAttribute(string id, string name, string value, string worst, string thresh, string when_failed)
        {
            id.ThrowIfNull(nameof(id));
            name.ThrowIfNull(nameof(name));
            value.ThrowIfNull(nameof(value));
            worst.ThrowIfNull(nameof(worst));
            thresh.ThrowIfNull(nameof(thresh));
            when_failed.ThrowIfNull(nameof(when_failed));

            this.Id = id;
            this.Name = name;
            this.Value = value;
            this.Worst = worst;
            this.Threshold = thresh;
            this.FailureTime = when_failed;
        }

        /// <summary>
        /// Get the Id of the attribute
        /// </summary>
        [JsonProperty(PropertyName = "id", Required = Required.Always)]
        public string Id { get; }

        /// <summary>
        /// Get the name of the attribute
        /// </summary>
        [JsonProperty(PropertyName = "name", Required = Required.Always)]
        public string Name { get; }

        /// <summary>
        /// Get the value of the attribute
        /// </summary>
        [JsonProperty(PropertyName = "value", Required = Required.Always)]
        public string Value { get; }

        /// <summary>
        /// Get the Worst value of the attribute
        /// </summary>
        [JsonProperty(PropertyName = "worst", Required = Required.Always)]
        public string Worst { get; }

        /// <summary>
        /// Get the threshold of the attribute
        /// </summary>
        [JsonProperty(PropertyName = "thresh", Required = Required.Always)]
        public string Threshold { get; }

        /// <summary>
        /// Get the time of failure
        /// </summary>
        [JsonProperty(PropertyName = "when_failed", Required = Required.Always)]
        public string FailureTime { get; }

        /// <summary>
        /// Evaluates the equality beteen two <see cref="SataSmartAttribute"/> instances
        /// </summary>
        /// <param name="other">The other <see cref="SataSmartAttribute"/> to evaluate equality against.</param>
        /// <returns>True/False if the two <see cref="SataSmartAttribute"/> are equal.</returns>
        public bool Equals(SataSmartAttribute other)
        {
            return other != null && this.GetHashCode() == other.GetHashCode();
        }

        /// <summary>
        /// Evaluates the equality between this and another object.
        /// </summary>
        /// <param name="obj">The object to compare equality against.</param>
        /// <returns>True/False if this and the object are equal.</returns>
        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            SataSmartAttribute other = obj as SataSmartAttribute;
            if (other == null)
            {
                return false;
            }

            return this.Equals(other);
        }

        /// <summary>
        /// Generates a unique hashcode for this.
        /// </summary>
        /// <returns>The hashcode for this.</returns>
        public override int GetHashCode()
        {
            if (this.hashCode == null)
            {
                this.hashCode = new StringBuilder()
                    .Append(this.Id)
                    .Append(this.Name)
                    .Append(this.Value)
                    .Append(this.Worst)
                    .Append(this.Threshold)
                    .Append(this.FailureTime)
                    .ToString().ToUpperInvariant().GetHashCode(StringComparison.Ordinal);
            }

            return this.hashCode.Value;
        }
    }
}
