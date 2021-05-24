namespace Juno.Execution.AgentRuntime.Contract
{
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;

    /// <summary>
    /// Encapsulate data from the FPGA-CONFIG section of the FPGA diagnostics output.
    /// </summary>
    public class FpgaConfig
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FpgaConfig"/> class.
        /// </summary>
        /// <param name="isStatusOK"></param>
        /// <param name="boardName"></param>
        /// <param name="roleID"></param>
        /// <param name="roleVersion"></param>
        /// <param name="shellID"></param>
        /// <param name="shellVersion"></param>
        /// <param name="isGolden"></param>
        [JsonConstructor]
        public FpgaConfig(
            bool isStatusOK, 
            string boardName, 
            string roleID,
            string roleVersion,
            string shellID,
            string shellVersion,
            bool isGolden)
        {
            boardName.ThrowIfNullOrWhiteSpace(nameof(boardName));
            roleID.ThrowIfNullOrWhiteSpace(nameof(roleID));

            this.IsStatusOK = isStatusOK;
            this.BoardName = boardName;
            this.RoleID = roleID;
            this.RoleVersion = roleVersion;
            this.ShellID = shellID;
            this.ShellVersion = shellVersion;
            this.IsGolden = isGolden;
        }

        /// <summary>
        /// Status of FPGA config.
        /// </summary>
        [JsonProperty(PropertyName = "isStatusOK", Required = Required.Always)]
        public bool IsStatusOK { get; }

        /// <summary>
        /// Name of the FPGA board.
        /// </summary>
        [JsonProperty(PropertyName = "boardName", Required = Required.Always)]
        public string BoardName { get; }

        /// <summary>
        /// FPGA role identifier.
        /// </summary>
        [JsonProperty(PropertyName = "roleID", Required = Required.Always)]
        public string RoleID { get; }

        /// <summary>
        /// Gives information about the Role Version in FPGA Configuration
        /// </summary>
        [JsonProperty(PropertyName = "roleVersion", Required = Required.Always)]
        public string RoleVersion { get; }

        /// <summary>
        /// Gives information about the Golden Image in FPGA Configuration
        /// </summary>
        [JsonProperty(PropertyName = "isGolden", Required = Required.Always)]
        public bool IsGolden { get; }

        /// <summary>
        /// Gives information about the Shell ID in FPGA Configuration
        /// </summary>
        [JsonProperty(PropertyName = "shellID", Required = Required.Always)]
        public string ShellID { get; }

        /// <summary>
        /// Gives information about the Shell Version in FPGA Configuration
        /// </summary>
        [JsonProperty(PropertyName = "shellVersion", Required = Required.Always)]
        public string ShellVersion { get; }
    }
}
