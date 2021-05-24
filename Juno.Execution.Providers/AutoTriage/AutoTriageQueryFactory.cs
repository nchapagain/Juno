namespace Juno.Execution.Providers.AutoTriage
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using Microsoft.Azure.CRC.Extensions;

    /// <summary>
    /// A factory to create queries for execution against Kusto clusters/databases.
    /// </summary>
    public static class AutoTriageQueryFactory
    {
        private const string EmptyQueryValue = "\"\"";

        /// <summary>
        /// Gets Arm deployment status events query.
        /// </summary>
        /// <param name="rGName">Specifies the resource group name.</param>
        /// <param name="timeRangeBegin">Specifies diagnostics window begin time.</param>
        /// <param name="timeRangeEnd">Specifies diagnostics window end time.</param>
        /// <returns>A valid kusto query.</returns>
        public static string GetArmDeploymentOperationQuery(string rGName, DateTime timeRangeBegin, DateTime timeRangeEnd)
        {
            rGName.ThrowIfNullOrWhiteSpace(nameof(rGName));
            AutoTriageQueryFactory.VerifyTimeRangeDates(timeRangeBegin, timeRangeEnd);

            string query = Properties.Resources.Diagnostics_ArmProdDeploymentOperationsQuery;
            query.ThrowIfNullOrEmpty(nameof(query), "Invalid query read from resource file");

            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters.Add(Constants.ResourceGroupNamePlaceHolder, rGName);
            parameters.Add(Constants.StartTimePlaceHolder, timeRangeBegin.ToString("s", DateTimeFormatInfo.InvariantInfo));
            parameters.Add(Constants.EndTimePlaceHolder, timeRangeEnd.ToString("s", DateTimeFormatInfo.InvariantInfo));

            return AutoTriageQueryFactory.ResolveQuery(query, parameters);
        }

        /// <summary>
        /// Gets Arm deployment status events query.
        /// </summary>
        /// <param name="rGName">Specifies the resource group name.</param>
        /// <param name="timeRangeBegin">Specifies diagnostics window begin time.</param>
        /// <param name="timeRangeEnd">Specifies diagnostics window end time.</param>
        /// <returns>A valid kusto query.</returns>
        public static string GetAzCrpVmApiQosEventQuery(string rGName, DateTime timeRangeBegin, DateTime timeRangeEnd)
        {
            rGName.ThrowIfNullOrWhiteSpace(nameof(rGName));
            AutoTriageQueryFactory.VerifyTimeRangeDates(timeRangeBegin, timeRangeEnd);

            string query = Properties.Resources.Diagnostics_AZCRPVMApiQosEventsQuery;
            query.ThrowIfNullOrEmpty(nameof(query), "Invalid query read from resource file");

            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters.Add(Constants.ResourceGroupNamePlaceHolder, rGName);
            parameters.Add(Constants.StartTimePlaceHolder, timeRangeBegin.ToString("s", DateTimeFormatInfo.InvariantInfo));
            parameters.Add(Constants.EndTimePlaceHolder, timeRangeEnd.ToString("s", DateTimeFormatInfo.InvariantInfo));

            return AutoTriageQueryFactory.ResolveQuery(query, parameters);
        }

        /// <summary>
        /// Gets Arm deployment status events query.
        /// </summary>
        /// <param name="tipSessionId">Specifies the Tip session ID.</param>
        /// <returns>A valid kusto query.</returns>
        public static string GetLogNodeSnapshotQuery(string tipSessionId)
        {
            tipSessionId.ThrowIfNullOrWhiteSpace(nameof(tipSessionId));

            string query = Properties.Resources.Diagnostics_AzureCMLogNodeSnapshotQuery;
            query.ThrowIfNullOrEmpty(nameof(query), "Invalid query read from resource file");

            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters.Add(Constants.TipSessionIdPlaceHolder, tipSessionId);

            return AutoTriageQueryFactory.ResolveQuery(query, parameters);
        }

        /// <summary>
        /// Gets microcode update failure events query.
        /// </summary>
        /// <param name="nodeId">Specifies the ID of the node related to the TiP session.</param>
        /// <param name="timeRangeBegin">Specifies diagnostics window begin time.</param>
        /// <param name="timeRangeEnd">Specifies diagnostics window end time.</param>
        /// <returns>A valid kusto query.</returns>
        public static string GetMicrocodeUpdateEventQuery(string nodeId, DateTime timeRangeBegin, DateTime timeRangeEnd)
        {
            nodeId.ThrowIfNullOrWhiteSpace(nameof(nodeId));
            AutoTriageQueryFactory.VerifyTimeRangeDates(timeRangeBegin, timeRangeEnd);

            string query = Properties.Resources.Diagnostics_MicrocodeUpdateEventsQuery;
            query.ThrowIfNullOrEmpty(nameof(query), "Invalid query read from resource file");

            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters.Add(Constants.NodeIdPlaceHolder, nodeId);
            parameters.Add(Constants.StartTimePlaceHolder, timeRangeBegin.ToString("s", DateTimeFormatInfo.InvariantInfo));
            parameters.Add(Constants.EndTimePlaceHolder, timeRangeEnd.ToString("s", DateTimeFormatInfo.InvariantInfo));

            return AutoTriageQueryFactory.ResolveQuery(query, parameters);
        }

        /// <summary>
        /// Gets tip session status events query.
        /// </summary>
        /// <param name="tipSessionId">Specifies the Tip session ID.</param>
        /// <returns>A valid kusto query.</returns>
        public static string GetTipSessionStatusEventsQuery(string tipSessionId)
        {
            tipSessionId.ThrowIfNullOrWhiteSpace(nameof(tipSessionId));

            string query = Properties.Resources.Diagnostics_TipSessionStatusEventsQuery;
            query.ThrowIfNullOrEmpty(nameof(query), "Invalid query read from resource file");

            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters.Add(Constants.TipSessionIdPlaceHolder, tipSessionId);

            return AutoTriageQueryFactory.ResolveQuery(query, parameters);
        }

        private static string ResolveQuery(string query, Dictionary<string, string> parameters)
        {
            foreach (var param in parameters)
            {
                query = !string.IsNullOrWhiteSpace(param.Value)
                    ? query = query.Replace(param.Key, $"dynamic('{param.Value}')", StringComparison.Ordinal)
                    : query.Replace(param.Key, AutoTriageQueryFactory.EmptyQueryValue, StringComparison.Ordinal);
            }

            return query;
        }

        private static void VerifyTimeRangeDates(DateTime timeRangeBegin, DateTime timeRangeEnd)
        {
            if (timeRangeBegin == DateTime.MinValue || timeRangeBegin == DateTime.MaxValue)
            {
                throw new ArgumentException("Invalid time range start", nameof(timeRangeBegin));
            }

            if (timeRangeEnd == DateTime.MinValue || timeRangeEnd == DateTime.MaxValue)
            {
                throw new ArgumentException("Invalid time range end", nameof(timeRangeEnd));
            }

            if (timeRangeBegin.Kind != DateTimeKind.Utc || timeRangeEnd.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("Time range is not specified in UTC");
            }

            if (timeRangeBegin > timeRangeEnd)
            {
                throw new ArgumentException("Time range start is greater than time range end");
            }
        }

        private class Constants
        {
            internal const string NodeId = "nodeId";
            internal const string NodeIdPlaceHolder = "$nodeId$";
            internal const string StartTime = "startTime";
            internal const string StartTimePlaceHolder = "$startTime$";
            internal const string EndTime = "endTime";
            internal const string EndTimePlaceHolder = "$endTime$";

            internal const string TipSessionId = "tipSessionId";
            internal const string TipSessionIdPlaceHolder = "$tipSessionId$";

            internal const string ResourceGroupNamePlaceHolder = "$rGName$";
            internal const string ResourceGroupName = "rgName";
        }
    }
}