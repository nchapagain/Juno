namespace Juno.GuestAgent
{
    using System;
    using System.CommandLine;
    using System.CommandLine.Builder;
    using System.CommandLine.Parsing;
    using System.IO;
    using System.Reflection;
    using System.Threading;
    using Juno.Execution.AgentRuntime.CommandLine;
    using Juno.Execution.AgentRuntime.Contract;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Contains entry point of guest agent
    /// </summary>
    public static class Program
    {
        internal static Assembly HostAssembly { get; } = Assembly.GetAssembly(typeof(ExperimentExecutionCommand));

        internal static string HostExePath { get; } = Path.GetDirectoryName(Program.HostAssembly.Location);

        internal static string HostName { get; } = "GuestAgent";

        internal static ILogger Logger { get; set; }

        /// <summary>
        /// Entry point for guest agent exe.
        /// </summary>
        public static int Main(string[] args)
        {
            // Ensure the working directory for the Guest Agent is the same directory where the
            // executable exists. When we run as a service, the working directory will be initially set
            // to the /Windows/System32 directory because that is where the servicehost.exe exists.
            Directory.SetCurrentDirectory(Program.HostExePath);

            using (CancellationTokenSource tokenSource = new CancellationTokenSource())
            {
                int result = HostExitCode.Default;
                try
                {
                    Console.CancelKeyPress += (sender, e) =>
                    {
                        tokenSource.Cancel();
                    };

                    CommandLineBuilder commandBuilder = ExperimentExecutionCommand.CreateBuilder(args, tokenSource.Token)
                        .WithDefaults();
                    
                    ParseResult parseResult = commandBuilder.Build().Parse(args);
                    parseResult.ThrowOnUsageError();

                    result = parseResult.InvokeAsync().GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                    // Expected when the Ctrl-C is pressed to cancel operation.
                    Program.Logger?.LogTelemetry($"{Program.HostName}.Cancelled", LogLevel.Warning, new EventContext(Guid.NewGuid()));
                }
                catch (ArgumentException exc)
                {
                    Program.Logger?.LogTelemetry($"{Program.HostName}.StartupError", LogLevel.Error, EventContext.Persisted()
                       .AddError(exc));

                    // Capture the unhandled exception event in the local event log as well to add another
                    // way for us to ensure we can identify the reasons we are crashing/exiting.
                    CommandExecution.WriteCrashInfoToEventLog(exc, Program.HostName);
                    result = HostExitCode.InvalidUsage;
                }
                catch (Exception exc)
                {
                    Program.Logger?.LogTelemetry($"{Program.HostName}.Crash", LogLevel.Error, EventContext.Persisted()
                        .AddError(exc));

                    // Capture the unhandled exception event in the local event log as well to add another
                    // way for us to ensure we can identify the reasons we are crashing/exiting.
                    CommandExecution.WriteCrashInfoToEventLog(exc, Program.HostName);
                    result = HostExitCode.UnhandledError;
                }

                return result;
            }
        }
    }
}
