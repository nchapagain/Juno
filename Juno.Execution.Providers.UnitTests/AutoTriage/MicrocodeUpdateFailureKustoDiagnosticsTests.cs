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
    public class MicrocodeUpdateFailureKustoDiagnosticsTests
    {
        private DiagnosticsProviderFixture mockFixture;
        private TestMicrocodeUpdateFailureKustoDiagnostics provider;
        private DiagnosticsRequest diagnosticRequest;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new DiagnosticsProviderFixture();
            this.provider = new TestMicrocodeUpdateFailureKustoDiagnostics(this.mockFixture.Services, this.mockFixture.RetryPolicy);
            this.diagnosticRequest = this.CreateMicrocodeUpdateFailureKustoDiagnosticRequests();
            this.SetupMockDefaults();
        }

        [Test]
        [TestCase("invalidParam")]
        [TestCase(DiagnosticsParameter.ExperimentId)]
        [TestCase(DiagnosticsParameter.ResourceGroupName)]
        [TestCase(DiagnosticsParameter.TipSessionId)]
        [TestCase(DiagnosticsParameter.SubscriptionId)]
        [TestCase(DiagnosticsParameter.ProviderName)]
        [TestCase(DiagnosticsParameter.TimeRangeBegin)]
        [TestCase(DiagnosticsParameter.TimeRangeEnd)]
        [TestCase(DiagnosticsParameter.TipSessionChangeId)]
        [TestCase("")]
        public void MicrocodeUpdateFailureKustoDiagnosticsProviderSkipsDiagnosticsForUnsupportedDiagnosticRequests(string expectedParam)
        {
            DiagnosticsRequest diagnosticsRequest = new DiagnosticsRequest(
                Guid.NewGuid().ToString(),
                "ID",
                DiagnosticsIssueType.MicrocodeUpdateFailure,
                DateTime.UtcNow.AddHours(-1),
                DateTime.UtcNow,
                new Dictionary<string, IConvertible>()
                {
                    { expectedParam, Guid.NewGuid().ToString() }
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
        [TestCase(DiagnosticsIssueType.ArmVmCreationFailure)]
        public void MicrocodeUpdateFailureKustoDiagnosticsProviderSkipsDiagnosticsForAnUnsupportedIssueTypes(DiagnosticsIssueType issueType)
        {
            DiagnosticsRequest diagnosticsRequest = new DiagnosticsRequest(
                Guid.NewGuid().ToString(),
                "ID",
                issueType,
                DateTime.UtcNow,
                DateTime.UtcNow,
                this.diagnosticRequest.Context);

            // Verify this request is not handled by the current diagnostic provider
            Assert.AreEqual(this.provider.IsHandled(diagnosticsRequest), false);

            // Invoke handler's DiagnoseAsync with an invalid issue type.
            var result = this.provider.DiagnoseAsync(
                diagnosticsRequest,
                this.mockFixture.EventContext,
                this.mockFixture.Logger.Object,
                this.mockFixture.CancellationToken).GetAwaiter().GetResult();

            // Verify IssueAsync method was never called from DiagnoseAsync function
            this.mockFixture.QueryIssuer.Verify(method => method.IssueAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);

            // Ensure that the execution result is succeeded for non-handled requests
            Assert.AreEqual(result.Status, ExecutionStatus.Succeeded);
        }

        [Test]
        public void MicrocodeUpdateFailureKustoDiagnosticsProviderRunsDiagnosticsForRecognizedDiagnosticRequest()
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
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(1));

            // Diagnostic Execution is expected to succeed with the valid diagnostic request
            Assert.AreEqual(result.Status, ExecutionStatus.Succeeded);
        }

        [Test]
        public async Task MicrocodeUpdateFailureKustoDiagnosticsProviderRetriesAsExpectedWhenErrorsOccur()
        {
            // custom retry policy
            IAsyncPolicy customRetryPolicy = Policy.Handle<Exception>()
                .WaitAndRetryAsync(3, (retries) => TimeSpan.FromMilliseconds(5));

            // offset for initial call
            int actualRetryCount = -1;

            this.mockFixture.QueryIssuer
                .Setup(method => method.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception())
                .Callback(() =>
                {
                    actualRetryCount++;
                });

            var handler = new TestMicrocodeUpdateFailureKustoDiagnostics(this.mockFixture.Services, customRetryPolicy);

            // Invoke handler's DiagnoseAsync catching exception from base class call
            var result = await handler.DiagnoseAsync(
                    this.diagnosticRequest,
                    this.mockFixture.Logger.Object,
                    this.mockFixture.CancellationToken).ConfigureAwait(false);

            // Verify Logger's Log method was never called.
            this.mockFixture.Logger.Verify(level => level.Log<EventContext>(
                It.IsAny<LogLevel>(),
                It.Is<EventId>(e => e.Name == $"{nameof(MicrocodeUpdateFailureKustoDiagnostics)}.DiagnosticsResults"),
                It.IsAny<EventContext>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<EventContext, Exception, string>>()), Times.Never());

            Assert.That(actualRetryCount, Is.EqualTo(3));
            Assert.AreEqual(result.Status, ExecutionStatus.Failed);
        }

        [Test]
        public void MicrocodeUpdateFailureKustoDiagnosticsProviderExecutesExpectedWorkflowForSuccessfulDiagnostics()
        {
            var expectedStartEventName = $"{nameof(MicrocodeUpdateFailureKustoDiagnostics)}.DiagnoseStart";
            var expectedResultEventName = $"{nameof(MicrocodeUpdateFailureKustoDiagnostics)}.DiagnosticsResults";
            var expectedEndEventName = $"{nameof(MicrocodeUpdateFailureKustoDiagnostics)}.DiagnoseStop";

            var result = this.provider.DiagnoseAsync(
                    this.diagnosticRequest,
                    this.mockFixture.EventContext,
                    this.mockFixture.Logger.Object,
                    this.mockFixture.CancellationToken).GetAwaiter().GetResult();

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
        /// Creates a valid Diagnostic Request with required parameters for the MicrocodeUpdateFailureKustoDiagnostics provider
        /// </summary>
        public DiagnosticsRequest CreateMicrocodeUpdateFailureKustoDiagnosticRequests()
        {
            return this.mockFixture.CreateValidDiagnosticRequest(
                issueType: DiagnosticsIssueType.MicrocodeUpdateFailure,
                queryContext: new Dictionary<string, IConvertible>()
                {
                    { DiagnosticsParameter.TipNodeId, Guid.NewGuid().ToString() }
                });
        }

        /// <summary>
        /// Creates a valid deployment operation kusto response table for microcode update diagnostic provider
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
        /// Creates a valid Arm vm API QoS Kusto data table result for microcode update diagnostic provider
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
        /// Creates a valid log node snapshot response table result for microcode update diagnostic provider
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
            row["lastStateChangeTime"] = "lastStateChangeTime";
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
            this.mockFixture.QueryIssuer
            .Setup(method => method.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(MicrocodeUpdateFailureKustoDiagnosticsTests.CreateValidArmVmDeploymentOperationKustoResponseTable())
            .Callback(() =>
            {
                this.mockFixture.QueryIssuer
                    .Setup(method => method.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(MicrocodeUpdateFailureKustoDiagnosticsTests.CreateValidArmVmAPIQoSKustoResponseTable())
                    .Callback(() =>
                    {
                        this.mockFixture.QueryIssuer
                            .Setup(method => method.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                            .ReturnsAsync(MicrocodeUpdateFailureKustoDiagnosticsTests.CreateValidArmVmLogNodeSnapshotKustoResponseTable());
                    });
            });
        }

        /// <summary>
        /// Used to expose protected methods of the underlying provider
        /// </summary>
        private class TestMicrocodeUpdateFailureKustoDiagnostics : MicrocodeUpdateFailureKustoDiagnostics
        {
            public TestMicrocodeUpdateFailureKustoDiagnostics(IServiceCollection services, IAsyncPolicy retryPolicy = null)
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