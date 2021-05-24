namespace Juno.Execution.Providers.Verification
{
    using System;
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
    /// BMC verification provider.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.Payload, SupportedStepTarget.ExecuteOnNode)]
    [SupportedParameter(Name = Parameters.BmcVersion, Type = typeof(string), Required = true)]
    public class BmcVerificationProvider : ExperimentProvider
    {
        /// <summary>
        /// Initialize a new instance of <see cref="BmcVerificationProvider"/>
        /// </summary>
        /// <param name="services">Collection of services used for dependency injection.</param>
        public BmcVerificationProvider(IServiceCollection services)
            : base(services)
        {
        }

        /// <inheritdoc/>
        public override Task ConfigureServicesAsync(ExperimentContext context, ExperimentComponent component)
        {
            if (!this.Services.HasService<IFirmwareReader<BmcInfo>>())
            {
                this.Services.AddTransient<IFirmwareReader<BmcInfo>>((provider) => new BmcReader());
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Compares the BMC version supplied with the BMC version that is supplied on the node.
        /// </summary>
        protected override Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(new ExecutionResult(ExecutionStatus.Cancelled));
            }

            component.ThrowIfNull(nameof(component));
            IFirmwareReader<BmcInfo> reader = this.Services.GetService<IFirmwareReader<BmcInfo>>();
            string actualBmcVersion = reader.Read().Version;
            string expectedBmcVersion = component.Parameters.GetValue<string>(Parameters.BmcVersion);

            telemetryContext.AddContext(nameof(actualBmcVersion), actualBmcVersion);
            telemetryContext.AddContext(nameof(expectedBmcVersion), expectedBmcVersion);

            if (!expectedBmcVersion.Equals(actualBmcVersion, StringComparison.OrdinalIgnoreCase))
            {
                throw new ProviderException($"Versions did not match. Expected version: {expectedBmcVersion}. Actual version: {actualBmcVersion}");
            }

            return Task.FromResult(new ExecutionResult(ExecutionStatus.Succeeded));
        }

        private static class Parameters
        {
            public const string BmcVersion = nameof(Parameters.BmcVersion);
        }
    }
}
