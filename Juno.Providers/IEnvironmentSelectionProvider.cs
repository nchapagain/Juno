namespace Juno.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Interface for providers that select resources with constraints
    /// or filters.
    /// </summary>
    public interface IEnvironmentSelectionProvider
    {
        /// <summary>
        /// A list of services used for dependency injection
        /// </summary>
        IServiceCollection Services { get; }

        /// <summary>
        /// Logger interface used for capturing telemetry
        /// </summary>
        ILogger Logger { get; }

        /// <summary>
        /// Current execution Context
        /// </summary>
        IConfiguration Configuration { get; }

        /// <summary>
        /// Time to live in cache
        /// </summary>
        TimeSpan TTL { get; }

        /// <summary>
        /// Configure all services neccessary for successful completion of filter execution
        /// </summary>
        /// <returns>An awaitable task</returns>
        Task ConfigureServicesAsync();

        /// <summary>
        /// Returns environment entities (e.g. racks, nodes) that match the criteria of the filter.
        /// </summary>
        /// <param name="filter">Filter on a set of resources</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns>
        /// A dictionary whose keys are determined by the provider, and the value is an eligible
        /// environment candidate
        /// </returns>
        Task<IDictionary<string, EnvironmentCandidate>> ExecuteAsync(EnvironmentFilter filter, CancellationToken cancellationToken);
    }
}
