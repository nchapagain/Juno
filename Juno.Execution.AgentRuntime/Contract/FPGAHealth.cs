namespace Juno.Execution.AgentRuntime.Contract
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;

    /// <summary>
    /// Class to store FPGA Health Details
    /// </summary>
    public class FpgaHealth : IEquatable<FpgaHealth>
    {
        private int? hashCode;

        /// <summary>
        /// Initializes a new instance of the <see cref="FpgaHealth"/> class.
        /// </summary>
        /// <param name="fpgaConfig"></param>
        /// <param name="fpgaTemperature"></param>
        /// <param name="fpgaNetwork"></param>
        /// <param name="fpgaId"></param>
        /// <param name="fpgaClockReset"></param>
        /// <param name="fpgaPcie"></param>
        /// <param name="fpgaDram"></param>
        /// <param name="fpgaCables"></param>
        [JsonConstructor]
        public FpgaHealth(
            FpgaConfig fpgaConfig,
            FpgaTemperature fpgaTemperature,
            FpgaNetwork fpgaNetwork,
            FpgaID fpgaId,
            FpgaClockReset fpgaClockReset,
            FpgaPcie fpgaPcie,
            FpgaDram fpgaDram,
            FpgaCables fpgaCables)
        {
            fpgaConfig.ThrowIfNull(nameof(fpgaConfig));
            fpgaTemperature.ThrowIfNull(nameof(fpgaTemperature));
            fpgaNetwork.ThrowIfNull(nameof(fpgaNetwork));
            fpgaId.ThrowIfNull(nameof(fpgaId));
            fpgaClockReset.ThrowIfNull(nameof(fpgaClockReset));
            fpgaPcie.ThrowIfNull(nameof(fpgaPcie));
            fpgaDram.ThrowIfNull(nameof(fpgaDram));
            fpgaCables.ThrowIfNull(nameof(fpgaCables));

            this.FPGAConfig = fpgaConfig;
            this.FPGATemperature = fpgaTemperature;
            this.FPGANetwork = fpgaNetwork;
            this.FPGAID = fpgaId;
            this.FPGAClockReset = fpgaClockReset;
            this.FPGAPcie = fpgaPcie;
            this.FPGADram = fpgaDram;
            this.FPGACables = fpgaCables;
        }

        /// <summary>
        /// Copy Constructor for FPGA Health 
        /// </summary>
        /// <param name="other">
        /// FPGAHealth to copy into this instance.
        /// </param>
        public FpgaHealth(FpgaHealth other)
            : this(
                  other?.FPGAConfig,
                  other?.FPGATemperature,
                  other?.FPGANetwork,
                  other?.FPGAID,
                  other?.FPGAClockReset,
                  other?.FPGAPcie,
                  other?.FPGADram,
                  other?.FPGACables)
        {
        }

        /// <summary>
        /// Encapsulate data from the FPGA-CONFIG section of the FPGA diagnostics output.
        /// </summary>
        [JsonProperty(PropertyName = "fpgaConfig", Required = Required.Always)]
        public FpgaConfig FPGAConfig { get; }

        /// <summary>
        /// Encapsulate data from the FPGA-TEMP section of the FPGA diagnostics output.
        /// </summary>
        [JsonProperty(PropertyName = "fpgaTemperature", Required = Required.Always)]
        public FpgaTemperature FPGATemperature { get; }

        /// <summary>
        /// Encapsulate data from the FPGA-NETWORK section of the FPGA diagnostics output.
        /// </summary>
        [JsonProperty(PropertyName = "fpgaNetwork", Required = Required.Always)]
        public FpgaNetwork FPGANetwork { get; }

        /// <summary>
        /// Encapsulate data from the FPGA-ID section of the FPGA diagnostics output.
        /// </summary>
        [JsonProperty(PropertyName = "fpgaID", Required = Required.Always)]
        public FpgaID FPGAID { get; }

        /// <summary>
        /// Encapsulate data from the FPGA-CLOCKRESET section of the FPGA diagnostics output.
        /// </summary>
        [JsonProperty(PropertyName = "fpgaClockReset", Required = Required.Always)]
        public FpgaClockReset FPGAClockReset { get; }

        /// <summary>
        /// Encapsulate data from the FPGA-PCIE section of the FPGA diagnostics output.
        /// </summary>
        [JsonProperty(PropertyName = "fpgaPcie", Required = Required.Always)]
        public FpgaPcie FPGAPcie { get; }

        /// <summary>
        /// Encapsulate data from the FPGA-DRAM section of the FPGA diagnostics output.
        /// </summary>
        [JsonProperty(PropertyName = "fpgaDram", Required = Required.Always)]
        public FpgaDram FPGADram { get; }

        /// <summary>
        /// Encapsulate data from the FPGA-CABLES section of the FPGA diagnostics output.
        /// </summary>
        [JsonProperty(PropertyName = "fpgaCables", Required = Required.Always)]
        public FpgaCables FPGACables { get; }

        /// <summary>
        /// Determines if two objects are equal.
        /// </summary>
        /// <param name="lhs">The left hand side.</param>
        /// <param name="rhs">The right hand side.</param>
        /// <returns>True if the objects are equal. False otherwise.</returns>
        public static bool operator ==(FpgaHealth lhs, FpgaHealth rhs)
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
        public static bool operator !=(FpgaHealth lhs, FpgaHealth rhs)
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
                FpgaHealth itemDescription = obj as FpgaHealth;
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
        /// <param name="other">Defines the other object to compare against</param>
        /// <returns>True if the objects are equal, false otherwise</returns>
        public virtual bool Equals(FpgaHealth other)
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
                this.hashCode = new StringBuilder(this.FPGAConfig.BoardName)
                    .Append(this.FPGAConfig.RoleID)
                    .Append(this.FPGAConfig.IsGolden)
                    .ToString().GetHashCode(StringComparison.OrdinalIgnoreCase);
            }

            return this.hashCode.Value;
        }
       
    }
        
}
