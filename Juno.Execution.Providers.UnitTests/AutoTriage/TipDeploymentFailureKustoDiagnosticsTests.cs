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
    public class TipDeploymentFailureKustoDiagnosticsTests
    {
        private DiagnosticsProviderFixture mockFixture;
        private TestTipDeploymentFailureKustoDiagnostics provider;
        private DiagnosticsRequest diagnosticRequest;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new DiagnosticsProviderFixture();
            this.provider = new TestTipDeploymentFailureKustoDiagnostics(this.mockFixture.Services);
            this.diagnosticRequest = this.CreateTipDeploymentFailureKustoDiagnosticRequest();
            this.SetUpMockDefaults();
        }

        [Test]
        [TestCase("invalidParam")]
        [TestCase(DiagnosticsParameter.ExperimentId)]
        [TestCase(DiagnosticsParameter.ResourceGroupName)]
        [TestCase(DiagnosticsParameter.TipNodeId)]
        [TestCase(DiagnosticsParameter.SubscriptionId)]
        [TestCase(DiagnosticsParameter.ProviderName)]
        [TestCase(DiagnosticsParameter.TimeRangeBegin)]
        [TestCase(DiagnosticsParameter.TimeRangeEnd)]
        [TestCase(DiagnosticsParameter.TipSessionChangeId)]
        [TestCase("")]
        public void TipDeploymentFailureKustoDiagnosticsProviderSkipsDiagnosticsForUnsupportedDiagnosticRequests(string expectedParam)
        {
            DiagnosticsRequest diagnosticsRequest = new DiagnosticsRequest(
                Guid.NewGuid().ToString(),
                "ID",
                DiagnosticsIssueType.TipPilotfishPackageDeploymentFailure,
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

            // Verify GetActivityLogs method was never called.
            this.mockFixture.ArmClient.Verify(method =>
                method.GetSubscriptionActivityLogsAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<IEnumerable<string>>()),
                    Times.Never());

            // Ensure that the execution result is succeeded for non-handled requests
            Assert.AreEqual(result.Status, ExecutionStatus.Succeeded);
        }

        [Test]
        [TestCase(DiagnosticsIssueType.Undefined)]
        [TestCase(DiagnosticsIssueType.MicrocodeUpdateFailure)]
        [TestCase(DiagnosticsIssueType.ArmVmCreationFailure)]
        public void TipDeploymentFailureKustoDiagnosticsProviderSkipsDiagnosticsUnsupportedIssueTypes(DiagnosticsIssueType issueType)
        {
            // invalid diagnostic request
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
                this.mockFixture.EventContext,
                this.mockFixture.Logger.Object,
                this.mockFixture.CancellationToken).GetAwaiter().GetResult();

            // Verify Logger's Log method was never called.
            this.mockFixture.Logger.Verify(level => level.Log<EventContext>(
                It.IsAny<LogLevel>(),
                It.Is<EventId>(e => e.Name ==
                    $"{nameof(TipDeploymentFailureKustoDiagnostics)}.DiagnosticsResults"),
                It.IsAny<EventContext>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<EventContext, Exception, string>>()), Times.Never());

            // Ensure that the execution result is succeeded for non-handled requests
            Assert.AreEqual(result.Status, ExecutionStatus.Succeeded);
        }

        [Test]
        public void TipDeploymentFailureKustoDiagnosticsProviderRunsDiagnosticsForRecognizedDiagnosticRequest()
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
        public async Task TipDeploymentFailureKustoDiagnosticsProviderRetriesAndLogsExceptionsAsExpectedWhenErrorsOccur()
        {
            IAsyncPolicy customRetryPolicy = Policy.Handle<Exception>()
               .WaitAndRetryAsync(3, (retries) => TimeSpan.FromMilliseconds(1));
            string expectedEventName = $"{nameof(TipDeploymentFailureKustoDiagnostics)}.Diagnose{EventNameSuffix.Error}";
            Exception expectedException = new Exception("The expected exception");
            int actualRetryCount = -1;
            this.mockFixture.QueryIssuer
                .Setup(method => method.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception())
                .Callback(() =>
                {
                    actualRetryCount++;
                });

            var handler = new TestTipDeploymentFailureKustoDiagnostics(this.mockFixture.Services, customRetryPolicy);

            // Exceptions are caught at the base class
            var result = await handler.DiagnoseAsync(
                this.diagnosticRequest,
                this.mockFixture.Logger.Object,
                this.mockFixture.CancellationToken).ConfigureAwait(false);

            // Verify IssueAsync method called each kusto query for a total of 4 times (1 initial, 3 with 1 retry each).
            this.mockFixture.QueryIssuer.Verify(method => method.IssueAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(4));

            // Verify the logger logs an error for the diagnostic error
            this.mockFixture.Logger.Verify(method => method.Log(
                It.Is<LogLevel>(level => level == LogLevel.Error),
                It.Is<EventId>(e => e == new EventId(expectedEventName.GetHashCode(StringComparison.OrdinalIgnoreCase), expectedEventName)),
                It.IsAny<EventContext>(),
                null,
                null), Times.Exactly(1));

            Assert.That(actualRetryCount, Is.EqualTo(3));
            Assert.AreEqual(result.Status, ExecutionStatus.Failed);
        }

        [Test]
        public void TipDeploymentFailureKustoDiagnosticsProviderSavesStateAsExpected()
        {
            var expectedStartEventName = $"{nameof(TipDeploymentFailureKustoDiagnostics)}.DiagnoseStart";
            var expectedResultEventName = $"{nameof(TipDeploymentFailureKustoDiagnostics)}.DiagnosticsResults";
            var expectedEndEventName = $"{nameof(TipDeploymentFailureKustoDiagnostics)}.DiagnoseStop";

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
        /// Creates a valid diagnostic request with required parameters for the TipDeploymentFailureKustoDiagnostics provider
        /// </summary>
        public DiagnosticsRequest CreateTipDeploymentFailureKustoDiagnosticRequest()
        {
            return this.mockFixture.CreateValidDiagnosticRequest(
                issueType: DiagnosticsIssueType.TipPilotfishPackageDeploymentFailure,
                queryContext: new Dictionary<string, IConvertible>()
                {
                    { DiagnosticsParameter.TipSessionId, Guid.NewGuid().ToString() }
                });
        }

        /// <summary>
        /// Creates a valid tip session status data table result for tip deployment failure diagostic provider
        /// </summary>
        private static DataTable CreateValidTipSessionStatusEventsKustoResponseTable()
        {
            // Create a new DataTable with schema.
            DataTable table = new DataTable("AzureCM");
            table.Columns.Add(DiagnosticsProviderFixture.CreateColumn("TIMESTAMP"));
            table.Columns.Add(DiagnosticsProviderFixture.CreateColumn("tipNodeSessionId"));
            table.Columns.Add(DiagnosticsProviderFixture.CreateColumn("AvailabilityZone"));
            table.Columns.Add(DiagnosticsProviderFixture.CreateColumn("Tenant"));
            table.Columns.Add(DiagnosticsProviderFixture.CreateColumn("message"));

            DataRow row = table.NewRow();
            row["TIMESTAMP"] = DateTime.Parse("7/25/2020 2:50:20 AM");
            row["tipNodeSessionId"] = "TipSessionId";
            row["AvailabilityZone"] = "AZ";
            row["Tenant"] = "Tenant";
            row["message"] = "Message";
            table.Rows.Add(row);
            return table;
        }

        /// <summary>
        /// The default mock behavior for the diagnostic provider which includes the expected success operation
        /// for the workflow
        /// </summary>
        private void SetUpMockDefaults()
        {
            this.mockFixture.QueryIssuer
                .Setup(method => method.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(TipDeploymentFailureKustoDiagnosticsTests.CreateValidTipSessionStatusEventsKustoResponseTable());
        }

        /// <summary>
        /// Used to expose protected methods of the underlying provider
        /// </summary>
        private class TestTipDeploymentFailureKustoDiagnostics : TipDeploymentFailureKustoDiagnostics
        {
            public TestTipDeploymentFailureKustoDiagnostics(IServiceCollection services, IAsyncPolicy retryPolicy = null)
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