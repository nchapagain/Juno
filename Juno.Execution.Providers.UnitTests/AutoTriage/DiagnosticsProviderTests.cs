namespace Juno.Execution.Providers.AutoTriage
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
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
    public class DiagnosticsProviderTests
    {
        private TestDiagnosticsProvider provider;
        private DiagnosticsProviderFixture mockFixture;
        private List<DiagnosticsProvider> providerList;
        private IEnumerable<DiagnosticsRequest> diagnosticRequests;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new DiagnosticsProviderFixture();
            this.provider = new TestDiagnosticsProvider(this.mockFixture.Services);
            this.providerList = new List<DiagnosticsProvider>()
            {
                new TipApiServiceFailureDiagnostics(this.mockFixture.Services),
                new TipDeploymentFailureKustoDiagnostics(this.mockFixture.Services),
                new ArmVmDeploymentFailureActivityLogDiagnostics(this.mockFixture.Services),
                new ArmVmDeploymentFailureKustoDiagnostics(this.mockFixture.Services),
                new MicrocodeUpdateFailureKustoDiagnostics(this.mockFixture.Services)
            };

            // create a list of the valid diagnostic requests
            this.diagnosticRequests = this.mockFixture.DiagnosticRequestsList;

            this.SetupMockDefaults();
        }

        [Test]
        public void DiagnosticsProvidersExecuteSuccessfulDiagnosticsWorkflow()
        {
            foreach (DiagnosticsProvider provider in this.providerList)
            {
                List<ExecutionResult> executionResults = new List<ExecutionResult>();
                List<bool> validRequests = new List<bool>();
                var expectedStartEventName = $"{provider.GetType().Name}.DiagnosticsStart";
                var expectedEndEventName = $"{provider.GetType().Name}.DiagnosticsStop";
                // create an individual logger for each diagnostic provider
                Mock<ILogger> providerLogger = this.mockFixture.Logger;
                foreach (DiagnosticsRequest request in this.diagnosticRequests)
                {
                    executionResults.Add(provider.DiagnoseAsync(
                    request,
                    providerLogger.Object,
                    CancellationToken.None).GetAwaiter().GetResult());

                    validRequests.Add(provider.IsHandled(request));
                }

                // The logger is expected to capture the start event for the diagnostics
                providerLogger.Verify(method => method.Log(
                    It.Is<LogLevel>(level => level == LogLevel.Information),
                    It.Is<EventId>(action => action ==
                        new EventId(
                            expectedStartEventName.GetHashCode(StringComparison.OrdinalIgnoreCase),
                            expectedStartEventName)),
                    It.IsAny<EventContext>(),
                    null,
                    null), Times.Exactly(5));

                // The logger is expected to capture the start event for the diagnostics
                providerLogger.Verify(method => method.Log(
                    It.Is<LogLevel>(level => level == LogLevel.Information),
                    It.Is<EventId>(action => action ==
                        new EventId(
                            expectedEndEventName.GetHashCode(StringComparison.OrdinalIgnoreCase),
                            expectedEndEventName)),
                    It.IsAny<EventContext>(),
                    null,
                    null), Times.Exactly(5));

                // Ideally there would only be one but the tip diagnostic providers have a common query attribute
                int expectedCount = provider.GetType().Name == nameof(TipDeploymentFailureKustoDiagnostics) ? 2 : 1;

                Assert.IsTrue(validRequests.Where(isHandled => isHandled == true).Count() == expectedCount);
                // Handled or not the provider should return a successful execution for each request
                Assert.IsTrue(executionResults.Where(status => status.Status == ExecutionStatus.Succeeded).Count() == 5);
            }
        }

        [Test]
        public async Task DiagnosticsProviderHandlesCancellationBeforeExecutingDiagnostics()
        {
            using (CancellationTokenSource tokenSource = new CancellationTokenSource())
            {
                tokenSource.Cancel();
                foreach (DiagnosticsRequest request in this.diagnosticRequests)
                {
                    ExecutionResult actualResult = await this.provider.DiagnoseAsync(
                        request,
                        this.mockFixture.Logger.Object,
                        tokenSource.Token);

                    Assert.IsNotNull(actualResult);
                    Assert.IsNull(actualResult.Error);
                    Assert.IsTrue(actualResult.Status == ExecutionStatus.Cancelled);
                }
            }
        }

        [Test]
        public async Task DiagnosticsProviderBaseClassCapturesExceptionDetailsWhenExceptionsOccur()
        {
            Exception expectedError = new Exception("The expected exception");
            this.provider.OnDiagnoseAsync = (request, telemetryContext, logger, cancellationToken) => throw expectedError;

            foreach (DiagnosticsRequest request in this.diagnosticRequests)
            {
                ExecutionResult actualResult = await this.provider.DiagnoseAsync(
                request,
                this.mockFixture.Logger.Object,
                CancellationToken.None);

                Assert.IsNotNull(actualResult);
                Assert.IsNotNull(actualResult.Error);
                Assert.IsTrue(object.ReferenceEquals(expectedError, actualResult.Error));
                Assert.IsTrue(actualResult.Status == ExecutionStatus.Failed);
            }
        }

        private void SetupMockDefaults()
        {
            using (HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK))
            {
                // arm always returns a successful result
                this.mockFixture.ArmClient.Setup(method => method.GetSubscriptionActivityLogsAsync(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<IEnumerable<string>>()))
                    .ReturnsAsync(response);
            }

            // query issuer always returns a data table object
            this.mockFixture.QueryIssuer.Setup(method => method.IssueAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .Returns(Task.FromResult<DataTable>(null));

            // tip session always returns valid tip session change details
            this.mockFixture.TipClient.Setup(method => method.GetTipSessionChangeAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new TipNodeSessionChangeDetails()));
        }

        [ExecutionConstraints(SupportedStepType.EnvironmentSetup, SupportedStepTarget.ExecuteRemotely)]
        private class TestDiagnosticsProvider : DiagnosticsProvider
        {
            public TestDiagnosticsProvider(IServiceCollection services, IAsyncPolicy retryPolicy = null)
                : base(services, retryPolicy)
            {
            }

            public Func<DiagnosticsRequest, EventContext, ILogger, CancellationToken, ExecutionResult> OnDiagnoseAsync { get; set; }

            public Func<DiagnosticsRequest, bool> OnIsHandled { get; set; }

            public override bool IsHandled(DiagnosticsRequest request)
            {
                return this.OnIsHandled != null
                    ? this.OnIsHandled.Invoke(request)
                    : true;
            }

            public new Task<ExecutionResult> DiagnoseAsync(DiagnosticsRequest request, ILogger logger, CancellationToken cancellationToken)
            {
                return base.DiagnoseAsync(request, logger, cancellationToken);
            }

            protected override Task<ExecutionResult> DiagnoseAsync(DiagnosticsRequest request, EventContext telemetryContext, ILogger logger, CancellationToken cancellationToken)
            {
                return this.OnDiagnoseAsync != null
                    ? Task.FromResult(this.OnDiagnoseAsync.Invoke(request, telemetryContext, logger, cancellationToken))
                    : Task.FromResult(new ExecutionResult(ExecutionStatus.Succeeded));
            }
        }
    }
}