namespace Juno.Execution.Providers.Verification
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Execution.AgentRuntime.Contract;
    using Juno.Execution.AgentRuntime.Windows;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Verifies the SSD version on the machine
    /// </summary>
    [ExecutionConstraints(SupportedStepType.Payload, SupportedStepTarget.ExecuteOnNode)]
    [SupportedParameter(Name = Parameters.FirmwareVersion, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.TargetModel, Type = typeof(string), Required = true)]
    public class SsdVerificationProvider : ExperimentProvider
    {
        /// <summary>
        /// Initialize a new instance of <see cref="SsdVerificationProvider"/>
        /// </summary>
        /// <param name="services">Collection of services used for dependency injection.</param>
        public SsdVerificationProvider(IServiceCollection services)
            : base(services)
        {
        }

        /// <inheritdoc/>
        public override Task ConfigureServicesAsync(ExperimentContext context, ExperimentComponent component)
        {
            if (!this.Services.HasService<IFirmwareReader<IEnumerable<SsdInfo>>>())
            {
                this.Services.AddTransient<IFirmwareReader<IEnumerable<SsdInfo>>>((provider) => new SsdReader());
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Verifies the SSD version with the version on the machine with the version supplied in the parameters.
        /// </summary>
        protected override Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(new ExecutionResult(ExecutionStatus.Cancelled));
            }

            component.ThrowIfNull(nameof(component));
            IFirmwareReader<IEnumerable<SsdInfo>> reader = this.Services.GetService<IFirmwareReader<IEnumerable<SsdInfo>>>();
            IEnumerable<SsdInfo> ssdInfo = reader.Read();

            IList<string> expectedModels = component.Parameters[Parameters.TargetModel].ToString().ToList(',', ';');
            string expectedFirmware = component.Parameters[Parameters.FirmwareVersion].ToString();
            telemetryContext.AddContext(nameof(ssdInfo), ssdInfo.Select(s => new { Firmware = s.FirmwareVersion, ModelNumber = s.ModelName, Device = s.Device.Name }));

            IEnumerable<string> firmwareOfInterest = ssdInfo.Where(info => expectedModels.Contains(info.ModelName)).Select(info => info.FirmwareVersion);
            firmwareOfInterest.ThrowIfNullOrEmpty(nameof(firmwareOfInterest), $"Node does not contain any devices with model: {string.Join(", ", expectedModels)}");
            telemetryContext.AddContext(nameof(firmwareOfInterest), firmwareOfInterest);

            if (!firmwareOfInterest.All(f => f.Equals(expectedFirmware, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ProviderException($"Some drives do not have the correct firmware. Expected firmware: {expectedFirmware}. Actual firmwares: {string.Join(", ", firmwareOfInterest)}");
            }

            return Task.FromResult(new ExecutionResult(ExecutionStatus.Succeeded));
        }

        private class Parameters
        {
            internal const string FirmwareVersion = nameof(Parameters.FirmwareVersion);
            internal const string TargetModel = nameof(Parameters.TargetModel);
        }
    }
}
