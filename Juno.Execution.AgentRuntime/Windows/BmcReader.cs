namespace Juno.Execution.AgentRuntime.Windows
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using Juno.Execution.AgentRuntime.Contract;
    using Microsoft.Azure.CRC;

    /// <inheritdoc/>
    public class BmcReader : IFirmwareReader<BmcInfo>
    {
        private readonly IProcessExecution processExecution;

        /// <summary>
        /// Initailize a new instance of <see cref="BmcReader"/>
        /// </summary>
        public BmcReader(IProcessExecution processExecution = null)
        {
            this.processExecution = processExecution ?? new ProcessExecution();
        }

        /// <summary>
        /// Any IPMI util reader can read properties out of the box.
        /// </summary>
        /// <returns>True/False if can read property.</returns>
        public bool CanRead(Type t) => t == typeof(BmcInfo);

        /// <inheritdoc/>
        public BmcInfo Read()
        {
            string result = string.Empty;
            string ipmiArgs = "health ";
            var commandArg = $"/c \"{AgentRuntimeConstants.IpmiUtilExeName} {ipmiArgs}\"";
            ProcessExecutionResult processResult = this.processExecution.ExecuteProcessAsync("cmd.exe", commandArg, null, AgentRuntimeConstants.IpmiUtilExePath).GetAwaiter().GetResult();
            // This regex does a match and then does another match with capture group. 
            // there should only be one match with capture group.
            string bmcStdString = string.Join(",", processResult.Output);
            string bmcVersionPattern = @"BMC version ([\s]+=[\s]+[\d.]+)";
            Match bmcVersionMatch = Regex.Match(bmcStdString, bmcVersionPattern);
            if (bmcVersionMatch.Success)
            {
                string bmcMatchString = bmcVersionMatch.Groups[1].Value.Trim();
                Match bmcVersion = Regex.Match(bmcMatchString, @"=[\s]+([\d.]+)");
                result = bmcVersion.Groups[1].Value.Trim();
            }

            return new BmcInfo(result);
        }
    }
}
