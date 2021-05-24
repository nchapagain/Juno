namespace Juno.Scheduler.Preconditions.Manager
{
    using System.Data;
    using System.Threading.Tasks;
    using Juno.Contracts.Configuration;
    using Microsoft.Azure.CRC.Kusto;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// Interface to assist in connection with Kusto.
    /// </summary>
    public interface IKustoManager
    {
        /// <summary>
        /// Allow an interface to set the Configuration for the Kusto Manager
        /// Can only be set once.
        /// </summary>
        /// <param name="configuration"><see cref="IConfiguration"/></param>
        void SetUp(IConfiguration configuration);

        /// <summary>
        /// Allow an interface to set the kustoQueryIssuer for the kusto manager
        /// </summary>
        /// <param name="issuer"><see cref="KustoQueryIssuer"/></param>
        void SetUp(IKustoQueryIssuer issuer);

        /// <summary>
        /// Helper method to connect with AzureCM Kusto Cluster
        /// </summary>
        /// <param name="cacheKey">The key to be used to retrieve the result of the Kusto query from the cache</param>
        /// <param name="kustoSettings">Kusto Settings that offer information on which kusto data source to connect to</param>
        /// <param name="query">Kusto Query for which a response is generated</param>
        /// <param name="minutesToLive"> The Amount of time the result of the query should live in the cache</param>
        /// <returns><see cref="DataTable"/> that has column and row data</returns>
        Task<DataTable> GetKustoResponseAsync(string cacheKey, KustoSettings kustoSettings, string query, double? minutesToLive = null);
    }
}
