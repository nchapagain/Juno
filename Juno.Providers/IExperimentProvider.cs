namespace Juno.Providers
{
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Provides methods for handling the runtime requirements of an experiment
    /// workflow step/execution component.
    /// </summary>
    public interface IExperimentProvider
    {
        /// <summary>
        /// Gets the service provider/locator for the experiment provider.
        /// </summary>
        IServiceCollection Services { get; }

        /// <summary>
        /// When overridden in derived classes enables the provider to configure or adds any additional
        /// required services for the provider to the services collection.
        /// </summary>
        /// <param name="context">
        /// Provides context information about the experiment within which the provider is running.
        /// </param>
        /// <param name="component">The experiment component definition for which the provider is associated.</param>
        Task ConfigureServicesAsync(ExperimentContext context, ExperimentComponent component);

        /// <summary>
        /// Executes the logic required to handle the runtime requirements of the
        /// experiment workflow step/execution component.
        /// </summary>
        /// <param name="context">The experiment context.</param>
        /// <param name="component">The experiment component that describes the runtime requirements.</param>
        /// <param name="cancellationToken">A token that can be used to request the provider cancel its operations.</param>
        /// <returns>
        /// A task that can be used to execute an experiment workflow step
        /// asynchronously.
        /// </returns>
        Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, CancellationToken cancellationToken);
    }
}
