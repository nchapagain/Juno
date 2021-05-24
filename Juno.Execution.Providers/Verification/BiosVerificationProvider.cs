namespace Juno.Execution.Providers.Verification
{
    using System;
    using System.Text.RegularExpressions;
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
    /// Provider to check the activated BIOS version.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.Payload, SupportedStepTarget.ExecuteOnNode)]
    [SupportedParameter(Name = Parameters.BiosVersion, Type = typeof(string), Required = true)]
    [ProviderInfo(Name = "Verify BIOS version", Description = "Verify desired BIOS version is installed on the physical nodes/blades in the experiment group", FullDescription = "Step to verify desired BIOS version is installed on the physical nodes/blades in the experiment group.")]
    public class BiosVerificationProvider : ExperimentProvider
    {
        private const int PartCount = 4;

        /// <summary>
        /// Initialize a new instance of <see cref="BiosVerificationProvider"/>
        /// </summary>
        /// <param name="services">Collection of services used for dependency injection.</param>
        public BiosVerificationProvider(IServiceCollection services)
            : base(services)
        {
        }

        /// <inheritdoc/>
        public override Task ConfigureServicesAsync(ExperimentContext context, ExperimentComponent component)
        {
            if (!this.Services.HasService<IFirmwareReader<BiosInfo>>())
            {
                this.Services.AddTransient((provider) => new BiosReader());
            }

            return base.ConfigureServicesAsync(context, component);
        }

        /// <summary>
        /// Verifies the bios version from the machine is the same as the bios version
        /// that was supplied by the parameters.
        /// </summary>
        protected override Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(new ExecutionResult(ExecutionStatus.Cancelled));
            }

            component.ThrowIfNull(nameof(component));
            IFirmwareReader<BiosInfo> reader = this.Services.GetService<IFirmwareReader<BiosInfo>>();
            BiosInfo info = reader.Read();

            string actualVersion = info.BiosVersion;
            string expectedVersion = component.Parameters.GetValue<string>(Parameters.BiosVersion);
            telemetryContext.AddContext(nameof(actualVersion), actualVersion);
            telemetryContext.AddContext(nameof(expectedVersion), expectedVersion);

            bool verified = true;
            string[] expectedParts = expectedVersion.Split('.');
            string[] actualParts = actualVersion.Split('.');
            for (int i = 0; i < BiosVerificationProvider.PartCount - 1; i++)
            {
                verified = verified && expectedParts[i].Equals(actualParts[i], StringComparison.OrdinalIgnoreCase);
            }

            int expectedLastPart = int.Parse(Regex.Match(expectedParts[BiosVerificationProvider.PartCount - 1], @"\d+").Value);
            int actualLastPart = int.Parse(Regex.Match(actualParts[BiosVerificationProvider.PartCount - 1], @"\d+").Value);
            verified = verified && expectedLastPart == actualLastPart;

            if (!verified)
            {
                throw new ProviderException($"Versions did not match. Expected version: {expectedVersion}. Actual version: {actualVersion}");
            }

            return Task.FromResult(new ExecutionResult(ExecutionStatus.Succeeded));
        }

        private static class Parameters
        {
            public const string BiosVersion = nameof(Parameters.BiosVersion);
        }
    }
}
