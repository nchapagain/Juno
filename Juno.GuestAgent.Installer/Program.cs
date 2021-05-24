namespace Juno.GuestAgent.Installer
{
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Builder;
    using System.CommandLine.Parsing;
    using System.Reflection;
    using System.Threading;
    using Juno.Execution.AgentRuntime.CommandLine;
    using Juno.Execution.AgentRuntime.Contract;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Contains entry point of guest agent installer.
    /// </summary>
    public static class Program
    {
        internal static Assembly HostAssembly { get; } = Assembly.GetAssembly(typeof(Program));

        internal static string HostName { get; } = "GuestAgentInstaller";

        internal static ILogger Logger { get; set; }

        /// <summary>
        /// Entry point for guest agent installer exe.
        /// </summary>
        /// <param name="args"></param>
        /// <remarks>
        /// Debugging Notes:
        /// The --token value expects a JWT, and you will need to request one from Azure.
        /// Some code has been provided below which will get a JWT for Juno Guest Agent AAD principal (e.g. juno-dev01-guestagent-principal).
        /// </remarks>
        public static int Main(string[] args)
        {
            using (CancellationTokenSource tokenSource = new CancellationTokenSource())
            {
                int result = HostExitCode.Default;

                try
                {
                    Console.CancelKeyPress += (sender, e) =>
                    {
                        tokenSource.Cancel();
                    };

                    CommandLineBuilder commandBuilder = AgentInstallationCommand.CreateBuilder(args, tokenSource.Token)
                        .WithDefaults();

                    ParseResult parseResult = commandBuilder.Build().Parse(args);
                    parseResult.ThrowOnUsageError();

                    result = parseResult.InvokeAsync().GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                    // Expected when the Ctrl-C is pressed to cancel operation.
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
                    Program.Logger?.LogTelemetry($"{Program.HostName}.StartupError", LogLevel.Error, EventContext.Persisted()
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
