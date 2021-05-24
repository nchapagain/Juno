namespace Juno.Execution.AgentRuntime.Windows
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.CRC;

    /// <summary>
    /// Provides the ability to interact with the CRCBladeCertification agent
    /// </summary>
    public interface ICertificationManager
    {
       /// <summary>
       /// Certifies the node using the CRC Blade certification agent
       /// </summary>
       /// <param name="processExecution">Process execution</param>
       /// <param name="message">output message from the certification</param>
       /// <returns></returns>
        bool Certify(IProcessExecution processExecution, out string message);
    }
}
