namespace Juno.Execution.AgentRuntime.Contract
{
    using System;
    using System.Text;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;

    /// <summary>
    /// Class that is serialized from the output of smartctl
    /// </summary>
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class SsdDrive : IEquatable<SsdDrive>
    {
        private int? hashCode;

        /// <summary>
        /// Initializes a new instance of the <see cref="SsdDrive"/> class.
        /// </summary>
        /// <param name="name">Name of the drive.</param>
        /// <param name="type">Type of the drive.</param>
        /// <param name="protocol">Protocol of the drive.</param>
        [JsonConstructor]
        public SsdDrive(string name, string type, string protocol)
        {
            name.ThrowIfNull(nameof(name));
            type.ThrowIfNull(nameof(type));
            protocol.ThrowIfNull(nameof(protocol));

            this.Name = name;
            this.Type = type;
            this.Protocol = protocol;
        }

        /// <summary>
        /// The name of the Drive
        /// </summary>
        [JsonProperty(PropertyName = "name", Required = Required.Always)]
        public string Name { get; }

        /// <summary>
        /// The type of the drive
        /// </summary>
        [JsonProperty(PropertyName = "type", Required = Required.Always)]
        public string Type { get; }

        /// <summary>
        /// Protocol the drive follows.
        /// </summary>
        [JsonProperty(PropertyName = "protocol", Required = Required.Always)]
        public string Protocol { get; }

        /// <summary>
        /// Evaluates the equality between two <see cref="SsdDrive"/> instances.
        /// </summary>
        /// <param name="other">The other <see cref="SsdDrive"/> to evaluate equality against.</param>
        /// <returns>True/False if the two <see cref="SsdDrive"/> are equal.</returns>
        public bool Equals(SsdDrive other)
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

            SsdDrive other = obj as SsdDrive;
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
                    .Append(this.Name)
                    .Append(this.Type)
                    .Append(this.Protocol)
                    .ToString().ToUpperInvariant().GetHashCode(StringComparison.Ordinal);
            }

            return this.hashCode.Value;
        }
    }
}
