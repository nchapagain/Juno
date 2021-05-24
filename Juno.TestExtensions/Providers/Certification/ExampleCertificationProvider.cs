namespace Juno.Providers.Certification
{
    using Juno.Contracts;
    using Juno.Providers;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// A mock <see cref="IExperimentProvider"/> implementation of an experiment
    /// dependency provider.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.Certification, SupportedStepTarget.ExecuteOnNode)]
    public class ExampleCertificationProvider : MockExperimentProvider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExampleCertificationProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public ExampleCertificationProvider(IServiceCollection services)
            : base(services)
        {
        }
    }
}
