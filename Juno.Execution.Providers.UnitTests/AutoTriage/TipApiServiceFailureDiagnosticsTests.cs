namespace Juno.Execution.Providers.AutoTriage
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Moq;
    using NUnit.Framework;
    using Polly;
    using TipGateway.Entities;

    [TestFixture]
    [Category("Unit")]
    public class TipApiServiceFailureDiagnosticsTests
    {
        private TestTipApiServiceFailureDiagnostics provider;
        private DiagnosticsProviderFixture mockFixture;
        private DiagnosticsRequest diagnosticRequest;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new DiagnosticsProviderFixture();
            this.provider = new TestTipApiServiceFailureDiagnostics(this.mockFixture.Services, this.mockFixture.RetryPolicy);
            this.diagnosticRequest = this.CreateTipApiServiceFailureDiagnosticRequest();
            this.SetUpMockDefaults();
        }

        [Test]
        [TestCase(DiagnosticsParameter.TipSessionChangeId, "invalidParam")]
        [TestCase(DiagnosticsParameter.TipSessionChangeId, DiagnosticsParameter.ExperimentId)]
        [TestCase(DiagnosticsParameter.TipSessionChangeId, DiagnosticsParameter.ResourceGroupName)]
        [TestCase(DiagnosticsParameter.TipSessionChangeId, DiagnosticsParameter.TipNodeId)]
        [TestCase(DiagnosticsParameter.TipSessionChangeId, DiagnosticsParameter.SubscriptionId)]
        [TestCase(DiagnosticsParameter.TipSessionChangeId, DiagnosticsParameter.ProviderName)]
        [TestCase(DiagnosticsParameter.TipSessionChangeId, DiagnosticsParameter.TimeRangeBegin)]
        [TestCase(DiagnosticsParameter.TipSessionChangeId, DiagnosticsParameter.TimeRangeEnd)]
        [TestCase(DiagnosticsParameter.TipSessionChangeId, "")]
        [TestCase(DiagnosticsParameter.TipSessionId, "invalidParam")]
        [TestCase(DiagnosticsParameter.TipSessionId, DiagnosticsParameter.ExperimentId)]
        [TestCase(DiagnosticsParameter.TipSessionId, DiagnosticsParameter.ResourceGroupName)]
        [TestCase(DiagnosticsParameter.TipSessionId, DiagnosticsParameter.TipNodeId)]
        [TestCase(DiagnosticsParameter.TipSessionId, DiagnosticsParameter.SubscriptionId)]
        [TestCase(DiagnosticsParameter.TipSessionId, DiagnosticsParameter.ProviderName)]
        [TestCase(DiagnosticsParameter.TipSessionId, DiagnosticsParameter.TimeRangeBegin)]
        [TestCase(DiagnosticsParameter.TipSessionId, DiagnosticsParameter.TimeRangeEnd)]
        [TestCase(DiagnosticsParameter.TipSessionId, "")]
        public void TipApiServiceFailureDiagnosticsProviderSkipsDiagnosticsForUnsupportedDiagnosticRequests(string expectedParam1, string expectedParam2)
        {
            DiagnosticsRequest diagnosticsRequest = new DiagnosticsRequest(
                Guid.NewGuid().ToString(),
                "ID",
                DiagnosticsIssueType.TipPilotfishPackageDeploymentFailure,
                DateTime.UtcNow.AddHours(-1),
                DateTime.UtcNow,
                new Dictionary<string, IConvertible>()
                {
                    { expectedParam1, Guid.NewGuid().ToString() },
                    { expectedParam2, Guid.NewGuid().ToString() }
                });

            // Verify this request is not handled by the current diagnostic provider
            Assert.IsFalse(this.provider.IsHandled(diagnosticsRequest));

            // Invoke handler's DiagnoseAsync with an invalid issue type.
            var result = this.provider.DiagnoseAsync(
                    diagnosticsRequest,
                    this.mockFixture.EventContext,
                    this.mockFixture.Logger.Object,
                    this.mockFixture.CancellationToken).GetAwaiter().GetResult();

            // Verify GetTipSessionChangeAsyn method was never called from within DiagnoseAsync method.
            this.mockFixture.TipClient.Verify(method => method.GetTipSessionChangeAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never());

            // Ensure that the execution result is succeeded for non-handled requests
            Assert.AreEqual(result.Status, ExecutionStatus.Succeeded);
        }

        [Test]
        [TestCase(DiagnosticsIssueType.Undefined)]
        [TestCase(DiagnosticsIssueType.MicrocodeUpdateFailure)]
        [TestCase(DiagnosticsIssueType.ArmVmCreationFailure)]
        public void TipApiServiceFailureDiagnosticsProviderSkipsDiagnosticsForUnsuportedIssueTypes(DiagnosticsIssueType issueType)
        {
            // valid diagnostic request
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
                this.mockFixture.CancellationToken)
                .GetAwaiter()
                .GetResult();

            // Verify GetTipSessionChangeAsyn method was never called from within DiagnoseAsync method.
            this.mockFixture.TipClient.Verify(method => method.GetTipSessionChangeAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never());

            // Ensure that the execution result is succeeded for non-handled requests
            Assert.AreEqual(result.Status, ExecutionStatus.Succeeded);
        }

        [Test]
        public void TipApiServiceFailureDiagnosticsProviderRunsDiagnosticsForRecognizedDiagnosticRequest()
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

            // Verify GetTipSessionChangeAsyn method was called from within DiagnoseAsync method.
            this.mockFixture.TipClient.Verify(method => method.GetTipSessionChangeAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once());

            // Diagnostic Execution is expected to succeed with the valid diagnostic request
            Assert.AreEqual(result.Status, ExecutionStatus.Succeeded);
        }

        [Test]
        public void TipApiServiceFailureDiagnosticsProviderRetriesAsExpectedWhenErrorsOccur()
        {
            IAsyncPolicy customRetryPolicy = Policy.Handle<Exception>()
               .WaitAndRetryAsync(3, (retries) => TimeSpan.FromMilliseconds(1));

            string expectedEventName = $"{nameof(TipApiServiceFailureDiagnostics)}.Diagnose{EventNameSuffix.Error}";

            int actualRetryCount = -1;
            // throw an error the first time, then return a bad request on the next retry
            this.mockFixture.TipClient
                .Setup(client => client.GetTipSessionChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("The expected exception"))
                .Callback(() =>
                {
                    actualRetryCount++;
                });

            var handler = new TestTipApiServiceFailureDiagnostics(this.mockFixture.Services, customRetryPolicy);

            // Exceptions are captured from the base class
            var result = handler.DiagnoseAsync(
                    this.diagnosticRequest,
                    this.mockFixture.Logger.Object,
                    CancellationToken.None).GetAwaiter().GetResult();

            // Verify IssueAsync method called each kusto query for a total of 4 times (1 initial, 3 retry attempts).
            this.mockFixture.TipClient.Verify(method => method.GetTipSessionChangeAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(4));

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
        public void TipApiServiceFailureDiagnosticsProviderReturnsAValidDiagnosticsEntryListWhenAValidResultsReceivedFromTipService()
        {
            // Arrange the mock tip client to return a valid object.
            this.mockFixture.TipClient
                .Setup(method => method.GetTipSessionChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new TipNodeSessionChangeDetails()));

            // Arrange the mock logger to do nothing when Log method is called.
            this.mockFixture.Logger
                .Setup(method => method.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.Is<It.IsAnyType>((v, t) => true), null, null))
                .Callback(() => { });

            // Invoke provider's diagnose asynchoronous method with valid TIP diagnostics info context.
            this.provider.DiagnoseAsync(
                    this.diagnosticRequest,
                    this.mockFixture.EventContext,
                    this.mockFixture.Logger.Object,
                    this.mockFixture.CancellationToken).GetAwaiter().GetResult();

            // Verify Log method of ILogger was called three times (Log start event, Log stop event and core message event)
            this.mockFixture.Logger
                .Verify(method => method.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.Is<It.IsAnyType>((v, t) => true), null, null),
                Times.Exactly(3));
        }

        [Test]
        public void TipApiServiceFailureDiagnosticsProviderReturnsAValidDiagnosticsEntryListWhenNullValueFromTipService()
        {
            // Arrange the mock tip client to return null object.
            this.mockFixture.TipClient
                .Setup(method => method.GetTipSessionChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<TipNodeSessionChangeDetails>(null));

            // Arrange the mock logger to do nothing when Log method is called.
            this.mockFixture.Logger
                .Setup(method => method.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.Is<It.IsAnyType>((v, t) => true), null, null))
                .Callback(() => { });

            // Invoke provider's diagnose asynchoronous method with valid TIP diagnostics info context.
            this.provider.DiagnoseAsync(
                this.diagnosticRequest,
                this.mockFixture.EventContext,
                this.mockFixture.Logger.Object,
                this.mockFixture.CancellationToken).GetAwaiter().GetResult();

            // Verify Log method of ILogger was called two times (Log start event, Log stop event and no core message event)
            this.mockFixture.Logger
                .Verify(method => method.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.Is<It.IsAnyType>((v, t) => true), null, null),
                Times.Exactly(2));
        }

        [Test]
        public void TipApiServiceFailureDiagnosticsProviderExecutesExpectedWorkflowForSuccessfulDiagnostics()
        {
            var expectedStartEventName = $"{nameof(TipApiServiceFailureDiagnostics)}.DiagnoseStart";
            var expectedResultEventName = $"{nameof(TipApiServiceFailureDiagnostics)}.DiagnosticsResults";
            var expectedEndEventName = $"{nameof(TipApiServiceFailureDiagnostics)}.DiagnoseStop";

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
        /// Creates a valid Diagnostic Request for the TipApiServiceFailureDiagnostics provider
        /// </summary>
        public DiagnosticsRequest CreateTipApiServiceFailureDiagnosticRequest()
        {
            return this.mockFixture.CreateValidDiagnosticRequest(
                issueType: DiagnosticsIssueType.TipPilotfishPackageDeploymentFailure,
                queryContext: new Dictionary<string, IConvertible>
                {
                    { DiagnosticsParameter.TipSessionId, Guid.NewGuid().ToString() },
                    { DiagnosticsParameter.TipSessionChangeId, Guid.NewGuid().ToString() }
                });
        }

        /// <summary>
        /// The default mock behavior for the diagnostic provider which includes the expected success operation
        /// for the workflow
        /// </summary>
        private void SetUpMockDefaults()
        {
            this.mockFixture.TipClient
                .Setup(method => method.GetTipSessionChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new TipNodeSessionChangeDetails()));
        }

        /// <summary>
        /// Used to expose protected methods of the underlying provider
        /// </summary>
        private class TestTipApiServiceFailureDiagnostics : TipApiServiceFailureDiagnostics
        {
            public TestTipApiServiceFailureDiagnostics(IServiceCollection services, IAsyncPolicy retryPolicy = null)
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