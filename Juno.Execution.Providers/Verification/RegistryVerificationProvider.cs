namespace Juno.Execution.Providers.Verification
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Execution.AgentRuntime.Windows;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Registry verification provider.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.Payload, SupportedStepTarget.ExecuteOnNode)]
    [SupportedParameter(Name = Parameters.KeyName, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.ValueName, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.Type, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.ExpectedValue, Type = typeof(string), Required = true)]
    public class RegistryVerificationProvider : ExperimentProvider
    {
        /// <summary>
        /// Initialize a new instance of <see cref="RegistryVerificationProvider"/>
        /// </summary>
        /// <param name="services">Collection of services used for dependency injection.</param>
        public RegistryVerificationProvider(IServiceCollection services)
            : base(services)
        {
        }

        /// <inheritdoc />
        public override Task ConfigureServicesAsync(ExperimentContext context, ExperimentComponent component)
        {
            if (!this.Services.HasService<IRegistryHelper>())
            {
                this.Services.AddTransient<IRegistryHelper>((provider) => new RegistryHelper(this.Services.TryGetService<IRegistry>(out IRegistry registry) ? registry : new WindowsRegistry()));
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Verifies the registry value on the machine matches the value supplied.
        /// </summary>
        protected override Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(new ExecutionResult(ExecutionStatus.Cancelled));
            }

            component.ThrowIfNull(nameof(component));
            string keyName = component.Parameters.GetValue<string>(Parameters.KeyName);
            string valueName = component.Parameters.GetValue<string>(Parameters.ValueName);
            Type type = Type.GetType(component.Parameters.GetValue<string>(Parameters.Type)) ?? typeof(string);
            string expectedValue = component.Parameters.GetValue<string>(Parameters.ExpectedValue);

            IRegistryHelper helper = this.Services.GetService<IRegistryHelper>();
            string actualValue = helper.ReadRegistryKeyByType(keyName, valueName, type);

            telemetryContext.AddContext(nameof(actualValue), actualValue);

            if (!expectedValue.Equals(actualValue, StringComparison.OrdinalIgnoreCase))
            {
                throw new ProviderException($"Registry values did not match. Expected registry value: {expectedValue}. Actual registry value: {actualValue}");
            }

            return Task.FromResult(new ExecutionResult(ExecutionStatus.Succeeded));
        }

        private static class Parameters
        {
            public const string KeyName = nameof(Parameters.KeyName);
            public const string ValueName = nameof(Parameters.ValueName);
            public const string Type = nameof(Parameters.Type);
            public const string ExpectedValue = nameof(Parameters.ExpectedValue);
        }
    }
}
