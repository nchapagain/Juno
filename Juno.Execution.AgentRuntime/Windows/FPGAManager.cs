namespace Juno.Execution.AgentRuntime.Windows
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Cosmos.Table;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.Management.ContainerRegistry.Fluent;
    using Microsoft.Azure.Management.Network.Fluent.ApplicationGatewayProbe.Update;
    using Polly;

    /// <summary>
    /// Interacts with the FPGA management tools on the windows host
    /// </summary>
    public class FPGAManager : IFPGAManager
    {
        private readonly string appFilePath = "C:\\app";
        private readonly string reconfigOutput = "reconfig.log";
        private readonly string reconfigSuccessMsg = "Command reconfig-golden succeeded!";
        private readonly string reconfigGoldenCmd = "/reconfig-golden";
        private readonly string flashGoldenCmd = "-writeflashgolden";
        private readonly string flashOutput = "goldenFlash.log";
        private readonly string flashSuccessMsg = "Exiting WriteFlashSlot FPGA_STATUS 0x0.";
        private readonly string fpgaDiagnostics = "FPGADiagnostics.exe";
        private readonly string fpgaMgmt = "FPGAMgmt.exe";
        private readonly string errorLog = "2>&1";
        private readonly string errorMsg1 = "status 73";
        private readonly string errorMsg2 = "status 32";
        private readonly int retryCount = 4;

        /// <inheritdoc/>
        public FPGAManagerResult FlashFPGA(IProcessExecution processExecution, string imageName)
        {
            processExecution.ThrowIfNull(nameof(processExecution));
            imageName.ThrowIfNullOrEmpty(nameof(imageName));

            var retry =
                Policy.HandleResult<string>(exception => exception.Contains(this.errorMsg1, StringComparison.OrdinalIgnoreCase) | exception.Contains(this.errorMsg2, StringComparison.OrdinalIgnoreCase))
                .WaitAndRetry(retryCount: this.retryCount, (retries) =>
                {
                    return TimeSpan.FromSeconds(Math.Pow(2, retries));
                });
            string result = retry.Execute(() => this.FPGAFlash(processExecution, imageName));
            var succeeded = result.Contains(this.flashSuccessMsg, StringComparison.OrdinalIgnoreCase);
            if (succeeded)
            {
                return new FPGAManagerResult
                {
                    Succeeded = succeeded,
                    ExecutionResult = result
                };
            }
            else
            {
                throw new Exception($"Cannot flash the FPGA Error:{result}");
            }
        }

        /// <inheritdoc/>
        public FPGAManagerResult ReconfigFPGA(IProcessExecution processExecution)
        {
            processExecution.ThrowIfNull(nameof(processExecution));

            var retry =
                Policy.HandleResult<string>(exception => exception.Contains(this.errorMsg1, StringComparison.OrdinalIgnoreCase) | exception.Contains(this.errorMsg2, StringComparison.OrdinalIgnoreCase))
                .WaitAndRetry(retryCount: this.retryCount, (retries) =>
                {
                    return TimeSpan.FromSeconds(Math.Pow(2, retries));
                });
            string result = retry.Execute(() => this.FPGAReconfig(processExecution));
            var succeeded = result.Contains(this.reconfigSuccessMsg, StringComparison.OrdinalIgnoreCase);
            if (succeeded)
            {
                return new FPGAManagerResult
                {
                    Succeeded = succeeded,
                    ExecutionResult = result
                };
            }
            else
            {
                throw new Exception($"Cannot reconfigure the FPGA Error:{result}");
            }
        }

        private string FPGAFlash(IProcessExecution processExecution, string imageName)
        {
            var fpgaDiagPaths = Directory.GetFiles(this.appFilePath, $"*{this.fpgaDiagnostics}", SearchOption.AllDirectories);

            if (fpgaDiagPaths.Length == 0)
            {
                throw new Exception("Unable to find path to the FPGADiagnostics tool for the tip node");
            }

            var tipFpgaDiag = fpgaDiagPaths.FirstOrDefault(s => s.Contains("TipNode_", StringComparison.OrdinalIgnoreCase));

            if (tipFpgaDiag == null)
            {
                throw new Exception("Unable to find path to the FPGAMgmt tool for the tip node");
            }

            var fpgaDir = Path.GetDirectoryName(tipFpgaDiag);
            var commandArg = $"/c \"{this.fpgaDiagnostics} {this.flashGoldenCmd} {imageName} > {this.flashOutput} {this.errorLog}\"";
            ProcessExecutionResult processResult = processExecution.ExecuteProcessAsync("cmd.exe", commandArg, null, fpgaDir, false).GetAwaiter().GetResult();
            var flashPath = Path.Combine(fpgaDir, this.flashOutput);
            var output = File.ReadAllText(flashPath);
            return output;
        }

        private string FPGAReconfig(IProcessExecution processExecution)
        {
            var fpgaMgmtPaths = Directory.GetFiles(this.appFilePath, $"*{this.fpgaMgmt}", SearchOption.AllDirectories);

            if (fpgaMgmtPaths.Length == 0)
            {
                throw new Exception($"Unable to find path to the FPGAMgmt tool for the tip node");
            }

            var tipFpgaMgmt = fpgaMgmtPaths.FirstOrDefault(s => s.Contains(".TipNode_", StringComparison.OrdinalIgnoreCase));

            if (tipFpgaMgmt == null)
            {
                throw new Exception($"Unable to find path to the FPGAMgmt tool for the tip node");
            }

            var fpgaDir = Path.GetDirectoryName(tipFpgaMgmt);
            var commandArg = $"/c \"{this.fpgaMgmt} {this.reconfigGoldenCmd} > {this.reconfigOutput} {this.errorLog}\"";
            ProcessExecutionResult processResult = processExecution.ExecuteProcessAsync("cmd.exe", commandArg, null, fpgaDir, false).GetAwaiter().GetResult();
            var reconfigPath = Path.Combine(fpgaDir, this.reconfigOutput);
            var output = File.ReadAllText(reconfigPath);
            return output;
        }
    }
}
