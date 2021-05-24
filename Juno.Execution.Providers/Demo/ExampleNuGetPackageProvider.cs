namespace Juno.Execution.Providers.Demo
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Example provider to illustrate installing the Juno Guest agent. Note that this provider
    /// is for example only and does not actually install the agent on any VMs.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.Dependency, SupportedStepTarget.ExecuteOnVirtualMachine)]
    [SupportedParameter(Name = Parameters.FeedUri, Type = typeof(Uri), Required = true)]
    [SupportedParameter(Name = Parameters.PackageName, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.PackageVersion, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.PersonalAccessToken, Type = typeof(string))]
    public class ExampleNuGetPackageProvider : ExperimentProvider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExampleInstallGuestAgentProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public ExampleNuGetPackageProvider(IServiceCollection services)
            : base(services)
        {
        }

        /// <inheritdoc />
        protected override Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            return Task.FromResult(new ExecutionResult(ExecutionStatus.Succeeded));
        }

        private class Parameters
        {
            internal const string FeedUri = "feedUri";
            internal const string PackageName = "packageName";
            internal const string PackageVersion = "packageVersion";
            internal const string PersonalAccessToken = "personalAccessToken";
        }
    }
}
