namespace Juno.Execution.AgentRuntime.Tasks
{
    using System;
    using System.Diagnostics;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.Extensions.Telemetry;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using Polly;

    /// <summary>
    /// A system monitor that reads and uploads SEL logs to a target
    /// </summary>
    public class SelLogMonitorTask : AgentMonitorTask<string>
    {
        private static readonly Regex SelLogSuccessExpression = new Regex("Completed Successfully", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Initializes an new instance of the <see cref="SelLogMonitorTask"/> class.
        /// </summary>
        /// <param name="services">
        /// Services collection provides required dependencies to the host task/operation.
        /// </param>
        /// <param name="settings">Configuration settings for the environment.</param>
        /// <param name="retryPolicy">A retry policy to apply to the execution of the task operation.</param>
        public SelLogMonitorTask(IServiceCollection services, EnvironmentSettings settings, IAsyncPolicy retryPolicy = null)
            : base(services, settings, retryPolicy)
        {
        }

        /// <summary>
        /// Executes logic in the background to monitor for SEL logs on an interval.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// A task that can be used to execute the monitor task asynchronously.
        /// </returns>
        public override Task ExecuteAsync(CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                try
                {
                    AgentMonitoringSettings monitoringSettings = this.Settings.AgentSettings.AgentMonitoringSettings;

                    EventContext telemetryContext = EventContext.Persisted()
                        .AddContext("command", $"{monitoringSettings.SelLogMonitorSettings.IpmiUtilExePath} {monitoringSettings.SelLogMonitorSettings.IpmiUtilSelLogCommand}");

                    this.Logger.LogTelemetry(nameof(SelLogMonitorTask), telemetryContext, () =>
                    {
                        this.RetryPolicy.ExecuteAsync(() =>
                        {
                            string standardOutput;
                            this.ExecuteProcess(monitoringSettings.SelLogMonitorSettings.IpmiUtilExePath, monitoringSettings.SelLogMonitorSettings.IpmiUtilSelLogCommand, out standardOutput);

                            if (SelLogMonitorTask.SelLogSuccessExpression.IsMatch(standardOutput))
                            {
                                this.OnResults(new ExecutionEventArgs<string>(standardOutput));
                            }
                            else
                            {
                                throw new ExperimentException(
                                    $"Attempt to read the SEL log failed with the following output.{Environment.NewLine}{Environment.NewLine}" +
                                    $"{standardOutput}");
                            }

                            return Task.CompletedTask;

                        }).GetAwaiter().GetResult();
                    });
                }
                catch
                {
                    // We don't want to surface the exception. The logging framework will capture
                    // the error information and this is sufficient to ensure we understand why
                    // we are failing to capture SEL log data.
                }
            });
        }

        /// <summary>
        /// Executes the process to capture the SEL log.
        /// </summary>
        /// <param name="exePath">The path to the ipmiutil.exe utility.</param>
        /// <param name="arguments">Arguments to supply to the ipmiutil.exe utility to capture a SEL log.</param>
        /// <param name="standardOutput">Standard output from the execution of the ipmiutil.exe command that contains the SEL log data/text.</param>
        protected virtual void ExecuteProcess(string exePath, string arguments, out string standardOutput)
        {
            // event severity
            // 0 :  information
            // 1 : Min
            // 2 : Maj
            // 3 : Crt
            // logging severity >= 1
            // Lowercase c required

            standardOutput = string.Empty;
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                FileName = exePath,
                CreateNoWindow = true,
                Arguments = arguments,
            };

            using (Process process = Process.Start(startInfo))
            {
                while (!process.StandardOutput.EndOfStream)
                {
                    standardOutput += process.StandardOutput.ReadToEnd();
                }

                process.WaitForExit();
            }
        }
    }
}
