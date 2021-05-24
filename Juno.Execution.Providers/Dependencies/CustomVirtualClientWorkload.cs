namespace Juno.Execution.Providers.Dependencies
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Execution.ArmIntegration;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Provider is used to inject a custom VirtualClient workload configuration to a target VC location.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.Dependency, SupportedStepTarget.ExecuteOnVirtualMachine)]
    [SupportedParameter(Name = StepParameters.Platform, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.WorkloadContentName, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.WorkloadFileName, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.PackageVersion, Type = typeof(string), Required = true)]
    [ProviderInfo(Name = "Inject a custom VirtualClient workload configuration.", Description = "Inject a custom VirtualClient workload configuration.")]
    public class CustomVirtualClientWorkload : ExperimentProvider
    {
        private const int Timeout = 600;

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomVirtualClientWorkload"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public CustomVirtualClientWorkload(IServiceCollection services)
            : base(services)
        {
        }

        /// <inheritdoc/>
        protected override Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));

            ExecutionResult result = new ExecutionResult(ExecutionStatus.Succeeded);

            string platform = component.Parameters.GetValue<string>(StepParameters.Platform, string.Empty);
            string packageVersion = component.Parameters.GetValue<string>(StepParameters.PackageVersion, string.Empty);
            string workloadContentName = component.Parameters.GetValue<string>(Parameters.WorkloadContentName, string.Empty);
            string workloadFileName = component.Parameters.GetValue<string>(Parameters.WorkloadFileName, string.Empty);
            string workloadProfileLocation;
            if (platform.ToLowerInvariant() == VmPlatform.LinuxX64.ToLowerInvariant() || platform.ToLowerInvariant() == VmPlatform.LinuxArm64.ToLowerInvariant())
            {
                workloadProfileLocation = $"{{NuGetPackagePath}}/virtualclient/{packageVersion}/content/{platform}/WorkloadProfiles";
            }
            else
            {
                workloadProfileLocation = $"{{NuGetPackagePath}}\\virtualclient\\{packageVersion}\\content\\{platform}\\WorkloadProfiles";
            }

            string workloadProfileLocationFullPath = Path.Combine(DependencyPaths.ReplacePathReferences(workloadProfileLocation));
            string workloadProfileContent = Properties.Resources.ResourceManager.GetObject(workloadContentName)?.ToString();

            int n = 0;
            while (!Directory.Exists(workloadProfileLocationFullPath) && n < CustomVirtualClientWorkload.Timeout) 
            { 
                Thread.Sleep(1000);
                n += 1;
            }

            File.WriteAllText(Path.Combine(workloadProfileLocationFullPath, workloadFileName), workloadProfileContent);

            return Task.FromResult(result);
        }

        private class Parameters
        {
            internal const string WorkloadFileName = "workloadFileName";
            internal const string WorkloadContentName = "workloadContentName";
            internal const string PackageVersion = "packageVersion";
        }
    }
}
