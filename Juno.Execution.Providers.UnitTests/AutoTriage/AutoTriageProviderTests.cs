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
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using NUnit.Framework;
    using TipGateway.Entities;

    [TestFixture]
    [Category("Unit")]
    public class AutoTriageProviderTests
    {
        private ProviderFixture mockFixture;
        private TestAutoTriageProvider provider;
        private AutoTriageProvider.State mockState;
        private DiagnosticsProviderFixture diagnosticFixture;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new ProviderFixture(typeof(AutoTriageProvider));
            // Add in the services needed by the diagnostic handlers
            this.diagnosticFixture = new DiagnosticsProviderFixture();

            this.mockState = new AutoTriageProvider.State()
            {
                StepTimeout = DateTime.UtcNow.AddMinutes(10)
            };

            this.provider = new TestAutoTriageProvider(this.diagnosticFixture.Services);
            this.SetupMockDefaults();
        }

        [Test]
        public async Task AutoTriageProviderExecutionSucceedsWithValidRequests()
        {
            List<DiagnosticsRequest> diagnosticList = new List<DiagnosticsRequest>()
            {
                new DiagnosticsRequest(
                    this.diagnosticFixture.ExperimentId,
                    Guid.NewGuid().ToString(),
                    DiagnosticsIssueType.ArmVmCreationFailure,
                    DateTime.UtcNow.AddMinutes(-30),
                    DateTime.UtcNow,
                    new Dictionary<string, IConvertible>
                        {
                            { DiagnosticsParameter.SubscriptionId, Guid.NewGuid().ToString() },
                            { DiagnosticsParameter.ResourceGroupName, Guid.NewGuid().ToString() }
                        })
            };
            this.diagnosticFixture.DataClient.OnGetDiagnosticsRequestsAsync().ReturnsAsync(diagnosticList);
            var result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, this.diagnosticFixture.CancellationToken);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.Succeeded);
        }

        [Test]
        public void AutoTriageProviderExecutionSucceedsWithNoDiagnosticRequests()
        {
            this.diagnosticFixture.DataClient.OnGetDiagnosticsRequestsAsync().ReturnsAsync(new List<DiagnosticsRequest>());
            var result = this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, this.diagnosticFixture.CancellationToken).GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.Succeeded);
        }

        [Test]
        public void AutoTriageProviderExecutionSucceedsWithNullDiagnosticRequest()
        {
            this.diagnosticFixture.DataClient.OnGetDiagnosticsRequestsAsync().ReturnsAsync(null as List<DiagnosticsRequest>);
            var result = this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, this.diagnosticFixture.CancellationToken).GetAwaiter().GetResult();
            
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.Succeeded);
        }

        [Test]
        public void AutoTriageProviderHandlesCancellationBeforeExecutingDiagnostics()
        {
            CancellationTokenSource tokenSource = new CancellationTokenSource();
            tokenSource.Cancel();
            var result = this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, tokenSource.Token)
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.Cancelled);
        }

        [Test]
        public void AutoTriageProviderInvokesCorrectApiCallsWhileExecutingSuccessfulExecutionWorkflow()
        {
            DiagnosticState diagnosticState = new DiagnosticState();
            this.diagnosticFixture.DataClient.OnGetState<DiagnosticState>().ReturnsAsync(diagnosticState);
            this.diagnosticFixture.DataClient.OnSaveState<DiagnosticState>().Returns(Task.CompletedTask);
            var result = this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, this.diagnosticFixture.CancellationToken).GetAwaiter().GetResult();

            // AutoTriage Provider calls the AutoTriageProvider state object to get the current state
            this.diagnosticFixture.DataClient.Verify(client => client.GetOrCreateStateAsync<AutoTriageProvider.State>(
                this.mockFixture.ExperimentId,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string>()));

            // AutoTriage Provider calls the Diagnostic state information from the diagnostic handlers
            this.diagnosticFixture.DataClient.Verify(client => client.GetOrCreateStateAsync<IEnumerable<DiagnosticsRequest>>(
                this.mockFixture.ExperimentId,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string>()));

            foreach (DiagnosticsRequest request in this.diagnosticFixture.DiagnosticRequestsList)
            {
                // Each diagnostic provider calls the diagnostic state object to get or create the current state
                this.diagnosticFixture.DataClient.Verify(client => client.GetOrCreateStateAsync<DiagnosticState>(
                    request.ExperimentId,
                    $"{request.ExperimentId}-diagnostics",
                    It.IsAny<CancellationToken>(),
                    $"{request.Id}-diagnostics-state"),
                    Times.AtLeastOnce());

                // Each diagnostic provider saves the diagnostic state object to get the current state
                this.diagnosticFixture.DataClient.Verify(client => client.SaveStateAsync<DiagnosticState>(
                    request.ExperimentId,
                    $"{request.ExperimentId}-diagnostics",
                    diagnosticState,
                    this.diagnosticFixture.CancellationToken,
                    $"{request.Id}-diagnostics-state"));
            }

            // AutoTriage Provider saves the current autotriage provider state before exiting ExecuteAsync function
            this.diagnosticFixture.DataClient.Verify(client => client.SaveStateAsync(
                this.mockFixture.ExperimentId,
                It.IsAny<string>(),
                this.mockState,
                It.IsAny<CancellationToken>(),
                It.IsAny<string>()));

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.Succeeded);
        }

        [Test]
        public void AutoTriageProviderCreatesCorrectAmountOfDiagnosticProviders()
        {
            Assert.AreEqual(this.provider.CreateDiagnosticsProviders().Count(), 5);
        }

        private void SetupMockDefaults()
        {
            this.diagnosticFixture.DataClient.OnGetState<AutoTriageProvider.State>().ReturnsAsync(this.mockState);
            this.diagnosticFixture.DataClient.OnSaveState<AutoTriageProvider.State>().Returns(Task.CompletedTask);
            this.diagnosticFixture.DataClient.OnAddDiagnosticsRequestAsync().Returns(Task.CompletedTask);
            this.diagnosticFixture.DataClient.OnGetDiagnosticsRequestsAsync().Returns(Task.FromResult(this.diagnosticFixture.DiagnosticRequestsList));

            // ensure the successful path defaults to http success codes
            using (HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK))
            {
                // arm always returns a successful result
                this.diagnosticFixture.ArmClient.Setup(method => method.GetSubscriptionActivityLogsAsync(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<IEnumerable<string>>()))
                    .ReturnsAsync(response);
            }

            // query issuer always returns a data table object
            this.diagnosticFixture.QueryIssuer.Setup(method => method.IssueAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .Returns(Task.FromResult<DataTable>(null));

            // tip session always returns valid tip session change details
            this.diagnosticFixture.TipClient.Setup(method => method.GetTipSessionChangeAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new TipNodeSessionChangeDetails()));
        }

        private class TestAutoTriageProvider : AutoTriageProvider
        {
            public TestAutoTriageProvider(IServiceCollection services)
                : base(services)
            {
            }

            public new IEnumerable<DiagnosticsProvider> CreateDiagnosticsProviders()
            {
                List<DiagnosticsProvider> diagnosticProviders = new List<DiagnosticsProvider>()
                {
                    new MicrocodeUpdateFailureKustoDiagnostics(this.Services),
                    new TipDeploymentFailureKustoDiagnostics(this.Services),
                    new ArmVmDeploymentFailureActivityLogDiagnostics(this.Services),
                    new ArmVmDeploymentFailureKustoDiagnostics(this.Services),
                    new TipApiServiceFailureDiagnostics(this.Services)
                };

                return diagnosticProviders;
            }
        }
    }
}