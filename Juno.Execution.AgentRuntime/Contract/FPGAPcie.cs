namespace Juno.Execution.AgentRuntime.Contract
{
    using Newtonsoft.Json;

    /// <summary>
    /// Encapsulate data from the FPGA-PCIE section of the FPGA diagnostics output.
    /// </summary>
    public class FpgaPcie
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FpgaPcie"/> class.
        /// </summary>
        /// <param name="isStatusOK"></param>
        [JsonConstructor]
        public FpgaPcie(bool isStatusOK)
        {
            this.IsStatusOK = isStatusOK;
        }

        /// <summary>
        /// Status of FPGA PCIE.
        /// </summary>
        /// [JsonProperty(PropertyName = "isStatusOK", Required = Required.Always)]
        public bool IsStatusOK { get; }
    }
}
