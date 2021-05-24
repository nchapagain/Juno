namespace Juno.DataManagement
{
    using System;
    using System.Text;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Repository.Cosmos;

    /// <summary>
    /// Provides a factory for creating address/location components required
    /// to retrieve Schedule Documents and Schedule Time Tables.
    /// </summary>
    public static class ExperimentTemplateAddressFactory
    {
        internal const string ExecutionTemplateDatabase = "experimentDefinitions";
        internal const string ExecutionTemplateContainerId = "experiments";
        internal const string ExecutionTemplatePartitionKeyPath = "/definition/metadata/teamName";

        /// <summary>
        /// Generate a valid <see cref="CosmosAddress"/> that can be used to 
        /// retrieve an Execution Goal in Cosmos Table.
        /// </summary>
        /// <param name="experimentTemplateId">The Id of the Experiment Template to retrieve</param>
        /// <param name="teamName">The Execution Goal Partition Key</param>
        /// <returns><see cref="CosmosAddress"/></returns>
        internal static CosmosAddress CreateExperimentTemplateAddress(string teamName, string experimentTemplateId = null)
        {
            teamName.ThrowIfNullOrWhiteSpace(nameof(teamName));

            return new CosmosAddress()
            {
                DatabaseId = ExperimentTemplateAddressFactory.ExecutionTemplateDatabase,
                ContainerId = ExperimentTemplateAddressFactory.ExecutionTemplateContainerId,
                PartitionKeyPath = ExperimentTemplateAddressFactory.ExecutionTemplatePartitionKeyPath,
                PartitionKey = teamName,
                DocumentId = experimentTemplateId
            };
        }   
    }
}
