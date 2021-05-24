namespace Juno.Execution.AgentRuntime.Contract
{
    using Newtonsoft.Json;

    /// <summary>
    /// Encapsulate data from the FPGA-CLOCKRESET section of the FPGA diagnostics output.
    /// </summary>
    public class FpgaClockReset
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FpgaClockReset"/> class.
        /// </summary>
        /// <param name="isStatusOK"></param>
        [JsonConstructor]
        public FpgaClockReset(bool isStatusOK)
        {
            this.IsStatusOK = isStatusOK;
        }

        /// <summary>
        /// Status of FPGA clockreset.
        /// </summary>
        [JsonProperty(PropertyName = "isStatusOK", Required = Required.Always)]
        public bool IsStatusOK { get; }
    }
}
