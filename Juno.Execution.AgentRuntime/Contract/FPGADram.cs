namespace Juno.Execution.AgentRuntime.Contract
{
    using Newtonsoft.Json;

    /// <summary>
    /// Encapsulate data from the FPGA-DRAM section of the FPGA diagnostics output.
    /// </summary>
    public class FpgaDram
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FpgaDram"/> class.
        /// </summary>
        /// <param name="isStatusOK"></param>
        [JsonConstructor]
        public FpgaDram(bool isStatusOK)
        {
            this.IsStatusOK = isStatusOK;
        }

        /// <summary>
        /// Status of FPGA DRAM.
        /// </summary>
        [JsonProperty(PropertyName = "isStatusOK", Required = Required.Always)]
        public bool IsStatusOK { get; }
    }
}
