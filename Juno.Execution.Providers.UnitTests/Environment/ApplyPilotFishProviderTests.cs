namespace Juno.Execution.Providers.Environment
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Execution.Providers.AutoTriage;
    using Juno.Execution.TipIntegration;
    using Juno.Providers;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;
    using TipGateway.Entities;
    using static Juno.Execution.Providers.Environment.ApplyPilotFishProvider;

    [TestFixture]
    [Category("Unit")]
    public class ApplyPilotFishProviderTests
    {
        private ProviderFixture mockFixture;
        private Mock<ITipClient> mockTipClient;
        private ApplyPilotFishProvider provider;
        private ApplyPilotFishState state;
        private List<DiagnosticsRequest> mockDiagnosticRequest;

        [SetUp]
        public void SetupTest()
        {
            // This unit test mimics applying pilotfish on a group with two tip sessions.:
            // 1. Provider first get provissioned tipsessions and install pilotfish service and update state, return in progress.
            // 2. Provider checks tip session change status, if not finished will return in progress.
            // 3. Provider third time check if progress is created and return success.
            // 4. If checking heart beat exceeds maximum wait time, providor will error and timeout.

            this.mockFixture = new ProviderFixture(typeof(ApplyPilotFishProvider));
            this.mockFixture.SetupExperimentMocks();
            this.mockFixture.DataClient.SetReturnsDefault(new HttpResponseMessage(HttpStatusCode.OK));

            this.mockFixture.Component = new ExperimentComponent(
                typeof(ApplyPilotFishProvider).FullName,
                "Apply a pilot fish",
                "Any Description",
                "Group A",
                parameters: new Dictionary<string, IConvertible>
                {
                    { "Timeout", "00:00:20:00" },
                    { "pilotFishServiceName", "SomePackage" },
                    { "pilotFishServicePath", "SomeLocation" }
                });

            this.mockTipClient = new Mock<ITipClient>();

            this.mockFixture.Services = new ServiceCollection();
            this.mockFixture.Services.AddSingleton(NullLogger.Instance);
            this.mockFixture.Services.AddSingleton(this.mockTipClient.Object);
            this.mockFixture.Services.AddSingleton(this.mockFixture.DataClient.Object);
            this.provider = new ApplyPilotFishProvider(this.mockFixture.Services);

            this.state = new ApplyPilotFishState()
            {
                InstallationRequested = false,
                InstallationConfirmed = false,
                InstallationRequestTime = DateTime.UtcNow
            };

            this.mockDiagnosticRequest = new List<DiagnosticsRequest>()
                {
                    new DiagnosticsRequest(
                        this.mockFixture.ExperimentId,
                        Guid.NewGuid().ToString(),
                        DiagnosticsIssueType.TipPilotfishPackageDeploymentFailure,
                        DateTime.UtcNow.AddHours(-2),
                        DateTime.UtcNow,
                        new Dictionary<string, IConvertible>()
                        {
                            { DiagnosticsParameter.TipNodeId,  "Node01" },
                            { DiagnosticsParameter.TipSessionId, "AnySessionId1" },
                            { DiagnosticsParameter.ExperimentId, this.mockFixture.ExperimentId },
                            { DiagnosticsParameter.ProviderName, nameof(ApplyPilotFishProvider) }
                        })
                };

            this.InitializeDefaultMockBehaviors();
        }

        [Test]
        public void ApplyPilotFishProviderValidatesRequiredParameters()
        {
            var provider = new ApplyPilotFishProvider(this.mockFixture.Services);
            Assert.ThrowsAsync<ArgumentException>(() => provider.ExecuteAsync(null, this.mockFixture.Component, CancellationToken.None));
            Assert.ThrowsAsync<ArgumentException>(() => provider.ExecuteAsync(this.mockFixture.Context, null, CancellationToken.None));
        }

        [Test]
        public async Task ApplyPilotFishProviderSavesStateInTheExpectedLocation()
        {
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None)
                .ConfigureAwait(false);

            this.mockFixture.DataClient
                .Verify(client => client.GetOrCreateStateAsync<ApplyPilotFishState>(
                    this.mockFixture.Context.Experiment.Id,
                    $"state-{this.mockFixture.Context.ExperimentStep.Id}",
                    It.IsAny<CancellationToken>(),
                    null));
        }

        [Test]
        [Ignore("The workflow in the provider was refactored to be more efficient. This unit test class is going to require cleanup in a follow up PR.")]
        public async Task ApplyPilotFishProviderInstallPilotFishOnAllTipSessions()
        {
            // Tip client should apply pilotfish.
            this.mockTipClient.Setup(c => c.ApplyPilotFishServicesAsync(It.IsAny<string>(), It.IsAny<List<KeyValuePair<string, string>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string tipSessionId, List<KeyValuePair<string, string>> pilotfish, CancellationToken token) => new TipNodeSessionChange()
                {
                    TipNodeSessionId = tipSessionId,
                    TipNodeSessionChangeId = $"Change-tipSessionId",
                    Status = TipNodeSessionChangeStatus.Queued
                })
                .Verifiable();

            // provider should save Tip with updated change id.
            this.mockFixture.DataClient
                .Setup(c => c.UpdateStateItemsAsync<EnvironmentEntity>(
                    this.mockFixture.Context.Experiment.Id,
                    ContractExtension.EntitiesProvisioned,
                    It.IsAny<IEnumerable<EnvironmentEntity>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            // Provider should save state
            this.mockFixture.DataClient
                .Setup(c => c.SaveStateAsync<ApplyPilotFishState>(
                    this.mockFixture.Context.Experiment.Id,
                    $"state-{this.mockFixture.Context.ExperimentStep.Id}",
                    It.IsAny<ApplyPilotFishState>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .Callback<string, string, ApplyPilotFishState, CancellationToken, string>((experimentId, stepId, state, token, stateId) =>
                {
                    Assert.IsTrue(state.InstallationRequested);
                    Assert.IsFalse(state.InstallationConfirmed);
                    Assert.IsFalse(state.InstallationRequestTime == null);
                })
                .Returns(Task.CompletedTask)
                .Verifiable();

            ExecutionResult result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None)
                .ConfigureAwait(false);

            // Provider should return inprogress when starting new.
            Assert.AreEqual(ExecutionStatus.InProgress, result.Status);

            this.mockTipClient.Verify(c => c.ApplyPilotFishServicesAsync(It.IsAny<string>(), It.IsAny<List<KeyValuePair<string, string>>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            this.mockFixture.DataClient.Verify();
        }

        [Test]
        public async Task ApplyPilotFishProviderPassesInvokesCorrectTipClientFunctionWhenIsSocService()
        {
            this.mockTipClient.Setup(tc => tc.ApplyPilotFishServicesOnSocAsync(It.IsAny<string>(), It.IsAny<List<KeyValuePair<string, string>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TipNodeSessionChange());
            ExperimentComponent socComponent = new ExperimentComponent(this.mockFixture.Component);
            socComponent.Parameters.Add("isSocService", true);
            
            ExecutionResult result = await this.provider.ExecuteAsync(this.mockFixture.Context, socComponent, CancellationToken.None);

            this.mockTipClient.Verify(tc => tc.ApplyPilotFishServicesOnSocAsync(It.IsAny<string>(), It.IsAny<List<KeyValuePair<string, string>>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Test]
        public async Task ApplyPilotFishProviderWaitForConfirmationWillReturnInprogressIfNoConfirmation()
        {
            this.state.InstallationRequested = true;

            // Tip client return in progress.
            this.mockTipClient.Setup(c => c.GetTipSessionChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string tipSessionId, string changeId, CancellationToken token) => new TipNodeSessionChangeDetails()
                {
                    TipNodeSessionId = tipSessionId,
                    TipNodeSessionChangeId = $"Change-tipSessionId",
                    Result = TipNodeSessionChangeResult.Unknown,
                    Status = TipNodeSessionChangeStatus.Queued
                })
                .Verifiable();

            // Provider should save state
            this.mockFixture.DataClient
                .Setup(c => c.SaveStateAsync<ApplyPilotFishState>(
                    this.mockFixture.Context.Experiment.Id, 
                    $"state-{this.mockFixture.Context.ExperimentStep.Id}", 
                    It.IsAny<ApplyPilotFishState>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .Callback<string, string, ApplyPilotFishState, CancellationToken, string>((experimentId, stepId, state, token, stateId) =>
                {
                    Assert.IsTrue(state.InstallationRequested);
                    Assert.IsFalse(state.InstallationConfirmed);
                })
                .Returns(Task.CompletedTask)
                .Verifiable();

            ExecutionResult result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None)
                .ConfigureAwait(false);

            // Provider should return inprogress when heart beat were not received.
            Assert.AreEqual(ExecutionStatus.InProgress, result.Status);

            this.mockTipClient.Verify();
            this.mockFixture.DataClient.Verify();
        }

        [Test]
        public async Task ApplyPilotFishProviderWaitForConfirmationWillReturnSuccessWhenAllTipChangeSucceeded()
        {
            this.state.InstallationRequested = true;

            // Tip client return success.
            this.mockTipClient.Setup(c => c.GetTipSessionChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string tipSessionId, string changeId, CancellationToken token) => new TipNodeSessionChangeDetails()
                {
                    TipNodeSessionId = tipSessionId,
                    TipNodeSessionChangeId = $"Change-tipSessionId",
                    Result = TipNodeSessionChangeResult.Succeeded,
                    Status = TipNodeSessionChangeStatus.Finished
                })
                .Verifiable();

            // Provider should save state
            this.mockFixture.DataClient
                .Setup(c => c.SaveStateAsync<ApplyPilotFishState>(
                    this.mockFixture.Context.Experiment.Id,
                    $"state-{this.mockFixture.Context.ExperimentStep.Id}",
                    It.IsAny<ApplyPilotFishState>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .Callback<string, string, ApplyPilotFishState, CancellationToken, string>((experimentId, stepId, state, token, stateId) =>
                {
                    Assert.IsTrue(state.InstallationRequested);
                    Assert.IsTrue(state.InstallationConfirmed);
                })
                .Returns(Task.CompletedTask)
                .Verifiable();

            ExecutionResult result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None)
                .ConfigureAwait(false);

            // Provider should return succeed when heartbeast were heard.
            Assert.AreEqual(ExecutionStatus.Succeeded, result.Status);

            this.mockTipClient.Verify();
            this.mockFixture.DataClient.Verify();
        }

        [Test]
        [Ignore("The workflow in the provider was refactored to be more efficient. This unit test class is going to require cleanup in a follow up PR.")]
        public async Task ApplyPilotFishProviderTimesOutIfConfirmationsAreNotReceivedWithinTimeoutPeriod()
        {
            this.state.InstallationRequested = true;
            this.state.InstallationRequestTime = DateTime.UtcNow - TimeSpan.FromHours(1);

            // Tip client return in progress.
            this.mockTipClient.Setup(c => c.GetTipSessionChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string tipSessionId, string changeId, CancellationToken token) => new TipNodeSessionChangeDetails()
                {
                    TipNodeSessionId = tipSessionId,
                    TipNodeSessionChangeId = $"Change-tipSessionId",
                    Result = TipNodeSessionChangeResult.Unknown,
                    Status = TipNodeSessionChangeStatus.Executing
                })
                .Verifiable();

            ExecutionResult result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None)
                .ConfigureAwait(false);

            // Provider should fail when timed out.
            Assert.AreEqual(ExecutionStatus.Failed, result.Status);
            Assert.IsTrue(result.Error.Message.Contains("Timing out the experiment."));

            this.mockTipClient.Verify();
            this.mockFixture.DataClient.Verify();
        }

        [Test]
        public void ApplyPilotFishProviderRequestsAutoTriageDiagnosticsOnFailedExperimentsWithDiagnosticsEnabled()
        {
            // Enable Diagnostics flag is on
            this.mockFixture.Component.Parameters[StepParameters.EnableDiagnostics] = true;
            // Execution will fail with invalid resource group deployment
            this.ValidatePilotFishDeployment(TipNodeSessionChangeResult.Failed, ExecutionStatus.Failed);

            // call to request autotriage diagnostics are made
            this.mockFixture.DataClient.Verify(method =>
                method.UpdateStateItemsAsync(
                    this.mockFixture.ExperimentId,
                    ContractExtension.DiagnosticsRequests,
                    this.mockDiagnosticRequest,
                    CancellationToken.None,
                    null),
                Times.AtMostOnce);
        }

        [Test]
        public void ApplyPilotFishProviderDoesNotRequestAutoTriageDiagnosticsOnFailedExperimentsWithoutDiagnosticsEnabled()
        {
            // Enable Diagnostics flag is not enabled
            this.mockFixture.Component.Parameters[StepParameters.EnableDiagnostics] = false;
            // Execution will fail with invalid resource group deployment
            this.ValidatePilotFishDeployment(TipNodeSessionChangeResult.Failed, ExecutionStatus.Failed);

            // call to request autotriage diagnostics are not made
            this.mockFixture.DataClient.Verify(method =>
                method.UpdateStateItemsAsync(
                    this.mockFixture.ExperimentId,
                    ContractExtension.DiagnosticsRequests,
                    this.mockDiagnosticRequest,
                    CancellationToken.None,
                    null),
                Times.Never);
        }

        [Test]
        [TestCase(TipNodeSessionChangeResult.Unknown, ExecutionStatus.InProgress)]
        [TestCase(TipNodeSessionChangeResult.Succeeded, ExecutionStatus.Succeeded)]
        public void ApplyPilotFishProviderWithDiagnosticsEnabledDoesNotRequestDiagnosticsFromUnfailedExperiments(TipNodeSessionChangeResult state, ExecutionStatus status)
        {
            // Enable Diagnostics flag is enabled
            this.mockFixture.Component.Parameters[StepParameters.EnableDiagnostics] = true;
            // Testing other resource group deployment execution statuses
            this.ValidatePilotFishDeployment(state, status);

            // call to request autotriage diagnostics are not made
            this.mockFixture.DataClient.Verify(method =>
                method.UpdateStateItemsAsync(
                    this.mockFixture.ExperimentId,
                    ContractExtension.DiagnosticsRequests,
                    this.mockDiagnosticRequest,
                    CancellationToken.None,
                    null),
                Times.Never);
        }

        [Test]
        public void ApplyPilotFishProviderReturnCorrectStatusonResourceDeployment()
        {
            this.ValidatePilotFishDeployment(TipNodeSessionChangeResult.Unknown, ExecutionStatus.InProgress);
            this.ValidatePilotFishDeployment(TipNodeSessionChangeResult.Failed, ExecutionStatus.Failed);
            this.ValidatePilotFishDeployment(TipNodeSessionChangeResult.Succeeded, ExecutionStatus.Succeeded);
        }

        private static IEnumerable<EnvironmentEntity> CreateMockTipSessions(TipSessionStatus status)
        {
            List<EnvironmentEntity> sessions = new List<EnvironmentEntity>();
            sessions.Add(TipSession.ToEnvironmentEntity(new TipSession()
            {
                TipSessionId = "session-node-a",
                ClusterName = "cluster-a",
                Region = "region1",
                GroupName = "Group A",
                NodeId = "node1",
                ChangeIdList = new List<string>() { "Change-node-a" },
                Status = status,
                CreatedTimeUtc = DateTime.UtcNow.Subtract(TimeSpan.FromDays(1)),
                ExpirationTimeUtc = DateTime.UtcNow.Add(TimeSpan.FromDays(1)),
                DeletedTimeUtc = DateTime.MaxValue
            }, "Group A"));

            sessions.Add(TipSession.ToEnvironmentEntity(new TipSession()
            {
                TipSessionId = "session-node-b",
                ClusterName = "cluster-b",
                Region = "region2",
                GroupName = "Group B",
                NodeId = "node2",
                ChangeIdList = new List<string>() { "Change-node-b" },
                Status = status,
                CreatedTimeUtc = DateTime.UtcNow.Subtract(TimeSpan.FromDays(1)),
                ExpirationTimeUtc = DateTime.UtcNow.Add(TimeSpan.FromDays(1)),
                DeletedTimeUtc = DateTime.MaxValue
            }, "Group A"));

            return sessions;
        }

        private void ValidatePilotFishDeployment(TipNodeSessionChangeResult tipSessionState, ExecutionStatus expectedStatus)
        {
            this.state.InstallationRequested = true;

            // Tip client return success.
            this.mockTipClient.Setup(c => c.GetTipSessionChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string tipSessionId, string changeId, CancellationToken token) => new TipNodeSessionChangeDetails()
                {
                    TipNodeSessionId = tipSessionId,
                    TipNodeSessionChangeId = $"Change-tipSessionId",
                    Result = tipSessionState,
                    Status = TipNodeSessionChangeStatus.Finished
                });

            if (ExecutionResult.CompletedStatuses.Contains(expectedStatus))
            {
                // Provider should save state
                this.mockFixture.DataClient
                    .Setup(c => c.SaveStateAsync<ApplyPilotFishState>(
                        this.mockFixture.Context.Experiment.Id,
                        $"state-{this.mockFixture.Context.ExperimentStep.Id}",
                        It.IsAny<ApplyPilotFishState>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<string>()))
                    .Callback<string, string, ApplyPilotFishState, CancellationToken, string>((experimentId, stepId, state, token, stateId) =>
                    {
                        Assert.IsTrue(state.InstallationRequested);
                        Assert.IsTrue(state.InstallationConfirmed);
                    })
                    .Returns(Task.CompletedTask);
            }
            else
            {
                // Provider should save state
                this.mockFixture.DataClient
                    .Setup(c => c.SaveStateAsync<ApplyPilotFishState>(
                        this.mockFixture.Context.Experiment.Id,
                        $"state-{this.mockFixture.Context.ExperimentStep.Id}",
                        It.IsAny<ApplyPilotFishState>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<string>()))
                    .Callback<string, string, ApplyPilotFishState, CancellationToken, string>((experimentId, stepId, state, token, stateId) =>
                    {
                        Assert.IsTrue(state.InstallationRequested);
                        Assert.IsFalse(state.InstallationConfirmed);
                    })
                    .Returns(Task.CompletedTask);
            }

            ExecutionResult result = this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None).Result;
            
            // Provider should return succeed when heartbeast were heard.
            Assert.AreEqual(expectedStatus, result.Status);
        }

        private void InitializeDefaultMockBehaviors()
        {
            // Default behavior for getting the generic state object for the
            // provider.
            this.mockFixture.DataClient
               .Setup(c => c.GetOrCreateStateAsync<ApplyPilotFishState>(
                   this.mockFixture.Context.Experiment.Id,
                   $"state-{this.mockFixture.Context.ExperimentStep.Id}",
                   It.IsAny<CancellationToken>(),
                   It.IsAny<string>()))
               .ReturnsAsync(this.state);

            // Default behavior for getting the 'entitiesProvisioned' from the shared/global
            // state object for the experiment.
            this.mockFixture.DataClient
                .Setup(c => c.GetOrCreateStateAsync<IEnumerable<EnvironmentEntity>>(
                    this.mockFixture.Context.Experiment.Id,
                    ContractExtension.EntitiesProvisioned,
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .ReturnsAsync(ApplyPilotFishProviderTests.CreateMockTipSessions(TipSessionStatus.Created))
                .Verifiable();
        }
    }
}
