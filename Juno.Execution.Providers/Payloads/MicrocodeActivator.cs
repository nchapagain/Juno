namespace Juno.Execution.Providers.Payloads
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Execution.AgentRuntime;
    using Juno.Execution.AgentRuntime.Contract;
    using Juno.Extensions.Telemetry;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;

    /// <summary>
    /// Class responsible for checking 
    /// CPU Microcode activation 
    /// </summary>
    public class MicrocodeActivator : IPayloadActivator
    {
        private const string FirstUpdateCode = "0";
        private const string SubsequestUpdateCode = "6";
        private ILogger logger;
        private ISystemPropertyReader hostProperties;

        /// <summary>
        /// Inits a new instance of MicrocodeActivator
        /// </summary>
        /// <param name="ucodeVersion">The expected microcode version.</param>
        /// <param name="timeout">
        /// The maximum amount of time the activator will attempt to verify the activation of the microcode 
        /// update before exiting.
        /// </param>
        /// <param name="hostProperties">Properties of the local host.</param>
        /// <param name="logger">A logger that can be used to capture telemetry.</param>
        public MicrocodeActivator(string ucodeVersion, TimeSpan timeout, ISystemPropertyReader hostProperties = null, ILogger logger = null)
        {
            ucodeVersion.ThrowIfNull(nameof(ucodeVersion));
            timeout.ThrowIfNull(nameof(timeout));

            this.UcodeVersion = ucodeVersion.ToLower();
            this.Timeout = timeout;
            this.logger = logger ?? NullLogger.Instance;
            this.hostProperties = hostProperties ?? SystemManagerFactory.Get().PropertyReader;
        }

        /// <summary>
        /// Microcode version under test 
        /// from the experiment definition  
        /// </summary>
        public string UcodeVersion { get; }

        /// <summary>
        /// The maximum amount of time the activator will attempt to verify the activation
        /// of the microcode update before exiting.
        /// </summary>
        public TimeSpan Timeout { get; private set; }

        /// <summary>
        /// Responsible for activating/validating the 
        /// microcode under test.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns>Activationresult object</returns>
        public async Task<ActivationResult> ActivateAsync(CancellationToken cancellationToken)
        {
            ActivationResult result = new ActivationResult(false);

            EventContext telemetryContext = EventContext.Persisted()
               .AddContext("microcodeExpectedVersion", this.UcodeVersion);

            try 
            {
                Stopwatch timer = Stopwatch.StartNew();

                do
                {
                    this.logger.LogTelemetry($"{nameof(MicrocodeActivator)}.VerifyActivation", telemetryContext, () =>
                    {
                        var actualUcodeVersion = this.hostProperties.Read(AzureHostProperty.CpuMicrocodeVersion).ToLower();
                        var updateStatus = this.hostProperties.Read(AzureHostProperty.CpuMicrocodeUpdateStatus);

                        telemetryContext.AddContext("microcodeActualVersion", actualUcodeVersion);
                        telemetryContext.AddContext("microcodeUpdateStatus", updateStatus);

                        // updatestatus will be 0 for the first time update,
                        // if Pilot fish Serive manager tried to update second time update status will be 6 (No operation).
                        // Using contains rather than equality because registry value read and comaprision string are not same in length always 
                        // fo e.g. Microcode version returned by registry reader : b00003900000000 , Microcode version string to check : b000039
                        if (actualUcodeVersion.Contains(this.UcodeVersion, StringComparison.OrdinalIgnoreCase)
                                && (updateStatus == MicrocodeActivator.FirstUpdateCode || updateStatus == MicrocodeActivator.SubsequestUpdateCode))
                        {
                            result = new ActivationResult(true, DateTime.UtcNow);
                        }
                    });

                    if (!result.IsActivated)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureDefaults();
                    }
                }
                while (!result.IsActivated 
                    && timer.ElapsedMilliseconds <= this.Timeout.TotalMilliseconds
                    && !cancellationToken.IsCancellationRequested);
            }
            catch
            {
                // We don't want to surface the exception. The logging framework will capture
                // the error information and this is sufficient to ensure we understand why
                // we are failing to read windows registry values to check the Microcode version.
            }

            return result;
        }
    }
}
