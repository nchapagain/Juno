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
    /// Provider to check the FPGA properties on the machine.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.Payload, SupportedStepTarget.ExecuteOnNode)]
    [SupportedParameter(Name = Parameters.BoardName, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.RoleId, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.RoleVersion, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.IsGolden, Type = typeof(bool), Required = true)]
    public class FpgaVerificationProvider : ExperimentProvider
    {
        /// <summary>
        /// Initialize a new instance of <see cref="FpgaVerificationProvider"/>
        /// </summary>
        /// <param name="services">Collection of services used for dependency injection.</param>
        public FpgaVerificationProvider(IServiceCollection services)
            : base(services)
        {
        }

        /// <inheritdoc/>
        public override Task ConfigureServicesAsync(ExperimentContext context, ExperimentComponent component)
        {
            if (!this.Services.HasService<IFirmwareReader<FpgaHealth>>())
            {
                this.Services.AddTransient<IFirmwareReader<FpgaHealth>>((provider) => new FpgaReader());
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Verifies the FPGA configuration on machine against the configuration supplied.
        /// </summary>
        protected override Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(new ExecutionResult(ExecutionStatus.Cancelled));
            }

            component.ThrowIfNull(nameof(component));
            IFirmwareReader<FpgaHealth> reader = this.Services.GetService<IFirmwareReader<FpgaHealth>>();
            FpgaHealth info = reader.Read();

            telemetryContext.AddContext(nameof(info), info);

            string boardName = component.Parameters.GetValue<string>(Parameters.BoardName);
            string roleId = component.Parameters.GetValue<string>(Parameters.RoleId);
            string roleVersion = component.Parameters.GetValue<string>(Parameters.RoleVersion);
            bool isGolden = component.Parameters.GetValue<bool>(Parameters.IsGolden);

            bool verification = info.FPGAConfig.BoardName.Equals(boardName, StringComparison.OrdinalIgnoreCase) && info.FPGAConfig.RoleID.Equals(roleId, StringComparison.OrdinalIgnoreCase)
                && info.FPGAConfig.RoleVersion.Equals(roleVersion, StringComparison.OrdinalIgnoreCase) && info.FPGAConfig.IsGolden == isGolden;

            if (!verification)
            {
                throw new ProviderException($"Attributes did not match. Expected board name: {boardName}. Actual board name {info.FPGAConfig.BoardName}. Expected Role ID: {roleId}. Actual Role ID: {info.FPGAConfig.RoleID}" +
                    $"Expected Role Version: {roleVersion}. Actual Role Version: {info.FPGAConfig.RoleVersion}. Expected IsGolden: {isGolden}. Actual IsGolden: {info.FPGAConfig.IsGolden}");
            }

            return Task.FromResult(new ExecutionResult(ExecutionStatus.Succeeded));
        }

        private static class Parameters
        {
            public const string BoardName = nameof(Parameters.BoardName);
            public const string RoleId = nameof(Parameters.RoleId);
            public const string RoleVersion = nameof(Parameters.RoleVersion);
            public const string IsGolden = nameof(Parameters.IsGolden);
        }
    }
}
