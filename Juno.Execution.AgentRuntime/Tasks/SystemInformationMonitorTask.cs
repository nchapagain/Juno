namespace Juno.Execution.AgentRuntime.Tasks
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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
    /// Executes logic in the background to send heartbeats to the
    /// Juno system.
    /// </summary>
    public class SystemInformationMonitorTask : AgentTask
    {
        private ISystemManager systemManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="SystemInformationMonitorTask"/> class.
        /// </summary>
        /// <param name="services">
        /// Services collection provides required dependencies to the host task/operation.
        /// </param>
        /// <param name="settings">Configuration settings for the environment.</param>
        /// <param name="retryPolicy">A retry policy to apply to API calls.</param>
        public SystemInformationMonitorTask(IServiceCollection services, EnvironmentSettings settings, IAsyncPolicy retryPolicy = null)
            : base(services, settings, retryPolicy)
        {
            this.systemManager = SystemManagerFactory.Get();
        }

        /// <inheritdoc/>
        public override Task ExecuteAsync(CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                try
                {
                    EventContext telemetryContext = EventContext.Persisted();
                    this.Logger.LogTelemetry($"{nameof(SystemInformationMonitorTask)}.Info", telemetryContext, () =>
                    {
                        List<Exception> errors = new List<Exception>();

                        this.AddNodeInfo(telemetryContext, errors);
                        this.AddHostOsInfo(telemetryContext, errors);
                        this.AddCpuInfo(telemetryContext, errors);
                        this.AddSsdInfo(telemetryContext, errors);
                        this.AddInfo<BiosInfo>(telemetryContext, errors, "biosInfo");
                        this.AddInfo<BmcInfo>(telemetryContext, errors, "bmcInfo");

                        if (errors.Any())
                        {
                            // Let the telemetry framework catch and log the errors.
                            throw new AggregateException("Errors reading system/host information.", errors);
                        }
                    });
                }
                catch
                {
                    // We don't want to surface the exception. The logging framework will capture
                    // the error information and this is sufficient
                }
            });
        }

        private void AddInfo<T>(EventContext telemetryContext, List<Exception> errors, string propertyName)
        {
            try
            {
                IFirmwareReader<T> reader = this.Services.GetService<IFirmwareReader<T>>();
                telemetryContext.AddContext(propertyName, reader.Read());
            }
            catch (Exception exc)
            {
                errors.Add(exc);
            }
        }

        private void AddSsdInfo(EventContext telemetryContext, List<Exception> errors)
        {
            try
            {
                IFirmwareReader<IEnumerable<SsdInfo>> smartCtlReader = this.Services.GetService<IFirmwareReader<IEnumerable<SsdInfo>>>();
                IFirmwareReader<IEnumerable<SsdWmicInfo>> wmicReader = this.Services.GetService<IFirmwareReader<IEnumerable<SsdWmicInfo>>>();
                IEnumerable<SsdWmicInfo> ssdInfo = smartCtlReader.CanRead(typeof(IEnumerable<SsdInfo>))
                    ? smartCtlReader.Read().Select(fw => new SsdWmicInfo(fw.FirmwareVersion, fw.ModelName, fw.SerialNumber))
                    : ssdInfo = wmicReader.Read();

                telemetryContext.AddContext("ssdInfo", ssdInfo);
            }
            catch (Exception exc)
            {
                errors.Add(exc);
            }

        }

        private void AddCpuInfo(EventContext telemetryContext, List<Exception> errors)
        {
            try
            {
                ISystemPropertyReader systemReader = this.systemManager.PropertyReader;
                IMsr msr = this.Services.GetService<IMsr>();
                string patrolScrubMsrValue = null;
                string jccMsrRegisterValue = null;

                try
                {
                    patrolScrubMsrValue = msr.Read(CpuConstants.PatrolScrubMsrReg);
                    jccMsrRegisterValue = msr.Read(CpuConstants.JccMsrReg);
                    telemetryContext.AddContext("rdmsrToolsetInstalled", true);
                }
                catch (Exception exc)
                {
                    telemetryContext.AddContext("rdmsrToolsetInstalled", false);

                    // If the CSIMicrocodeUpdate package is not installed on the system, then the
                    // MSR toolset required to read the CPU register values above does not exist. An
                    // exception is thrown in that case. We do not want to lose ALL CPU information because
                    // the toolset does not exist.
                    errors.Add(exc);
                }

                telemetryContext.AddContext("cpuInfo", new CpuInfo(
                    systemReader.Read(AzureHostProperty.CpuIdentifier),
                    systemReader.Read(AzureHostProperty.CpuManufacturer),
                    systemReader.Read(AzureHostProperty.CpuMicrocodeUpdateStatus),
                    systemReader.Read(AzureHostProperty.PreviousCpuMicrocodeVersion),
                    systemReader.Read(AzureHostProperty.CpuProcessorNameString),
                    systemReader.Read(AzureHostProperty.UpdatedCpuMicrocodeVersion),
                    patrolScrubMsrValue,
                    jccMsrRegisterValue));
            }
            catch (Exception exc)
            {
                // We capture as much as we can but we do not want a failure in any one system 
                // information read to cause other information to be lost.
                errors.Add(exc);
            }
        }

        private void AddHostOsInfo(EventContext telemetryContext, List<Exception> errors)
        {
            try
            {
                ISystemPropertyReader systemReader = this.systemManager.PropertyReader;

                telemetryContext.AddContext("osInfo", new HostOsInfo(
                   systemReader.Read(AzureHostProperty.OsWinNtBuildLabEx),
                   systemReader.Read(AzureHostProperty.OsWinNtCurrentBuildNumber),
                   systemReader.Read(AzureHostProperty.OsWinNtReleaseId),
                   systemReader.Read(AzureHostProperty.OSWinNtUBR),
                   systemReader.Read(AzureHostProperty.OsWinNtProductName),
                   systemReader.Read(AzureHostProperty.OsWinAzBuildLabEx),
                   systemReader.Read(AzureHostProperty.CloudCoreBuild),
                   systemReader.Read(AzureHostProperty.CloudCoreSupportBuild)));
            }
            catch (Exception exc)
            {
                // We capture as much as we can but we do not want a failure in any one system 
                // information read to cause other information to be lost.
                errors.Add(exc);
            }
        }

        private void AddNodeInfo(EventContext telemetryContext, List<Exception> errors)
        {
            try
            {
                ISystemPropertyReader systemReader = this.systemManager.PropertyReader;

                telemetryContext.AddContext("nodeInfo", new NodeIdentifier(
                    systemReader.Read(AzureHostProperty.TipSessionId),
                    systemReader.Read(AzureHostProperty.NodeId),
                    systemReader.Read(AzureHostProperty.ClusterName)));
            }
            catch (Exception exc)
            {
                // We capture as much as we can but we do not want a failure in any one system 
                // information read to cause other information to be lost.
                errors.Add(exc);
            }
        }
    }
}
