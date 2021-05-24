namespace Juno.Execution.AgentRuntime
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using Juno.Execution.AgentRuntime.Windows;
    using Microsoft.Azure.CRC;

    /// <inheritdoc/>
    public class BscReader : IBscReader
    {
        private const string HardwareSkuListFile = "HardwareList.hwlist";
        private const string BladeSkuCheckExe = "BladeSkuCheck.exe";

        /// <inheritdoc/>
        public string Read()
        {
            var fileName = Path.Combine(Path.GetTempPath(), BscReader.HardwareSkuListFile);
            var bscPath = Path.Combine(Directory.GetCurrentDirectory(), "BSC", BscReader.BladeSkuCheckExe);
            var processExecution = new ProcessExecution();
            ProcessExecutionResult processResult = processExecution.ExecuteProcessAsync(bscPath, string.Empty).GetAwaiter().GetResult();

            // Run the external process & wait for it to finish
            if (processResult.ExitCode == 0)
            {
                return File.ReadAllText(fileName);
            }
            else
            {
                throw new System.Exception($"Cannot run BSC exitCode:{processResult.ExitCode}, Error:{processResult.Error}");
            }

        }
    }
}
