namespace Juno.Execution.AgentRuntime.Contract
{
    /// <summary>
    /// Constants used throughout the Agent Runtime assembly.
    /// </summary>
    public static class AgentRuntimeConstants
    {
        /// <summary>
        /// The path to ipmiutil.exe on node.
        /// </summary>
        public const string IpmiUtilExePath = @"C:\BladeFX\BladeFX\Tools\IpmiUtil\";
        
        /// <summary>
        /// The name of the ipmi util executable.
        /// </summary>
        public const string IpmiUtilExeName = @"ipmiutil.exe";

        /// <summary>
        /// The folder on Node that contains application data.
        /// </summary>
        public const string AppFolder = @"C:\App";

        /// <summary>
        /// Fpga payload name.
        /// </summary>
        public const string FpgaPayload = "PayloadFromSME";

        /// <summary>
        /// Ssd payload name.
        /// </summary>
        public const string SsdPayload = "JunoSsdPayload";

        /// <summary>
        /// CSI microcode payload.
        /// </summary>
        public const string CsiMicrocodePayload = "CSIMicrocodeUpdate";

        /// <summary>
        /// The path to OTP Packages on the node.
        /// </summary>
        public const string OtpPackagesPath = @"D:\Windows\otp_packages\common\";

        /// <summary>
        /// Name of the devcon executable.
        /// </summary>
        public const string DevconExe = @"devcon.exe";

        /// <summary>
        /// The folder that contains the DRI driver and executable.
        /// </summary>
        public const string DriFolder = @"DRI";

        /// <summary>
        /// Name of the DRI driver installation file.
        /// </summary>
        public const string DriInf = "dri.inf";

        /// <summary>
        /// Name of the DRI executable.
        /// </summary>
        public const string DriExe = "dri.exe";
    }
}
