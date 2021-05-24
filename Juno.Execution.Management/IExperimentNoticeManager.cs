namespace Juno.Execution.Management
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;

    /// <summary>
    /// Provides methods to interact with the Juno system to get notices of work.
    /// </summary>
    public interface IExperimentNoticeManager
    {
        /// <summary>
        /// Deletes the work notice from the pool.
        /// </summary>
        /// <param name="workNotice">The work notice to delete/remove from the pool.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        Task DeleteWorkNoticeAsync(ExperimentMetadataInstance workNotice, CancellationToken cancellationToken);

        /// <summary>
        /// Gets a notice of work for experiments in progress.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        Task<ExperimentMetadataInstance> GetWorkNoticeAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Creates a new notice based on the original notice.
        /// </summary>
        /// <param name="workNotice">The original work notice retrieved from the Juno system.</param>
        /// <param name="visibilityDelay">
        /// A time span that represents the amount of time the notice should remain hidden before it can be picked up again.
        /// </param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        Task SetWorkNoticeVisibilityAsync(ExperimentMetadataInstance workNotice, TimeSpan visibilityDelay, CancellationToken cancellationToken);
    }
}