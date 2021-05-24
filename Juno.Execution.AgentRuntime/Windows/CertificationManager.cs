namespace Juno.Execution.AgentRuntime.Windows
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Extensions;

    /// <summary>
    /// Providers interaction with the crc blade certification agent
    /// </summary>
    public class CertificationManager : ICertificationManager
    {
        private readonly string appFilePath = "C:\\app";
        private readonly string canDeleteFile = "canDelete.csv";
        private readonly string cannotDeleteFile = "cannotDelete.csv";       
        private readonly string certOutput = "certificationOutput.log";
        private readonly string certificationAgent = "Juno.CRCBladeCertificationAgent.exe";

        /// <inheritdoc/>        
        public bool Certify(IProcessExecution processExecution, out string message)
        {
            processExecution.ThrowIfNull(nameof(processExecution));
            var certAgentPaths = Directory.GetFiles(this.appFilePath, $"*{this.certificationAgent}", SearchOption.AllDirectories);

            if (certAgentPaths.Length == 0)
            {
                throw new Exception("Unable to find path to the FPGAMgmt tool for the tip node");
            }

            var tipCertAgentPath = certAgentPaths.FirstOrDefault(s => s.Contains("TipNode_", StringComparison.OrdinalIgnoreCase));

            if (tipCertAgentPath == null)
            {
                throw new Exception("Unable to find path to the FPGAMgmt tool for the tip node");
            }

            var certAgentDir = Path.GetDirectoryName(tipCertAgentPath);
            var commandArg = $"/c \"{this.certificationAgent} -e juno-dev01 > {this.certOutput}\"";
            ProcessExecutionResult processResult = processExecution.ExecuteProcessAsync("cmd.exe", commandArg, null, certAgentDir, false).GetAwaiter().GetResult();

            // Run the external process & wait for it to finish
            if (processResult.ExitCode == 0)
            {
                var canDeletePath = Path.Combine(certAgentDir, this.canDeleteFile);
                var cannotDeletePath = Path.Combine(certAgentDir, this.cannotDeleteFile);
                if (File.Exists(canDeletePath))
                {
                    var output = File.ReadAllText(canDeletePath);
                    message = output;
                    return true;
                }
                else if (File.Exists(cannotDeletePath))
                {
                    var output = File.ReadAllText(cannotDeletePath);
                    message = output;
                    return false;
                }
                else
                {
                    var certOutput = Path.Combine(certAgentDir, this.certOutput);
                    var certOutputTxt = File.ReadAllText(certOutput);
                    var output = $"Execution failed with exit code {processResult.ExitCode}.{certOutputTxt}";
                    message = output;
                    return false;
                }
            }
            else
            {
                var certOutput = Path.Combine(certAgentDir, this.certOutput);
                var certOutputTxt = File.ReadAllText(certOutput);
                throw new Exception($"Cannot certify the node:{processResult.ExitCode}, Error:{certOutputTxt}");
            }

        }
    }
}
