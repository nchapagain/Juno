namespace Juno.Execution.AgentRuntime
{
    using System;
    using System.Diagnostics;

    /// <summary>
    /// Acts as a limited proxy to provide information about a process running
    /// on the local system.
    /// </summary>
    public interface IProcessProxy : IDisposable
    {
        /// <summary>
        /// Gets the ID of the underlying process.
        /// </summary>
        int Id { get; }

        /// <summary>
        /// Gets the name of the underlying process.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the exit code for the underlying process.
        /// </summary>
        int ExitCode { get; }

        /// <summary>
        /// Gets true/false whether the underlying process has exited.
        /// </summary>
        bool HasExited { get; }

        /// <summary>
        /// Gets the process start information.
        /// </summary>
        ProcessStartInfo StartInfo { get; }

        /// <summary>
        /// Instructs the underlying process to begin reading output.
        /// </summary>
        /// <param name="standardOutput">True if the underlying process should begin reading standard output.</param>
        /// <param name="standardError">True if the underlying process should begin reading standard error output.</param>
        void BeginReadingOutput(bool standardOutput = true, bool standardError = true);

        /// <summary>
        /// Returns the underlying process associated with the proxy.
        /// </summary>
        /// <returns>
        /// The <see cref="Process"/> instances associated with the proxy.
        /// </returns>
        Process GetProcess();

        /// <summary>
        /// Promptly terminates/kills the underlying process without waiting for a
        /// graceful exit.
        /// </summary>
        void Kill();

        /// <summary>
        /// Starts the underlying process.
        /// </summary>
        bool Start();
    }
}
