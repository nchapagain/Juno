namespace Juno.Execution.AgentRuntime.Tasks
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts.Configuration;
    using Juno.Extensions.Telemetry;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using Polly;

    /// <summary>
    /// A monitor that monitor VM uptime by using wall clock.
    /// </summary>
    public class VmUptimeMonitorTask : AgentMonitorTask<string>
    {
        private ISystemManager systemManager;
        private DateTime lastRealTimeClock;
        private TimeSpan lastCpuUptime;

        /// <summary>
        /// Initializes an new instance of the <see cref="VmUptimeMonitorTask"/> class.
        /// </summary>
        /// <param name="services">
        /// Services collection provides required dependencies to the host task/operation.
        /// </param>
        /// <param name="settings">Configuration settings for the environment.</param>
        /// <param name="retryPolicy">A retry policy to apply to the execution of the task operation.</param>
        public VmUptimeMonitorTask(IServiceCollection services, EnvironmentSettings settings, IAsyncPolicy retryPolicy = null)
            : base(services, settings, retryPolicy)
        {
            this.systemManager = SystemManagerFactory.Get();
            this.lastCpuUptime = this.systemManager.GetUptime();
            this.lastRealTimeClock = DateTime.UtcNow;
        }

        /// <summary>
        /// Executes logic in the background to monitor Vm uptime on an interval.
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
                    EventContext telemetryContext = EventContext.Persisted()
                        .AddContext("lastRtc", this.lastRealTimeClock.ToString())
                        .AddContext("lastCpuUptime", this.lastCpuUptime.ToString());

                    this.Logger.LogTelemetry(nameof(VmUptimeMonitorTask), telemetryContext, () =>
                    {
                        TimeSpan diff = this.CalculateCpuAndRtcTimeDifference();

                        telemetryContext
                            .AddContext("newRtc", this.lastRealTimeClock.ToString())
                            .AddContext("newCpuUptime", this.lastCpuUptime.ToString())
                            .AddContext("difference", diff.ToString());
                    });
                }
                catch
                {
                    // We don't want to surface the exception. The logging framework will capture
                    // the error information and this is sufficient to ensure we understand why
                    // we are failing to get time difference.
                }
            });
        }

        private TimeSpan CalculateCpuAndRtcTimeDifference()
        {
            DateTime newRtc = DateTime.UtcNow;
            TimeSpan newCpuUpTime = this.systemManager.GetUptime();
            TimeSpan diff = (newRtc - this.lastRealTimeClock) - (newCpuUpTime - this.lastCpuUptime);

            this.lastCpuUptime = newCpuUpTime;
            this.lastRealTimeClock = newRtc;
            return diff;
        }
    }
}
