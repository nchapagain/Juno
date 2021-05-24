namespace Juno.Providers.Dependencies
{
    using Juno.Contracts;
    using Juno.Providers;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// A mock <see cref="IExperimentProvider"/> implementation of an experiment
    /// dependency provider.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.Dependency, SupportedStepTarget.ExecuteOnVirtualMachine)]
    public class ExampleDependencyProvider : MockExperimentProvider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExampleDependencyProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public ExampleDependencyProvider(IServiceCollection services)
            : base(services)
        {
        }
    }
}
