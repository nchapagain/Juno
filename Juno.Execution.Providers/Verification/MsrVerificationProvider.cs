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
    /// MSR verification provider.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.Payload, SupportedStepTarget.ExecuteOnNode)]
    [SupportedParameter(Name = Parameters.MsrRegister, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.ExpectedMsrValue, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.CpuNumber, Type = typeof(string), Required = false)]
    [ProviderInfo(Name = "Verify MSR", Description = "Verify that user provided MSR address exists on the host and verify that its value matches the expected value.")]
    public class MsrVerificationProvider : ExperimentProvider
    {
        /// <summary>
        /// Initialize a new instance of <see cref="MsrVerificationProvider"/>
        /// </summary>
        /// <param name="services">Collection of services used for dependency injection.</param>
        public MsrVerificationProvider(IServiceCollection services)
            : base(services)
        {
        }

        /// <inheritdoc/>
        public override Task ConfigureServicesAsync(ExperimentContext context, ExperimentComponent component)
        {
            if (!this.Services.HasService<IMsr>())
            {
                this.Services.AddTransient<IMsr>((provider) => new Msr());
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Compares the Model specifc register with the register that is on the machine with the register supplied.
        /// </summary>
        protected override Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(new ExecutionResult(ExecutionStatus.Cancelled));
            }

            component.ThrowIfNull(nameof(component));

            IMsr msr = this.Services.GetService<IMsr>();
            string msrRegister = component.Parameters.GetValue<string>(Parameters.MsrRegister);
            string expectedMsrValue = component.Parameters.GetValue<string>(Parameters.ExpectedMsrValue);
            string cpuNumber = component.Parameters.GetValue<string>(Parameters.CpuNumber, "0x00");

            string actualMsr = msr.Read(msrRegister, cpuNumber);
            if (!expectedMsrValue.Equals(actualMsr, StringComparison.OrdinalIgnoreCase))
            {
                throw new ProviderException($"Register values do not match. Expected register value: {expectedMsrValue}. Actual register value: {actualMsr}"); 
            }

            return Task.FromResult(new ExecutionResult(ExecutionStatus.Succeeded));
        }

        private static class Parameters
        {
            public const string MsrRegister = nameof(Parameters.MsrRegister);
            public const string CpuNumber = nameof(Parameters.CpuNumber);
            public const string ExpectedMsrValue = nameof(Parameters.ExpectedMsrValue);
        }
    }
}
