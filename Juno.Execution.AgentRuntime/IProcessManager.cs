namespace Juno.Execution.AgentRuntime
{
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.CRC.Telemetry;

    /// <summary>
    /// A manager used to manage behavior of a <see cref="Process"/>
    /// </summary>
    public interface IProcessManager
    {
        /// <summary>
        /// Attempts to start the managed process
        /// </summary>
        /// <param name="telemetryContext">Context used in logging telemetry</param>
        /// <param name="cancellationToken">A cancellation token</param>
        Task StartProcessAsync(EventContext telemetryContext, CancellationToken cancellationToken);

        /// <summary>
        /// Checks if the process is currently running. Should return false if the process has not been created/started or if it has already exited
        /// </summary>
        bool IsProcessRunning();

        /// <summary>
        /// Attempts to kill and dispose of the managed process
        /// </summary>
        /// <param name="telemetryContext">Context used in logging telemetry</param>
        /// <param name="cancellationToken">A cancellation token</param>
        Task StopProcessAsync(EventContext telemetryContext, CancellationToken cancellationToken);

        /// <summary>
        /// Checks if the process has exited.
        /// </summary>
        /// <param name="exitCode">The exit code of the managed process.</param>
        bool TryGetProcessExitCode(out int? exitCode);

        /// <summary>
        /// Tries to get the process commonly referred to in this process manager.
        /// </summary>
        /// <param name="process">The process that is retrieved.</param>
        public bool TryGetProcess(out IProcessProxy process);
    }
}
