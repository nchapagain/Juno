namespace Juno.Scheduler.Preconditions.Manager
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Text;
    using Juno.Contracts;
    using NUnit.Framework;
    using static Juno.Scheduler.Preconditions.Manager.KustoDataTableExtension;

    [TestFixture]
    [Category("Unit")]
    public class KustoDataTableExtensionTests
    {
        [Test]
        public void ParseSuccessfulExperimentsValidatesParameters()
        {
            DataTable nullTable = null;
            Assert.Throws<ArgumentException>(() => nullTable.ParseSingleRowSingleKustoColumn(KustoColumn.FailureRate));
        }

        [Test]
        public void ParseSingleRowSingleKustoColumnThrowsExceptionWhenMoreThanOneRowIsSupplied()
        {
            DataTable table = new DataTable();
            DataColumn column = new DataColumn("column");
            table.Columns.Add(column);
            
            DataRow row = table.NewRow();
            DataRow row2 = table.NewRow();

            row["column"] = "row1";
            row2["column"] = "row2";
            
            table.Rows.Add(row);
            table.Rows.Add(row2);

            Assert.Throws<ProviderException>(() => table.ParseSingleRowSingleKustoColumn(KustoColumn.ExperimentCount));
        }

        [Test]
        public void ParseSingleRowSingleKustoColumnThrowsExceptionWhenNoRowsAreSupplied()
        {
            DataTable table = new DataTable();
            table.Columns.Add(new DataColumn("Column"));

            Assert.Throws<ProviderException>(() => table.ParseSingleRowSingleKustoColumn(KustoColumn.ExperimentCount));
        }

        [Test]
        public void ParseSingleRowSingleKustoColumnThrowsExceptionWhenExpectedColumnDoesntExist()
        {
            DataTable table = new DataTable();
            table.Columns.Add(new DataColumn("column"));

            DataRow row = table.NewRow();
            row["column"] = 50;
            table.Rows.Add(row);

            Assert.Throws<ProviderException>(() => table.ParseSingleRowSingleKustoColumn(KustoColumn.ExperimentCount));
        }

        [Test]
        public void ParseSingleRowSingleKustoColumnThrowsExceptionWhenValueIsNotOfExpectedType()
        {
            DataTable table = new DataTable();
            table.Columns.Add(new DataColumn(KustoColumn.ExperimentCount));

            DataRow row = table.NewRow();
            row[KustoColumn.ExperimentCount] = "string";
            table.Rows.Add(row);

            Assert.Throws<ProviderException>(() => table.ParseSingleRowSingleKustoColumn(KustoColumn.ExperimentCount));
        }

        [Test]
        public void ParseSingleRowSingleKustoColumnReturnsExpectedValue()
        {
            const int expectedValue = 50;
            DataTable table = new DataTable();
            table.Columns.Add(new DataColumn(KustoColumn.ExperimentCount));

            DataRow row = table.NewRow();
            row[KustoColumn.ExperimentCount] = expectedValue;
            table.Rows.Add(row);

            int actualValue = table.ParseSingleRowSingleKustoColumn(KustoColumn.ExperimentCount);
            Assert.AreEqual(expectedValue, actualValue);
        }
    }
}
