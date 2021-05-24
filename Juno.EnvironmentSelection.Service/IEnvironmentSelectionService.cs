namespace Juno.EnvironmentSelection.Service
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;

    /// <summary>
    /// Interface that exposes methods for environment selection
    /// </summary>
    public interface IEnvironmentSelectionService
    {
        /// <summary>
        /// Retrieves the set of Nodes that satisfy the given filters
        /// </summary>
        /// <param name="query">List of filters that include attributes the nodes must have</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns>A list of nodes that satisfy the requirements defined by the filters.</returns>
        Task<IEnumerable<EnvironmentCandidate>> GetNodesAsync(EnvironmentQuery query, CancellationToken cancellationToken);

        /// <summary>
        /// Retrieves the set of Environments that satsify the given filters. 
        /// </summary>
        /// <param name="query">The list of filters to apply to the set of Environments</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns></returns>
        Task<IEnumerable<EnvironmentCandidate>> GetEnvironmentCandidatesAsync(EnvironmentQuery query, CancellationToken cancellationToken);

        /// <summary>
        /// Extends the TTL of the node in the cache, telling ESS that the node is being used for a TiP node.
        /// </summary>
        /// <param name="reservedCandidates">List of candidates to reserve.</param>
        /// <param name="reservationDuration">Duration in which to keep candidates reserved for.</param>
        /// <param name="cancellationToken">Token used for cancelling current thread of execution.</param>
        /// <returns><see cref="ReservedNodes" /> that were created.</returns>
        Task<IEnumerable<EnvironmentCandidate>> ReserveEnvironmentCandidatesAsync(IEnumerable<EnvironmentCandidate> reservedCandidates, TimeSpan reservationDuration, CancellationToken cancellationToken);

        /// <summary>
        /// Removed the node from the ESS cache, telling ESS that the node is not being used anymore.
        /// </summary>
        /// <param name="reservedCandidates">List of candidates to removed from the reserved list.</param>
        /// <param name="cancellationToken">Token used for cancelling current thread of execution.</param>
        /// <returns><see cref="ReservedNodes" /> that were deleted.</returns>
        Task<IEnumerable<EnvironmentCandidate>> DeleteReservationsAsync(IEnumerable<EnvironmentCandidate> reservedCandidates, CancellationToken cancellationToken);
    }
}
