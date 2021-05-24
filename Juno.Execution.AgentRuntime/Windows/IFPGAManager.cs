using Microsoft.Azure.CRC;

namespace Juno.Execution.AgentRuntime.Windows
{
    /// <summary>
    /// Interacts with the FPGA management utilities
    /// </summary>
    public interface IFPGAManager
    {
        /// <summary>
        /// Reconfigures the FPGA to golden
        /// </summary>
        /// <returns>Exit code from the reconfig command and its output</returns>
        FPGAManagerResult ReconfigFPGA(IProcessExecution processExecution);

        /// <summary>
        /// Flashes the FPGA golden image
        /// </summary>
        /// <returns>Exist code from the flash command and its output</returns>
        FPGAManagerResult FlashFPGA(IProcessExecution processExecution, string imagePath);
    }
}
