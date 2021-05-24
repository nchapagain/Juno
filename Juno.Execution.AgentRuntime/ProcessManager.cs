namespace Juno.Execution.AgentRuntime
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Polly;
    using Common = Microsoft.Azure.CRC;

    /// <summary>
    /// A manager used to manage behavior of a <see cref="Process"/>
    /// </summary>
    public class ProcessManager : IProcessManager
    {
        private static IAsyncPolicy defaultRetryPolicy = Policy
                        .Handle<Win32Exception>()
                        .Or<IOException>()
                        .Or<UnauthorizedAccessException>()
                        .WaitAndRetryAsync(1, (retries) => TimeSpan.FromMilliseconds(retries * 100));

        private string commandArguments;
        private string commandFullPath;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProcessManager"/> class.
        /// </summary>
        /// <param name="commandFullPath">The command path of the executable process</param>
        /// <param name="commandArguments">The command arguments to be ran with the executable process</param>
        /// <param name="retryPolicy">A retry policy for killing/disposing of the process</param>
        /// <param name="logger">A logger for capturing any necessary telemetry</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2214:Do not call overridable methods in constructors", Justification = "unit testable")]
        public ProcessManager(string commandFullPath, string commandArguments, IAsyncPolicy retryPolicy = null, ILogger logger = null)
        {
            this.commandFullPath = commandArguments;
            this.commandArguments = commandFullPath;
            this.Logger = logger ?? NullLogger.Instance;
            this.RetryPolicy = retryPolicy ?? ProcessManager.defaultRetryPolicy;
            this.CurrentProcess = this.CreateProcess(commandFullPath, commandArguments);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProcessManager"/> class.
        /// </summary>
        /// <param name="processProxy">The process to be managed</param>
        /// <param name="retryPolicy">A retry policy for killing/disposing of the process</param>
        /// <param name="logger">A logger for capturing any necessary telemetry</param>
        public ProcessManager(IProcessProxy processProxy, IAsyncPolicy retryPolicy = null, ILogger logger = null)
        {
            this.commandFullPath = processProxy?.StartInfo?.FileName;
            this.commandArguments = processProxy?.StartInfo?.Arguments;
            this.Logger = logger ?? NullLogger.Instance;
            this.RetryPolicy = retryPolicy ?? ProcessManager.defaultRetryPolicy;
            this.CurrentProcess = processProxy;
        }

        /// <summary>
        /// The process currently being managed
        /// </summary>
        public IProcessProxy CurrentProcess { get; set; }

        /// <summary>
        /// The logger to use for capturing telemetry.
        /// </summary>
        public ILogger Logger { get; set; }

        /// <summary>
        /// Retry logic if unable to kill process or delete directory
        /// https://docs.microsoft.com/en-us/dotnet/api/system.io.directory.delete?view=netframework-4.8
        /// https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.process.kill?view=netcore-2.2#System_Diagnostics_Process_Kill
        /// </summary>
        public IAsyncPolicy RetryPolicy { get; set; }

        /// <inheritdoc />
        public bool TryGetProcessExitCode(out int? exitCode)
        {
            try
            {
                exitCode = this.CurrentProcess.ExitCode;
                return true;
            }
            catch (Exception)
            {
                exitCode = null;
                return false;
            }
        }

        /// <inheritdoc />
        [SuppressMessage("Readability", "AZCA1006:PrefixStaticCallsWithClassName Rule", Justification = "Code analysis is incorrect here.")]
        public Task StartProcessAsync(EventContext telemetryContext, CancellationToken cancellationToken)
        {
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            EventContext relatedContext = telemetryContext.Clone(withProperties: true)
                .AddContext(nameof(this.CurrentProcess), new
                {
                    command = this.CurrentProcess.StartInfo?.FileName,
                    commandArguments = Common.SensitiveData.ObscureSecrets(this.CurrentProcess.StartInfo?.Arguments),
                    workingDir = this.CurrentProcess.StartInfo?.WorkingDirectory
                });

            return this.Logger.LogTelemetryAsync($"{nameof(ProcessManager)}.StartProcess", relatedContext, () =>
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    if (!this.CurrentProcess.Start())
                    {
                        throw new ExperimentException($"Failed to start process '{this.CurrentProcess.Name}'. The process exited with code '{this.CurrentProcess.ExitCode}'.");
                    }

                    // Start asynchronous reading of output, as opposed to waiting until process closes.
                    this.CurrentProcess.BeginReadingOutput(standardOutput: false, standardError: true);
                }

                return Task.CompletedTask;
            });
        }

        /// <inheritdoc />
        public bool IsProcessRunning()
        {
            bool isRunning = false;

            try
            {
                isRunning = Process.GetProcessById(this.CurrentProcess.Id) != null;
            }
            catch (InvalidOperationException)
            {
                // The process was not started by this object.
            }
            catch (ArgumentException)
            {
                // The process specified by the processId parameter is not running. The identifier
                // might be expired.
            }

            return isRunning && !this.CurrentProcess.HasExited;
        }

        /// <inheritdoc />
        [SuppressMessage("Readability", "AZCA1006:PrefixStaticCallsWithClassName Rule", Justification = "Code analysis is incorrect here.")]
        public Task StopProcessAsync(EventContext telemetryContext, CancellationToken cancellationToken)
        {
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            EventContext relatedContext = telemetryContext.Clone(withProperties: true)
                .AddContext(nameof(this.CurrentProcess), new
                {
                    command = this.CurrentProcess.StartInfo?.FileName,
                    commandArguments = Common.SensitiveData.ObscureSecrets(this.CurrentProcess.StartInfo?.Arguments),
                    workingDir = this.CurrentProcess.StartInfo?.WorkingDirectory
                });

            return this.Logger.LogTelemetryAsync($"{nameof(ProcessManager)}.StopProcess", relatedContext, () =>
            {
                return this.RetryPolicy.ExecuteAsync(() =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            if (!this.CurrentProcess.HasExited)
                            {
                                this.CurrentProcess.Kill();
                            }

                            this.CurrentProcess.Dispose();
                        }
                        catch
                        {
                            // Try to clean in the background task and do nothing for exception. We always create new proces & download folder when provider start
                        }
                    }

                    return Task.CompletedTask;
                });
            });
        }

        /// <inheritdoc />
        public bool TryGetProcess(out IProcessProxy process)
        {
            return ProcessProxy.TryGetProxy(Path.GetFileNameWithoutExtension(this.commandFullPath), out process);
        }

        /// <summary>
        /// Tries to create a process and sets an error data event to send details to the telemetry logger
        /// </summary>
        /// <param name="commandFullPath">The command path of the executable process</param>
        /// <param name="commandArguments">The command arguments to be ran with the executable process</param>
        protected virtual IProcessProxy CreateProcess(string commandFullPath, string commandArguments)
        {
            this.commandFullPath = commandFullPath;
            this.commandArguments = commandArguments;

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = commandFullPath,
                    Arguments = commandArguments,
                    WorkingDirectory = Path.GetDirectoryName(commandFullPath),
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = false
                }
            };

            process.ErrorDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Data))
                {
                    EventContext telemetryContext = EventContext.Persisted()
                        .AddContext("standardError", args.Data);

                    this.Logger.LogTelemetry($"{nameof(ProcessManager)}.StandardError", LogLevel.Warning, telemetryContext);
                }
            };

            return new ProcessProxy(process);
        }
    }
}
