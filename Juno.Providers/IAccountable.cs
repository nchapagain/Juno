namespace Juno.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;

    /// <summary>
    /// Interface that allows external systems to call back to ESS
    /// </summary>
    public interface IAccountable
    {
        /// <summary>
        /// Reserve candidates
        /// </summary>
        /// <param name="candidate">Candidate to reserve</param>
        /// <param name="reservationDuration">TimeSpan that defines the reservation duration.</param>
        /// <param name="cancellationToken">Token used for cancelling current thread of execution.</param>
        /// <returns>True/False if the candidate was reserved successfully</returns>
        Task<bool> ReserveCandidateAsync(EnvironmentCandidate candidate, TimeSpan reservationDuration, CancellationToken cancellationToken);

        /// <summary>
        /// Delete reservation
        /// </summary>
        /// <param name="candidate">Candidate to delete</param>
        /// <param name="cancellationToken">Token used for cancelling current thread of execution.</param>
        /// <returns>True/False if the candidate was deleted successfully</returns>
        Task<bool> DeleteReservationAsync(EnvironmentCandidate candidate, CancellationToken cancellationToken);
    }
}
