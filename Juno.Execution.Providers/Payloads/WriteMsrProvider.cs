namespace Juno.Execution.Providers.Payloads
{
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Execution.AgentRuntime.Windows;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Provider that can write a value to a model-specific register (MSR).
    /// </summary>
    [ExecutionConstraints(SupportedStepType.Payload, SupportedStepTarget.ExecuteOnNode)]
    [SupportedParameter(Name = Parameters.MsrRegister, Type = typeof(string), Required = true)]    
    [SupportedParameter(Name = Parameters.Value, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.ProcessorCount, Type = typeof(string), Required = false)]
    [ProviderInfo(Name = "Write a value to an MSR", Description = "Write a value to a model-specific register on physical cores of the CPU.")]
    public class WriteMsrProvider : ExperimentProvider
    {
        private const int DefaultProcessorCount = 1;

        /// <summary>
        /// Initializes a new instance of <see cref="WriteMsrProvider"/>
        /// </summary>
        public WriteMsrProvider(IServiceCollection services)
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
        /// Writes a value to a model-specific register.
        /// </summary>
        protected override Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(new ExecutionResult(ExecutionStatus.Cancelled));
            }

            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            IMsr msr = this.Services.GetService<IMsr>();
            string msrRegister = component.Parameters.GetValue<string>(Parameters.MsrRegister);
            string value = component.Parameters.GetValue<string>(Parameters.Value);
            int processorCount = component.Parameters.GetValue<int>(Parameters.ProcessorCount, WriteMsrProvider.DefaultProcessorCount);

            for (int i = 0; i < processorCount; i++)
            {
                msr.Write(msrRegister, i.ToString(), value);
            }

            return Task.FromResult(new ExecutionResult(ExecutionStatus.Succeeded));
        }

        private static class Parameters
        {
            public const string MsrRegister = nameof(Parameters.MsrRegister);
            public const string ProcessorCount = nameof(Parameters.ProcessorCount);
            public const string Value = nameof(Parameters.Value);
        }
    }
}
