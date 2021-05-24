namespace Juno.Execution.AgentRuntime.Windows
{
    using System;
    using System.Text.RegularExpressions;
    using Juno.Execution.AgentRuntime.Contract;
    using Microsoft.Azure.CRC;

    /// <summary>
    /// Properties that reperesnt different settings of BIOS
    /// </summary>
    public class BiosReader : IFirmwareReader<BiosInfo>
    {
        private IProcessExecution processExecution;
        private ISystemPropertyReader propertyReader;

        /// <summary>
        /// Initializes a new instance of the <see cref="BiosReader"/> class.
        /// </summary>
        /// <param name="processExecution">A class which can execute a process.</param>
        /// <param name="propertyReader">Property reader used for reading registry values.</param>
        public BiosReader(IProcessExecution processExecution = null, ISystemPropertyReader propertyReader = null)
        {
            this.processExecution = processExecution ?? new ProcessExecution();
            this.propertyReader = propertyReader ?? new WindowsPropertyReader();
        }

        /// <inheritdoc/>
        public bool CanRead(Type t) => t == typeof(BiosInfo);

        /// <inheritdoc/>
        public BiosInfo Read()
        {
            return new BiosInfo(
                this.propertyReader.Read(AzureHostProperty.BiosVersion),
                this.propertyReader.Read(AzureHostProperty.BiosVendor),
                this.ReadImeVersion());
        }

        private string ReadImeVersion()
        {
            string result = string.Empty;

            string spsCommand = "cmd -m062c00 00 20 18 01";

            var commandArg = $"/c \"{AgentRuntimeConstants.IpmiUtilExeName} {spsCommand}\"";
            ProcessExecutionResult processResult = this.processExecution.ExecuteProcessAsync("cmd.exe", commandArg, null, AgentRuntimeConstants.IpmiUtilExePath).GetAwaiter().GetResult();

            // Sample response of IPMI ME request command
            /* ipmiutil ver 2.95
                set MC at IPMB bus = 6 sa = 2c lun = 0
                icmd ver 2.95
                This is a test tool to compose IPMI commands.
                Do not use without knowledge of the IPMI specification.
                -- BMC version 3.13, IPMI version 2.0
                respData[len=15]: 50 01 03 13 02 21 57 01 00 05 0b 03 03 20 01,
                send_icmd ret = 0
                ipmiutil cmd, completed successfully"              
            */

            if (processResult.ExitCode == 0)
            {
                string output = string.Concat(processResult.Output);
                string pattern = @"respData\[len=15\]:.*";

                Match match = Regex.Match(output, pattern, RegexOptions.Compiled);

                if (match.Success)
                {
                    // expected result : 50 01 03 13 02 21 57 01 00 05 0b 03 03 20 01
                    // we can parse it or use as-is for blade certification
                    result = match.Value.Trim().Split(':')[1];
                }
            }

            return result;
        }
    }
}
