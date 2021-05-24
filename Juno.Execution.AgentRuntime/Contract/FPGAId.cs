namespace Juno.Execution.AgentRuntime.Contract
{
    using Newtonsoft.Json;

    /// <summary>
    /// Encapsulate data from the FPGA-ID section of the FPGA diagnostics output.
    /// </summary>
    public class FpgaID
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FpgaID"/> class.
        /// </summary>
        /// <param name="isStatusOK"></param>
        /// <param name="chipID"></param>
        [JsonConstructor]
        public FpgaID(bool isStatusOK, string chipID)
        {
            this.IsStatusOK = isStatusOK;
            this.ChipID = chipID;
        }

        /// <summary>
        /// Status of FPGA id.
        /// </summary>
        [JsonProperty(PropertyName = "isStatusOK", Required = Required.Always)]
        public bool IsStatusOK { get; }

        /// <summary>
        /// FPGA chip identifier.
        /// </summary>
        [JsonProperty(PropertyName = "chipId", Required = Required.Always)]
        public string ChipID { get; }
    }
}
