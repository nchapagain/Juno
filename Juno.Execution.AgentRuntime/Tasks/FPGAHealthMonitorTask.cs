namespace Juno.Execution.AgentRuntime.Tasks
{
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts.Configuration;
    using Juno.Execution.AgentRuntime.Contract;
    using Juno.Execution.AgentRuntime.Windows;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using Polly;

    /// <summary>
    /// A system monitor that periodically sends FPGA health data.
    /// </summary>
    public class FpgaHealthMonitorTask : AgentTask
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FpgaHealthMonitorTask"/> class.
        /// </summary>
        /// <param name="services">
        /// Services collection provides required dependencies to the host task/operation.
        /// </param>
        /// <param name="settings">Configuration settings for the environment.</param>
        /// <param name="retryPolicy">A retry policy to apply to API calls.</param>
        public FpgaHealthMonitorTask(IServiceCollection services, EnvironmentSettings settings, IAsyncPolicy retryPolicy = null)
            : base(services, settings, retryPolicy)
        {
        }

        /// <inheritdoc/>
        public override Task ExecuteAsync(CancellationToken cancellationToken)
        {            
            return Task.Run(() =>
            {
                try
                {
                    IFirmwareReader<FpgaHealth> reader = this.Services.TryGetService<IFirmwareReader<FpgaHealth>>(out IFirmwareReader<FpgaHealth> value) ? value : new FpgaReader();
                    if (reader.CanRead(typeof(FpgaHealth)))
                    {
                        EventContext telemetryContext = EventContext.Persisted();
                        this.Logger.LogTelemetry($"{nameof(FpgaHealthMonitorTask)}.Info", telemetryContext, () =>
                        {
                            FpgaHealth fpgaHealth = reader.Read();
                            telemetryContext.AddContext("fpgaHealth", fpgaHealth);
                        });
                    }
                }
                catch
                {
                    // We don't want to surface the exception. The logging framework will capture
                    // the error information and this is sufficient
                }
            });
        }
    }
}
