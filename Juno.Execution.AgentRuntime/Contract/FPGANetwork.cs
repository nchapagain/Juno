namespace Juno.Execution.AgentRuntime.Contract
{
    using Newtonsoft.Json;

    /// <summary>
    /// Encapsulate data from the FPGA-NETWORK section of the FPGA diagnostics output.
    /// </summary>
    public class FpgaNetwork
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FpgaNetwork"/> class.
        /// </summary>
        /// <param name="isStatusOK"></param>
        /// <param name="softNetworkStatus"></param>
        /// <param name="torMacLanesDeskew"></param>
        /// <param name="torMacLanesStable"></param>
        /// <param name="torMacHardwareError"></param>
        /// <param name="torMacPcsHardwareError"></param>
        /// <param name="torMacLinkDrops"></param>
        /// <param name="torMacReceiveFcsErrors"></param>
        /// <param name="torMacReceiveCount"></param>
        /// <param name="torMacTransferCount"></param>
        /// <param name="nicMacLanesDeskew"></param>
        /// <param name="nicMacLanesStable"></param>
        /// <param name="nicMacHardwareError"></param>
        /// <param name="nicMacPcsHardwareError"></param>
        /// <param name="nicMacLinkDrops"></param>
        /// <param name="nicMacReceiveFcsErrors"></param>
        /// <param name="nicMacReceiveCount"></param>
        /// <param name="nicMacTransferCount"></param>
        [JsonConstructor]
        public FpgaNetwork(
            bool isStatusOK,
            string softNetworkStatus,
            string torMacLanesDeskew,
            string torMacLanesStable,
            string torMacHardwareError,
            string torMacPcsHardwareError,
            string torMacLinkDrops,
            string torMacReceiveFcsErrors,
            string torMacReceiveCount,
            string torMacTransferCount,
            string nicMacLanesDeskew,
            string nicMacLanesStable,
            string nicMacHardwareError,
            string nicMacPcsHardwareError,
            string nicMacLinkDrops,
            string nicMacReceiveFcsErrors,
            string nicMacReceiveCount,
            string nicMacTransferCount)
        {
            this.IsStatusOK = isStatusOK;
            this.SoftNetworkStatus = softNetworkStatus;
            this.TorMacLanesDeskew = torMacLanesDeskew;
            this.TorMacLanesStable = torMacLanesStable;
            this.TorMacHardwareError = torMacHardwareError;
            this.TorMacPcsHardwareError = torMacPcsHardwareError;
            this.TorMacLinkDrops = torMacLinkDrops;
            this.TorMacReceiveFcsErrors = torMacReceiveFcsErrors;
            this.TorMacReceiveCount = torMacReceiveCount;
            this.TorMacTransferCount = torMacTransferCount;
            this.NicMacLanesDeskew = nicMacLanesDeskew;
            this.NicMacLanesStable = nicMacLanesStable;
            this.NicMacHardwareError = nicMacHardwareError;
            this.NicMacPcsHardwareError = nicMacPcsHardwareError;
            this.NicMacLinkDrops = nicMacLinkDrops;
            this.NicMacReceiveFcsErrors = nicMacReceiveFcsErrors;
            this.NicMacReceiveCount = nicMacReceiveCount;
            this.NicMacTransferCount = nicMacTransferCount;
        }

        /// <summary>
        /// Status of FPGA network.
        /// </summary>
        [JsonProperty(PropertyName = "isStatusOK", Required = Required.Always)]
        public bool IsStatusOK { get; }

        /// <summary>
        /// Status of FPGA soft network.
        /// </summary>
        [JsonProperty(PropertyName = "softNetworkStatus", Required = Required.Always)]
        public string SoftNetworkStatus { get; }

        /// <summary>
        /// TOR lanes deskew.
        /// </summary>
        [JsonProperty(PropertyName = "torMacLanesDeskew", Required = Required.Always)]
        public string TorMacLanesDeskew { get; }

        /// <summary>
        /// TOR lanes stable.
        /// </summary>
        [JsonProperty(PropertyName = "torMacLanesStable", Required = Required.Always)]
        public string TorMacLanesStable { get; }

        /// <summary>
        /// TOR hardware error.
        /// </summary>
        [JsonProperty(PropertyName = "torMacHardwareError", Required = Required.Always)]
        public string TorMacHardwareError { get; }

        /// <summary>
        /// TOR PCS hardware error.
        /// </summary>
        [JsonProperty(PropertyName = "torMacPcsHardwareError", Required = Required.Always)]
        public string TorMacPcsHardwareError { get; }

        /// <summary>
        /// TOR link drops.
        /// </summary>
        [JsonProperty(PropertyName = "torMacLinkDrops", Required = Required.Always)]
        public string TorMacLinkDrops { get; }

        /// <summary>
        /// TOR receive FCS errors.
        /// </summary>
        [JsonProperty(PropertyName = "torMacReceiveFcsErrors", Required = Required.Always)]
        public string TorMacReceiveFcsErrors { get; }

        /// <summary>
        /// TOR receive count.
        /// </summary>
        [JsonProperty(PropertyName = "torMacReceiveCount", Required = Required.Always)]
        public string TorMacReceiveCount { get; }

        /// <summary>
        /// TOR transfer count.
        /// </summary>
        [JsonProperty(PropertyName = "torMacTransferCount", Required = Required.Always)]
        public string TorMacTransferCount { get; }

        /// <summary>
        /// NIC lanes deskew.
        /// </summary>
        [JsonProperty(PropertyName = "nicMacLanesDeskew", Required = Required.Always)]
        public string NicMacLanesDeskew { get; }

        /// <summary>
        /// NIC lanes stable.
        /// </summary>
        [JsonProperty(PropertyName = "nicMacLanesStable", Required = Required.Always)]
        public string NicMacLanesStable { get; }

        /// <summary>
        /// NIC hardware error.
        /// </summary>
        [JsonProperty(PropertyName = "nicMacHardwareError", Required = Required.Always)]
        public string NicMacHardwareError { get; }

        /// <summary>
        /// NIC PCS hardware error.
        /// </summary>
        [JsonProperty(PropertyName = "nicMacPcsHardwareError", Required = Required.Always)]
        public string NicMacPcsHardwareError { get; }

        /// <summary>
        /// NIC link drops.
        /// </summary>
        [JsonProperty(PropertyName = "nicMacLinkDrops", Required = Required.Always)]
        public string NicMacLinkDrops { get; }

        /// <summary>
        /// NIC receive FCS errors.
        /// </summary>
        [JsonProperty(PropertyName = "nicMacReceiveFcsErrors", Required = Required.Always)]
        public string NicMacReceiveFcsErrors { get; }

        /// <summary>
        /// NIC receive count.
        /// </summary>
        [JsonProperty(PropertyName = "nicMacReceiveCount", Required = Required.Always)]
        public string NicMacReceiveCount { get; }

        /// <summary>
        /// NIC transfer count.
        /// </summary>
        [JsonProperty(PropertyName = "nicMacTransferCount", Required = Required.Always)]
        public string NicMacTransferCount { get; }
    }
}
