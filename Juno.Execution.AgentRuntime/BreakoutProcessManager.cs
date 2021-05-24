namespace Juno.Execution.AgentRuntime
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Polly;

    /// <summary>
    /// A process manager that launches a process that is not bound by the parent process's service resource limits.
    /// </summary>
    public class BreakoutProcessManager : IProcessManager
    {
        private const int ExecuteTimeoutMillis = 5000;
        private const int ServiceDelayMillis = 3000;
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
        public BreakoutProcessManager(string commandFullPath, string commandArguments, IAsyncPolicy retryPolicy = null, ILogger logger = null)
        {
            commandArguments.ThrowIfNullOrWhiteSpace(nameof(commandArguments));
            commandFullPath.ThrowIfNullOrWhiteSpace(nameof(commandFullPath));
            this.commandFullPath = commandFullPath;
            this.commandArguments = commandArguments.Replace("\"", "\\\"", StringComparison.OrdinalIgnoreCase);
            this.Logger = logger ?? NullLogger.Instance;
            this.RetryPolicy = retryPolicy ?? BreakoutProcessManager.defaultRetryPolicy;
            this.ValidateCommands();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProcessManager"/> class.
        /// </summary>
        /// <param name="processProxy">The process to be managed</param>
        /// <param name="retryPolicy">A retry policy for killing/disposing of the process</param>
        /// <param name="logger">A logger for capturing any necessary telemetry</param>
        public BreakoutProcessManager(IProcessProxy processProxy, IAsyncPolicy retryPolicy = null, ILogger logger = null)
        {
            try
            {
                this.commandFullPath = processProxy?.StartInfo?.FileName;
                this.commandArguments = processProxy?.StartInfo?.Arguments;
            }
            catch (InvalidOperationException)
            {
                this.commandFullPath = string.Empty;
                this.commandArguments = string.Empty;
            }

            this.Logger = logger ?? NullLogger.Instance;
            this.RetryPolicy = retryPolicy ?? BreakoutProcessManager.defaultRetryPolicy;
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
        /// Retry logic if unable to kill process or create/start/delete a service
        /// https://docs.microsoft.com/en-us/dotnet/api/system.io.directory.delete?view=netframework-4.8
        /// https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.process.kill?view=netcore-2.2#System_Diagnostics_Process_Kill
        /// </summary>
        public IAsyncPolicy RetryPolicy { get; set; }

        /// <inheritdoc />
        public bool IsProcessRunning()
        {
            bool isRunning = false;

            if (this.CurrentProcess == null)
            {
                return false;
            }

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
        public Task StartProcessAsync(EventContext telemetryContext, CancellationToken cancellationToken)
        {
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            return this.Logger.LogTelemetryAsync($"{nameof(BreakoutProcessManager)}.StartProcess", telemetryContext, async () =>
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    await this.RunProcessServiceAsync(telemetryContext, cancellationToken).ConfigureDefaults();
                    this.TryGetProcess(out IProcessProxy process);
                    this.CurrentProcess = process;
                }
            });
        }

        /// <inheritdoc />
        public Task StopProcessAsync(EventContext telemetryContext, CancellationToken cancellationToken)
        {
            telemetryContext.ThrowIfNull(nameof(telemetryContext));
            this.CurrentProcess.ThrowIfNull(nameof(this.CurrentProcess), "Process has not been started.");

            EventContext relatedContext = telemetryContext.Clone(withProperties: true);

            return this.Logger.LogTelemetryAsync($"{nameof(BreakoutProcessManager)}.StopProcess", relatedContext, () =>
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
        public bool TryGetProcess(out IProcessProxy process)
        {
            bool processFound = ProcessProxy.TryGetProxy(Path.GetFileNameWithoutExtension(this.commandFullPath), out process);

            return processFound;
        }

        /// <summary>
        /// Creates, starts, and deletes a service used to run a process under the system
        /// </summary>
        /// <param name="telemetryContext">Context used in logging telemetry</param>
        /// <param name="cancellationToken">A cancellation token</param>
        public async Task RunProcessServiceAsync(EventContext telemetryContext, CancellationToken cancellationToken)
        {
            Guid serviceName = Guid.NewGuid();

            // Create new service registration
            await this.ExecuteServiceProcessCommandAsync(telemetryContext, $@"create {serviceName} binpath= ""cmd /c start {this.commandFullPath} {this.commandArguments}""", cancellationToken).ConfigureDefaults();
            await this.ExecuteServiceProcessCommandAsync(telemetryContext, $@"start {serviceName}", cancellationToken, true).ConfigureDefaults();
            await this.ExecuteServiceProcessCommandAsync(telemetryContext, $@"delete {serviceName}", cancellationToken).ConfigureDefaults();
        }

        /// <summary>
        /// Tries to create a process and sets an error data event to send details to the telemetry logger
        /// </summary>
        /// <param name="commandFullPath">The command path of the executable process</param>
        /// <param name="commandArguments">The command arguments to be ran with the executable process</param>
        public virtual IProcessProxy CreateProcess(string commandFullPath, string commandArguments)
        {
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

                    this.Logger.LogTelemetry($"{nameof(BreakoutProcessManager)}.StandardError", LogLevel.Warning, telemetryContext);
                }
            };

            return new ProcessProxy(process);
        }

        private void ValidateCommands()
        {
            if (this.commandFullPath.Contains("\"", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("The command path cannot contain any quotes.");
            }

            if (this.commandFullPath?.Any(s => char.IsWhiteSpace(s)) == true)
            {
                throw new ArgumentException("The command full path does not support white space at this time.");
            }

            if (this.commandArguments.Length - 2 > this.commandArguments.Replace("\"", string.Empty, StringComparison.OrdinalIgnoreCase).Length)
            {
                throw new ArgumentException("The command arguments cannot contain more than two quotes.");
            }
        }

        private async Task ExecuteServiceProcessCommandAsync(EventContext telemetryContext, string command, CancellationToken cancellationToken, bool addDelay = false)
        {
            telemetryContext.AddContext("executeServiceCommand", command);

            await this.Logger.LogTelemetryAsync($"{nameof(BreakoutProcessManager)}.ExecuteServiceCommand", telemetryContext, async () =>
            {
                await this.RetryPolicy.ExecuteAsync(() =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        using (IProcessProxy processProxy = this.CreateProcess("sc", command))
                        {
                            if (processProxy.Start())
                            {
                                // Start asynchronous reading of output, as opposed to waiting until process closes.
                                processProxy.BeginReadingOutput(standardOutput: false, standardError: true);

                                if (processProxy.GetProcess().WaitForExit(BreakoutProcessManager.ExecuteTimeoutMillis))
                                {
                                    telemetryContext.AddContext("processExitCode", processProxy.ExitCode);
                                }
                                else
                                {
                                    telemetryContext.AddContext("processTimeout", true);
                                }
                            }
                        }
                    }

                    return Task.CompletedTask;
                }).ConfigureDefaults();
            }).ConfigureDefaults();

            if (addDelay)
            {
                await Task.Delay(BreakoutProcessManager.ServiceDelayMillis).ConfigureDefaults();
            }
        }
    }
}
