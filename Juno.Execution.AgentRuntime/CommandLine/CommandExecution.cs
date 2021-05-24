namespace Juno.Execution.AgentRuntime.CommandLine
{
    using System;
    using System.Diagnostics;
    using System.Security;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;

    /// <summary>
    /// A class that combines command line instructions with a project execution plan
    /// </summary>
    public abstract class CommandExecution
    {
        private ILogger logger;

        /// <summary>
        /// A simple Constructor
        /// </summary>
        public CommandExecution()
        {
        }

        /// <summary>
        /// The full path to the environment configuration/settings file.
        /// </summary>
        public string ConfigurationPath { get; set; }

        /// <summary>
        /// The environment in which the agent/command is running (e.g. juno-dev01, juno-prod01).
        /// </summary>
        public string Environment { get; set; }

        /// <summary>
        /// A logger used to instrument the command line application.
        /// </summary>
        public ILogger Logger
        {
            get
            {
                return this.logger ?? NullLogger.Instance;
            }

            set
            {
                this.logger = value;
            }
        }

        /// <summary>
        /// Writes exception details to the system event log.
        /// </summary>
        /// <param name="exc">The exception being thrown.</param>
        /// <param name="agentType">The program being ran.</param>
        public static void WriteCrashInfoToEventLog(Exception exc, string agentType)
        {
            if (System.Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                string desiredSource = "Juno";
                string backupSource = "Application";
                string crashMessage = $"The Juno {agentType} crashed with an unhandled exception:{System.Environment.NewLine}{exc.ToString(true, true)}";

                try
                {
                    if (!EventLog.SourceExists(desiredSource))
                    {
                        EventLog.CreateEventSource(desiredSource, "Application");
                    }

                    EventLog.WriteEntry(desiredSource, crashMessage, EventLogEntryType.Error);
                }
                catch (SecurityException)
                {
                    // Capture the error in the default 'Application' source otherwise.
                    EventLog.WriteEntry(backupSource, crashMessage, EventLogEntryType.Error);
                }
            }
        }

        /// <summary>
        /// This is executed after the command line is parsed
        /// </summary>
        /// <param name="args">The set of command line arguments initially supplied.</param>
        /// <param name="cancellationToken">A cancellation token</param>
        /// <param name="services">Optional parameter allows dependencies to be provided to the command.</param>
        /// <returns>A task with 0 as success and 1 as failure</returns>
        public abstract Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken, IServiceCollection services = null);
    }
}
