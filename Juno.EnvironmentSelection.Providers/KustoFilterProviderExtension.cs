namespace Juno.EnvironmentSelection
{
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Reflection;
    using Juno.Contracts;
    using Juno.Contracts.Validation;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;

    /// <summary>
    /// Static class that offers extensions for the Kusto Filters
    /// </summary>
    public static class KustoFilterProviderExtension
    {
        /// <summary>
        /// Translates the given data table into an envrionment candidate. 
        /// Appends any additional info given defined by the provider into the Environment Candidate
        /// </summary>
        /// <param name="expectedColumns">The provider that defines additional columsn to append</param>
        /// <param name="dictionaryKey">The key that maps to a data column, the values stored here, will be the dictionary's key</param>
        /// <param name="cacheEntries">The temporary storage for the cache entries</param>
        /// <param name="table">The data table to derive the Environment Candidate from</param>
        /// <param name="functions">Set of functions to assist in the caching of results from kusto queries.</param>
        /// <returns>A dictionary where the key is the Node id and the value is an environment candidate</returns>
        internal static IDictionary<string, EnvironmentCandidate> ToEnvironmentCandidate(
            this DataTable table, 
            ICachingFunctions functions,
            IEnumerable<KustoColumnAttribute> expectedColumns,
            string dictionaryKey,
            out IDictionary<ProviderCacheKey, IList<string>> cacheEntries)
        {
            expectedColumns.ThrowIfNull(nameof(expectedColumns));
            table.ThrowIfNull(nameof(table));

            cacheEntries = new Dictionary<ProviderCacheKey, IList<string>>();

            var additionalColumns = expectedColumns.Where(column => column.AdditionalInfo);
            IDictionary<string, EnvironmentCandidate> result = new Dictionary<string, EnvironmentCandidate>();
            bool hasSubscription = table.Columns.Contains(ProviderConstants.Subscription);
            bool hasCluster = table.Columns.Contains(ProviderConstants.ClusterId);
            bool hasRegion = table.Columns.Contains(ProviderConstants.Region);
            bool hasPool = table.Columns.Contains(ProviderConstants.MachinePoolName);
            bool hasRack = table.Columns.Contains(ProviderConstants.Rack);
            bool hasNode = table.Columns.Contains(ProviderConstants.NodeId);
            bool hasVmSku = table.Columns.Contains(ProviderConstants.VmSku);
            bool hasCpuId = table.Columns.Contains(ProviderConstants.CpuId);
            foreach (DataRow row in table.Rows)
            {
                EnvironmentCandidate currentCandidate = new EnvironmentCandidate(
                    hasSubscription ? (string)row[ProviderConstants.Subscription] : null,
                    hasCluster ? (string)row[ProviderConstants.ClusterId] : null,
                    hasRegion ? (string)row[ProviderConstants.Region] : null,
                    hasPool ? (string)row[ProviderConstants.MachinePoolName] : null,
                    hasRack ? (string)row[ProviderConstants.Rack] : null,
                    hasNode ? (string)row[ProviderConstants.NodeId] : null,
                    hasVmSku ? JsonConvert.DeserializeObject<List<string>>((string)row[ProviderConstants.VmSku]) : null,
                    hasCpuId ? (string)row[ProviderConstants.CpuId] : null);

                foreach (KustoColumnAttribute additionalColumn in additionalColumns)
                {
                    currentCandidate.AdditionalInfo.Add(additionalColumn.Name, (string)row[additionalColumn.Name]);
                }

                result.Add((string)row[dictionaryKey], currentCandidate);

                ProviderCacheKey key = functions.GenerateCacheKey(row, expectedColumns);
                string value = functions.GenerateCacheValue(currentCandidate);
                if (!cacheEntries.ContainsKey(key))
                {
                    cacheEntries.Add(key, new List<string> { value });
                }
                else
                {
                    cacheEntries[key].Add(value);
                }
            }

            return result;
        }

        /// <summary>
        /// Validates the results returned from a kusto query.
        /// </summary>
        /// <param name="provider">The provider that defines the required columns</param>
        /// <param name="table">The Datatable to validate</param>
        internal static ValidationResult ValidateResult(this KustoFilterProvider provider, DataTable table)
        {
            provider.ThrowIfNull(nameof(provider));
            table.ThrowIfNull(nameof(table));

            var expectedColumns = provider.GetType().GetCustomAttributes<KustoColumnAttribute>(true);
            List<string> errorList = new List<string>();

            foreach (KustoColumnAttribute attribute in expectedColumns)
            {
                if (!table.Columns.Contains(attribute.Name))
                {
                    errorList.Add($"Expected column: {attribute.Name} in filter type: {provider.GetType().Name}");
                }
            }

            return new ValidationResult(!errorList.Any(), errorList);
        }
    }
}
