namespace Juno.Execution.Providers.AutoTriage
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Threading.Tasks;
    using Juno.Extensions.Telemetry;
    using Microsoft.Azure.CRC.Kusto;
    using Moq;
    using NUnit.Framework;
    using Polly;

    [TestFixture]
    [Category("Unit")]
    public class KustoQueryIssuerExtensionTests
    {
        private const string NodeId = "node-id";
        private const string TipSessionId = "tip-session-id";
        private const string RGName = "resource-group-name";

        private ExperimentFixture mockFixture;
        private Mock<IKustoQueryIssuer> mockQueryIssuer;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new ExperimentFixture();
            this.mockFixture.SetupExperimentMocks();
            this.mockQueryIssuer = new Mock<IKustoQueryIssuer>();
        }

        [Test]
        public void GetMicrocodeUpdateDiagnosticsExtensionThrowsExceptionWhenNullObjectReceivedFromKustoDatabase()
        {
            this.mockQueryIssuer
                .Setup(method => method.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult<DataTable>(null));

            // Invoke kusto reader's GetResultsAsync method and verify whether reader throws exception.
            List<DiagnosticsEntry> results = KustoDiagnosticsQueryExtensions.GetMicrocodeUpdateDiagnosticsAsync(
                 this.mockQueryIssuer.Object,
                 KustoQueryIssuerExtensionTests.NodeId,
                 DateTime.UtcNow,
                 DateTime.UtcNow,
                 Policy.NoOpAsync()).GetAwaiter().GetResult() as List<DiagnosticsEntry>;

            // Verify results
            Assert.That(results, Is.Not.Null);
            Assert.That(results.Count, Is.EqualTo(0));
        }

        [Test]
        public void GetMicrocodeUpdateDiagnosticsExtensionThrowsExceptionWhenEmptyResultsReceivedFromKustoDatabase()
        {
            this.mockQueryIssuer
                .Setup(method => method.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(KustoQueryIssuerExtensionTests.CreateMicrocodeUpdateFailureEmptyKustoResponseTableAsync());

            // Invoke kusto reader's GetResultsAsync method and verify whether reader throws exception.
            List<DiagnosticsEntry> results = KustoDiagnosticsQueryExtensions.GetMicrocodeUpdateDiagnosticsAsync(
                 this.mockQueryIssuer.Object,
                 KustoQueryIssuerExtensionTests.NodeId,
                 DateTime.UtcNow,
                 DateTime.UtcNow,
                 Policy.NoOpAsync()).GetAwaiter().GetResult() as List<DiagnosticsEntry>;

            // Verify results
            Assert.That(results, Is.Not.Null);
            Assert.That(results.Count, Is.EqualTo(0));
        }

        [Test]
        public void GetMicrocodeUpdateDiagnosticsExtensionReturnsAValidDiagnosticsEntryListWhenAValidResultsReceivedFromKustoDatabase()
        {
            this.mockQueryIssuer
                .Setup(method => method.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(KustoQueryIssuerExtensionTests.CreateMicrocodeUpdateFailureKustoResponseTableWithValidResultsAsync());

            List<DiagnosticsEntry> results = KustoDiagnosticsQueryExtensions.GetMicrocodeUpdateDiagnosticsAsync(
                this.mockQueryIssuer.Object,
                KustoQueryIssuerExtensionTests.NodeId,
                DateTime.UtcNow,
                DateTime.UtcNow,
                Policy.NoOpAsync()).GetAwaiter().GetResult() as List<DiagnosticsEntry>;

            // Verify results.
            Assert.That(results, Is.Not.Null);
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Source, Is.EqualTo("Kusto.AzureCM.CSIMicrocode"));
            KustoQueryIssuerExtensionTests.VerifyMessageBeginsWithRightTime(results[0].Message["timestamp"].ToString(), "7/25/2020 2:50:20 AM");
            Assert.IsTrue(results[0].Message.Keys.Contains("status"));
            Assert.IsTrue(results[0].Message.Keys.Contains("signature"));
            Assert.IsTrue(results[0].Message.Keys.Contains("description"));
        }

        [Test]
        public void GetTipDeploymentDiagnosticsExtensionThrowsExceptionWhenNullObjectReceivedFromKustoDatabase()
        {
            this.mockQueryIssuer
                .Setup(method => method.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult<DataTable>(null));

            // Invoke kusto reader's GetResultsAsync method and verify whether reader throws exception.
            List<DiagnosticsEntry> results = KustoDiagnosticsQueryExtensions.GetTipDeploymentFailureDiagnosticsAsync(
                 this.mockQueryIssuer.Object,
                 KustoQueryIssuerExtensionTests.TipSessionId,
                 Policy.NoOpAsync()).GetAwaiter().GetResult() as List<DiagnosticsEntry>;

            // Verify results
            Assert.That(results, Is.Not.Null);
            Assert.That(results.Count, Is.EqualTo(0));
        }

        [Test]
        public void GetTipDeploymentDiagnosticsExtensionThrowsExceptionWhenEmptyResultsReceivedFromKustoDatabase()
        {
            this.mockQueryIssuer
                .Setup(method => method.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(KustoQueryIssuerExtensionTests.CreateTipSessionStatusEventsEmptyKustoResponseTableAsync());

            // Invoke kusto reader's GetResultsAsync method and verify whether reader throws exception.
            List<DiagnosticsEntry> results = KustoDiagnosticsQueryExtensions.GetTipDeploymentFailureDiagnosticsAsync(
                 this.mockQueryIssuer.Object,
                 KustoQueryIssuerExtensionTests.TipSessionId,
                 Policy.NoOpAsync()).GetAwaiter().GetResult() as List<DiagnosticsEntry>;

            // Verify results
            Assert.That(results, Is.Not.Null);
            Assert.That(results.Count, Is.EqualTo(0));
        }

        [Test]
        public void GetTipDeploymentDiagnosticsExtensionReturnsAValidDiagnosticsEntryListWhenAValidResultsReceivedFromKustoDatabase()
        {
            this.mockQueryIssuer
                .Setup(method => method.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(KustoQueryIssuerExtensionTests.CreateTipSessionStatusEventsKustoResponseTableWithValidResultsAsync());

            List<DiagnosticsEntry> results = KustoDiagnosticsQueryExtensions.GetTipDeploymentFailureDiagnosticsAsync(
                this.mockQueryIssuer.Object,
                KustoQueryIssuerExtensionTests.TipSessionId,
                Policy.NoOpAsync()).GetAwaiter().GetResult() as List<DiagnosticsEntry>;

            // Verify results.
            Assert.That(results, Is.Not.Null);
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Source, Is.EqualTo("Kusto.AzureCM.LogTipNodeSessionStatusEventMessage"));
            KustoQueryIssuerExtensionTests.VerifyMessageBeginsWithRightTime(results[0].Message["timestamp"].ToString(), "7/25/2020 2:50:20 AM");
            Assert.IsTrue(results[0].Message.Keys.Contains("tipNodeSessionId"));
            Assert.IsTrue(results[0].Message["tipNodeSessionId"].ToString() == "TipSessionId");
            Assert.IsTrue(results[0].Message.Keys.Contains("availabilityZone"));
            Assert.IsTrue(results[0].Message["availabilityZone"].ToString() == "AZ");
            Assert.IsTrue(results[0].Message.Keys.Contains("tenant"));
            Assert.IsTrue(results[0].Message["tenant"].ToString() == "Tenant");
            Assert.IsTrue(results[0].Message.Keys.Contains("message"));
            Assert.IsTrue(results[0].Message["message"].ToString() == "Message");
        }

        [Test]
        public void GetArmVmDeploymentFailureKustoDiagnosticsExtensionThrowsExceptionWhenNullObjectReceivedFromKustoDatabase()
        {
            this.mockQueryIssuer
                .Setup(method => method.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult<DataTable>(null));

            // Invoke kusto reader's GetResultsAsync method and verify whether reader throws exception.
            List<DiagnosticsEntry> results = KustoDiagnosticsQueryExtensions.GetArmVmDeploymentFailureKustoDiagnosticsAsync(
                 this.mockQueryIssuer.Object,
                 KustoQueryIssuerExtensionTests.NodeId,
                 KustoQueryIssuerExtensionTests.RGName,
                 DateTime.UtcNow,
                 DateTime.UtcNow,
                 Policy.NoOpAsync()).GetAwaiter().GetResult().ToList();

            // Verify results
            Assert.That(results, Is.Not.Null);
            Assert.That(results.Count, Is.EqualTo(0));
        }

        [Test]
        public void GetArmVmDeploymentFailureKustoDiagnosticsExtensionThrowsExceptionWhenEmptyResultsReceivedFromKustoDatabase()
        {
            this.mockQueryIssuer
                .Setup(method => method.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(KustoQueryIssuerExtensionTests.CreateArmVmDeploymentOperationEmptyKustoResponseTableAsync())
                .Callback(() =>
                {
                    this.mockQueryIssuer
                        .Setup(method => method.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                        .Returns(KustoQueryIssuerExtensionTests.CreateArmVmAPIQoSEmptyKustoResponseTableAsync())
                        .Callback(() =>
                        {
                            this.mockQueryIssuer
                                .Setup(method => method.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                                .Returns(KustoQueryIssuerExtensionTests.CreateArmVmLogNodeSnapshotEmptyKustoResponseTableAsync());
                        });
                });

            // Invoke kusto reader's GetResultsAsync method and verify whether reader throws exception.
            List<DiagnosticsEntry> results = KustoDiagnosticsQueryExtensions.GetArmVmDeploymentFailureKustoDiagnosticsAsync(
                 this.mockQueryIssuer.Object,
                 KustoQueryIssuerExtensionTests.NodeId,
                 KustoQueryIssuerExtensionTests.RGName,
                 DateTime.UtcNow,
                 DateTime.UtcNow,
                 Policy.NoOpAsync()).GetAwaiter().GetResult().ToList();

            // Verify results
            Assert.That(results, Is.Not.Null);
            Assert.That(results.Count, Is.EqualTo(0));
        }

        [Test]
        public void GetArmVmDeploymentFailureKustoDiagnosticsExtensionReturnsAValidDiagnosticsEntryListWhenAValidResultsReceivedFromKustoDatabase()
        {
            this.mockQueryIssuer
                .Setup(method => method.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(KustoQueryIssuerExtensionTests.CreateArmVmDeploymentOperationKustoResponseTableWithValidResultsAsync())
                .Callback(() =>
                {
                    this.mockQueryIssuer
                        .Setup(method => method.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                        .Returns(KustoQueryIssuerExtensionTests.CreateArmVmAPIQoSKustoResponseTableWithValidResultsAsync())
                        .Callback(() =>
                        {
                            this.mockQueryIssuer
                                .Setup(method => method.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                                .Returns(KustoQueryIssuerExtensionTests.CreateArmVmLogNodeSnapshotKustoResponseTableWithValidResultsAsync());
                        });
                });

            List<DiagnosticsEntry> results = KustoDiagnosticsQueryExtensions.GetArmVmDeploymentFailureKustoDiagnosticsAsync(
                this.mockQueryIssuer.Object,
                KustoQueryIssuerExtensionTests.TipSessionId,
                KustoQueryIssuerExtensionTests.RGName,
                DateTime.UtcNow,
                DateTime.UtcNow,
                Policy.NoOpAsync()).GetAwaiter().GetResult().ToList();

            // Verify results.
            Assert.That(results, Is.Not.Null);
            Assert.That(results.Count, Is.EqualTo(3));
            Assert.That(results[0].Source, Is.EqualTo("Kusto.ARMProd.DeploymentOperations"));
            KustoQueryIssuerExtensionTests.VerifyMessageBeginsWithRightTime(results[0].Message["timestamp"].ToString(), "7/25/2020 2:50:20 AM");
            Assert.IsTrue(results[0].Message.Keys.Contains("tenantId"));
            Assert.IsTrue(results[0].Message.Keys.Contains("resourceGroupName"));
            Assert.IsTrue(results[0].Message.Keys.Contains("executionStatus"));
            Assert.IsTrue(results[0].Message.Keys.Contains("statusCode"));
            Assert.IsTrue(results[0].Message.Keys.Contains("statusMessage"));
            Assert.That(results[1].Source, Is.EqualTo("Kusto.AzCrp.VmApiQosEvent"));
            KustoQueryIssuerExtensionTests.VerifyMessageBeginsWithRightTime(results[1].Message["timestamp"].ToString(), "7/25/2020 2:50:20 AM");
            Assert.IsTrue(results[1].Message.Keys.Contains("correlationId"));
            Assert.IsTrue(results[1].Message.Keys.Contains("operationId"));
            Assert.IsTrue(results[1].Message.Keys.Contains("resourceGroupName"));
            Assert.IsTrue(results[1].Message.Keys.Contains("resourceName"));
            Assert.IsTrue(results[1].Message.Keys.Contains("subscriptionId"));
            Assert.IsTrue(results[1].Message.Keys.Contains("exceptionType"));
            Assert.IsTrue(results[1].Message.Keys.Contains("errorDetails"));
            Assert.IsTrue(results[1].Message.Keys.Contains("vMId"));
            Assert.IsTrue(results[1].Message.Keys.Contains("vMSize"));
            Assert.IsTrue(results[1].Message.Keys.Contains("oSType"));
            Assert.IsTrue(results[1].Message.Keys.Contains("oSDiskStorageAccountType"));
            Assert.IsTrue(results[1].Message.Keys.Contains("availabilitySet"));
            Assert.IsTrue(results[1].Message.Keys.Contains("fabricCluster"));
            Assert.IsTrue(results[1].Message.Keys.Contains("allocationAction"));
            Assert.That(results[2].Source, Is.EqualTo("Kusto.AzureCM.LogNodeSnapshot"));
            KustoQueryIssuerExtensionTests.VerifyMessageBeginsWithRightTime(results[2].Message["timestamp"].ToString(), "7/25/2020 2:50:20 AM");
            Assert.IsTrue(results[2].Message.Keys.Contains("nodeState"));
            Assert.IsTrue(results[2].Message.Keys.Contains("nodeAvailabilityState"));
            Assert.IsTrue(results[2].Message.Keys.Contains("faultInfo"));
            Assert.IsTrue(results[2].Message.Keys.Contains("hostingEnvironment"));
            Assert.IsTrue(results[2].Message.Keys.Contains("faultDomain"));
            KustoQueryIssuerExtensionTests.VerifyMessageBeginsWithRightTime(results[2].Message["lastStateChangeTime"].ToString(), "7/25/2020 3:30:00 AM");
            Assert.IsTrue(results[2].Message.Keys.Contains("nsProgressHealthStatus"));
            Assert.IsTrue(results[2].Message.Keys.Contains("tipNodeSessionId"));
            Assert.IsTrue(results[2].Message.Keys.Contains("healthSignals")); 
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "AZCA1002:AsyncMethodNaming Rule", Justification = "Test code")]
        private static Task<DataTable> CreateMicrocodeUpdateFailureKustoResponseTableWithValidResultsAsync()
        {
            // Create a new DataTable and add a row.
            DataTable table = KustoQueryIssuerExtensionTests.CreateMicrocodeUpdateFailureEmptyKustoResponseTableAsync().GetAwaiter().GetResult();

            DataRow row = table.NewRow();
            row["Timestamp"] = DateTime.Parse("7/25/2020 2:50:20 AM");
            row["Status"] = "Status";
            row["Signature"] = "Signature";
            row["Description"] = "Description";
            table.Rows.Add(row);

            return Task.FromResult(table);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1204:Static elements should appear before instance elements", Justification = "Test code")]
        private static Task<DataTable> CreateMicrocodeUpdateFailureEmptyKustoResponseTableAsync()
        {
            // Create a new DataTable with schema.
            DataTable table = new DataTable("AzureCM");
            table.Columns.Add(KustoQueryIssuerExtensionTests.CreateColumn("Timestamp"));
            table.Columns.Add(KustoQueryIssuerExtensionTests.CreateColumn("Status"));
            table.Columns.Add(KustoQueryIssuerExtensionTests.CreateColumn("Signature"));
            table.Columns.Add(KustoQueryIssuerExtensionTests.CreateColumn("Description"));
            return Task.FromResult(table);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "AZCA1002:AsyncMethodNaming Rule", Justification = "Test code")]
        private static Task<DataTable> CreateTipSessionStatusEventsKustoResponseTableWithValidResultsAsync()
        {
            // Create a new DataTable and add a row.
            DataTable table = KustoQueryIssuerExtensionTests.CreateTipSessionStatusEventsEmptyKustoResponseTableAsync().GetAwaiter().GetResult();

            DataRow row = table.NewRow();
            row["TIMESTAMP"] = DateTime.Parse("7/25/2020 2:50:20 AM");
            row["tipNodeSessionId"] = "TipSessionId";
            row["AvailabilityZone"] = "AZ";
            row["Tenant"] = "Tenant";
            row["message"] = "Message";
            table.Rows.Add(row);
            return Task.FromResult(table);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1204:Static elements should appear before instance elements", Justification = "Test code")]
        private static Task<DataTable> CreateTipSessionStatusEventsEmptyKustoResponseTableAsync()
        {
            // Create a new DataTable with schema.
            DataTable table = new DataTable("AzureCM");
            table.Columns.Add(KustoQueryIssuerExtensionTests.CreateColumn("TIMESTAMP"));
            table.Columns.Add(KustoQueryIssuerExtensionTests.CreateColumn("tipNodeSessionId"));
            table.Columns.Add(KustoQueryIssuerExtensionTests.CreateColumn("AvailabilityZone"));
            table.Columns.Add(KustoQueryIssuerExtensionTests.CreateColumn("Tenant"));
            table.Columns.Add(KustoQueryIssuerExtensionTests.CreateColumn("message"));
            return Task.FromResult(table);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "AZCA1002:AsyncMethodNaming Rule", Justification = "Test code")]
        private static Task<DataTable> CreateArmVmDeploymentOperationKustoResponseTableWithValidResultsAsync()
        {
            // Create a new DataTable and add a row.
            DataTable table = KustoQueryIssuerExtensionTests.CreateArmVmDeploymentOperationEmptyKustoResponseTableAsync().GetAwaiter().GetResult();

            DataRow row = table.NewRow();
            row["timestamp"] = DateTime.Parse("7/25/2020 2:50:20 AM");
            row["tenantId"] = "tenantId";
            row["resourceGroupName"] = "resourceGroupName";
            row["executionStatus"] = "executionStatus";
            row["statusCode"] = "statusCode";
            row["statusMessage"] = "statusMessage";
            table.Rows.Add(row);

            return Task.FromResult(table);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1204:Static elements should appear before instance elements", Justification = "Test code")]
        private static Task<DataTable> CreateArmVmDeploymentOperationEmptyKustoResponseTableAsync()
        {
            // Create a new DataTable with schema.
            DataTable table = new DataTable("ARMProd");
            table.Columns.Add(KustoQueryIssuerExtensionTests.CreateColumn("timestamp"));
            table.Columns.Add(KustoQueryIssuerExtensionTests.CreateColumn("tenantId"));
            table.Columns.Add(KustoQueryIssuerExtensionTests.CreateColumn("resourceGroupName"));
            table.Columns.Add(KustoQueryIssuerExtensionTests.CreateColumn("executionStatus"));
            table.Columns.Add(KustoQueryIssuerExtensionTests.CreateColumn("statusCode"));
            table.Columns.Add(KustoQueryIssuerExtensionTests.CreateColumn("statusMessage"));
            return Task.FromResult(table);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "AZCA1002:AsyncMethodNaming Rule", Justification = "Test code")]
        private static Task<DataTable> CreateArmVmAPIQoSKustoResponseTableWithValidResultsAsync()
        {
            // Create a new DataTable and add a row.
            DataTable table = KustoQueryIssuerExtensionTests.CreateArmVmAPIQoSEmptyKustoResponseTableAsync().GetAwaiter().GetResult();

            DataRow row = table.NewRow();
            row["timestamp"] = DateTime.Parse("7/25/2020 2:50:20 AM");
            row["correlationId"] = "correlationId";
            row["operationId"] = "operationId";
            row["resourceGroupName"] = "resourceGroupName";
            row["resourceName"] = "resourceName";
            row["subscriptionId"] = "subscriptionId";
            row["exceptionType"] = "exceptionType";
            row["errorDetails"] = "errorDetails";
            row["vMId"] = "vMId";
            row["vMSize"] = "vMSize";
            row["oSType"] = "oSType";
            row["oSDiskStorageAccountType"] = "oSDiskStorageAccountType";
            row["availabilitySet"] = "availabilitySet";
            row["fabricCluster"] = "fabricCluster";
            row["allocationAction"] = "allocationAction";
            table.Rows.Add(row);

            return Task.FromResult(table);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1204:Static elements should appear before instance elements", Justification = "Test code")]
        private static Task<DataTable> CreateArmVmAPIQoSEmptyKustoResponseTableAsync()
        {
            // Create a new DataTable with schema.
            DataTable table = new DataTable("AzCrp");
            table.Columns.Add(KustoQueryIssuerExtensionTests.CreateColumn("timestamp"));
            table.Columns.Add(KustoQueryIssuerExtensionTests.CreateColumn("correlationId"));
            table.Columns.Add(KustoQueryIssuerExtensionTests.CreateColumn("operationId"));
            table.Columns.Add(KustoQueryIssuerExtensionTests.CreateColumn("resourceGroupName"));
            table.Columns.Add(KustoQueryIssuerExtensionTests.CreateColumn("resourceName"));
            table.Columns.Add(KustoQueryIssuerExtensionTests.CreateColumn("subscriptionId"));
            table.Columns.Add(KustoQueryIssuerExtensionTests.CreateColumn("exceptionType"));
            table.Columns.Add(KustoQueryIssuerExtensionTests.CreateColumn("errorDetails"));
            table.Columns.Add(KustoQueryIssuerExtensionTests.CreateColumn("vMId"));
            table.Columns.Add(KustoQueryIssuerExtensionTests.CreateColumn("vMSize"));
            table.Columns.Add(KustoQueryIssuerExtensionTests.CreateColumn("oSType"));
            table.Columns.Add(KustoQueryIssuerExtensionTests.CreateColumn("oSDiskStorageAccountType"));
            table.Columns.Add(KustoQueryIssuerExtensionTests.CreateColumn("availabilitySet"));
            table.Columns.Add(KustoQueryIssuerExtensionTests.CreateColumn("fabricCluster"));
            table.Columns.Add(KustoQueryIssuerExtensionTests.CreateColumn("allocationAction"));
            return Task.FromResult(table);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "AZCA1002:AsyncMethodNaming Rule", Justification = "Test code")]
        private static Task<DataTable> CreateArmVmLogNodeSnapshotKustoResponseTableWithValidResultsAsync()
        {
            // Create a new DataTable and add a row.
            DataTable table = KustoQueryIssuerExtensionTests.CreateArmVmLogNodeSnapshotEmptyKustoResponseTableAsync().GetAwaiter().GetResult();

            DataRow row = table.NewRow();
            row["timestamp"] = DateTime.Parse("7/25/2020 2:50:20 AM");
            row["nodeState"] = "nodeState";
            row["nodeAvailabilityState"] = "nodeAvailabilityState";
            row["faultInfo"] = "faultInfo";
            row["hostingEnvironment"] = "hostingEnvironment";
            row["faultDomain"] = "faultDomain";
            row["lastStateChangeTime"] = DateTime.Parse("7/25/2020 3:30:00 AM");
            row["nsProgressHealthStatus"] = "nsProgressHealthStatus";
            row["tipNodeSessionId"] = "tipNodeSessionId";
            row["healthSignals"] = "healthSignals";
            table.Rows.Add(row);

            return Task.FromResult(table);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1204:Static elements should appear before instance elements", Justification = "Test code")]
        private static Task<DataTable> CreateArmVmLogNodeSnapshotEmptyKustoResponseTableAsync()
        {
            // Create a new DataTable with schema.
            DataTable table = new DataTable("AzureCM");
            table.Columns.Add(KustoQueryIssuerExtensionTests.CreateColumn("timestamp"));
            table.Columns.Add(KustoQueryIssuerExtensionTests.CreateColumn("nodeState"));
            table.Columns.Add(KustoQueryIssuerExtensionTests.CreateColumn("nodeAvailabilityState"));
            table.Columns.Add(KustoQueryIssuerExtensionTests.CreateColumn("faultInfo"));
            table.Columns.Add(KustoQueryIssuerExtensionTests.CreateColumn("hostingEnvironment"));
            table.Columns.Add(KustoQueryIssuerExtensionTests.CreateColumn("faultDomain"));
            table.Columns.Add(KustoQueryIssuerExtensionTests.CreateColumn("lastStateChangeTime"));
            table.Columns.Add(KustoQueryIssuerExtensionTests.CreateColumn("nsProgressHealthStatus"));
            table.Columns.Add(KustoQueryIssuerExtensionTests.CreateColumn("tipNodeSessionId"));
            table.Columns.Add(KustoQueryIssuerExtensionTests.CreateColumn("healthSignals"));
            return Task.FromResult(table);
        }

        private static DataColumn CreateColumn(string columnName)
        {
            DataColumn column = new DataColumn();
            column.DataType = Type.GetType("System.String");
            column.ColumnName = columnName;
            column.AutoIncrement = false;
            column.Caption = columnName;
            column.ReadOnly = false;
            column.Unique = false;
            return column;
        }

        private static void VerifyMessageBeginsWithRightTime(string message, string time)
        {
            string firstTime = message.Split(',')[0];
            Assert.AreEqual(DateTime.Parse(firstTime), DateTime.Parse(time));
        }
    }
}