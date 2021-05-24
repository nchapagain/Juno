namespace Juno.Execution.AgentRuntime.Contract
{
    using System;
    using System.Text;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;

    /// <summary>
    /// Info for SSDs returned from WMIC
    /// </summary>
    public class SsdWmicInfo : IEquatable<SsdWmicInfo>
    {
        private int? hashCode;

        /// <summary>
        /// Initializes an instance of <see cref="SsdWmicInfo"/>
        /// </summary>
        /// <param name="firmwareVersion">The firmware revision</param>
        /// <param name="modelName">The model name</param>
        /// <param name="serialNumber">The serial number</param>
        public SsdWmicInfo(string firmwareVersion, string modelName, string serialNumber)
        {
            firmwareVersion.ThrowIfNull(nameof(firmwareVersion));
            modelName.ThrowIfNull(nameof(modelName));
            serialNumber.ThrowIfNull(nameof(serialNumber));

            this.FirmwareVersion = firmwareVersion;
            this.ModelName = modelName;
            this.SerialNumber = serialNumber;
        }

        /// <summary>
        /// Get the firmware version of the ssd.
        /// </summary>
        [JsonProperty(PropertyName = "firmwareVersion")]
        public string FirmwareVersion { get; }

        /// <summary>
        /// Get the serial number of the ssd
        /// </summary>
        [JsonProperty(PropertyName = "serialNumber")]
        public string SerialNumber { get; }

        /// <summary>
        /// Get the name of the model.
        /// </summary>
        [JsonProperty(PropertyName = "modelName")]
        public string ModelName { get; }

        /// <summary>
        /// Evaluates the equality beteen two <see cref="SsdWmicInfo"/> instances
        /// </summary>
        /// <param name="other">The other <see cref="SsdWmicInfo"/> to evaluate equality against.</param>
        /// <returns>True/False if the two <see cref="SsdWmicInfo"/> are equal.</returns>
        public bool Equals(SsdWmicInfo other)
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

            SsdWmicInfo other = obj as SsdWmicInfo;
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
                    .Append(this.ModelName)
                    .Append(this.SerialNumber)
                    .Append(this.FirmwareVersion)
                    .ToString().ToUpperInvariant().GetHashCode(StringComparison.Ordinal);
            }

            return this.hashCode.Value;
        }
    }
}
