namespace Juno.Execution.AgentRuntime.Contract
{
    using System;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;

    /// <summary>
    /// Device info specific to SATA type devices.
    /// </summary>
    public class SataInfo : SsdInfo, IEquatable<SataInfo>
    {
        private int? hashCode;

        /// <summary>
        /// Initializes an instance of <see cref="SataInfo"/>
        /// </summary>
        /// <param name="device">The device which the info describes.</param>
        /// <param name="smart_status">The status of the device.</param>
        /// <param name="ata_smart_attributes">SmartCTL generated attributes that descripe device health.</param>
        /// <param name="model_name">The model name of the device.</param>
        /// <param name="firmware_version">The current firmware revision of the device.</param>
        /// <param name="serial_number">The serial number of the device.</param>
        [JsonConstructor]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Parameter names must match JSON property names.")]
        public SataInfo(SsdDrive device, SsdHealth smart_status, string model_name, string firmware_version, string serial_number, SataSmartAttributes ata_smart_attributes)
            : base(device, smart_status, model_name, firmware_version, serial_number)
        {
            ata_smart_attributes.ThrowIfNull(nameof(ata_smart_attributes));
            this.SmartAttributes = ata_smart_attributes;
        }

        /// <summary>
        /// Get list of SATA smart attributes.
        /// </summary>
        [JsonProperty(PropertyName = "ata_smart_attributes", Required = Required.Always)]
        public SataSmartAttributes SmartAttributes { get; }

        /// <summary>
        /// Evaluates the equality beteen two <see cref="SataInfo"/> instances
        /// </summary>
        /// <param name="other">The other <see cref="SataInfo"/> to evaluate equality against.</param>
        /// <returns>True/False if the two <see cref="SataInfo"/> are equal.</returns>
        public bool Equals(SataInfo other)
        {
            return other != null && this.GetHashCode() == other.GetHashCode();
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            if (this.hashCode == null)
            {
                this.hashCode = this.SmartAttributes.GetHashCode() + base.GetHashCode();
            }

            return this.hashCode.Value;
        }
    }
}
