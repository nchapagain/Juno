using System;
using System.Collections.Generic;
using System.Text;

namespace Juno.Execution.AgentRuntime.Windows
{
    /// <summary>
    /// Stores the result of an FPGA management command
    /// </summary>
    public class FPGAManagerResult
    {
        /// <summary>
        /// True if the operation succeeded
        /// </summary>
        public bool Succeeded { get; set; }

        /// <summary>
        /// Raw string containing the execution result
        /// </summary>
        public string ExecutionResult { get; set; }
    }
}
