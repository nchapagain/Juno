namespace Juno.Api.Client
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;

    /// <summary>
    /// Interface for Environment Client, allow for communication to ESS
    /// </summary>
    public interface IEnvironmentClient
    {
        /// <summary>
        /// Retrieve environment candidates from the system.
        /// </summary>
        /// <param name="query">filters to apply to the set of possible environments</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        Task<HttpResponseMessage> ReserveEnvironmentsAsync(EnvironmentQuery query, CancellationToken cancellationToken);

        /// <summary>
        /// Informs ESS to add the input list of nodes to its reserved list.
        /// </summary>
        /// <param name="reservedNodes">List of nodes to be marked as used.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        Task<HttpResponseMessage> CreateReservedNodesAsync(ReservedNodes reservedNodes, CancellationToken cancellationToken);

        /// <summary>
        /// Informs ESS to remove the input list of nodes from its reserved list.
        /// </summary>
        /// <param name="reservedNodes">List of nodes to be marked as used.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        Task<HttpResponseMessage> DeleteReservedNodesAsync(ReservedNodes reservedNodes, CancellationToken cancellationToken);
    }
}
