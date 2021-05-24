namespace Juno.Execution.Providers.AutoTriage
{
    using System;
    using System.Collections.Generic;
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
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using NuGet.Protocol;
    using NUnit.Framework;
    using Polly;

    [TestFixture]
    [Category("Unit")]
    public class ArmVmDeploymentFailureActivityLogDiagnosticsTests
    {
        private DiagnosticsProviderFixture mockFixture;
        private TestArmVmDeploymentFailureActivityLogDiagnostics provider;
        private DiagnosticsRequest diagnosticRequest;
        private HttpResponseMessage expectedResponse;
        private ArmActivityLogEntry mockActivityLog;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new DiagnosticsProviderFixture();
            this.provider = new TestArmVmDeploymentFailureActivityLogDiagnostics(this.mockFixture.Services, this.mockFixture.RetryPolicy);
            this.diagnosticRequest = this.CreateArmVmDeploymentFailureActivityLogDiagnosticRequest();
            this.SetupMockDefaults();
        }

        [Test]
        [TestCase(DiagnosticsParameter.ResourceGroupName, "invalidParam")]
        [TestCase(DiagnosticsParameter.ResourceGroupName, DiagnosticsParameter.ExperimentId)]
        [TestCase(DiagnosticsParameter.ResourceGroupName, DiagnosticsParameter.TipSessionId)]
        [TestCase(DiagnosticsParameter.ResourceGroupName, DiagnosticsParameter.TipNodeId)]
        [TestCase(DiagnosticsParameter.ResourceGroupName, DiagnosticsParameter.ProviderName)]
        [TestCase(DiagnosticsParameter.ResourceGroupName, DiagnosticsParameter.TimeRangeBegin)]
        [TestCase(DiagnosticsParameter.ResourceGroupName, DiagnosticsParameter.TimeRangeEnd)]
        [TestCase(DiagnosticsParameter.ResourceGroupName, DiagnosticsParameter.TipSessionChangeId)]
        [TestCase(DiagnosticsParameter.ResourceGroupName, "")]
        [TestCase(DiagnosticsParameter.SubscriptionId, "invalidParam")]
        [TestCase(DiagnosticsParameter.SubscriptionId, DiagnosticsParameter.ExperimentId)]
        [TestCase(DiagnosticsParameter.SubscriptionId, DiagnosticsParameter.TipSessionChangeId)]
        [TestCase(DiagnosticsParameter.SubscriptionId, DiagnosticsParameter.TipNodeId)]
        [TestCase(DiagnosticsParameter.SubscriptionId, DiagnosticsParameter.ProviderName)]
        [TestCase(DiagnosticsParameter.SubscriptionId, DiagnosticsParameter.TimeRangeBegin)]
        [TestCase(DiagnosticsParameter.SubscriptionId, DiagnosticsParameter.TimeRangeEnd)]
        [TestCase(DiagnosticsParameter.SubscriptionId, DiagnosticsParameter.TipSessionId)]
        [TestCase(DiagnosticsParameter.SubscriptionId, "")]
        public void ArmVmDeploymentFailureActivityLogDiagnosticsProviderSkipsDiagnosticsForUnsupportedDiagnosticRequests(string expectedParam1, string expectedParam2)
        {
            DiagnosticsRequest diagnosticsRequest = new DiagnosticsRequest(
                Guid.NewGuid().ToString(),
                "ID",
                DiagnosticsIssueType.ArmVmCreationFailure,
                DateTime.UtcNow,
                DateTime.UtcNow,
                new Dictionary<string, IConvertible>()
                {
                    { expectedParam1, Guid.NewGuid().ToString() },
                    { expectedParam2, Guid.NewGuid().ToString() }
                });

            // Verify this request is not handled by the current diagnostic provider
            Assert.IsFalse(this.provider.IsHandled(diagnosticsRequest));

            // Invoke handler's DiagnoseAsync with the invalid request.
            var result = this.provider.DiagnoseAsync(
                diagnosticsRequest,
                this.mockFixture.EventContext,
                this.mockFixture.Logger.Object,
                this.mockFixture.CancellationToken)
                .GetAwaiter()
                .GetResult();

            // Verify GetActivityLogs method was never called from diagnose async
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
        [TestCase(DiagnosticsIssueType.TipPilotfishPackageDeploymentFailure)]
        [TestCase(DiagnosticsIssueType.MicrocodeUpdateFailure)]
        public void ArmVmDeploymentFailureActivityLogDiagnosticsProviderSkipsDiagnosticsForUnsupportedIssueTypes(DiagnosticsIssueType issueType)
        {
            // With all other correct fields, test the issueType of the diagnostic request received
            DiagnosticsRequest diagnosticsRequest = new DiagnosticsRequest(
                Guid.NewGuid().ToString(),
                "ID",
                issueType,
                DateTime.UtcNow,
                DateTime.UtcNow,
                this.diagnosticRequest.Context);

            // Verify this request is not handled by the current diagnostic provider
            Assert.AreEqual(this.provider.IsHandled(diagnosticsRequest), false);

            // Invoke handler's DiagnoseAsync method with an invalid issue type
            var result = this.provider.DiagnoseAsync(
                diagnosticsRequest,
                this.mockFixture.EventContext,
                this.mockFixture.Logger.Object,
                this.mockFixture.CancellationToken)
                .GetAwaiter()
                .GetResult();

            // Verify the GetActivityLogs method was never called from DiagnoseAsync function
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
        public void ArmVmDeploymentFailureActivityLogDiagnosticsProviderRunsDiagnosticsForRecognizedDiagnosticRequest()
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

            // verify the subscription logs were fetched for valid request
            this.mockFixture.ArmClient.Verify(method =>
                method.GetSubscriptionActivityLogsAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<IEnumerable<string>>()),
                    Times.AtLeastOnce());

            // Diagnostic Execution is expected to succeed with the valid diagnostic request
            Assert.AreEqual(result.Status, ExecutionStatus.Succeeded);
        }

        [Test]
        public void ArmVmDeploymentFailureActivityLogDiagnosticsProviderGetActivityLogsAsyncPassesExpectedFilterValues()
        {
            IConvertible resourceGroupName;
            this.diagnosticRequest.Context.TryGetValue(DiagnosticsParameter.ResourceGroupName, out resourceGroupName);
            string expectedFilter = $"eventTimestamp ge '{this.diagnosticRequest.TimeRangeBegin.ToUniversalTime().ToString("o")}' " +
                $"and eventTimestamp le '{this.diagnosticRequest.TimeRangeEnd.ToUniversalTime().ToString("o")}' and resourceGroupName eq '{resourceGroupName}'";

            // Invoke handler's DiagnoseAsync method
            ExecutionResult result = this.provider.DiagnoseAsync(
                this.diagnosticRequest,
                this.mockFixture.EventContext,
                this.mockFixture.Logger.Object,
                this.mockFixture.CancellationToken)
                .GetAwaiter()
                .GetResult();

            this.mockFixture.ArmClient.Verify(m =>
                m.GetSubscriptionActivityLogsAsync(
                    It.IsAny<string>(),
                    It.Is<string>(actualfilter => actualfilter == expectedFilter),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<IEnumerable<string>>()));

            Assert.AreEqual(result.Status, ExecutionStatus.Succeeded);
        }

        [Test]
        public void ArmVmDeploymentFailureActivityLogDiagnosticsProviderGetDiagnosticEntriesAsyncLogsNothingWhenABadRequestOccurs()
        {
            string expectedEventName = $"{nameof(ArmVmDeploymentFailureActivityLogDiagnostics)}.Diagnose{EventNameSuffix.Error}";

            using (HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.BadRequest))
            {
                // return a bad request
                this.mockFixture.RestClient
                    .Setup(client => client.GetAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<HttpCompletionOption>()))
                    .ReturnsAsync(response);

                this.mockFixture.ArmClient.OnGetSubscriptionLogs().ReturnsAsync(response);
            }

            // Invoke handler's DiagnoseAsync method
            this.provider.DiagnoseAsync(
                this.diagnosticRequest,
                this.mockFixture.EventContext,
                this.mockFixture.Logger.Object,
                this.mockFixture.CancellationToken)
                .GetAwaiter()
                .GetResult();

            // Verify the logger does not log results for the bad request
            this.mockFixture.Logger.Verify(method => method.Log(
                It.Is<LogLevel>(level => level == LogLevel.Trace || level == LogLevel.Error),
                It.IsAny<EventId>(),
                It.IsAny<EventContext>(),
                null,
                null), Times.Never());
        }

        [Test]
        public void ArmVmDeploymentFailureActivityLogDiagnosticsProviderExecutesExpectedWorkflowForSuccessfulDiagnostics()
        {
            var expectedStartEventName = $"{nameof(ArmVmDeploymentFailureActivityLogDiagnostics)}.DiagnoseStart";
            var expectedResultEventName = $"{nameof(ArmVmDeploymentFailureActivityLogDiagnostics)}.DiagnosticsResults";
            var expectedEndEventName = $"{nameof(ArmVmDeploymentFailureActivityLogDiagnostics)}.DiagnoseStop";

            // Invoke handler's DiagnoseAsync method
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

        [Test]
        public async Task ArmVmDeploymentFailureActivityLogDiagnosticsProviderRetriesWhenExceptionsAreThrown()
        {
            string expectedEventName = $"{nameof(ArmVmDeploymentFailureActivityLogDiagnostics)}.Diagnose{EventNameSuffix.Error}";

            int expectedRetryCount = 3;
            int intervalInMilliseconds = 1;
            IAsyncPolicy retryPolicy = Policy.Handle<Exception>()
            .WaitAndRetryAsync(expectedRetryCount, (retries) => TimeSpan.FromMilliseconds(intervalInMilliseconds));

            int actualRetryCount = -1; // this is to offset the initial api callback

            this.mockFixture.ArmClient.OnGetSubscriptionLogs()
                .ThrowsAsync(new Exception("The expected exception"))
                .Callback(() =>
                {
                    actualRetryCount++;
                });

            var diagnosticProvider = new TestArmVmDeploymentFailureActivityLogDiagnostics(this.mockFixture.Services, retryPolicy);

            // Exceptions are captured by the base class
            var result = await diagnosticProvider.DiagnoseAsync(
                this.diagnosticRequest,
                this.mockFixture.Logger.Object,
                this.mockFixture.CancellationToken);

            // Verify the logger logs an error for the failed retries at least once
            this.mockFixture.Logger.Verify(method => method.Log(
                It.Is<LogLevel>(level => level == LogLevel.Error),
                It.Is<EventId>(action => action == new EventId(expectedEventName.GetHashCode(StringComparison.OrdinalIgnoreCase), expectedEventName)),
                It.IsAny<EventContext>(),
                null,
                null), Times.AtLeastOnce());

            Assert.AreEqual(expectedRetryCount, actualRetryCount);
            Assert.AreEqual(result.Status, ExecutionStatus.Failed);
        }

        [Test]
        public void ArmVmDeploymentFailureActivityLogDiagnosticsProviderGetsDiagnosticEntriesForMultiplePageActivityLog()
        {
            ArmActivityLogEntry twoPageActivityLog = ArmVmDeploymentFailureActivityLogDiagnosticsTests.CreateValidActivityLog();

            IEnumerable<EventLogDataValues> expectedLogs = this.mockActivityLog.Value.Concat(twoPageActivityLog.Value);

            // Set the sequence to fetch the first result with a second page, second result with no additional pages, return the combined logs
            this.mockFixture.ArmClient.SetupSequence(method => method.GetSubscriptionActivityLogsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<IEnumerable<string>>()))
                .ReturnsAsync(
                    ArmVmDeploymentFailureActivityLogDiagnosticsTests.CreateResponseMessage(
                        HttpStatusCode.OK,
                        twoPageActivityLog.ToJson()))
                .ReturnsAsync(this.expectedResponse)
                .ReturnsAsync(
                    ArmVmDeploymentFailureActivityLogDiagnosticsTests.CreateResponseMessage(
                        HttpStatusCode.OK,
                        expectedLogs.ToJson()));

            // Invoke handler's DiagnoseAsync method
            this.provider.DiagnoseAsync(
                    this.diagnosticRequest,
                    this.mockFixture.EventContext,
                    this.mockFixture.Logger.Object,
                    this.mockFixture.CancellationToken).GetAwaiter().GetResult();

            // The logger captures the diagnostic data based on successful workflow
            this.mockFixture.Logger.Verify(method => method.Log(
                It.Is<LogLevel>(level => level == LogLevel.Trace),
                It.Is<EventId>(e => e.Name == $"{nameof(ArmVmDeploymentFailureActivityLogDiagnostics)}.DiagnosticsResults"),
                It.Is<EventContext>(
                    context => context.ActivityId == this.mockFixture.EventContext.ActivityId
                    && context.Properties != null
                    && context.Properties.ContainsKey("items") // contains diagnostic entries
                    && !string.IsNullOrWhiteSpace(context.Properties["items"].ToString())),
                null,
                null), Times.Exactly(1));
        }

        [Test]
        public void ArmVmDeploymentFailureActivityLogDiagnosticsProviderDoesNotLogEmptyActivityLogEntries()
        {
            // Create an empty activity log (results are not in yet)
            ArmActivityLogEntry emptyLog = new ArmActivityLogEntry()
            {
                NextLink = null,
                Value = null
            };

            this.mockFixture.ArmClient.SetupSequence(method => method.GetSubscriptionActivityLogsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<IEnumerable<string>>()))
                .ReturnsAsync(ArmVmDeploymentFailureActivityLogDiagnosticsTests.CreateResponseMessage(HttpStatusCode.OK, emptyLog.ToJson()));

            this.provider.DiagnoseAsync(
                    this.diagnosticRequest,
                    this.mockFixture.EventContext,
                    this.mockFixture.Logger.Object,
                    this.mockFixture.CancellationToken).GetAwaiter().GetResult();

            // Verify the logger does not log results for the empty log
            this.mockFixture.Logger.Verify(method => method.Log(
                It.Is<LogLevel>(level => level == LogLevel.Trace || level == LogLevel.Error),
                It.IsAny<EventId>(),
                It.IsAny<EventContext>(),
                null,
                null), Times.Never());
        }

        private static HttpResponseMessage CreateResponseMessage(HttpStatusCode expectedStatusCode, string content)
        {
            HttpResponseMessage mockResponse = new HttpResponseMessage(expectedStatusCode);
            mockResponse.Content = new StringContent(content);

            return mockResponse;
        }

        private static ArmActivityLogEntry CreateValidActivityLog()
        {
            return new ArmActivityLogEntry()
            {
                NextLink = "nextUrl.com",
                Value = new List<EventLogDataValues>()
                {
                    new EventLogDataValues(
                        eventTimestamp: DateTime.UtcNow.AddMinutes(-2),
                        level: "Error",
                        correlationId: Guid.NewGuid().ToString(),
                        resourceGroupName: "rg-987654321",
                        resourceType: new JObject()
                        {
                            { "value", "Microsoft.Compute/virtualMachines/extensions" },
                            { "localizedValue", "Microsoft.Compute/virtualMachines/extensions/write" }
                        },
                        operationName: new JObject()
                        {
                            { "value", "microsoft.support/supporttickets/write" },
                            { "localizedValue", "microsoft.support/supporttickets/write" }
                        },
                        properties: new JObject()
                        {
                                ["statusMessage"] = "{\"status\":\"Failed\",\"error\":{\"code\":\"ResourceOperationFailure\"," +
                                "\"message\":\"The resource operation completed with terminal provisioning state 'Failed'.\"," +
                                "\"details\":[{\"code\":\"OSProvisioningTimedOut\",\"message\":\"OS Provisioning for VM '...' did not finish " +
                                "in the allotted time. The VM may still finish provisioning successfully.}]}}"
                        },
                        status: new JObject()
                        {
                            { "value", "Failed" },
                            { "localizedValue", "Failed" }
                        })
                }
            };
        }

        /// <summary>
        /// Creates a valid Diagnostic Request with required parameters for the ArmVmDeploymentFailureActivityLogDiagnostics provider
        /// </summary>
        private DiagnosticsRequest CreateArmVmDeploymentFailureActivityLogDiagnosticRequest()
        {
            return this.mockFixture.CreateValidDiagnosticRequest(
                issueType: DiagnosticsIssueType.ArmVmCreationFailure,
                queryContext: new Dictionary<string, IConvertible>
                {
                    { DiagnosticsParameter.SubscriptionId, Guid.NewGuid().ToString() },
                    { DiagnosticsParameter.ResourceGroupName, Guid.NewGuid().ToString() }
                });
        }

        /// <summary>
        /// The default mock behavior for the diagnostic provider which includes the expected success operation
        /// for the workflow
        /// </summary>
        private void SetupMockDefaults()
        {
            // this is verbose as it also serves as an example of the content from the activity log
            this.mockActivityLog = new ArmActivityLogEntry()
            {
                NextLink = string.Empty,
                Value = new List<EventLogDataValues>()
                {
                    new EventLogDataValues(
                        eventTimestamp: DateTime.UtcNow.AddMinutes(-2),
                        level: "Critical",
                        correlationId: Guid.NewGuid().ToString(),
                        resourceGroupName: "rg-123456789",
                        resourceType: new JObject()
                        {
                            { "value", "Microsoft.Compute/virtualMachines/extensions" },
                            { "localizedValue", "Microsoft.Compute/virtualMachines/extensions/write" }
                        },
                        operationName: new JObject()
                        {
                            { "value", "microsoft.support/supporttickets/write" },
                            { "localizedValue", "microsoft.support/supporttickets/write" }
                        },
                        properties: new JObject()
                        {
                            ["statusMessage"] = "{\"status\":\"Failed\"," +
                            "\"error\":{\"code\":\"ResourceOperationFailure\"," +
                            "\"message\":\"The resource operation completed with terminal provisioning state 'Failed'.\"," +
                            "\"details\":" +
                                "[{\"code\":\"OSProvisioningTimedOut\"," +
                                "\"message\":\"OS Provisioning for VM '12345' did not finish in the allotted time. " +
                                "The VM may still finish provisioning successfully. Please check provisioning state later.}]}}",
                            ["eventCategory"] = "Administrative",
                            ["entity"] = "/subscriptions/.../Microsoft.Azure.Security.AntimalwareSignature.AntimalwareConfiguration",
                            ["Message"] = "Microsoft.Compute/virtualMachines/extensions/write",
                            ["hierarchy"] = "..." 
                        },
                        status: new JObject()
                        {
                            ["value"] = "Failed",
                            ["localizedValue"] = "Failed"
                        })
                }
            };

            this.expectedResponse = ArmVmDeploymentFailureActivityLogDiagnosticsTests.CreateResponseMessage(HttpStatusCode.OK, this.mockActivityLog.ToJson());

            this.mockFixture.ArmClient.SetupSequence(method => method.GetSubscriptionActivityLogsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<IEnumerable<string>>()))
                .ReturnsAsync(this.expectedResponse)
                .ReturnsAsync(ArmVmDeploymentFailureActivityLogDiagnosticsTests.CreateResponseMessage(HttpStatusCode.OK, this.mockActivityLog.Value.ToJson()));
        }

        /// <summary>
        /// Used to expose protected methods of the underlying provider
        /// </summary>
        private class TestArmVmDeploymentFailureActivityLogDiagnostics : ArmVmDeploymentFailureActivityLogDiagnostics
        {
            public TestArmVmDeploymentFailureActivityLogDiagnostics(IServiceCollection services, IAsyncPolicy retryPolicy = null)
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