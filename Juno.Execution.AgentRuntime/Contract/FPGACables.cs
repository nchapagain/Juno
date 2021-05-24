namespace Juno.Execution.AgentRuntime.Contract
{
    using Newtonsoft.Json;

    /// <summary>
    /// Encapsulate data from the FPGA-CABLES section of the FPGA diagnostics output.
    /// </summary>
    public class FpgaCables
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FpgaCables"/> class.
        /// </summary>
        /// <param name="isStatusOK"></param>
        [JsonConstructor]
        public FpgaCables(bool isStatusOK)
        {
            this.IsStatusOK = isStatusOK;    
        }

        /// <summary>
        /// Status of FPGA cables.
        /// </summary>
        [JsonProperty(PropertyName = "isStatusOK", Required = Required.Always)]
        public bool IsStatusOK { get; }
    }
}
