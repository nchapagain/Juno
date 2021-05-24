namespace Juno.Execution.Providers.AutoTriage
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Threading.Tasks;
    using Juno.Extensions.Telemetry;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Kusto;
    using Polly;

    /// <summary>
    /// Contains Kusto query issuer extension methods.
    /// </summary>
    public static class KustoDiagnosticsQueryExtensions
    {
        private const string AzureCMClusterUrl = "https://azurecm.kusto.windows.net/";
        private const string AzureCMDatabase = "AzureCM";
        private const string ArmProdClusterUrl = "https://armprod.kusto.windows.net/";
        private const string ArmProdDatabase = "ARMProd";
        private const string AZCRPClusterUrl = "https://azcrp.kusto.windows.net/";
        private const string CRPAllProdDatabase = "crp_allprod";
        private const string ARMProdDeploymentOperationsDiagnosticsEntryKey = "Kusto.ARMProd.DeploymentOperations";
        private const string AZCRPVMApiQosEventDiagnosticsEntryKey = "Kusto.AzCrp.VmApiQosEvent";
        private const string LogNodeSnapshotDiagnosticsEntryKey = "Kusto.AzureCM.LogNodeSnapshot";
        private const string MicrocodeUpdateDiagnosticsEntryKey = "Kusto.AzureCM.CSIMicrocode";
        private const string TipDeploymentDiagnosticsEntryKey = "Kusto.AzureCM.LogTipNodeSessionStatusEventMessage";

        /// <summary>
        /// Kusto query issuer extension method to read microcode update failure diagnostics from kusto database.
        /// </summary>
        /// <param name="queryIssuer">Specifies query issuer object.</param>
        /// <param name="nodeId">Specifies the ID of the node related to the TiP session.</param>
        /// <param name="timeRangeBegin">Specifies diagnostics window begin time.</param>
        /// <param name="timeRangeEnd">Specifies diagnostics window end time.</param>
        /// <param name="retryPolicy">The retry policy for any asynchronous calls.</param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        public static async Task<IEnumerable<DiagnosticsEntry>> GetMicrocodeUpdateDiagnosticsAsync(
            this IKustoQueryIssuer queryIssuer, string nodeId, DateTime timeRangeBegin, DateTime timeRangeEnd, IAsyncPolicy retryPolicy)
        {
            retryPolicy.ThrowIfNull(nameof(retryPolicy));

            DataTable dataTable = null;
            await retryPolicy.ExecuteAsync(async () =>
            {
                string query = AutoTriageQueryFactory.GetMicrocodeUpdateEventQuery(nodeId, timeRangeBegin, timeRangeEnd);
                dataTable = await queryIssuer.IssueAsync(KustoDiagnosticsQueryExtensions.AzureCMClusterUrl, KustoDiagnosticsQueryExtensions.AzureCMDatabase, query).ConfigureAwait(false);
            }).ConfigureAwait(false);

            // Parses the datatable records and maps to list of diagnostics entries.
            return KustoDiagnosticsQueryExtensions.ReadMicrocodeUpdateDiagnosticsData(dataTable);
        }

        /// <summary>
        /// Kusto query issuer extension method to read TIP deployment failure diagnostics from kusto database.
        /// </summary>
        /// <param name="queryIssuer">Specifies query issuer object.</param>
        /// <param name="tipSessionId">Specifies the ID of the node related to the TiP session.</param>
        /// <param name="retryPolicy">The retry policy for any asynchronous calls.</param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        public static async Task<IEnumerable<DiagnosticsEntry>> GetTipDeploymentFailureDiagnosticsAsync(
            this IKustoQueryIssuer queryIssuer, string tipSessionId, IAsyncPolicy retryPolicy)
        {
            retryPolicy.ThrowIfNull(nameof(retryPolicy));

            DataTable dataTable = null;
            await retryPolicy.ExecuteAsync(async () =>
            {
                string query = AutoTriageQueryFactory.GetTipSessionStatusEventsQuery(tipSessionId);
                dataTable = await queryIssuer.IssueAsync(KustoDiagnosticsQueryExtensions.AzureCMClusterUrl, KustoDiagnosticsQueryExtensions.AzureCMDatabase, query).ConfigureAwait(false);
            }).ConfigureAwait(false);

            // Parses the datatable records and maps to list of diagnostics entries.
            return KustoDiagnosticsQueryExtensions.ReadTipDeploymentDiagnosticsData(dataTable);
        }

        /// <summary>
        /// Kusto query issuer extension method to read arm deployment failure diagnostics from kusto database.
        /// </summary>
        /// <param name="queryIssuer">Specifies query issuer object.</param>
        /// <param name="tipSessionId">Specifies the ID of the node related to the TiP session.</param>
        /// <param name="rGName">Specifies the resource group name.</param>
        /// <param name="timeRangeBegin">Specifies diagnostics window begin time.</param>
        /// <param name="timeRangeEnd">Specifies diagnostics window end time.</param>
        /// <param name="retryPolicy">The retry policy for any asynchronous calls.</param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        public static async Task<IEnumerable<DiagnosticsEntry>> GetArmVmDeploymentFailureKustoDiagnosticsAsync(
            this IKustoQueryIssuer queryIssuer, string tipSessionId, string rGName, DateTime timeRangeBegin, DateTime timeRangeEnd, IAsyncPolicy retryPolicy)
        {
            retryPolicy.ThrowIfNull(nameof(retryPolicy));

            IEnumerable<DiagnosticsEntry> returnResults = null;

            await retryPolicy.ExecuteAsync(async () =>
            {
                string query = AutoTriageQueryFactory.GetArmDeploymentOperationQuery(rGName, timeRangeBegin, timeRangeEnd);
                DataTable dataTable = await queryIssuer.IssueAsync(KustoDiagnosticsQueryExtensions.ArmProdClusterUrl, KustoDiagnosticsQueryExtensions.ArmProdDatabase, query).ConfigureAwait(false);

                // Parses the datatable records and maps to list of diagnostics entries.
                returnResults = KustoDiagnosticsQueryExtensions.ReadArmProdDeploymentOperationDiagnosticsData(dataTable);
            }).ConfigureAwait(false);

            await retryPolicy.ExecuteAsync(async () =>
            {
                string query = AutoTriageQueryFactory.GetAzCrpVmApiQosEventQuery(rGName, timeRangeBegin, timeRangeEnd);
                DataTable dataTable = await queryIssuer.IssueAsync(KustoDiagnosticsQueryExtensions.AZCRPClusterUrl, KustoDiagnosticsQueryExtensions.CRPAllProdDatabase, query).ConfigureAwait(false);

                // Parses the datatable records and maps to list of diagnostics entries.
                IEnumerable<DiagnosticsEntry> queryResults = KustoDiagnosticsQueryExtensions.ReadAZCRPVMApiQosEventDiagnosticsData(dataTable);
                returnResults = (returnResults ?? Enumerable.Empty<DiagnosticsEntry>()).Concat(queryResults ?? Enumerable.Empty<DiagnosticsEntry>());
            }).ConfigureAwait(false);

            await retryPolicy.ExecuteAsync(async () =>
            {
                string query = AutoTriageQueryFactory.GetLogNodeSnapshotQuery(tipSessionId);
                DataTable dataTable = await queryIssuer.IssueAsync(KustoDiagnosticsQueryExtensions.AzureCMClusterUrl, KustoDiagnosticsQueryExtensions.AzureCMDatabase, query).ConfigureAwait(false);

                // Parses the datatable records and maps to list of diagnostics entries.
                IEnumerable<DiagnosticsEntry> queryResults = KustoDiagnosticsQueryExtensions.ReadLogNodeSnapshotDiagnosticsData(dataTable);
                returnResults = (returnResults ?? Enumerable.Empty<DiagnosticsEntry>()).Concat(queryResults ?? Enumerable.Empty<DiagnosticsEntry>());
            }).ConfigureAwait(false);

            return returnResults;
        }

        private static IEnumerable<DiagnosticsEntry> ReadMicrocodeUpdateDiagnosticsData(DataTable dataTable)
        {
            List<DiagnosticsEntry> eventMessages = new List<DiagnosticsEntry>();
            if (dataTable?.Rows != null)
            {
                foreach (DataRow row in dataTable.Rows)
                {
                    DateTime timestamp = row[0] != null ? DateTime.Parse((string)row[0]) : DateTime.MinValue;
                    string status = row[1] != null ? (string)row[1] : string.Empty;
                    string signature = row[2] != null ? (string)row[2] : string.Empty;
                    string description = row[3] != null ? (string)row[3] : string.Empty;

                    Dictionary<string, IConvertible> diagnosticsMessage = new Dictionary<string, IConvertible>()
                    {
                        { "timestamp", timestamp },
                        { "status", status },
                        { "signature", signature },
                        { "description", description }
                    };

                    var diagnosticsEntry = new DiagnosticsEntry(KustoDiagnosticsQueryExtensions.MicrocodeUpdateDiagnosticsEntryKey, diagnosticsMessage);
                    eventMessages.Add(diagnosticsEntry);
                }
            }

            return eventMessages;
        }

        private static IEnumerable<DiagnosticsEntry> ReadTipDeploymentDiagnosticsData(DataTable dataTable)
        {
            List<DiagnosticsEntry> eventMessages = new List<DiagnosticsEntry>();
            if (dataTable?.Rows != null)
            {
                foreach (DataRow row in dataTable.Rows)
                {
                    DateTime timestamp = row[0] != null ? DateTime.Parse((string)row[0]) : DateTime.MinValue;
                    string tipNodeSessionId = row[1] != null ? (string)row[1] : string.Empty;
                    string availabilityZone = row[2] != null ? (string)row[2] : string.Empty;
                    string tenant = row[3] != null ? (string)row[3] : string.Empty;
                    string message = row[4] != null ? (string)row[4] : string.Empty;

                    Dictionary<string, IConvertible> diagnosticsMessage = new Dictionary<string, IConvertible>()
                    {
                        { "timestamp", timestamp },
                        { "tipNodeSessionId", tipNodeSessionId },
                        { "availabilityZone", availabilityZone },
                        { "tenant", tenant },
                        { "message", message }
                    };

                    var diagnosticsEntry = new DiagnosticsEntry(KustoDiagnosticsQueryExtensions.TipDeploymentDiagnosticsEntryKey, diagnosticsMessage);
                    eventMessages.Add(diagnosticsEntry);
                }
            }

            return eventMessages;
        }

        private static IEnumerable<DiagnosticsEntry> ReadArmProdDeploymentOperationDiagnosticsData(DataTable dataTable)
        {
            List<DiagnosticsEntry> eventMessages = new List<DiagnosticsEntry>();
            if (dataTable?.Rows != null)
            {
                foreach (DataRow row in dataTable.Rows)
                {
                    DateTime timestamp = row[0] != null ? DateTime.Parse((string)row[0]) : DateTime.MinValue;
                    string tenantId = row[1] != null ? (string)row[1] : string.Empty;
                    string resourceGroupName = row[2] != null ? (string)row[2] : string.Empty;
                    string executionStatus = row[3] != null ? (string)row[3] : string.Empty;
                    string statusCode = row[4] != null ? (string)row[4] : string.Empty;
                    string statusMessage = row[5] != null ? (string)row[5] : string.Empty;

                    Dictionary<string, IConvertible> diagnosticsMessage = new Dictionary<string, IConvertible>()
                    {
                        { "timestamp", timestamp },
                        { "tenantId", tenantId },
                        { "resourceGroupName", resourceGroupName },
                        { "executionStatus", executionStatus },
                        { "statusCode", statusCode },
                        { "statusMessage", statusMessage }
                    };
                    var diagnosticsEntry = new DiagnosticsEntry(KustoDiagnosticsQueryExtensions.ARMProdDeploymentOperationsDiagnosticsEntryKey, diagnosticsMessage);
                    eventMessages.Add(diagnosticsEntry);
                }
            }

            return eventMessages;
        }

        private static IEnumerable<DiagnosticsEntry> ReadAZCRPVMApiQosEventDiagnosticsData(DataTable dataTable)
        {
            List<DiagnosticsEntry> eventMessages = new List<DiagnosticsEntry>();
            if (dataTable?.Rows != null)
            {
                foreach (DataRow row in dataTable.Rows)
                {
                    DateTime timestamp = row[0] != null ? DateTime.Parse((string)row[0]) : DateTime.MinValue;
                    string correlationId = row[1] != null ? (string)row[1] : string.Empty;
                    string operationId = row[2] != null ? (string)row[2] : string.Empty;
                    string resourceGroupName = row[3] != null ? (string)row[3] : string.Empty;
                    string resourceName = row[4] != null ? (string)row[4] : string.Empty;
                    string subscriptionId = row[5] != null ? (string)row[5] : string.Empty;
                    string exceptionType = row[6] != null ? (string)row[6] : string.Empty;
                    string errorDetails = row[7] != null ? (string)row[7] : string.Empty;
                    string vMId = row[8] != null ? (string)row[8] : string.Empty;
                    string vMSize = row[9] != null ? (string)row[9] : string.Empty;
                    string oSType = row[10] != null ? (string)row[10] : string.Empty;
                    string oSDiskStorageAccountType = row[11] != null ? (string)row[11] : string.Empty;
                    string availabilitySet = row[12] != null ? (string)row[12] : string.Empty;
                    string fabricCluster = row[13] != null ? (string)row[13] : string.Empty;
                    string allocationAction = row[14] != null ? (string)row[14] : string.Empty;

                    Dictionary<string, IConvertible> diagnosticsMessage = new Dictionary<string, IConvertible>()
                    {
                        { "timestamp", timestamp },
                        { "correlationId", correlationId },
                        { "operationId", operationId },
                        { "resourceGroupName", resourceGroupName },
                        { "resourceName", resourceName },
                        { "subscriptionId", subscriptionId },
                        { "exceptionType", exceptionType },
                        { "errorDetails", errorDetails },
                        { "vMId", vMId },
                        { "vMSize", vMSize },
                        { "oSType", oSType },
                        { "oSDiskStorageAccountType", oSDiskStorageAccountType },
                        { "availabilitySet", availabilitySet },
                        { "fabricCluster", fabricCluster },
                        { "allocationAction", allocationAction }
                    };

                    var diagnosticsEntry = new DiagnosticsEntry(KustoDiagnosticsQueryExtensions.AZCRPVMApiQosEventDiagnosticsEntryKey, diagnosticsMessage);
                    eventMessages.Add(diagnosticsEntry);
                }
            }

            return eventMessages;
        }

        private static IEnumerable<DiagnosticsEntry> ReadLogNodeSnapshotDiagnosticsData(DataTable dataTable)
        {
            List<DiagnosticsEntry> eventMessages = new List<DiagnosticsEntry>();
            if (dataTable?.Rows != null)
            {
                foreach (DataRow row in dataTable.Rows)
                {
                    DateTime timestamp = row[0] != null ? DateTime.Parse((string)row[0]) : DateTime.MinValue;
                    string nodeState = row[1] != null ? (string)row[1] : string.Empty;
                    string nodeAvailabilityState = row[2] != null ? (string)row[2] : string.Empty;
                    string faultInfo = row[3] != null ? (string)row[3] : string.Empty;
                    string hostingEnvironment = row[4] != null ? (string)row[4] : string.Empty;
                    string faultDomain = row[5] != null ? (string)row[5] : string.Empty;
                    string lastStateChangeTime = row[6] != null ? (string)row[6] : string.Empty;
                    string nsProgressHealthStatus = row[7] != null ? (string)row[7] : string.Empty;
                    string tipNodeSessionId = row[8] != null ? (string)row[8] : string.Empty;
                    string healthSignals = row[9] != null ? (string)row[9] : string.Empty;

                    Dictionary<string, IConvertible> diagnosticsMessage = new Dictionary<string, IConvertible>()
                    {
                        { "timestamp", timestamp },
                        { "nodeState", nodeState },
                        { "nodeAvailabilityState", nodeAvailabilityState },
                        { "faultInfo", faultInfo },
                        { "hostingEnvironment", hostingEnvironment },
                        { "faultDomain", faultDomain },
                        { "lastStateChangeTime", lastStateChangeTime },
                        { "nsProgressHealthStatus", nsProgressHealthStatus },
                        { "tipNodeSessionId", tipNodeSessionId },
                        { "healthSignals", healthSignals }
                    };

                    var diagnosticsEntry = new DiagnosticsEntry(KustoDiagnosticsQueryExtensions.LogNodeSnapshotDiagnosticsEntryKey, diagnosticsMessage);
                    eventMessages.Add(diagnosticsEntry);
                }
            }

            return eventMessages;
        }
    }
}