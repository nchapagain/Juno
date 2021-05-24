namespace Juno.Providers.Workloads
{
    using Juno.Contracts;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// A mock <see cref="IExperimentProvider"/> implementation of an
    /// experiment workflow workload.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.Watchdog, SupportedStepTarget.ExecuteOnVirtualMachine)]
    public class ExampleWatchdogProvider : MockExperimentProvider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExampleWorkloadProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public ExampleWatchdogProvider(IServiceCollection services)
            : base(services)
        {
        }
    }
}
