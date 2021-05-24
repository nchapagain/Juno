namespace Juno.Execution.Providers.Payloads
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
    /// Append value to a user provided registry key and value name
    /// </summary>
    [ExecutionConstraints(SupportedStepType.Payload, SupportedStepTarget.ExecuteOnNode)]
    [SupportedParameter(Name = Parameters.KeyName, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.ValueName, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.Value, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.Type, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.ShouldAppend, Type = typeof(bool), Required = false)]
    [ProviderInfo(Name = "Append to Registry", Description = "Append value to a user provided registry key and value name.")]
    public class SetRegistryKeyProvider : ExperimentProvider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SetRegistryKeyProvider"/> class.
        /// </summary>
        /// <param name="services"></param>
        public SetRegistryKeyProvider(IServiceCollection services)
            : base(services)
        {
        }

        /// <inheritdoc/>
        public override Task ConfigureServicesAsync(ExperimentContext context, ExperimentComponent component)
        {
            IRegistry registry;
            if (!this.Services.TryGetService<IRegistry>(out registry))
            {
                registry = new WindowsRegistry();
                this.Services.AddTransient<IRegistry>((provider) => registry);
            }

            IRegistryHelper registryHelper;
            if (!this.Services.TryGetService<IRegistryHelper>(out registryHelper))
            {
                registryHelper = new RegistryHelper(registry);
                this.Services.AddTransient<IRegistryHelper>((provider) => registryHelper);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        protected override Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            ExecutionResult result = new ExecutionResult(ExecutionStatus.InProgress);
            string keyName = component.Parameters.GetValue<string>(Parameters.KeyName);
            string valueName = component.Parameters.GetValue<string>(Parameters.ValueName);
            Type type = Type.GetType(component.Parameters.GetValue<string>(Parameters.Type)) ?? typeof(string);
            string value = component.Parameters.GetValue<string>(Parameters.Value);
            bool shouldAppend = component.Parameters.GetValue<bool>(Parameters.ShouldAppend, false);
            var registryHelper = this.Services.GetService<IRegistryHelper>();
            string registryValue = registryHelper.ReadRegistryKeyByType(keyName, valueName, type);
            telemetryContext.AddContext("registryValueBefore", registryValue);
            try
            {
                if (shouldAppend && type.Name == nameof(String) && !registryValue.Contains(value, StringComparison.OrdinalIgnoreCase))
                {
                    registryHelper.AppendToRegistryKey(keyName, valueName, value);
                }
                else
                {
                    registryHelper.WriteToRegistryKeyByType(keyName, valueName, value, type);
                }
                
                result = new ExecutionResult(ExecutionStatus.Succeeded);
            }
            catch (Exception e)
            {
                throw new ProviderException($"Failed to append to registry.", ErrorReason.ProviderStateInvalid, e);
            }

            registryValue = registryHelper.ReadRegistryKeyByType(keyName, valueName, type);
            telemetryContext.AddContext("registryValueAfter", registryValue);

            return Task.FromResult(result);  
        }

        internal static class Parameters
        {
            internal const string KeyName = "keyName";
            internal const string ValueName = "valueName";
            internal const string Value = "value";
            internal const string Type = "type";
            internal const string ShouldAppend = "shouldAppend";
        }
    }
}
