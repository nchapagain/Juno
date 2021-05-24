namespace Juno.Execution.AgentRuntime.Windows
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Juno.Execution.AgentRuntime.Contract;
    using Microsoft.Azure.CRC;

    /// <summary>
    /// Implementation of <see cref="IFirmwareReader{TInfo}"/>/>
    /// </summary>
    public class SsdReaderWmic : IFirmwareReader<IEnumerable<SsdWmicInfo>>
    {
        private const string WmicCommand = "wmic";
        // {0}: attributes to get
        // {1}: Switches
        private const string WmicArguments = @"diskdrive GET {0} /VALUE";
        private static readonly string[] DiskDriveAttribtues = { "FirmwareRevision", "Model", "SerialNumber" };
        private readonly IProcessExecution processExecution;

        /// <summary>
        /// Initialize a new instance of <see cref="SsdReader"/>
        /// </summary>
        /// <param name="processExecution">Custom process executer.</param>
        public SsdReaderWmic(IProcessExecution processExecution = null)
        {
            this.processExecution = processExecution ?? new ProcessExecution();
        }

        /// <inheritdoc/>
        public bool CanRead(Type t) => t == typeof(IEnumerable<SsdInfo>);

        /// <inheritdoc/>
        public IEnumerable<SsdWmicInfo> Read()
        {
            string arguments = string.Format(SsdReaderWmic.WmicArguments, string.Join(", ", SsdReaderWmic.DiskDriveAttribtues));
            ProcessExecutionResult processResult = this.processExecution.ExecuteProcessAsync(SsdReaderWmic.WmicCommand, arguments).GetAwaiter().GetResult();
            processResult.ThrowIfErrored<ProcessExecutionException>("Unable to read SSD Settings from WMIC");

            List<SsdWmicInfo> ssdInfo = new List<SsdWmicInfo>();
            string[] output = processResult.Output.Where(s => !string.IsNullOrWhiteSpace(s.Trim())).ToArray();
            
            Func<string, int, string> transform = (line, pos) => line.Replace($"{SsdReaderWmic.DiskDriveAttribtues[pos]}=", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
            for (int i = 0; i < output.Length; i += SsdReaderWmic.DiskDriveAttribtues.Length)
            {
                ssdInfo.Add(new SsdWmicInfo(
                    transform.Invoke(output[i], 0),
                    transform.Invoke(output[i + 1], 1), 
                    transform.Invoke(output[i + 2], 2)));
            }

            return ssdInfo;
        }
    }
}