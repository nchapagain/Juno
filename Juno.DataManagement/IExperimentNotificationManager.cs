namespace Juno.DataManagement
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;

    /// <summary>
    /// Provides methods for managing Juno experiment data operations.
    /// </summary>
    public interface IExperimentNotificationManager
    {
        /// <summary>
        /// Creates a new experiment/work notice.
        /// </summary>
        /// <param name="workQueue">The name of the notication queue.</param>
        /// <param name="notice">The notice to publish.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="visibilityDelay">
        /// An optional timeout that can be applied to have the message initially hidden/invisible for a period of time.
        /// </param>
        /// <returns>
        /// An <see cref="ExperimentMetadataInstance"/> created in relation to the experiment.
        /// </returns>
        Task<ExperimentMetadataInstance> CreateNoticeAsync(string workQueue, ExperimentMetadata notice, CancellationToken cancellationToken, TimeSpan? visibilityDelay = null);

        /// <summary>
        /// Deletes/dequeues the notice from the queue.
        /// </summary>
        /// <param name="workQueue">The name of the notication queue.</param>
        /// <param name="messageId">The messageId of the notice.</param>
        /// <param name="popReceipt">The popReceipt of the notice.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        Task DeleteNoticeAsync(string workQueue, string messageId, string popReceipt, CancellationToken cancellationToken);

        /// <summary>
        /// Returns the latest/next notice in queue without removing it from the queue.
        /// </summary>
        /// <param name="workQueue">The name of the notication queue.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="visibilityDelay">
        /// An optional timeout that can be applied to have the message initially hidden/invisible for a period of time.
        /// </param>
        /// <returns>
        /// The latest/next <see cref="ExperimentMetadataInstance"/> on the queue.
        /// </returns>
        Task<ExperimentMetadataInstance> PeekNoticeAsync(string workQueue, CancellationToken cancellationToken, TimeSpan? visibilityDelay = null);

        /// <summary>
        /// Sets the visibility state of an existing notice on the queue.
        /// </summary>
        /// <param name="workQueue">The name of the notification queue.</param>
        /// <param name="messageId">The messageId of the notice.</param>
        /// <param name="popReceipt">The popReceipt of the notice.</param>
        /// <param name="visibilityDelay">The timeout that can be applied to have the message initially hidden/invisible for a period of time.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// A task that can be used to set the visibility of an existing notice on the queue.
        /// </returns>
        Task SetNoticeVisibilityAsync(string workQueue, string messageId, string popReceipt, TimeSpan visibilityDelay, CancellationToken cancellationToken);
    }
}