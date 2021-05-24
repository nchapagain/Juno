namespace Juno.Execution.Providers.Watchdog
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Execution.AgentRuntime;
    using Juno.Extensions.Telemetry;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// This provider checks if an expected device is present on the VM. This is useful for validating accelerated networking
    /// or other scenarios that rely on specialized devices.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.Watchdog, SupportedStepTarget.ExecuteOnVirtualMachine)]
    [SupportedParameter(Name = Parameters.DeviceName, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.DeviceClass, Type = typeof(string), Required = true)]
    [ProviderInfo(Name = "Validate System Device", Description = "Validates the correct functional readiness of a hardware device on the nodes/blades in the experiment group", FullDescription = "Step to validate the correct functional readiness of a hardware device on the nodes/blades in the experiment group.")]
    public class DeviceValidationProvider : ExperimentProvider
    {
        /// <summary>
        /// Default timeout for the device validation provider
        /// </summary>
        private TimeSpan defaultTimeout = TimeSpan.FromMinutes(5);

        private ISystemPropertyReader guestProperties;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceValidationProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public DeviceValidationProvider(IServiceCollection services)
            : base(services)
        {
            this.guestProperties = SystemManagerFactory.Get().PropertyReader;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceValidationProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        /// <param name="guestProperties">Reader for guest properties</param>
        public DeviceValidationProvider(IServiceCollection services, ISystemPropertyReader guestProperties = null)
            : base(services)
        {
            this.guestProperties = guestProperties ?? SystemManagerFactory.Get().PropertyReader;
        }

        /// <inheritdoc />
        protected override async Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            ExecutionResult result = new ExecutionResult(ExecutionStatus.InProgress);

            if (!cancellationToken.IsCancellationRequested)
            {
                var state = await this.GetStateAsync<DeviceValidationProviderState>(context, cancellationToken, sharedState: false).ConfigureDefaults()
                    ?? new DeviceValidationProviderState()
                {
                    StepInitializationTime = DateTime.UtcNow
                };

                // if the step has timedout, just return failure
                if (this.HasStepTimedOut(component, state))
                {
                    throw new ProviderException("The step has timed out before the geneva config could be successfully updated", ErrorReason.Timeout);
                }

                if (await this.CheckDeviceExistsAsync(component, telemetryContext, cancellationToken).ConfigureDefaults())
                {
                    result = new ExecutionResult(ExecutionStatus.Succeeded);
                }
                else
                {
                    await this.SaveStateAsync(context, state, cancellationToken, sharedState: false).ConfigureDefaults();
                    result = new ExecutionResult(ExecutionStatus.Failed);
                }

            }

            return result;
        }

        internal bool HasStepTimedOut(ExperimentComponent component, DeviceValidationProviderState state)
        {
            TimeSpan timeoutValue = this.defaultTimeout;
            if (component.Parameters?.ContainsKey(StepParameters.Timeout) == true)
            {
                timeoutValue = TimeSpan.Parse(component.Parameters.GetValue<string>(StepParameters.Timeout));
            }

            if (state.StepInitializationTime.Add(timeoutValue) <= DateTime.UtcNow)
            {
                return true;
            }

            return false;
        }

        internal Task<bool> CheckDeviceExistsAsync(ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            EventContext relatedContext = telemetryContext.Clone();
            var deviceExists = false;
            var deviceName = component.Parameters.GetValue<string>(nameof(Parameters.DeviceName));
            var deviceClass = component.Parameters.GetValue<string>(nameof(Parameters.DeviceClass));
            return this.Logger.LogTelemetryAsync($"{nameof(DeviceValidationProvider)}.CheckDeviceExists", relatedContext, () =>
            {
                if (!cancellationToken.IsCancellationRequested)
                {                   
                    var devices = this.guestProperties.ReadDevices(deviceClass);
                    if (devices.Contains(deviceName))
                    {
                        deviceExists = true;
                    }
                }

                return Task.FromResult(deviceExists);
            });
        }

        internal class DeviceValidationProviderState
        {
            public DateTime StepInitializationTime { get; set; }
        }

        /// <summary>
        /// Parameters class defines the keys that are expected from the user
        /// </summary>
        internal class Parameters
        {
            internal const string DeviceName = "deviceName";
            internal const string DeviceClass = "deviceClass";
        }

    }
}
