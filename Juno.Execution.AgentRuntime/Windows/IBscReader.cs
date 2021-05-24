using System;
using System.Collections.Generic;
using System.Text;

namespace Juno.Execution.AgentRuntime.Windows
{
    /// <summary>
    /// Provides the ability to read the BladeSkuCheck tool outputs
    /// </summary>
    public interface IBscReader
    {
        /// <summary>
        /// Read the BSC output which gives the hardware
        /// and firmware configuration of the blade
        /// </summary>
        /// <returns>Hardware and firmware config of the blade</returns>
        string Read();
    }
}
