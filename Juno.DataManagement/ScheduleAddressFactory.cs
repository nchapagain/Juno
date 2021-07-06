namespace Juno.DataManagement
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Repository.Cosmos;

    /// <summary>
    /// Provides a factory for creating address/location components required
    /// to retrieve Schedule Documents and Schedule Time Tables.
    /// </summary>
    public static class ScheduleAddressFactory
    {   
        internal const string ExecutionGoalDatabase = "executionGoalDefinitions";
        internal const string ExecutionGoalContainerId = "instances";
        internal const string ExecutionGoalPartitionKeyPath = "/definition/metadata/teamName";

        internal const string ExecutionGoalTemplateContainerId = "executionGoalTemplates";
        internal const string ExecutionGoalTemplateParitionKeyPath = "/definition/metadata/teamName";

        internal const string ExecutionGoalTriggerTable = "targetGoals";

        /// <summary>
        /// Generate a valid <see cref="CosmosAddress"/> that can be used to 
        /// retrieve an Execution Goal in Cosmos Table.
        /// </summary>
        /// <param name="executionGoalId">The Id of the Execution Goal to retrieve</param>
        /// <param name="teamName">The Execution Goal Partition Key</param>
        /// <returns><see cref="CosmosAddress"/></returns>
        internal static CosmosAddress CreateExecutionGoalAddress(string teamName = null, string executionGoalId = null)
        {
            return new CosmosAddress()
            {
                DatabaseId = ScheduleAddressFactory.ExecutionGoalDatabase,
                ContainerId = ScheduleAddressFactory.ExecutionGoalContainerId,
                PartitionKeyPath = ScheduleAddressFactory.ExecutionGoalPartitionKeyPath,
                PartitionKey = teamName,
                DocumentId = executionGoalId
            };
        }

        internal static CosmosAddress CreateExecutionGoalTemplateAddress(string teamName = null, string executionGoalId = null)
        {
            return new CosmosAddress()
            {
                DatabaseId = ScheduleAddressFactory.ExecutionGoalDatabase,
                ContainerId = ScheduleAddressFactory.ExecutionGoalTemplateContainerId,
                PartitionKeyPath = ScheduleAddressFactory.ExecutionGoalTemplateParitionKeyPath,
                PartitionKey = teamName,
                DocumentId = executionGoalId
            };
        }

        /// <summary>
        /// Generate a valid <see cref="CosmosTableAddress"/> to retrieve
        /// a target goal trigger.
        /// </summary>
        /// <param name="version">The version of the schedule</param>
        /// <param name="rowKey">Rowkey for one TargetGoalTrigger</param>
        /// <returns><see cref="CosmosTableAddress"/></returns>
        internal static CosmosTableAddress CreateTargetGoalTriggerAddress(string version, string rowKey = null)
        {
            return new CosmosTableAddress()
            { 
                TableName = ScheduleAddressFactory.ExecutionGoalTriggerTable,
                PartitionKey = version,
                RowKey = rowKey
            };
        }

        /// <summary>
        /// Generate a valid <see cref="CosmosTableAddress"/> to retrieve
        /// a target goal trigger.
        /// </summary>
        /// <returns><see cref="CosmosTableAddress"/></returns>
        internal static IList<CosmosTableAddress> CreateAllTargetGoalTriggerAddress()
        {
            IList<CosmosTableAddress> output = new List<CosmosTableAddress>();
            foreach (string paritionKey in ContractExtension.SupportedExecutionGoalVersions)
            {
                output.Add(new CosmosTableAddress()
                {
                    TableName = ScheduleAddressFactory.ExecutionGoalTriggerTable,
                    PartitionKey = paritionKey,
                    RowKey = null
                });
            }

            return output;
        }
    }
}
