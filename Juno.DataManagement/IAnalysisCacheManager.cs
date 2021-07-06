namespace Juno.DataManagement
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;

    /// <summary>
    /// Provides methods to manage analysis cache.
    /// </summary>
    public interface IAnalysisCacheManager
    {
        /// <summary>
        /// Returns a collection of Business Signals for Juno experiments.
        /// </summary>
        /// <param name="query">A Cosmos SQL API query to retrieve Business Signals for Juno experiments.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// A collection of <see cref="BusinessSignal"/> Business Signals for Juno experiments.
        /// </returns>
        Task<IEnumerable<BusinessSignal>> GetBusinessSignalsAsync(string query, CancellationToken cancellationToken);

        /// <summary>
        /// Returns a collection of progress indicators for Juno experiments.
        /// </summary>
        /// <param name="query">A Cosmos SQL API query to retrieve progress indicators for Juno experiments.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// A collection of <see cref="ExperimentProgress"/> progress indicators for Juno experiments.
        /// </returns>
        Task<IEnumerable<ExperimentProgress>> GetExperimentsProgressAsync(string query, CancellationToken cancellationToken);
    }
}