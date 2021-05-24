namespace Juno.Execution.AgentRuntime.Contract
{
    using Newtonsoft.Json;

    /// <summary>
    /// Encapsulate data from the FPGA-TEMP section of the FPGA diagnostics output.
    /// </summary>
    public class FpgaTemperature
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FpgaTemperature"/> class.
        /// </summary>
        /// <param name="isStatusOK"></param>
        /// <param name="isTemperatureWarningPresent"></param>
        /// <param name="temperature"></param>
        [JsonConstructor]
        public FpgaTemperature(bool isStatusOK, bool isTemperatureWarningPresent, string temperature)
        {
            this.IsStatusOK = isStatusOK;
            this.IsTemperatureWarningPresent = isTemperatureWarningPresent;
            this.Temperature = temperature;
        }

        /// <summary>
        /// Status of FPGA temperature.
        /// </summary>
        [JsonProperty(PropertyName = "isStatusOK", Required = Required.Always)]
        public bool IsStatusOK { get; }

        /// <summary>
        /// Indicates if a temperature warning is present on the FPGA module.
        /// </summary>
        [JsonProperty(PropertyName = "isTemperatureWarningPresent", Required = Required.Always)]
        public bool IsTemperatureWarningPresent { get; }

        /// <summary>
        /// Current temperature of the FPGA module.
        /// </summary>
        [JsonProperty(PropertyName = "temperature", Required = Required.Always)]
        public string Temperature { get; }
    }
}
