namespace Juno.Execution.AgentRuntime.Contract
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;

    /// <summary>
    /// Collection of <see cref="SsdDrive"/>
    /// </summary>
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class SsdDrives : IEnumerable<SsdDrive>, IEquatable<SsdDrives>
    {
        private int? hashCode;

        /// <summary>
        /// Initialize an instance of <see cref="SsdDrives"/>
        /// </summary>
        [JsonConstructor]
        public SsdDrives(IEnumerable<SsdDrive> devices)
        {
            devices.ThrowIfNull(nameof(devices));
            this.Devices = devices == null
                ? new List<SsdDrive>()
                : new List<SsdDrive>(devices);
        }

        /// <summary>
        /// List of devices.
        /// </summary>
        [JsonProperty(PropertyName = "devices", Required = Required.Always)]
        private List<SsdDrive> Devices { get; }

        /// <summary>
        /// Evaluates the equality between two <see cref="SsdDrives"/> instances 
        /// </summary>
        /// <param name="other">The other <see cref="SsdDrives"/> to evaluate equality against.</param>
        /// <returns>True/False if the two <see cref="SsdDrives"/> are equal.</returns>
        public bool Equals(SsdDrives other)
        {
            return other != null && this.GetHashCode() == other.GetHashCode();
        }

        /// <inheritdoc/>
        public IEnumerator<SsdDrive> GetEnumerator()
        {
            return this.Devices.GetEnumerator();
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.Devices.GetEnumerator();
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

            SsdDrives other = obj as SsdDrives;
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
                this.hashCode = this.Devices.Select(d => d.GetHashCode())
                    .Aggregate((result, current) => result += current);
            }

            return this.hashCode.Value;
        }
    }
}
