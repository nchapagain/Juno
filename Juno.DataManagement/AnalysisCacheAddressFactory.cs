namespace Juno.DataManagement
{
    using Microsoft.Azure.CRC.Repository.Cosmos;

    /// <summary>
    /// Provides the Cosmos Address of the analysis cache.
    /// </summary>
    public static class AnalysisCacheAddressFactory
    {
        internal const string AnalysisCaheDatabaseId = "argusCache";
        internal const string AnalysisCacheContainerId = "experimentsInfo";
        internal const string AnalysisCachePartitionKeyPath = "/partitionKey";

        internal static CosmosAddress GetAnalysisCacheCosmosAddress()
        {
            return new CosmosAddress()
            {
                DatabaseId = AnalysisCacheAddressFactory.AnalysisCaheDatabaseId,
                ContainerId = AnalysisCacheAddressFactory.AnalysisCacheContainerId,
                PartitionKeyPath = AnalysisCacheAddressFactory.AnalysisCachePartitionKeyPath
            };
        }
    }
}