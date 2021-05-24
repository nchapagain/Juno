namespace Juno.Execution.Providers.Environment
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Contracts;
    using Juno.Execution.ArmIntegration;
    using Juno.Execution.Providers.AutoTriage;
    using Juno.Execution.TipIntegration;
    using Juno.Providers;
    using Microsoft.Azure.Management.AppService.Fluent.Models;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;
    using TipGateway.Entities;
    using static Juno.Execution.Providers.Environment.DeployHostingEnvironmentProvider;

    [TestFixture]
    [Category("Unit")]
    public class DeployHostingEnvironmentProviderTests
    {
        private Fixture mockFixture;
        private Mock<ITipClient> mockTipClient;
        private Mock<IProviderDataClient> mockDataClient;
        private IConfiguration mockConfiguration;
        private ExperimentContext mockExperimentContext;
        private ExperimentComponent mockExperimentComponent;
        private ServiceCollection providerServices;
        private List<DiagnosticsRequest> mockDiagnosticRequest;
        private DeployHostingEnvironmentProvider provider;
        private DeployHostingEnvironmentState providerState;

        [SetUp]
        public void SetupTest()
        {
            // This unit test mimics deploying Hosting environment on a group with two tip sessions.:
            // 1. Provider first get provissioned tipsessions and deploy hosting environment and update state, return in progress.
            // 2. Provider checks tip session change status, if not finished will return in progress.
            // 3. Provider third time check if progress is created and return success.
            // 4. If checking heart beat exceeds maximum wait time, providor will error and timeout.

            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks();
            this.mockTipClient = new Mock<ITipClient>();
            this.mockDataClient = new Mock<IProviderDataClient>();
            this.mockDataClient.SetReturnsDefault(new HttpResponseMessage(HttpStatusCode.OK));

            this.mockExperimentComponent = new ExperimentComponent(
                typeof(DeployHostingEnvironmentProvider).FullName,
                "Deploy an HE",
                "Any Description",
                "Group A",
                parameters: new Dictionary<string, IConvertible>
                {
                    { "Timeout", "00:00:20:00" },
                    { "componentType", HostingEnvironmentComponent.OSHostPlugin.ToString() },
                    { "componentLocation", "SomeLocation" }
                });

            this.providerServices = new ServiceCollection();
            this.providerServices.AddSingleton(NullLogger.Instance);
            this.providerServices.AddSingleton(this.mockTipClient.Object);
            this.providerServices.AddSingleton(this.mockDataClient.Object);

            this.mockConfiguration = new ConfigurationBuilder()
                  .SetBasePath(Path.Combine(
                              Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                              @"Configuration"))
                          .AddJsonFile($"juno-dev01.environmentsettings.json")
                          .Build();

            this.mockExperimentContext = new ExperimentContext(
                this.mockFixture.Create<ExperimentInstance>(),
                this.mockFixture.Create<ExperimentStepInstance>(),
                this.mockConfiguration);
            this.provider = new DeployHostingEnvironmentProvider(this.providerServices);
            this.mockDiagnosticRequest = new List<DiagnosticsRequest>()
            {
                new DiagnosticsRequest(
                        this.mockExperimentContext.ExperimentId,
                        Guid.NewGuid().ToString(),
                        DiagnosticsIssueType.TipPilotfishPackageDeploymentFailure,
                        DateTime.UtcNow.AddHours(-2),
                        DateTime.UtcNow,
                        new Dictionary<string, IConvertible>()
                        {
                            { DiagnosticsParameter.TipNodeId, "node01" },
                            { DiagnosticsParameter.TipSessionId, "tipSession1" },
                            { DiagnosticsParameter.ExperimentId, this.mockExperimentContext.ExperimentId },
                            { DiagnosticsParameter.ProviderName, nameof(DeployHostingEnvironmentProvider) }
                        }) 
            };
        }

        [Test]
        public void DeployHostingEnvironmentProviderValidatesRequiredParameters()
        {
            Assert.ThrowsAsync<ArgumentException>(() => this.provider.ExecuteAsync(null, this.mockExperimentComponent, CancellationToken.None));
            Assert.ThrowsAsync<ArgumentException>(() => this.provider.ExecuteAsync(this.mockExperimentContext, null, CancellationToken.None));
        }

        [Test]
        public async Task DeployHostingEnvironmentProviderDeployHEOnAllTipSessions()
        {
            this.providerState = null;

            // First return null state for the provider.
            this.mockDataClient
                .Setup(c => c.GetOrCreateStateAsync<DeployHostingEnvironmentState>(
                    this.mockExperimentContext.Experiment.Id, 
                    $"state-{this.mockExperimentContext.ExperimentStep.Id}",
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .ReturnsAsync(this.providerState)
                .Verifiable();

            // Return provisioned tip sessions
            this.mockDataClient
                .Setup(c => c.GetOrCreateStateAsync<IEnumerable<EnvironmentEntity>>(
                    this.mockExperimentContext.Experiment.Id,
                    ContractExtension.EntitiesProvisioned,
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .ReturnsAsync(DeployHostingEnvironmentProviderTests.TipSessions(TipSessionStatus.Created))
                .Verifiable();

            // Tip client should deploy HE.
            this.mockTipClient.Setup(c => c.DeployHostingEnvironmentAsync(It.IsAny<string>(), It.IsAny<List<HostingEnvironmentLineItem>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string tipSessionId, List<HostingEnvironmentLineItem> he, CancellationToken token) => new TipNodeSessionChange()
                {
                    TipNodeSessionId = tipSessionId,
                    TipNodeSessionChangeId = $"Change-tipSessionId",
                    Status = TipNodeSessionChangeStatus.Queued
                })
                .Verifiable();

            // provider should save Tip with updated change id.
            this.mockDataClient
                .Setup(c => c.UpdateStateItemsAsync<EnvironmentEntity>(
                    this.mockExperimentContext.Experiment.Id,
                    ContractExtension.EntitiesProvisioned,
                    It.IsAny<IEnumerable<EnvironmentEntity>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            // Provider should save state
            this.mockDataClient
                .Setup(c => c.SaveStateAsync<DeployHostingEnvironmentState>(
                    this.mockExperimentContext.Experiment.Id,
                    $"state-{this.mockExperimentContext.ExperimentStep.Id}",
                    It.IsAny<DeployHostingEnvironmentState>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .Callback<string, string, DeployHostingEnvironmentState, CancellationToken, string>((experimentId, stepId, state, token, stateId) =>
                {
                    Assert.IsTrue(state.InstallationRequested);
                    Assert.IsFalse(state.InstallationConfirmed);
                    Assert.IsFalse(state.InstallationRequestTime == null);
                })
                .Returns(Task.CompletedTask)
                .Verifiable();

            ExecutionResult result = await this.provider.ExecuteAsync(this.mockExperimentContext, this.mockExperimentComponent, CancellationToken.None).ConfigureAwait(false);

            // Provider should return inprogress when starting new.
            Assert.AreEqual(ExecutionStatus.InProgress, result.Status);

            this.mockTipClient.Verify(c => c.DeployHostingEnvironmentAsync(It.IsAny<string>(), It.IsAny<List<HostingEnvironmentLineItem>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            this.mockTipClient.Verify();
            this.mockDataClient.Verify();
        }

        [Test]
        public async Task DeployHostingEnvironmentProviderWaitForConfirmationWillReturnInprogressIfNoConfirmation()
        {
            this.providerState = new DeployHostingEnvironmentState()
            {
                InstallationRequested = true,
                InstallationConfirmed = false,
                InstallationRequestTime = DateTime.UtcNow - TimeSpan.FromMinutes(10)
            };

            // Return state showing finished for the provider.
            this.mockDataClient
                .Setup(c => c.GetOrCreateStateAsync<DeployHostingEnvironmentState>(
                    this.mockExperimentContext.Experiment.Id,
                    $"state-{this.mockExperimentContext.ExperimentStep.Id}",
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .ReturnsAsync(this.providerState)
                .Verifiable();

            // Return provisioned tip sessions
            this.mockDataClient
                .Setup(c => c.GetOrCreateStateAsync<IEnumerable<EnvironmentEntity>>(
                    this.mockExperimentContext.Experiment.Id,
                    ContractExtension.EntitiesProvisioned, 
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .ReturnsAsync(DeployHostingEnvironmentProviderTests.TipSessions(TipSessionStatus.Created))
                .Verifiable();

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
            this.mockDataClient
                .Setup(c => c.SaveStateAsync<DeployHostingEnvironmentState>(
                    this.mockExperimentContext.Experiment.Id,
                    $"state-{this.mockExperimentContext.ExperimentStep.Id}",
                    It.IsAny<DeployHostingEnvironmentState>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .Callback<string, string, DeployHostingEnvironmentState, CancellationToken, string>((experimentId, stepId, state, token, stateId) =>
                {
                    Assert.IsTrue(state.InstallationRequested);
                    Assert.IsFalse(state.InstallationConfirmed);
                })
                .Returns(Task.CompletedTask)
                .Verifiable();

            ExecutionResult result = await this.provider.ExecuteAsync(this.mockExperimentContext, this.mockExperimentComponent, CancellationToken.None).ConfigureAwait(false);

            // Provider should return inprogress when heart beat were not received.
            Assert.AreEqual(ExecutionStatus.InProgress, result.Status);

            this.mockTipClient.Verify();
            this.mockDataClient.Verify();
        }

        [Test]
        public async Task DeployHostingEnvironmentProviderWaitForConfirmationWillReturnSuccessWhenAllTipChangeSucceeded()
        {
            this.providerState = new DeployHostingEnvironmentState()
            {
                InstallationRequested = true,
                InstallationConfirmed = false,
                InstallationRequestTime = DateTime.UtcNow - TimeSpan.FromMinutes(10)
            };

            // Return state showing finished for the provider.
            this.mockDataClient
                .Setup(c => c.GetOrCreateStateAsync<DeployHostingEnvironmentState>(
                    this.mockExperimentContext.Experiment.Id,
                    $"state-{this.mockExperimentContext.ExperimentStep.Id}",
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .ReturnsAsync(this.providerState)
                .Verifiable();

            // Return provisioned tip sessions
            this.mockDataClient
                .Setup(c => c.GetOrCreateStateAsync<IEnumerable<EnvironmentEntity>>(
                    this.mockExperimentContext.Experiment.Id,
                    ContractExtension.EntitiesProvisioned,
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .ReturnsAsync(DeployHostingEnvironmentProviderTests.TipSessions(TipSessionStatus.Created))
                .Verifiable();

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
            this.mockDataClient
                .Setup(c => c.SaveStateAsync<DeployHostingEnvironmentState>(
                    this.mockExperimentContext.Experiment.Id,
                    $"state-{this.mockExperimentContext.ExperimentStep.Id}",
                    It.IsAny<DeployHostingEnvironmentState>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .Callback<string, string, DeployHostingEnvironmentState, CancellationToken, string>((experimentId, stepId, state, token, stateId) =>
                {
                    Assert.IsTrue(state.InstallationRequested);
                    Assert.IsTrue(state.InstallationConfirmed);
                })
                .Returns(Task.CompletedTask)
                .Verifiable();

            ExecutionResult result = await this.provider.ExecuteAsync(this.mockExperimentContext, this.mockExperimentComponent, CancellationToken.None).ConfigureAwait(false);

            // Provider should return succeed when heartbeast were heard.
            Assert.AreEqual(ExecutionStatus.Succeeded, result.Status);

            this.mockTipClient.Verify();
            this.mockDataClient.Verify();
        }

        [Test]
        public async Task DeployHostingEnvironmentProviderTimesOutIfConfirmationsAreNotReceivedWithinTimeoutPeriod()
        {
            this.providerState = new DeployHostingEnvironmentState()
            {
                InstallationRequested = true,
                InstallationConfirmed = false,
                InstallationRequestTime = DateTime.UtcNow - TimeSpan.FromMinutes(30)
            };

            // Return state showing finished for the provider.
            this.mockDataClient
                .Setup(c => c.GetOrCreateStateAsync<DeployHostingEnvironmentState>(
                    this.mockExperimentContext.Experiment.Id,
                    $"state-{this.mockExperimentContext.ExperimentStep.Id}", 
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .ReturnsAsync(this.providerState)
                .Verifiable();

            // Return provisioned tip sessions
            this.mockDataClient
                .Setup(c => c.GetOrCreateStateAsync<IEnumerable<EnvironmentEntity>>(
                    this.mockExperimentContext.Experiment.Id, 
                    ContractExtension.EntitiesProvisioned, 
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .ReturnsAsync(DeployHostingEnvironmentProviderTests.TipSessions(TipSessionStatus.Created))
                .Verifiable();

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

            ExecutionResult result = await this.provider.ExecuteAsync(this.mockExperimentContext, this.mockExperimentComponent, CancellationToken.None).ConfigureAwait(false);

            // Provider should fail when timed out.
            Assert.AreEqual(ExecutionStatus.Failed, result.Status);
            Assert.IsTrue(result.Error.Message.Contains("Timing out the experiment."));

            this.mockTipClient.Verify();
            this.mockDataClient.Verify();
        }

        [Test]
        public void DeployHostingEnvironmentProviderRequestsAutoTriageDiagnosticsOnFailedExperimentsWithDiagnosticsEnabled()
        {
            // Enable Diagnostics flag is on
            this.mockExperimentComponent.Parameters[StepParameters.EnableDiagnostics] = true;
            // Execution will fail with invalid resource group deployment
            this.ValidateHostingEnvironmentDeployment(TipNodeSessionChangeResult.Failed, ExecutionStatus.Failed);

            // call to request autotriage diagnostics are made
            this.mockDataClient.Verify(method =>
                method.UpdateStateItemsAsync(
                    this.mockExperimentContext.ExperimentId,
                    ContractExtension.DiagnosticsRequests,
                    this.mockDiagnosticRequest,
                    CancellationToken.None,
                    null),
                Times.AtMostOnce);
        }

        [Test]
        public void DeployHostingEnvironmentProviderDoesNotRequestAutoTriageDiagnosticsOnFailedExperimentsWithoutDiagnosticsEnabled()
        {
            // Enable Diagnostics flag is not enabled
            this.mockExperimentComponent.Parameters[StepParameters.EnableDiagnostics] = false;
            // Execution will fail with invalid resource group deployment
            this.ValidateHostingEnvironmentDeployment(TipNodeSessionChangeResult.Failed, ExecutionStatus.Failed);

            // call to request autotriage diagnostics are not made
            this.mockDataClient.Verify(method =>
                method.UpdateStateItemsAsync(
                    this.mockExperimentContext.ExperimentId,
                    ContractExtension.DiagnosticsRequests,
                    this.mockDiagnosticRequest,
                    CancellationToken.None,
                    null),
                Times.Never);
        }

        [Test]
        [TestCase(TipNodeSessionChangeResult.Unknown, ExecutionStatus.InProgress)]
        [TestCase(TipNodeSessionChangeResult.Succeeded, ExecutionStatus.Succeeded)]
        public void DeployHostingEnvironmentProviderWithDiagnosticsEnabledDoesNotRequestDiagnosticsFromUnfailedExperiments(TipNodeSessionChangeResult state, ExecutionStatus status)
        {
            // Enable Diagnostics flag is enabled
            this.mockExperimentComponent.Parameters[StepParameters.EnableDiagnostics] = true;
            // Testing other resource group deployment execution statuses
            this.ValidateHostingEnvironmentDeployment(state, status);

            // call to request autotriage diagnostics are not made
            this.mockDataClient.Verify(method =>
                method.UpdateStateItemsAsync(
                    this.mockExperimentContext.ExperimentId,
                    ContractExtension.DiagnosticsRequests,
                    this.mockDiagnosticRequest,
                    CancellationToken.None,
                    null),
                Times.Never);
        }

        [Test]
        public void DeployHostingEnvironmentProviderReturnCorrectStatusonResourceDeployment()
        {
            this.ValidateHostingEnvironmentDeployment(TipNodeSessionChangeResult.Unknown, ExecutionStatus.InProgress);
            this.ValidateHostingEnvironmentDeployment(TipNodeSessionChangeResult.Failed, ExecutionStatus.Failed);
            this.ValidateHostingEnvironmentDeployment(TipNodeSessionChangeResult.Succeeded, ExecutionStatus.Succeeded);
        }

        private static IEnumerable<EnvironmentEntity> TipSessions(TipSessionStatus status)
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

        private void ValidateHostingEnvironmentDeployment(TipNodeSessionChangeResult tipSessionState, ExecutionStatus expectedStatus)
        {
            this.providerState = new DeployHostingEnvironmentState()
            {
                InstallationRequested = true,
                InstallationConfirmed = false,
                InstallationRequestTime = DateTime.UtcNow - TimeSpan.FromMinutes(10)
            };

            // Return state showing finished for the provider.
            this.mockDataClient
                .Setup(c => c.GetOrCreateStateAsync<DeployHostingEnvironmentState>(
                    this.mockExperimentContext.ExperimentId,
                    $"state-{this.mockExperimentContext.ExperimentStep.Id}",
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .ReturnsAsync(this.providerState);

            // Return provisioned tip sessions
            this.mockDataClient
                .Setup(c => c.GetOrCreateStateAsync<IEnumerable<EnvironmentEntity>>(
                    this.mockExperimentContext.ExperimentId,
                    ContractExtension.EntitiesProvisioned,
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .ReturnsAsync(DeployHostingEnvironmentProviderTests.TipSessions(TipSessionStatus.Created));

            // Tip client return success.
            this.mockTipClient.Setup(c => c.GetTipSessionChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string tipSessionId, string changeId, CancellationToken token) => new TipNodeSessionChangeDetails()
                {
                    TipNodeSessionId = tipSessionId,
                    TipNodeSessionChangeId = $"Change-tipSessionId",
                    Result = tipSessionState,
                    Status = TipNodeSessionChangeStatus.Finished
                });

            if (expectedStatus == ExecutionStatus.Succeeded)
            {
                this.providerState.InstallationConfirmed = true;
                // Provider should save state
                this.mockDataClient
                    .Setup(c => c.SaveStateAsync<DeployHostingEnvironmentState>(
                        this.mockExperimentContext.ExperimentId,
                        $"state-{this.mockExperimentContext.ExperimentStep.Id}",
                        It.IsAny<DeployHostingEnvironmentState>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<string>()))
                    .Returns(Task.CompletedTask);
            }
            else
            {
                // Provider should save state
                this.mockDataClient
                    .Setup(c => c.SaveStateAsync<DeployHostingEnvironmentState>(
                        this.mockExperimentContext.ExperimentId,
                        $"state-{this.mockExperimentContext.ExperimentStep.Id}",
                        It.IsAny<DeployHostingEnvironmentState>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<string>()))
                    .Returns(Task.CompletedTask);
            }

            ExecutionResult result = this.provider.ExecuteAsync(this.mockExperimentContext, this.mockExperimentComponent, CancellationToken.None).Result;

            // Provider should return succeed when heartbeast were heard.
            Assert.AreEqual(expectedStatus, result.Status);
        }
    }
}
