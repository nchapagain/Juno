namespace Juno.Execution.AgentRuntime.Windows
{
    using System;
    using System.Collections.Generic;
    using System.IO.Abstractions;
    using System.Linq;
    using Juno.Execution.AgentRuntime;
    using Juno.Execution.AgentRuntime.Contract;
    using Microsoft.Azure.CRC;
    using Newtonsoft.Json;

    /// <summary>
    /// Implementation of <see cref="IFirmwareReader{T}"/>
    /// </summary>
    public class SsdReader : IFirmwareReader<IEnumerable<SsdInfo>>
    {
        private static readonly Dictionary<string, Func<string, SsdInfo>> JsonConverters = new Dictionary<string, Func<string, SsdInfo>>(StringComparer.OrdinalIgnoreCase)
        {
            [DeviceTypes.Ata] = (value) => JsonConvert.DeserializeObject<SataInfo>(value),
            [DeviceTypes.Nvme] = (value) => JsonConvert.DeserializeObject<NvmeInfo>(value)
        };

        private readonly string smartctlBinary = "smartctl.exe";
        private readonly string listFlag = "--scan";
        private readonly string getInfoFlag = "-a";
        private readonly string jsonFlag = "-j";
        private readonly IFileSystem fileSystem;
        private readonly IProcessExecution processExecution;

        /// <summary>
        /// Initializes a new instance of the <see cref="SsdReader"/> class.
        /// </summary>
        /// <param name="fileSystem">Interface for interacting with the file system</param>
        /// <param name="processExecution">Interface for executing processes</param>
        public SsdReader(IFileSystem fileSystem = null, IProcessExecution processExecution = null)
        {
            this.fileSystem = fileSystem ?? new FileSystem();
            this.processExecution = processExecution ?? new ProcessExecution();
        }

        /// <inheritdoc/>
        public bool CanRead(Type t)
        {
            return t == typeof(IEnumerable<SsdInfo>) && this.fileSystem.Directory.FileExists(AgentRuntimeConstants.SsdPayload, this.smartctlBinary);
        }

        /// <inheritdoc/>
        public IEnumerable<SsdInfo> Read()
        {
            string binaryFullPath = this.fileSystem.Directory.GetFile(AgentRuntimeConstants.SsdPayload, this.smartctlBinary);

            // 2. List all drives on machine
            string arguments = string.Join(" ", this.listFlag, this.jsonFlag);
            ProcessExecutionResult listResult = this.processExecution.ExecuteProcessAsync(binaryFullPath, arguments).GetAwaiter().GetResult();
            listResult.ThrowIfErrored<ProcessExecutionException>($"{nameof(listResult)} returned non-zero exit code: {listResult.ExitCode} " +
                    $"with errors: {string.Join(", ", listResult.Error)}");

            SsdDrives drives = JsonConvert.DeserializeObject<SsdDrives>(string.Concat(listResult.Output));

            // 3. Get Firmware version of all drives on machine
            List<string> errors = new List<string>();
            List<SsdInfo> driveInfo = new List<SsdInfo>();
            foreach (SsdDrive drive in drives)
            {
                ProcessExecutionResult result = this.processExecution.ExecuteProcessAsync(binaryFullPath, string.Join(" ", this.getInfoFlag, this.jsonFlag, drive.Name)).GetAwaiter().GetResult();
                if (result.IsErrored())
                {
                    errors.Add($"{nameof(listResult)} returned non-zero exit code: {listResult.ExitCode} " +
                        $"with errors: {string.Join(", ", listResult.Error)}");
                    continue;
                }

                try
                {
                    driveInfo.Add(SsdReader.JsonConverters[drive.Type].Invoke(string.Concat(result.Output)));
                }
                catch (JsonReaderException exc)
                {
                    errors.Add(exc.Message);
                }
            }

            if (errors.Any())
            {
                throw new AggregateException($"Errors occurred deserializing response from {binaryFullPath} with flag {this.getInfoFlag}.\nErrors:{string.Join(", ", errors)}");
            }

            return driveInfo;
        }

        private static class DeviceTypes
        {
            public const string Ata = nameof(DeviceTypes.Ata);
            public const string Nvme = nameof(DeviceTypes.Nvme);
        }
    }
}
