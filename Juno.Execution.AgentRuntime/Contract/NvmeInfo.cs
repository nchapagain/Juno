namespace Juno.Execution.AgentRuntime.Contract
{
    using System;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;

    /// <summary>
    /// SSD device info specific to an NVME device.
    /// </summary>
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class NvmeInfo : SsdInfo, IEquatable<NvmeInfo>
    {
        private int? hashCode;

        /// <summary>
        /// Initializes a new instance of <see cref="NvmeInfo"/>
        /// </summary>
        /// <param name="device">The ssd device.</param>
        /// <param name="smart_status">The smart status of the device.</param>
        /// <param name="nvme_smart_health_information_log">The nvme specific health information</param>
        /// <param name="model_name">The model name.</param>
        /// <param name="firmware_version">The firmware version</param>
        /// <param name="serial_number">The serial number.</param>
        [JsonConstructor]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Parameter names must match JSON property names.")]
        public NvmeInfo(SsdDrive device, SsdHealth smart_status, string model_name, string firmware_version, string serial_number, NvmeHealth nvme_smart_health_information_log)
            : base(device, smart_status, model_name, firmware_version, serial_number)
        {
            nvme_smart_health_information_log.ThrowIfNull(nameof(nvme_smart_health_information_log));
            this.NvmeHealth = nvme_smart_health_information_log;
        }

        /// <summary>
        /// Get the device info's nvme health.
        /// </summary>
        [JsonProperty(PropertyName = "nvme_smart_health_information_log", Required = Required.Always)]
        public NvmeHealth NvmeHealth { get; }

        /// <summary>
        /// Evaluates the equality beteen two <see cref="NvmeInfo"/> instances
        /// </summary>
        /// <param name="other">The other <see cref="NvmeInfo"/> to evaluate equality against.</param>
        /// <returns>True/False if the two <see cref="NvmeInfo"/> are equal.</returns>
        public bool Equals(NvmeInfo other)
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

            NvmeInfo other = obj as NvmeInfo;
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
                this.hashCode = this.NvmeHealth.GetHashCode() + base.GetHashCode();
            }

            return this.hashCode.Value;
        }
    }
}
