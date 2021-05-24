namespace Juno.DataManagement
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.CRC.Repository.Storage;

    /// <summary>
    /// Provides methods for managing file content associated with
    /// Juno experiments.
    /// </summary>
    public interface IExperimentFileManager
    {
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
        Task CreateFileAsync(string experimentId, string fileName, BlobStream file, DateTime timestamp, CancellationToken cancellationToken, string agentType = null, string agentId = null);
    }
}
