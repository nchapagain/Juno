namespace Juno.Providers.Environment
{
    using Juno.Contracts;
    using Juno.Providers;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// A mock <see cref="IExperimentProvider"/> implementation of an experiment
    /// environment setup provider.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.EnvironmentCleanup, SupportedStepTarget.ExecuteRemotely)]
    public class ExampleCleanupProvider : MockExperimentProvider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExampleCleanupProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public ExampleCleanupProvider(IServiceCollection services)
            : base(services)
        {
        }
    }
}
