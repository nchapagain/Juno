namespace Juno.Providers
{
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// A mock <see cref="IExperimentProvider"/> implementation of an
    /// experiment workflow workload.
    /// </summary>
    public abstract class MockExperimentProvider : IExperimentProvider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MockExperimentProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        protected MockExperimentProvider(IServiceCollection services)
        {
            services.ThrowIfNull(nameof(services));
            this.Services = services;
        }

        /// <summary>
        /// Gets the service provider/locator for the experiment provider.
        /// </summary>
        public IServiceCollection Services { get; }

        /// <summary>
        /// Executes the mock services configuration operation.
        /// </summary>
        public Task ConfigureServicesAsync(ExperimentContext context, ExperimentComponent component)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Executes a mock provider operation.
        /// </summary>
        public virtual Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, CancellationToken cancellationToken)
        {
            return Task.FromResult(new ExecutionResult(ExecutionStatus.Succeeded));
        }
    }
}
