namespace Juno.DataManagement
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Extensions.Telemetry;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Repository;
    using Microsoft.Azure.CRC.Repository.Storage;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;

    /// <summary>
    /// Provides methods for managing Juno experiment notification operations.
    /// </summary>
    public class ExperimentNotificationManager : IExperimentNotificationManager
    {
        /// <summary>
        /// Initializes and instance of the <see cref="ExperimentNotificationManager"/> class.
        /// </summary>
        /// <param name="queueStore">Provides methods to manage notifications with queue mechanics.</param>
        /// <param name="logger">A logger to use for capturing telemetry data.</param>
        public ExperimentNotificationManager(IQueueStore<QueueAddress> queueStore, ILogger logger = null)
        {
            queueStore.ThrowIfNull(nameof(queueStore));
            this.QueueStore = queueStore;
            this.Logger = logger ?? NullLogger.Instance;
        }

        /// <summary>
        /// Gets the logger for capturing operation telemetry.
        /// </summary>
        protected ILogger Logger { get; }

        /// <summary>
        /// Gets the notification queue/store data provider.
        /// </summary>
        protected IQueueStore<QueueAddress> QueueStore { get; }

        /// <inheritdoc />
        public async Task<ExperimentMetadataInstance> CreateNoticeAsync(string workQueue, ExperimentMetadata notice, CancellationToken cancellationToken, TimeSpan? visibilityDelay = null)
        {
            workQueue.ThrowIfNullOrWhiteSpace(nameof(workQueue));
            notice.ThrowIfNull(nameof(notice));

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(nameof(workQueue), workQueue)
                .AddContext(nameof(notice), notice)
                .AddContext(nameof(visibilityDelay), visibilityDelay?.ToString());

            return await this.Logger.LogTelemetryAsync(EventNames.CreateNotice, telemetryContext, async () =>
            {
                QueueAddress address = ExperimentAddressFactory.CreateNoticeAddress(workQueue);
                ExperimentMetadataInstance instance = new ExperimentMetadataInstance(Guid.NewGuid().ToString(), notice);

                ExperimentMetadataInstance queuedNotice = await this.QueueStore.EnqueueItemAsync(address, instance, cancellationToken, visibilityDelay)
                    .ConfigureDefaults();

                telemetryContext.AddContext(queuedNotice, "noticen");
                return queuedNotice;
            }).ConfigureDefaults();
        }

        /// <inheritdoc />
        public async Task DeleteNoticeAsync(string workQueue, string messageId, string popReceipt, CancellationToken cancellationToken)
        {
            workQueue.ThrowIfNullOrWhiteSpace(nameof(workQueue));
            messageId.ThrowIfNull(nameof(messageId));
            popReceipt.ThrowIfNull(nameof(popReceipt));

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(nameof(workQueue), workQueue)
                .AddContext(nameof(messageId), messageId)
                .AddContext(nameof(popReceipt), popReceipt);

            await this.Logger.LogTelemetryAsync(EventNames.DeleteNotice, telemetryContext, async () =>
            {
                QueueAddress address = ExperimentAddressFactory.CreateNoticeAddress(
                    workQueue,
                    messageId,
                    popReceipt);

                await this.QueueStore.DeleteItemAsync(address, cancellationToken).ConfigureDefaults();

            }).ConfigureDefaults();
        }

        /// <inheritdoc />
        public async Task<ExperimentMetadataInstance> PeekNoticeAsync(string workQueue, CancellationToken cancellationToken, TimeSpan? visibilityDelay = null)
        {
            workQueue.ThrowIfNullOrWhiteSpace(nameof(workQueue));

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(nameof(workQueue), workQueue)
                .AddContext(nameof(visibilityDelay), visibilityDelay?.ToString());

            return await this.Logger.LogTelemetryAsync(EventNames.PeekNotice, telemetryContext, async () =>
            {
                QueueAddress address = ExperimentAddressFactory.CreateNoticeAddress(workQueue);
                ExperimentMetadataInstance notice = await this.QueueStore.PeekItemAsync<ExperimentMetadataInstance>(address, cancellationToken, visibilityDelay)
                    .ConfigureDefaults();

                telemetryContext.AddContext(notice, "noticen");

                return notice;

            }).ConfigureDefaults();
        }

        /// <inheritdoc />
        public async Task SetNoticeVisibilityAsync(string workQueue, string messageId, string popReceipt, TimeSpan visibilityDelay, CancellationToken cancellationToken)
        {
            workQueue.ThrowIfNullOrWhiteSpace(nameof(workQueue));
            messageId.ThrowIfNull(nameof(messageId));
            popReceipt.ThrowIfNull(nameof(popReceipt));
            visibilityDelay.ThrowIfNull(nameof(visibilityDelay));
            visibilityDelay.ThrowIfInvalid(
                nameof(visibilityDelay),
                delay => delay.TotalSeconds >= 1,
                $"The visibility delay cannot be less than 1 second.");

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(nameof(workQueue), workQueue)
                .AddContext(nameof(messageId), messageId)
                .AddContext(nameof(popReceipt), popReceipt)
                .AddContext(nameof(visibilityDelay), visibilityDelay.ToString());

            await this.Logger.LogTelemetryAsync(EventNames.SetNoticeVisibility, telemetryContext, async () =>
            {
                QueueAddress address = ExperimentAddressFactory.CreateNoticeAddress(
                    workQueue, messageId, popReceipt);

                await this.QueueStore.SetItemVisibilityAsync(address, visibilityDelay, cancellationToken)
                    .ConfigureDefaults();

            }).ConfigureDefaults();
        }

        /// <summary>
        /// Provides the names of telemetry events emitted by the data manager.
        /// </summary>
        private static class EventNames
        {
            public static readonly string CreateNotice = EventContext.GetEventName(nameof(ExperimentNotificationManager), "CreateNotice");
            public static readonly string DeleteNotice = EventContext.GetEventName(nameof(ExperimentNotificationManager), "DeleteNotice");
            public static readonly string PeekNotice = EventContext.GetEventName(nameof(ExperimentNotificationManager), "PeekNotice");
            public static readonly string SetNoticeVisibility = EventContext.GetEventName(nameof(ExperimentNotificationManager), "SetNoticeVisibility");
        }
    }
}