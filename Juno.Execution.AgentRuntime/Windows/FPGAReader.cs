namespace Juno.Execution.AgentRuntime.Windows
{
    using System;
    using System.IO.Abstractions;
    using System.Text.RegularExpressions;
    using Juno.Execution.AgentRuntime.Contract;
    using Microsoft.Azure.CRC;

    /// <summary>
    /// Matches the parameter names from the string with the help of Regex
    /// </summary>
    public class FpgaReader : IFirmwareReader<FpgaHealth>
    {
        private const string OK = "OK";
        private const string IsTrue = "1";
        private const string DumpHealthCmd = "-dumpHealth";
        private const string FpgaDiagnostics = "FPGADiagnostics.exe";
        private readonly IProcessExecution processExecution;
        private readonly IFileSystem fileSystem;

        /// <summary>
        /// Initializes a new instance of the <see cref="FpgaReader"/> class.
        /// </summary>
        /// <param name="processExecution"></param>
        /// <param name="fileSystem"></param>
        public FpgaReader(IProcessExecution processExecution = null, IFileSystem fileSystem = null)
        {
            this.processExecution = processExecution ?? new ProcessExecution();
            this.fileSystem = fileSystem ?? new FileSystem();
        }

        /// <inheritdoc/>
        public FpgaHealth Read()
        {
            string fpgaDir = this.fileSystem.Directory.GetParentDirectory(AgentRuntimeConstants.FpgaPayload, FpgaReader.FpgaDiagnostics);
            var commandArg = $"/c \"{FpgaReader.FpgaDiagnostics} {FpgaReader.DumpHealthCmd}\"";
            ProcessExecutionResult processResult = this.processExecution.ExecuteProcessAsync("cmd.exe", commandArg, null, fpgaDir, true).GetAwaiter().GetResult();
            processResult.ThrowIfErrored<ProcessExecutionException>($"Unable to execute: {FpgaReader.FpgaDiagnostics}. Process exited with Exit Code: {processResult.ExitCode}");
            return this.ParseHealthString(string.Concat(processResult.Output));
        }

        /// <inheritdoc/>
        public bool CanRead(Type t)
        {
            return t == typeof(FpgaHealth) && this.fileSystem.Directory.FileExists(AgentRuntimeConstants.FpgaPayload, FpgaReader.FpgaDiagnostics);
        }

        private static string GetMatchIfExists(Regex regex, string text, int groupIndex = 1)
        {
            Match match = regex.Match(text);
            if (match.Success && match.Groups.Count >= groupIndex + 1)
            {
                return match.Groups[groupIndex].Value;
            }

            return string.Empty;
        }

        private FpgaHealth ParseHealthString(string healthInfo)
        {
            return new FpgaHealth(
                new FpgaConfig(
                    isStatusOK: FpgaReader.GetMatchIfExists(FpgaRegex.FpgaConfigRegex, healthInfo, 1) == FpgaReader.OK,
                    boardName: FpgaReader.GetMatchIfExists(FpgaRegex.BoardRegex, healthInfo),
                    roleID: FpgaReader.GetMatchIfExists(FpgaRegex.RoleIdRegex, healthInfo),
                    roleVersion: FpgaReader.GetMatchIfExists(FpgaRegex.FpgaConfigRegex, healthInfo, 2),
                    shellID: FpgaReader.GetMatchIfExists(FpgaRegex.FpgaConfigRegex, healthInfo, 3),
                    shellVersion: FpgaReader.GetMatchIfExists(FpgaRegex.FpgaConfigRegex, healthInfo, 4),
                    isGolden: FpgaReader.GetMatchIfExists(FpgaRegex.IsGoldenRegex, healthInfo) == FpgaReader.IsTrue),
                new FpgaTemperature(
                    isStatusOK: FpgaReader.GetMatchIfExists(FpgaRegex.FpgaTemperatureRegex, healthInfo, 1) == FpgaReader.OK,
                    isTemperatureWarningPresent: FpgaReader.GetMatchIfExists(FpgaRegex.FpgaTemperatureRegex, healthInfo, 2) == FpgaReader.IsTrue,
                    temperature: FpgaReader.GetMatchIfExists(FpgaRegex.FpgaTemperatureRegex, healthInfo, 3)),
                new FpgaNetwork(
                    isStatusOK: FpgaReader.GetMatchIfExists(FpgaRegex.FpgaNetworkRegex, healthInfo, 1) == FpgaReader.OK,
                    softNetworkStatus: FpgaReader.GetMatchIfExists(FpgaRegex.FpgaNetworkRegex, healthInfo, 2),
                    torMacLanesDeskew: FpgaReader.GetMatchIfExists(FpgaRegex.FpgaNetworkRegex, healthInfo, 3),
                    torMacLanesStable: FpgaReader.GetMatchIfExists(FpgaRegex.FpgaNetworkRegex, healthInfo, 4),
                    torMacHardwareError: FpgaReader.GetMatchIfExists(FpgaRegex.FpgaNetworkRegex, healthInfo, 5),
                    torMacPcsHardwareError: FpgaReader.GetMatchIfExists(FpgaRegex.FpgaNetworkRegex, healthInfo, 6),
                    torMacLinkDrops: FpgaReader.GetMatchIfExists(FpgaRegex.FpgaNetworkRegex, healthInfo, 7),
                    torMacReceiveFcsErrors: FpgaReader.GetMatchIfExists(FpgaRegex.FpgaNetworkRegex, healthInfo, 8),
                    torMacReceiveCount: FpgaReader.GetMatchIfExists(FpgaRegex.FpgaNetworkRegex, healthInfo, 9),
                    torMacTransferCount: FpgaReader.GetMatchIfExists(FpgaRegex.FpgaNetworkRegex, healthInfo, 10),
                    nicMacLanesDeskew: FpgaReader.GetMatchIfExists(FpgaRegex.FpgaNetworkRegex, healthInfo, 11),
                    nicMacLanesStable: FpgaReader.GetMatchIfExists(FpgaRegex.FpgaNetworkRegex, healthInfo, 12),
                    nicMacHardwareError: FpgaReader.GetMatchIfExists(FpgaRegex.FpgaNetworkRegex, healthInfo, 13),
                    nicMacPcsHardwareError: FpgaReader.GetMatchIfExists(FpgaRegex.FpgaNetworkRegex, healthInfo, 14),
                    nicMacLinkDrops: FpgaReader.GetMatchIfExists(FpgaRegex.FpgaNetworkRegex, healthInfo, 15),
                    nicMacReceiveFcsErrors: FpgaReader.GetMatchIfExists(FpgaRegex.FpgaNetworkRegex, healthInfo, 16),
                    nicMacReceiveCount: FpgaReader.GetMatchIfExists(FpgaRegex.FpgaNetworkRegex, healthInfo, 17),
                    nicMacTransferCount: FpgaReader.GetMatchIfExists(FpgaRegex.FpgaNetworkRegex, healthInfo, 18)),
                new FpgaID(
                    isStatusOK: FpgaReader.GetMatchIfExists(FpgaRegex.FpgaIdRegex, healthInfo, 1) == FpgaReader.OK,
                    chipID: FpgaReader.GetMatchIfExists(FpgaRegex.FpgaIdRegex, healthInfo, 2)),
                new FpgaClockReset(
                    isStatusOK: FpgaReader.GetMatchIfExists(FpgaRegex.FpgaClockResetRegex, healthInfo, 1) == FpgaReader.OK),
                new FpgaPcie(
                    isStatusOK: FpgaReader.GetMatchIfExists(FpgaRegex.FpgaPcieRegex, healthInfo, 1) == FpgaReader.OK),
                new FpgaDram(
                    isStatusOK: FpgaReader.GetMatchIfExists(FpgaRegex.FpgaDramRegex, healthInfo, 1) == FpgaReader.OK),
                new FpgaCables(
                    isStatusOK: FpgaReader.GetMatchIfExists(FpgaRegex.FpgaCablesRegex, healthInfo, 1) == FpgaReader.OK));
        }

        private static class FpgaRegex
        {
            public static readonly Regex FpgaIdRegex = new Regex(@"\[FPGA-ID\s+\]\s(\w+)\s\[chipid:(\d+).*\]", RegexOptions.Compiled);
            public static readonly Regex FpgaConfigRegex = new Regex(@"\[FPGA-CONFIG\s+\]\s(\w+)\s\[.*role_ver:(\w+).*,shell_id:(\w+).*,shell_ver:(\w+).*\]", RegexOptions.Compiled);
            public static readonly Regex BoardRegex = new Regex(@"board:([A-z]+)", RegexOptions.Compiled);
            public static readonly Regex RoleIdRegex = new Regex(@"role_id:([\w]+x[\w]+)", RegexOptions.Compiled);
            public static readonly Regex IsGoldenRegex = new Regex(@"golden:([\d])", RegexOptions.Compiled);
            public static readonly Regex FpgaClockResetRegex = new Regex(@"\[FPGA-CLOCKRESET\]\s(\w+)\s.*", RegexOptions.Compiled);
            public static readonly Regex FpgaTemperatureRegex = new Regex(@"\[FPGA-TEMP\s+\]\s(\w+)\s\[temp_warn:(\d).*,temp_c:(\d+).*]", RegexOptions.Compiled);
            public static readonly Regex FpgaNetworkRegex = new Regex(@"\[FPGA-NETWORK\s+\]\s(\w+)\s\[Soft-Network-Status:(\w+)\]\s\[TOR-MAC,lanes_deskew:(\d+),lanes_stable:(\d+),mac_hw_err:(\w+),pcs_hw_err:(\w+),linkdrops:(\d+),rx_fcs_errs:(\d+),rx_count:(\d+),tx_count:(\d+)\]\s\[NIC-MAC,lanes_deskew:(\d+),lanes_stable:(\d+),mac_hw_err:(\w+),pcs_hw_err:(\w+),linkdrops:(\d+),rx_fcs_errs:(\d+),rx_count:(\d+),tx_count:(\d+)\]", RegexOptions.Compiled);
            public static readonly Regex FpgaPcieRegex = new Regex(@"\[FPGA-PCIE\s+\]\s(\w+)\s.*", RegexOptions.Compiled);
            public static readonly Regex FpgaDramRegex = new Regex(@"\[FPGA-DRAM\s+\]\s(\w+)\s.*", RegexOptions.Compiled);
            public static readonly Regex FpgaCablesRegex = new Regex(@"\[FPGA-CABLES\s+\]\s(\w+)\s.*", RegexOptions.Compiled);
        }
    }
}
