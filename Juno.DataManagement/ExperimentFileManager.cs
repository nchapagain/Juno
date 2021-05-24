namespace Juno.DataManagement
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Extensions.Telemetry;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Repository;
    using Microsoft.Azure.CRC.Repository.Storage;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Provides methods for managing file content associated with
    /// Juno experiments.
    /// </summary>
    public class ExperimentFileManager : IExperimentFileManager
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExperimentFileManager"/> class.
        /// </summary>
        /// <param name="blobStore">The blob store where the files will be stored.</param>
        /// <param name="logger"></param>
        public ExperimentFileManager(IBlobStore<BlobAddress, BlobStream> blobStore, ILogger logger = null)
        {
            blobStore.ThrowIfNull(nameof(blobStore));
            logger.ThrowIfNull(nameof(blobStore));

            this.BlobStore = blobStore;
            this.Logger = logger;
        }

        /// <summary>
        /// Gets the data store where the files will be stored.
        /// </summary>
        protected IBlobStore<BlobAddress, BlobStream> BlobStore { get; }

        /// <summary>
        /// Gets the logger for capturing telemetry.
        /// </summary>
        protected ILogger Logger { get; }

        /// <summary>
        /// Creates a file in the backing data store.
        /// </summary>
        /// <param name="experimentId">The unique ID of the experiment in which the file is associated.</param>
        /// <param name="fileName">The name of the file (e.g. sellog).</param>
        /// <param name="file">Provides the file to upload and information about its contents.</param>
        /// <param name="timestamp">A timestamp that indicates the origin time of the file contents.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="agentType">Optional parameter defines the type of agent (e.g. Host, Guest).</param>
        /// <param name="agentId">Optional parameter defines the ID of the agent.</param>
        public async Task CreateFileAsync(
            string experimentId,
            string fileName,
            BlobStream file,
            DateTime timestamp,
            CancellationToken cancellationToken,
            string agentType = null,
            string agentId = null)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));
            fileName.ThrowIfNullOrWhiteSpace(nameof(fileName));
            file.ThrowIfNull(nameof(file));

            EventContext telemetryContext = EventContext.Persisted()
               .AddContext(nameof(experimentId), experimentId)
               .AddContext(nameof(fileName), fileName)
               .AddContext("contentEncoding", file.ContentEncoding.WebName)
               .AddContext("contentType", file.ContentType);

            await this.Logger.LogTelemetryAsync(EventNames.CreateFile, telemetryContext, async () =>
            {
                BlobAddress address = ExperimentAddressFactory.CreateExperimentFileAddress(experimentId, fileName, timestamp, agentType, agentId);
                await this.BlobStore.SaveBlobAsync(address, file, cancellationToken)
                .ConfigureDefaults();
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Provides the names of telemetry events emitted by the file manager.
        /// </summary>
        private static class EventNames
        {
            public static readonly string CreateFile = EventContext.GetEventName(nameof(ExperimentFileManager), "CreateFile");
        }
    }
}
