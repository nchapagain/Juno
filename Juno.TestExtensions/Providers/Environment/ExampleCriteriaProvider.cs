namespace Juno.Providers.Environment
{
    using Juno.Contracts;
    using Juno.Providers;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// A mock <see cref="IExperimentProvider"/> implementation of an experiment
    /// environment selection/criteria provider.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.EnvironmentCriteria, SupportedStepTarget.ExecuteRemotely)]
    public class ExampleCriteriaProvider : MockExperimentProvider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExampleCriteriaProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public ExampleCriteriaProvider(IServiceCollection services)
            : base(services)
        {
        }
    }
}
