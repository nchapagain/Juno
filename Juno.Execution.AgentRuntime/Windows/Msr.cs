namespace Juno.Execution.AgentRuntime.Windows
{
    using System;
    using System.IO;
    using Juno.Execution.AgentRuntime.Contract;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;

    /// <summary>
    /// Provides an interface to read and write to MSRs.
    /// </summary>
    public class Msr : IMsr
    {
        private const string InstallCommand = "dp_add";
        private const string ReadMsr = "readmsr";
        private const string WriteMsr = "writemsr";
        private const string ProcessorIndex = "p";
        private const string RegisterId = "r";
        private const string Value = "v";
        private const string WinHvFlag = "h";
        private readonly IProcessExecution processExecution;
        private string driExe;

        /// <summary>
        /// Initializes an instance of the <see cref="Msr"/> class.
        /// </summary>
        /// <param name="processExecution"></param>
        public Msr(IProcessExecution processExecution = null)
        {
            this.processExecution = processExecution ?? new ProcessExecution();
            this.InstallDriDriver();
        }

        /// <inheritdoc/>
        public string Read(string registerId, string processorIndex)
        {
            registerId.ThrowIfNullOrWhiteSpace(nameof(registerId));
            processorIndex.ThrowIfNullOrWhiteSpace(nameof(processorIndex));

            string readMsrCommand = string.Join(" ", Msr.ReadMsr, Msr.ProcessorIndex, processorIndex, Msr.RegisterId, registerId, Msr.WinHvFlag, true.ToString());
            ProcessExecutionResult result = this.processExecution.ExecuteProcessAsync(this.driExe, readMsrCommand)
                .GetAwaiter().GetResult();
            string output = result.Output?.Count > 0 ? result.Output[0] : string.Empty;
            result.ThrowIfErrored<ProcessExecutionException>($"Reading MSR ({registerId}) for processor {processorIndex} with the DRI tool failed with exit code: {result.ExitCode} Output: {output}");

            var readmsrOutput = JsonConvert.DeserializeObject<ReadMsrOutput>(output);

            return readmsrOutput.Value;
        }

        /// <inheritdoc/>
        public void Write(string registerId, string processorIndex, string value)
        {
            registerId.ThrowIfNullOrWhiteSpace(nameof(registerId));
            processorIndex.ThrowIfNullOrWhiteSpace(nameof(processorIndex));
            value.ThrowIfNullOrWhiteSpace(nameof(value));

            string writeMsrCommand = string.Join(" ", Msr.WriteMsr, Msr.ProcessorIndex, processorIndex, Msr.RegisterId, registerId, Msr.Value, value, Msr.WinHvFlag, true.ToString());
            ProcessExecutionResult result = this.processExecution.ExecuteProcessAsync(this.driExe, writeMsrCommand)
                .GetAwaiter().GetResult();
            string output = result.Output?.Count > 0 ? result.Output[0] : string.Empty;
            result.ThrowIfErrored<ProcessExecutionException>($"Writing {value} to MSR ({registerId}) for processor {processorIndex} with the DRI tool failed with exit code: {result.ExitCode} Output: {output}");
        }

        /// <summary>
        /// Uses devcon.exe on the node to install the DRI driver.
        /// </summary>
        private void InstallDriDriver()
        {
            string devconExe = Path.Combine(AgentRuntimeConstants.OtpPackagesPath, AgentRuntimeConstants.DevconExe);
            string driInf = Path.Combine(Environment.CurrentDirectory, AgentRuntimeConstants.DriFolder, AgentRuntimeConstants.DriInf);
            this.driExe = Path.Combine(Environment.CurrentDirectory, AgentRuntimeConstants.DriFolder, AgentRuntimeConstants.DriExe);
            string installDriDriverCommand = string.Join(" ", Msr.InstallCommand, driInf);

            ProcessExecutionResult result = this.processExecution.ExecuteProcessAsync(devconExe, installDriDriverCommand)
                .GetAwaiter().GetResult();
            string output = result.Output?.Count > 0 ? result.Output[0] : string.Empty;
            result.ThrowIfErrored<SystemException>($"DRI driver installation failed with exit code: {result.ExitCode} Output: {output}");
        }

        internal class ReadMsrOutput
        {
            [JsonProperty]
            public string Value { get; set; }
        }
    }
}
