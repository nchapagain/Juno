namespace Juno.Execution.AgentRuntime.Contract
{
    using System;
    using System.Text;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;

    /// <summary>
    /// Info pertaining to a particular Device
    /// </summary>
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class SsdInfo : IEquatable<SsdInfo>
    {
        private int? hashCode;

        /// <summary>
        /// Initializes an instance of <see cref="SsdInfo"/>
        /// </summary>
        /// <param name="device">The device which the info describes.</param>
        /// <param name="smart_status">The status of the device.</param>
        /// <param name="model_name">The model name of the device.</param>
        /// <param name="firmware_version">The current firmware revision of the device.</param>
        /// <param name="serial_number">The serial number of the device.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Parameter names must match JSON property names.")]
        public SsdInfo(SsdDrive device, SsdHealth smart_status, string model_name, string firmware_version, string serial_number)
        {
            device.ThrowIfNull(nameof(device));
            smart_status.ThrowIfNull(nameof(smart_status));
            model_name.ThrowIfNull(nameof(model_name));
            firmware_version.ThrowIfNull(nameof(firmware_version));
            serial_number.ThrowIfNull(nameof(serial_number));

            this.Device = device;
            this.SmartHealth = smart_status;
            this.ModelName = model_name;
            this.FirmwareVersion = firmware_version;
            this.SerialNumber = serial_number;
        }

        /// <summary>
        /// The SSD Drive that this info describes
        /// </summary>
        [JsonProperty(PropertyName = "device", Required = Required.Always)]
        public SsdDrive Device { get; }

        /// <summary>
        /// The deisgnated smart health of the device.
        /// </summary>
        [JsonProperty(PropertyName = "smart_status")]
        public SsdHealth SmartHealth { get; }

        /// <summary>
        /// Name of the model of the Device
        /// </summary>
        [JsonProperty(PropertyName = "model_name", Required = Required.Always)]
        public string ModelName { get; }

        /// <summary>
        /// Firmware Version of the Device
        /// </summary>
        [JsonProperty(PropertyName = "firmware_version", Required = Required.Always)]
        public string FirmwareVersion { get; }

        /// <summary>
        /// Serial Number of the device.
        /// </summary>
        [JsonProperty(PropertyName = "serial_number", Required = Required.Always)]
        public string SerialNumber { get; }

        /// <summary>
        /// Evaluates the equality beteen two <see cref="SsdInfo"/> instances
        /// </summary>
        /// <param name="other">The other <see cref="SsdInfo"/> to evaluate equality against.</param>
        /// <returns>True/False if the two <see cref="SsdInfo"/> are equal.</returns>
        public bool Equals(SsdInfo other)
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

            SsdInfo other = obj as SsdInfo;
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
                this.hashCode += this.Device.GetHashCode();
            }

            return this.hashCode.Value;
        }
    }
}
