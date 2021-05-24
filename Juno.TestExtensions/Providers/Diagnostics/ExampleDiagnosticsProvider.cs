namespace Juno.Providers.Diagnostics
{
    using Juno.Contracts;
    using Juno.Providers;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// A mock <see cref="IExperimentProvider"/> implementation of an experiment
    /// environment setup provider.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.Diagnostics, SupportedStepTarget.ExecuteRemotely)]
    public class ExampleDiagnosticsProvider : MockExperimentProvider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExampleDiagnosticsProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public ExampleDiagnosticsProvider(IServiceCollection services)
            : base(services)
        {
        }
    }
}
