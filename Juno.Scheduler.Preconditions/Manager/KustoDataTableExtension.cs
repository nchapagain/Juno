namespace Juno.Scheduler.Preconditions.Manager
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Extensions;

    /// <summary>
    /// Extension Methods to Parse Responses for Kusto
    /// </summary>
    public static class KustoDataTableExtension
    {
        internal static List<JunoOFRNode> ParseOFRNodes(this DataTable dataTable)
        {
            List<JunoOFRNode> ofrNodes = new List<JunoOFRNode>();

            if (dataTable?.Rows != null)
            {
                foreach (DataRow row in dataTable.Rows)
                {
                    if (!DateTime.TryParse((string)row[KustoColumn.Timestamp], out DateTime timeStampOFR))
                    {
                        throw new FormatException("Unable to parse TimeStamp of OFRNode from Kusto Response");
                    }

                    JunoOFRNode ofrNode = new JunoOFRNode()
                    {
                        TimeStamp = timeStampOFR,
                        NodeId = (string)row[KustoColumn.NodeId],
                        TipSessionId = (string)row[KustoColumn.TipNodeSessionId],
                        ExperimentName = dataTable.Columns.Contains(KustoColumn.ExperimentName) ? (string)row[KustoColumn.ExperimentName] : null,
                        ExperimentId = dataTable.Columns.Contains(KustoColumn.ExperimentId) ? (string)row[KustoColumn.ExperimentId] : null
                    };

                    ofrNodes.Add(ofrNode);
                }
            }

            return ofrNodes;
        }

        internal static int ParseSingleRowSingleKustoColumn(this DataTable dataTable, string kustoColumnName)
        {
            dataTable.ThrowIfNull(nameof(dataTable));

            int result = -1;

            if (dataTable.Rows.Count < 1)
            {
                throw new ProviderException($"Unexpected result set. The query for {kustoColumnName} has less than one row");
            }

            if (dataTable.Rows.Count > 1)
            {
                throw new ProviderException($"Unexpected result set. The query for {kustoColumnName} has returned more than one one row");
            }

            if (!dataTable.Columns.Contains(kustoColumnName))
            {
                throw new ProviderException($"The query returned a data table in the incorrect format. Missing column: {kustoColumnName}");
            }

            foreach (DataRow row in dataTable.Rows)
            {
                if (!int.TryParse((string)row[kustoColumnName], out result))
                {
                    throw new ProviderException("The query returned a value that can not be parsed as an int");
                }
            }

            return result;
        }

        internal static class KustoColumn
        {
            internal const string Timestamp = "TIMESTAMP";
            internal const string NodeId = "nodeId";
            internal const string TipNodeSessionId = "tipnodeSessionId";
            internal const string ExperimentName = "experimentName";
            internal const string ExperimentId = "experimentId";
            internal const string ExperimentCount = "experimentCount";
            internal const string FailureRate = "failureRate";
        }
    }
}
