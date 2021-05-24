namespace Juno.DataManagement
{
    using System;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Repository.Cosmos;
    using Microsoft.Azure.CRC.Repository.Storage;

    /// <summary>
    /// Provides a factory for creating address/location components required
    /// to store experiment data in Juno data stores.
    /// </summary>
    public static class ExperimentAddressFactory
    {
        internal const string AgentHeartbeatTableName = "agentHeartbeats";
        internal const string ExperimentContainerId = "instances";
        internal const string ExperimentDatabaseId = "experiments";
        internal const string ExperimentStateContainerId = "instances";
        internal const string ExperimentStateDatabaseId = "experimentState";
        internal const string ExperimentPartitionKeyPath = "/_partition";
        internal const string ExperimentStepsTableName = "experimentSteps";
        internal const string ExperimentAgentsTableName = "experimentAgents";
        internal const string ExperimentAgentStepsTableName = "experimentAgentSteps";

        internal const int PartitionCharacters = 4;

        /// <summary>
        /// Creates a valid <see cref="CosmosTableAddress"/> that can be used to create
        /// agent heartbeat in Cosmos Table.
        /// </summary>
        /// <param name="heartbeatTimestamp">The agent table entry row key</param>
        /// <param name="agentId">Define specific agent infromations such as cluster name, node name etc.</param>
        /// <param name="rowKey">The unique ID of the entry in the partition.</param>
        /// <returns>
        /// A <see cref="CosmosTableAddress"/> that defines where the agent heartbeat to be stored.
        /// </returns>
        internal static CosmosTableAddress CreateAgentHeartbeatAddress(DateTime heartbeatTimestamp, string agentId, string rowKey = null)
        {
            // Cosmos Table is case-sensitive. We are lowercasing the agent ID and agent type 
            // to ensure consistency with queries.
            DateTime effectiveTimestamp = heartbeatTimestamp.Kind == DateTimeKind.Utc
                ? heartbeatTimestamp
                : heartbeatTimestamp.ToUniversalTime();

            // Get the 10-min swath in the hour (e.g. 00, 10, 20, 30, 40, 50)
            int tenMinSwath = (effectiveTimestamp.Minute / 10) * 10;
            string minutesRepresentation = tenMinSwath <= 0 ? "00" : tenMinSwath.ToString();

            CosmosTableAddress address = new CosmosTableAddress
            {
                // Partition is the hour of the day. This allows an easy way to query for
                // ALL heartbeats in the past hour for example. Agents generally heartbeat on 1 - 5 minute
                // intervals as an example, so it is reasonable to assume that we can see ALL agents
                // alive in the system given a 10 min swath of time for them to report back. We are using a
                // partial round-trip format for the heartbeat timestamp.
                //
                // Format
                // 2020-07-27T11:00-by3prdapp19,0252f6d4-6393-4cf0-b8bf-adc5de8cc496,e1ab4e78-9e0a-415e-93b4-52bf8e5b55f8
                TableName = ExperimentAddressFactory.AgentHeartbeatTableName,
                PartitionKey = $"{effectiveTimestamp.ToString($"yyyy-MM-ddTHH")}:{minutesRepresentation}-{agentId.ToLowerInvariant()}"
            };

            if (!string.IsNullOrWhiteSpace(rowKey))
            {
                address.RowKey = rowKey.ToLowerInvariant();
            }

            return address;
        }

        /// <summary>
        /// Creates a valid <see cref="CosmosTableAddress"/> that can be used to locate all
        /// experiment steps in Cosmos Table.
        /// </summary>
        /// <param name="experimentId">The ID of the experiment for which the agent step is related.</param>
        /// <param name="stepId">The ID of the experiment step.</param>
        /// <returns>
        /// A <see cref="CosmosTableAddress"/> that defines where the experiment steps are stored.
        /// </returns>
        internal static CosmosTableAddress CreateAgentStepAddress(string experimentId, string stepId = null)
        {
            experimentId.ThrowIfNull(nameof(experimentId));
            CosmosTableAddress address = new CosmosTableAddress
            {
                TableName = ExperimentAddressFactory.ExperimentAgentStepsTableName,
                PartitionKey = experimentId.ToLowerInvariant()
            };

            if (!string.IsNullOrWhiteSpace(stepId))
            {
                // The following characters are not allowed in PartitionKey and RowKey fields:
                // - The forward slash(/) character
                // - The backslash(\) character
                // - The number sign(#) character
                // - The question mark (?) character
                address.RowKey = stepId.ToLowerInvariant();
            }

            return address;
        }

        /// <summary>
        /// Creates a valid <see cref="CosmosAddress"/> that can be used to locate
        /// the experiment in Cosmos DB.
        /// </summary>
        /// <returns>
        /// A <see cref="CosmosAddress"/> that defines where the experiment is stored.
        /// </returns>
        internal static CosmosAddress CreateExperimentAddress()
        {
            return new CosmosAddress
            {
                DatabaseId = ExperimentAddressFactory.ExperimentDatabaseId,
                ContainerId = ExperimentAddressFactory.ExperimentContainerId
            };
        }

        /// <summary>
        /// Creates a valid <see cref="CosmosAddress"/> that can be used to locate
        /// the experiment in Cosmos DB.
        /// </summary>
        /// <param name="experimentId">The ID of the experiment to be stored.</param>
        /// <returns>
        /// A <see cref="CosmosAddress"/> that defines where the experiment is stored.
        /// </returns>
        internal static CosmosAddress CreateExperimentAddress(string experimentId)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));

            return new CosmosAddress
            {
                DatabaseId = ExperimentAddressFactory.ExperimentDatabaseId,
                ContainerId = ExperimentAddressFactory.ExperimentContainerId,
                PartitionKey = ExperimentAddressFactory.GetExperimentPartition(experimentId),
                PartitionKeyPath = ExperimentAddressFactory.ExperimentPartitionKeyPath,
                DocumentId = experimentId
            };
        }

        /// <summary>
        /// Creates a valid <see cref="CosmosTableAddress"/> that can be used to store the
        /// experiment/agent mapping in Cosmos Table.
        /// </summary>
        /// <param name="agentId">The unique ID of the agent.</param>
        /// <param name="experimentId">The unique ID of the experiment in which the agent is associated.</param>
        /// <returns>
        /// A <see cref="CosmosTableAddress"/> that defines where the experiment/agent record
        /// is stored.
        /// </returns>
        internal static CosmosTableAddress CreateExperimentAgentAddress(string agentId, string experimentId = null)
        {
            // https://docs.microsoft.com/en-us/rest/api/storageservices/Understanding-the-Table-Service-Data-Model?redirectedfrom=MSDN
            // The following characters are not allowed in PartitionKey and RowKey fields:
            // - The forward slash(/) character
            // - The backslash(\) character
            // - The number sign(#) character
            // - The question mark (?) character
            //
            // Agent IDs are comprised of the following information:
            // Nodes:
            // - Data Center Cluster
            // - Node ID
            // - TiP Session ID
            //
            //   Examples:
            //   blaprdapp17,23c75d97-11f1-4349-bc2f-eb92b2ecbb48,3f3c8c37-4491-45ff-a941-d2db0e73f03b
            //
            // Virtual Machines:
            // - Data Center Cluster
            // - Node ID
            // - Virtual Machine ID
            // - TiP Session ID
            //
            //   Examples:
            //   blaprdapp17,23c75d97-11f1-4349-bc2f-eb92b2ecbb48,6a8b966a927-0,3f3c8c37-4491-45ff-a941-d2db0e73f03b

            // Cosmos Table is case-sensitive. We are lowercasing the partition key and row key
            // to ensure consistency with queries.
            CosmosTableAddress address = new CosmosTableAddress
            {
                TableName = ExperimentAddressFactory.ExperimentAgentsTableName,
                PartitionKey = agentId.ToLowerInvariant()
            };

            if (!string.IsNullOrWhiteSpace(experimentId))
            {
                address.RowKey = experimentId.ToLowerInvariant();
            }

            return address;
        }

        /// <summary>
        /// Creates a valid <see cref="CosmosAddress"/> that can be used to locate
        /// the experiment context/metadata in Cosmos DB.
        /// </summary>
        /// <param name="experimentId">The ID of the experiment to which the context is related.</param>
        /// <param name="contextId">Optionally defines the specific ID of the context to retrieve. When not defined, returns the global context for the experiment.</param>
        /// <returns>
        /// A <see cref="CosmosAddress"/> that defines where the experiment context/metadata
        /// is stored.
        /// </returns>
        internal static CosmosAddress CreateExperimentContextAddress(string experimentId, string contextId = null)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));

            return new CosmosAddress
            {
                DatabaseId = ExperimentAddressFactory.ExperimentStateDatabaseId,
                ContainerId = ExperimentAddressFactory.ExperimentStateContainerId,
                PartitionKey = ExperimentAddressFactory.GetExperimentPartition(experimentId),
                PartitionKeyPath = ExperimentAddressFactory.ExperimentPartitionKeyPath,
                DocumentId = contextId == null ? $"{experimentId}-context" : $"{experimentId}-context-{contextId}"
            };
        }

        /// <summary>
        /// Creates a valid <see cref="BlobAddress"/> that can be used to locate a file for
        /// a given experiemnt in an Azure Blob store.
        /// </summary>
        /// <param name="experimentId">The ID of the experiment to which the file is related.</param>
        /// <param name="fileName">The name of the file (ex. sellog).</param>
        /// <param name="timestamp">A timestamp that indicates the origin time of the file contents.</param>
        /// <param name="agentType">Optional parameter defines the type of agent (e.g. Host, Guest).</param>
        /// <param name="agentId">Optional parameter defines the ID of the agent.</param>
        /// <returns>
        /// A <see cref="BlobAddress"/> that defines where the file is/will be stored.
        /// </returns>
        internal static BlobAddress CreateExperimentFileAddress(string experimentId, string fileName, DateTime timestamp, string agentType = null, string agentId = null)
        {
            // Partitioning/Load Balancing
            // https://docs.microsoft.com/en-us/azure/storage/blobs/storage-performance-checklist#partitioning
            //
            // Example Naming Convention
            // Container: 29fab68-66ae-4a9c-ad51-89b4ebbb5df1
            // BlobName: Host/Node01/sellog_2020-02-28t13:45:30.0000000z
            string blobName = null;
            if (!string.IsNullOrWhiteSpace(agentType) && !string.IsNullOrWhiteSpace(agentId))
            {
                blobName = $"{agentType.Trim()}/{agentId.Trim()}/{fileName.Trim()}_{timestamp.ToString("o")}";
            }
            else
            {
                blobName = $"{fileName.Trim()}_{timestamp.ToString("o")}";
            }

            return new BlobAddress
            {
                BlobName = blobName.ToLowerInvariant(),
                ContainerName = experimentId.Trim().ToLowerInvariant()
            };
        }

        /// <summary>
        /// Creates a unique ID for the experiment given its name.
        /// </summary>
        /// <returns>
        /// A unique ID for the experiment.
        /// </returns>
        internal static string CreateExperimentId()
        {
            return Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Creates a valid <see cref="CosmosTableAddress"/> that can be used to locate all
        /// experiment steps in Cosmos Table.
        /// </summary>
        /// <param name="experimentId">The ID of the experiment.</param>
        /// <returns>
        /// A <see cref="CosmosTableAddress"/> that defines where the experiment steps are stored.
        /// </returns>
        internal static CosmosTableAddress CreateExperimentStepAddress(string experimentId)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));

            // Cosmos Table is case-sensitive. We are lowercasing the partition key
            // to ensure consistency with queries.
            CosmosTableAddress address = new CosmosTableAddress
            {
                TableName = ExperimentAddressFactory.ExperimentStepsTableName,
                PartitionKey = experimentId.ToLowerInvariant()
            };

            return address;
        }

        /// <summary>
        /// Creates a valid <see cref="CosmosTableAddress"/> that can be used to locate all
        /// experiment steps in Cosmos Table.
        /// </summary>
        /// <param name="experimentId">The ID of the experiment.</param>
        /// <param name="stepId">The ID of the experiment step.</param>
        /// <returns>
        /// A <see cref="CosmosTableAddress"/> that defines where the experiment steps are stored.
        /// </returns>
        internal static CosmosTableAddress CreateExperimentStepAddress(string experimentId, string stepId = null)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));

            // Cosmos Table is case-sensitive. We are lowercasing the partition key and row key
            // to ensure consistency with queries.
            CosmosTableAddress address = new CosmosTableAddress
            {
                TableName = ExperimentAddressFactory.ExperimentStepsTableName,
                PartitionKey = experimentId.ToLowerInvariant()
            };

            if (!string.IsNullOrWhiteSpace(stepId))
            {
                address.RowKey = stepId.ToLowerInvariant();
            }

            return address;
        }

        /// <summary>
        /// Creates a valid <see cref="QueueAddress"/> that can be used to locate/store the
        /// notice in Azure Queue.
        /// </summary>
        /// <returns></returns>
        internal static QueueAddress CreateNoticeAddress(string queueName, string messageId = null, string popReceipt = null)
        {
            // Azure Queue names MUST be lowercased
            // https://docs.microsoft.com/en-us/rest/api/storageservices/naming-queues-and-metadata

            return new QueueAddress
            {
                QueueName = queueName.ToLowerInvariant(),
                MessageId = messageId,
                PopReceipt = popReceipt
            };
        }

        /// <summary>
        /// Returns the partition for the experiment ID provided. This is used when
        /// storing an experiment in Cosmos DB.
        /// </summary>
        /// <param name="experimentId">The unique ID of the experiment.</param>
        /// <returns>
        /// The partition to use when storing the experiment in Cosmos DB.
        /// </returns>
        internal static string GetExperimentPartition(string experimentId)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));

            // We are generating Guid-formatted ID strings where the first 4 characters are generated
            // from a repeatable hash of the experiment name.  This will be used as the partition key for
            // experiments in the Cosmos DB. This will ensure with some consistency that experiments with
            // the same name end up in the same partition in Cosmos.
            // 
            // By using the first 4 characters of the experiment ID as the partition, there are 65,536 possible partitions
            // in the Cosmos DB store (16 x 16 x 16 x 16)...Guid characters have 16 possible values (a-f, 0-9).
            //
            // References:
            // https://docs.microsoft.com/en-us/azure/cosmos-db/partition-data
            // https://docs.microsoft.com/en-us/azure/cosmos-db/partitioning-overview#choose-partitionkey

            return experimentId.Substring(0, ExperimentAddressFactory.PartitionCharacters);
        }
    }
}
