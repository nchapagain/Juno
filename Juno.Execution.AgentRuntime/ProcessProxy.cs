namespace Juno.Execution.AgentRuntime
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Extensions;

    /// <summary>
    /// Acts as a proxy for a <see cref="Process"/> running on the local
    /// system.
    /// </summary>
    public class ProcessProxy : IProcessProxy
    {
        private Process underlyingProcess;
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProcessProxy"/> class.
        /// </summary>
        /// <param name="process">The process associated with the proxy.</param>
        public ProcessProxy(Process process)
        {
            process.ThrowIfNull(nameof(process));
            this.underlyingProcess = process;
        }

        /// <summary>
        /// Gets the ID of the underlying process (as defined by the operating system).
        /// </summary>
        public int Id => this.underlyingProcess.Id;

        /// <summary>
        /// Gets the Name of the underlying process (as defined by the operating system).
        /// </summary>
        public string Name => this.underlyingProcess.ProcessName;

        /// <summary>
        /// Gets the exit code for the underlying process.
        /// </summary>
        public int ExitCode => this.underlyingProcess.ExitCode;

        /// <summary>
        /// Gets true/false whether the underlying process has exited (stopped running).
        /// </summary>
        public bool HasExited
        {
            get
            {
                try
                {
                    return this.underlyingProcess.HasExited;
                }
                catch (InvalidOperationException)
                {
                    // No process is associated with this object.
                    return true;
                }
            }
        }

        /// <summary>
        /// Gets the underlying process start information.
        /// </summary>
        public ProcessStartInfo StartInfo => this.underlyingProcess.StartInfo;

        /// <summary>
        /// Returns true if the underlying process is found running on the local system and outputs
        /// the <see cref="Process"/> itself.
        /// </summary>
        /// <param name="processId">The ID of the process to find.</param>
        /// <param name="proxy">
        /// Output parameter defines the process proxy if the process exists and is running on the local system.
        /// </param>
        /// <returns>
        /// True if the process is found on the local system, false if it not.
        /// </returns>
        public static bool TryGetProxy(int processId, out IProcessProxy proxy)
        {
            proxy = null;

            try
            {
                proxy = new ProcessProxy(Process.GetProcessById(processId));
            }
            catch (ArgumentException)
            {
                // Process is not running and does not exist.
            }

            return proxy != null;
        }

        /// <summary>
        /// Returns true if the underlying process is found running on the local system and outputs
        /// the <see cref="Process"/> itself.
        /// </summary>
        /// <param name="processName">The name of the process to find.</param>
        /// <param name="proxy">
        /// Output parameter defines the process proxy if the process exists and is running on the local system.
        /// </param>
        /// <returns>
        /// True if the process is found on the local system, false if it not.
        /// </returns>
        public static bool TryGetProxy(string processName, out IProcessProxy proxy)
        {
            Process[] processes = Process.GetProcessesByName(processName);

            if (processes != null && processes.Length > 1)
            {
                throw new ExperimentException($"Multiple processes found. Process name: {processName}");
            }

            proxy = processes?.Any() == true ? new ProcessProxy(processes[0]) : null;

            return proxy != null;
        }

        /// <summary>
        /// Instructs the underlying process to begin reading output.
        /// </summary>
        /// <param name="standardOutput">True if the underlying process should begin reading standard output.</param>
        /// <param name="standardError">True if the underlying process should begin reading standard error output.</param>
        public void BeginReadingOutput(bool standardOutput = true, bool standardError = true)
        {
            if (standardError)
            {
                this.underlyingProcess.BeginErrorReadLine();
            }

            if (standardOutput)
            {
                this.underlyingProcess.BeginOutputReadLine();
            }
        }

        /// <summary>
        /// Disposes of resources used by the proxy.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Returns the process associated with the proxy.
        /// </summary>
        /// <returns>
        /// The <see cref="Process"/> instances associated with the proxy.
        /// </returns>
        public Process GetProcess()
        {
            return this.underlyingProcess;
        }

        /// <summary>
        /// Promptly terminates/kills the underlying process without waiting for a
        /// graceful exit.
        /// </summary>
        public void Kill()
        {
            this.underlyingProcess.Kill();
        }

        /// <summary>
        /// Starts the underlying process.
        /// </summary>
        public bool Start()
        {
            return this.underlyingProcess.Start();
        }

        /// <summary>
        /// Disposes of resources used by the proxy.
        /// </summary>
        /// <param name="disposing">True to dispose of unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    this.underlyingProcess.Dispose();
                }

                this.disposed = true;
            }
        }
    }
}
