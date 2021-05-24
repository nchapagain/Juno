namespace Juno.Execution.AgentRuntime.Contract
{
    using System;
    using System.Text;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;

    /// <summary>
    /// Properties of an SSD that signify the health of an SSD.
    /// </summary>
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class SsdHealth : IEquatable<SsdHealth>
    {
        private int? hashCode;

        /// <summary>
        /// Initializes a new instance of <see cref="SsdHealth"/>
        /// </summary>
        public SsdHealth(string passed)
        {
            passed.ThrowIfNull(nameof(passed));
            this.Passed = passed;
        }

        /// <summary>
        /// Get the propert Passed
        /// </summary>
        [JsonProperty(PropertyName = "passed", Required = Required.Always)]
        public string Passed { get; }

        /// <summary>
        /// Evaluates the equality between two <see cref="SsdHealth"/>
        /// </summary>
        /// <param name="other">The other <see cref="SsdHealth"/> to evaluate equality against.</param>
        /// <returns>True/False if the two <see cref="SsdHealth"/> are equal.</returns>
        public bool Equals(SsdHealth other)
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

            SsdHealth other = obj as SsdHealth;
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
                    .Append(this.Passed)
                    .ToString().ToUpperInvariant().GetHashCode(StringComparison.Ordinal);
            }

            return this.hashCode.Value;
        }
    }
}
