namespace Juno.Execution.Providers.AutoTriage
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Moq;
    using NUnit.Framework;
    using Polly;

    [TestFixture]
    [Category("Unit")]
    public class ArmVmDeploymentFailureKustoDiagnosticsTests
    {
        private DiagnosticsProviderFixture mockFixture;
        private TestArmVmDeploymentFailureKustoDiagnostics provider;
        private DiagnosticsRequest diagnosticRequest;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new DiagnosticsProviderFixture();
            this.provider = new TestArmVmDeploymentFailureKustoDiagnostics(this.mockFixture.Services);
            this.diagnosticRequest = this.CreateArmVmDeploymentKustoDiagnosticRequest();
            this.SetupMockDefaults();
        }

        [Test]
        [TestCase(DiagnosticsParameter.ResourceGroupName, "invalidParam")]
        [TestCase(DiagnosticsParameter.ResourceGroupName, DiagnosticsParameter.ExperimentId)]
        [TestCase(DiagnosticsParameter.ResourceGroupName, DiagnosticsParameter.TipSessionChangeId)]
        [TestCase(DiagnosticsParameter.ResourceGroupName, DiagnosticsParameter.TipNodeId)]
        [TestCase(DiagnosticsParameter.ResourceGroupName, DiagnosticsParameter.SubscriptionId)]
        [TestCase(DiagnosticsParameter.ResourceGroupName, DiagnosticsParameter.ProviderName)]
        [TestCase(DiagnosticsParameter.ResourceGroupName, DiagnosticsParameter.TimeRangeBegin)]
        [TestCase(DiagnosticsParameter.ResourceGroupName, DiagnosticsParameter.TimeRangeEnd)]
        [TestCase(DiagnosticsParameter.ResourceGroupName, "")]
        [TestCase(DiagnosticsParameter.TipSessionId, "invalidParam")]
        [TestCase(DiagnosticsParameter.TipSessionId, DiagnosticsParameter.ExperimentId)]
        [TestCase(DiagnosticsParameter.TipSessionId, DiagnosticsParameter.TipSessionChangeId)]
        [TestCase(DiagnosticsParameter.TipSessionId, DiagnosticsParameter.TipNodeId)]
        [TestCase(DiagnosticsParameter.TipSessionId, DiagnosticsParameter.SubscriptionId)]
        [TestCase(DiagnosticsParameter.TipSessionId, DiagnosticsParameter.ProviderName)]
        [TestCase(DiagnosticsParameter.TipSessionId, DiagnosticsParameter.TimeRangeBegin)]
        [TestCase(DiagnosticsParameter.TipSessionId, DiagnosticsParameter.TimeRangeEnd)]
        [TestCase(DiagnosticsParameter.TipSessionId, "")]
        public void ArmVmDeploymentFailureKustoDiagnosticsProviderSkipsDiagnosticsForUnsupportedDiagnosticRequests(string expectedParam1, string expectedParam2)
        {
            var diagnosticsRequest = new DiagnosticsRequest(
                this.mockFixture.ExperimentId,
                "ID",
                DiagnosticsIssueType.ArmVmCreationFailure,
                DateTime.UtcNow.AddHours(-1),
                DateTime.UtcNow,
                new Dictionary<string, IConvertible>()
                {
                    { expectedParam1, Guid.NewGuid().ToString() },
                    { expectedParam2, Guid.NewGuid().ToString() },
                });

            // Verify this request is not handled by the current diagnostic provider
            Assert.IsFalse(this.provider.IsHandled(diagnosticsRequest));

            // Invoke handler's DiagnoseAsync with an invalid issue type.
            var result = this.provider.DiagnoseAsync(
                diagnosticsRequest,
                this.mockFixture.EventContext,
                this.mockFixture.Logger.Object,
                this.mockFixture.CancellationToken)
                .GetAwaiter()
                .GetResult();

            // Verify IssueAsync method was never called from DiagnoseAsync function
            this.mockFixture.QueryIssuer.Verify(method => method.IssueAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);

            // Ensure that the execution result is succeeded for non-handled requests
            Assert.AreEqual(result.Status, ExecutionStatus.Succeeded);
        }

        [Test]
        [TestCase(DiagnosticsIssueType.Undefined)]
        [TestCase(DiagnosticsIssueType.TipPilotfishPackageDeploymentFailure)]
        [TestCase(DiagnosticsIssueType.MicrocodeUpdateFailure)]
        public void ArmVmDeploymentFailureKustoDiagnosticsProviderSkipsDiagnosticsForAnInvalidIssueType(DiagnosticsIssueType issueType)
        {
            // valid diagnostic request
            DiagnosticsRequest diagnosticRequest = new DiagnosticsRequest(
                Guid.NewGuid().ToString(),
                "ID",
                issueType,
                DateTime.UtcNow,
                DateTime.UtcNow,
                this.diagnosticRequest.Context);

            // Verify this request is not handled by the current diagnostic provider
            Assert.AreEqual(this.provider.IsHandled(diagnosticRequest), false);

            // Invoke handler's DiagnoseAsync with an invalid issue type.
            var result = this.provider.DiagnoseAsync(
                diagnosticRequest,
                new EventContext(Guid.NewGuid()),
                this.mockFixture.Logger.Object,
                this.mockFixture.CancellationToken).GetAwaiter().GetResult();

            // Verify IssueAsync method was never called from DiagnoseAsync function
            this.mockFixture.QueryIssuer.Verify(method => method.IssueAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never());

            // Ensure that the execution result is succeeded for non-handled requests
            Assert.AreEqual(result.Status, ExecutionStatus.Succeeded);
        }

        [Test]
        public void ArmVmDeploymentFailureKustoDiagnosticsProviderRunsDiagnosticsForRecognizedDiagnosticRequest()
        {
            // Creates a valid request for this provider
            DiagnosticsRequest diagnosticsRequest = this.diagnosticRequest;

            // Invoke provider's DiagnoseAsync with non-VmResourceGroupDefinition context.
            Assert.IsTrue(this.provider.IsHandled(diagnosticsRequest));

            // Invoke handler's DiagnoseAsync method
            var result = this.provider.DiagnoseAsync(
                    this.diagnosticRequest,
                    this.mockFixture.EventContext,
                    this.mockFixture.Logger.Object,
                    this.mockFixture.CancellationToken).GetAwaiter().GetResult();

            // Verifies the IssueAsync function was performed for the successful query
            this.mockFixture.QueryIssuer.Verify(method => method.IssueAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(3));

            // Diagnostic Execution is expected to succeed with the valid diagnostic request
            Assert.AreEqual(result.Status, ExecutionStatus.Succeeded);
        }

        [Test]
        public void ArmVmDeploymentFailureKustoDiagnosticsProviderUtilizesFallbackAndLogsExceptionWhenAnErrorOccurs()
        {
            string expectedEventName = $"{nameof(ArmVmDeploymentFailureKustoDiagnostics)}.Diagnose{EventNameSuffix.Error}";
            // set up query issuer to throw an exception
            this.mockFixture.QueryIssuer.SetupSequence(method =>
                method.IssueAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .Throws(new Exception("The expected exception"));

            IAsyncPolicy retryPolicy = Policy.Handle<Exception>()
                .WaitAndRetryAsync(1, (retries) => TimeSpan.FromMilliseconds(1));

            this.provider = new TestArmVmDeploymentFailureKustoDiagnostics(this.mockFixture.Services, retryPolicy);

            // Invoke provider's DiagnoseAsync with VmResourceGroupDefinition context.
            var result = this.provider.DiagnoseAsync(
                    this.diagnosticRequest,
                    this.mockFixture.EventContext,
                    this.mockFixture.Logger.Object,
                    this.mockFixture.CancellationToken).GetAwaiter().GetResult();

            // Verify IssueAsync method called each kusto query for a total of 6 times (3 initial, 3 with 1 retry each).
            this.mockFixture.QueryIssuer.Verify(method => method.IssueAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(6));

            // Verify the logger logs an error for the failed retries 3 times
            this.mockFixture.Logger.Verify(method => method.Log(
                It.Is<LogLevel>(level => level == LogLevel.Error),
                It.Is<EventId>(action => action ==
                    new EventId(
                        expectedEventName.GetHashCode(StringComparison.OrdinalIgnoreCase),
                        expectedEventName)),
                It.Is<EventContext>(context => context.ActivityId == this.mockFixture.EventContext.ActivityId),
                null,
                null), Times.Exactly(3));

            Assert.AreEqual(result.Status, ExecutionStatus.Succeeded);
        }

        [Test]
        public void ArmVmDeploymentFailureKustoDiagnosticsProviderExecutesExpectedWorkflowForSuccessfulDiagnostics()
        {
            var expectedStartEventName = $"{nameof(ArmVmDeploymentFailureKustoDiagnostics)}.DiagnoseStart";
            var expectedResultEventName = $"{nameof(ArmVmDeploymentFailureKustoDiagnostics)}.DiagnosticsResults";
            var expectedEndEventName = $"{nameof(ArmVmDeploymentFailureKustoDiagnostics)}.DiagnoseStop";

            var result = this.provider.DiagnoseAsync(
                this.diagnosticRequest,
                this.mockFixture.EventContext,
                this.mockFixture.Logger.Object,
                this.mockFixture.CancellationToken)
                .GetAwaiter()
                .GetResult();

            // Diagnostic Provider initialized the state via the GetOrCreate State call
            this.mockFixture.DataClient.Verify(client => client.GetOrCreateStateAsync<DiagnosticState>(
                this.mockFixture.ExperimentId,
                $"{this.mockFixture.ExperimentId}-diagnostics",
                this.mockFixture.CancellationToken,
                $"{this.diagnosticRequest.Id}-diagnostics-state"));

            // The logger captures the start event for diagnostics
            this.mockFixture.Logger.Verify(method => method.Log(
                It.Is<LogLevel>(level => level == LogLevel.Information),
                It.Is<EventId>(action => action ==
                    new EventId(
                        expectedStartEventName.GetHashCode(StringComparison.OrdinalIgnoreCase),
                        expectedStartEventName)),
                It.Is<EventContext>(context => context.ActivityId == this.mockFixture.EventContext.ActivityId),
                null,
                null), Times.Exactly(1));

            // The logger captures the diagnostic data based on successful workflow
            this.mockFixture.Logger.Verify(method => method.Log(
                It.Is<LogLevel>(level => level == LogLevel.Trace),
                It.Is<EventId>(action => action ==
                    new EventId(
                        expectedResultEventName.GetHashCode(StringComparison.OrdinalIgnoreCase),
                        expectedResultEventName)),
                It.Is<EventContext>(
                    context => context.ActivityId == this.mockFixture.EventContext.ActivityId
                    && context.Properties != null
                    && context.Properties.ContainsKey("items") // contains diagnostic entries
                    && !string.IsNullOrWhiteSpace(context.Properties["items"].ToString())),
                null,
                null), Times.Exactly(1));

            // The logger captures the stop event for diagnostics
            this.mockFixture.Logger.Verify(method => method.Log(
                It.Is<LogLevel>(level => level == LogLevel.Information),
                It.Is<EventId>(action => action ==
                    new EventId(
                        expectedEndEventName.GetHashCode(StringComparison.OrdinalIgnoreCase),
                        expectedEndEventName)),
                It.Is<EventContext>(context => context.ActivityId == this.mockFixture.EventContext.ActivityId),
                null,
                null), Times.Exactly(1));

            // The provider saves the current diagnostic provider state before exiting DiagnoseAsync function
            this.mockFixture.DataClient.Verify(client => client.SaveStateAsync<DiagnosticState>(
                this.mockFixture.ExperimentId.ToString(),
                $"{this.mockFixture.ExperimentId}-diagnostics",
                It.IsAny<DiagnosticState>(),
                this.mockFixture.CancellationToken,
                $"{this.diagnosticRequest.Id}-diagnostics-state"));

            // The diagnostics were executed successfully
            Assert.AreEqual(result.Status, ExecutionStatus.Succeeded);
        }

        /// <summary>
        /// Creates a valid deployment operation kusto response table for arm vm diagostic provider
        /// </summary>
        private static DataTable CreateValidArmVmDeploymentOperationKustoResponseTable()
        {
            DataTable table = new DataTable("ARMProd");
            table.Columns.Add(DiagnosticsProviderFixture.CreateColumn("timestamp"));
            table.Columns.Add(DiagnosticsProviderFixture.CreateColumn("tenantId"));
            table.Columns.Add(DiagnosticsProviderFixture.CreateColumn("resourceGroupName"));
            table.Columns.Add(DiagnosticsProviderFixture.CreateColumn("executionStatus"));
            table.Columns.Add(DiagnosticsProviderFixture.CreateColumn("statusCode"));
            table.Columns.Add(DiagnosticsProviderFixture.CreateColumn("statusMessage"));

            DataRow row = table.NewRow();
            row["timestamp"] = DateTime.Parse("7/25/2020 2:50:20 AM");
            row["tenantId"] = "tenantId";
            row["resourceGroupName"] = "resourceGroupName";
            row["executionStatus"] = "executionStatus";
            row["statusCode"] = "statusCode";
            row["statusMessage"] = "statusMessage";
            table.Rows.Add(row);

            return table;
        }

        /// <summary>
        /// Creates a valid data table result for arm vm diagostic provider
        /// </summary>
        private static DataTable CreateValidArmVmAPIQoSKustoResponseTable()
        {
            // Create a new DataTable and add a row.
            DataTable table = new DataTable("AzCrp");
            table.Columns.Add(DiagnosticsProviderFixture.CreateColumn("timestamp"));
            table.Columns.Add(DiagnosticsProviderFixture.CreateColumn("correlationId"));
            table.Columns.Add(DiagnosticsProviderFixture.CreateColumn("operationId"));
            table.Columns.Add(DiagnosticsProviderFixture.CreateColumn("resourceGroupName"));
            table.Columns.Add(DiagnosticsProviderFixture.CreateColumn("resourceName"));
            table.Columns.Add(DiagnosticsProviderFixture.CreateColumn("subscriptionId"));
            table.Columns.Add(DiagnosticsProviderFixture.CreateColumn("exceptionType"));
            table.Columns.Add(DiagnosticsProviderFixture.CreateColumn("errorDetails"));
            table.Columns.Add(DiagnosticsProviderFixture.CreateColumn("vMId"));
            table.Columns.Add(DiagnosticsProviderFixture.CreateColumn("vMSize"));
            table.Columns.Add(DiagnosticsProviderFixture.CreateColumn("oSType"));
            table.Columns.Add(DiagnosticsProviderFixture.CreateColumn("oSDiskStorageAccountType"));
            table.Columns.Add(DiagnosticsProviderFixture.CreateColumn("availabilitySet"));
            table.Columns.Add(DiagnosticsProviderFixture.CreateColumn("fabricCluster"));
            table.Columns.Add(DiagnosticsProviderFixture.CreateColumn("allocationAction"));

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

            return table;
        }

        /// <summary>
        /// Creates a valid log node snapshot response table result for arm vm diagostic provider
        /// </summary>
        private static DataTable CreateValidArmVmLogNodeSnapshotKustoResponseTable()
        {
            // Create a new DataTable and add a row.
            DataTable table = new DataTable("AzureCM");
            table.Columns.Add(DiagnosticsProviderFixture.CreateColumn("timestamp"));
            table.Columns.Add(DiagnosticsProviderFixture.CreateColumn("nodeState"));
            table.Columns.Add(DiagnosticsProviderFixture.CreateColumn("nodeAvailabilityState"));
            table.Columns.Add(DiagnosticsProviderFixture.CreateColumn("faultInfo"));
            table.Columns.Add(DiagnosticsProviderFixture.CreateColumn("hostingEnvironment"));
            table.Columns.Add(DiagnosticsProviderFixture.CreateColumn("faultDomain"));
            table.Columns.Add(DiagnosticsProviderFixture.CreateColumn("lastStateChangeTime"));
            table.Columns.Add(DiagnosticsProviderFixture.CreateColumn("nsProgressHealthStatus"));
            table.Columns.Add(DiagnosticsProviderFixture.CreateColumn("tipNodeSessionId"));
            table.Columns.Add(DiagnosticsProviderFixture.CreateColumn("healthSignals"));

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

            return table;
        }

        /// <summary>
        /// The default mock behavior for the diagnostic provider which includes the expected success operation
        /// for the workflow
        /// </summary>
        private void SetupMockDefaults()
        {
            // set the query issue async method to return a data table object
            this.mockFixture.QueryIssuer.SetupSequence(
                method => method.IssueAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync(ArmVmDeploymentFailureKustoDiagnosticsTests.CreateValidArmVmDeploymentOperationKustoResponseTable())
            .ReturnsAsync(ArmVmDeploymentFailureKustoDiagnosticsTests.CreateValidArmVmAPIQoSKustoResponseTable())
            .ReturnsAsync(ArmVmDeploymentFailureKustoDiagnosticsTests.CreateValidArmVmLogNodeSnapshotKustoResponseTable());
        }

        /// <summary>
        /// Creates a valid diagnostic request with required parameters for the ArmVmDeploymentKustoDiagnostics provider
        /// </summary>
        private DiagnosticsRequest CreateArmVmDeploymentKustoDiagnosticRequest()
        {
            return this.mockFixture.CreateValidDiagnosticRequest(
                issueType: DiagnosticsIssueType.ArmVmCreationFailure,
                queryContext: new Dictionary<string, IConvertible>
                {
                    { DiagnosticsParameter.TipSessionId, Guid.NewGuid().ToString() },
                    { DiagnosticsParameter.ResourceGroupName, Guid.NewGuid().ToString() }
                });
        }

        /// <summary>
        /// Used to expose protected methods of the underlying provider
        /// </summary>
        private class TestArmVmDeploymentFailureKustoDiagnostics : ArmVmDeploymentFailureKustoDiagnostics
        {
            public TestArmVmDeploymentFailureKustoDiagnostics(IServiceCollection services, IAsyncPolicy retryPolicy = null)
                : base(services, retryPolicy)
            {
            }

            public new Task<ExecutionResult> DiagnoseAsync(DiagnosticsRequest request, EventContext telemetryContext, ILogger logger, CancellationToken cancellationToken)
            {
                return base.DiagnoseAsync(request, telemetryContext, logger, cancellationToken);
            }

            public new bool IsHandled(DiagnosticsRequest request)
            {
                return base.IsHandled(request);
            }
        }
    }
}