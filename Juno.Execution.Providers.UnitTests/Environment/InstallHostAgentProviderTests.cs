namespace Juno.Execution.Providers.Environment
{
    using System;
    using System.Collections.Generic;
    using System.IO;
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
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Identity;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.IdentityModel.Clients.ActiveDirectory;
    using Microsoft.OData.Edm;
    using Moq;
    using NUnit.Framework;
    using TipGateway.Entities;
    using static Juno.Execution.Providers.Environment.InstallHostAgentProvider;

    [TestFixture]
    [Category("Unit")]
    public class InstallHostAgentProviderTests
    {
        // The set of error messages/parts for which the provider is expected to retry
        // agent installation. These are all errors we've seen as part of running Juno experiments
        // that caused experiment failures.
        private static IEnumerable<string> expectedRetryableErrors = new List<string>
        {
            @"System.Exception: Building Job for service: JunoHostAgent and build location: " +
            @"\\rd\Builds\branches\any\release-x64\Deployment\Prod\App\JunoHostAgent failed with error message: " +
            @"remoteJob failed for service: JunoHostAgent and label: TipNode_061091de-b875-4c0d-a877-139b5f9e5a93 with error message: proxy 1.2.3.4 reported error: DP SJC201021202055: " +
            @"EDP010196: Error 121: The semaphore timeout period has expired.   [by SJC201021202055]",

            @"System.Exception: Building Job for service: JunoHostAgent and build location: \\rd\Builds\branches\any\release-x64\Deployment\Prod\App\JunoHostAgent " +
            @"failed with error message: remoteJob failed for service: JunoHostAgent and label: TipNode_217b16b8-b180-4a87-b6f1-71e09b1b6e87 with error message: Failed to upload chunk to " +
            @"dynamic storage: offs 42190193, size 10607/10607",

            @"System.Exception: Building Job for service: JunoHostAgent and build location: \\rd\Builds\branches\any\release-x64\Deployment\Prod\App\JunoHostAgent " +
            @"failed with error message: remoteJob failed for service: JunoHostAgent and label: TipNode_f7f5315d-1a34-4ade-ae48-f4f2da3d7262 with error message: proxy 25.66.144.205 reported error: DP CH1PHY104010401: " +
            @"EDP010358: CreateFile('\\?\UNC\reddog\Builds\branches\any\release-x64\Deployment\Prod\App\JunoHostAgent\Microsoft.Extensions.Localization.Abstractions.dll') " +
            @"failed: Error 64: The specified network name is no longer available.   [by CH1PHY104010401]"
        };

        private ProviderFixture mockFixture;
        private Mock<ITipClient> mockTipClient;
        private Mock<IAuthenticationProvider<AuthenticationResult>> mockAuthProvider;
        private InstallHostAgentProvider provider;
        private InstallHostAgentProvider.State providerState;
        private List<DiagnosticsRequest> mockDiagnosticRequest;

        [SetUp]
        public void SetupTest()
        {
            // This unit test mimics installing host agent on a group with two tip sessions.:
            // 1. Provider first get provissioned tipsessions and install pilotfish service and update state, return in progress.
            // 2. Provider checks host agent heart beat, if not found will return in progress.
            // 3. Provider third time check if progress is created and return success.
            // 4. If checking heart beat exceeds maximum wait time, providor will error and timeout.

            this.mockFixture = new ProviderFixture(typeof(InstallHostAgentProvider));
            this.mockFixture.SetupExperimentMocks();
            this.mockFixture.SetupIdentityMocks(DateTime.UtcNow);
            this.mockTipClient = new Mock<ITipClient>();
            this.mockAuthProvider = new Mock<IAuthenticationProvider<AuthenticationResult>>();
            this.mockAuthProvider.Setup(p => p.AuthenticateAsync()).ReturnsAsync(this.mockFixture.Create<AuthenticationResult>());

            this.mockFixture.Component.Parameters.Add("PilotFishServicePath", @"\\any\official\build\share\path");
            this.mockFixture.Component.Parameters.Add("Timeout", "00:00:20:00");

            this.mockFixture.Services.AddSingleton(NullLogger.Instance);
            this.mockFixture.Services.AddSingleton(this.mockTipClient.Object);
            this.mockFixture.Services.AddSingleton(this.mockFixture.DataClient.Object);
            this.mockFixture.Services.AddSingleton<IAuthenticationProvider<AuthenticationResult>>(this.mockAuthProvider.Object);

            this.provider = new InstallHostAgentProvider(this.mockFixture.Services);
            this.providerState = new InstallHostAgentProvider.State();

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
                            { DiagnosticsParameter.TipNodeId, "node1" },
                            { DiagnosticsParameter.TipSessionId, "tipSession1" },
                            { DiagnosticsParameter.ExperimentId, this.mockFixture.ExperimentId },
                            { DiagnosticsParameter.ProviderName, nameof(InstallHostAgentProvider) }
                        })
            };

            this.InitializeMockDefaultBehaviors();
        }

        ////    [Test]
        ////    public void ProviderValidatesRequiredParameters()
        ////    {
        ////        var provider = new InstallHostAgentProvider(this.providerServices);
        ////        Assert.ThrowsAsync<ArgumentException>(() => provider.ExecuteAsync(null, this.mockExperimentComponent, CancellationToken.None));
        ////        Assert.ThrowsAsync<ArgumentException>(() => provider.ExecuteAsync(this.mockExperimentContext, null, CancellationToken.None));
        ////    }

        ////    [Test]
        ////    public async Task InstallHostAgentProviderInjectJwtTokenOnAllTipSessionsWhenStarting()
        ////    {
        ////        var installHostAgentProvider = new InstallHostAgentProvider(this.providerServices);
        ////        State providerState = null;

        ////        // First return null state for the provider.
        ////        this.mockDataClient
        ////            .Setup(c => c.GetOrCreateStateAsync<State>(
        ////                this.mockExperimentContext.Experiment.Id, 
        ////                $"state-{this.mockExperimentContext.ExperimentStep.Id}",
        ////                It.IsAny<CancellationToken>(),
        ////                It.IsAny<string>()))
        ////            .ReturnsAsync(providerState)
        ////            .Verifiable();

        ////        // Return provisioned tip sessions
        ////        this.mockDataClient
        ////            .Setup(c => c.GetOrCreateStateAsync<IEnumerable<EnvironmentEntity>>(
        ////                this.mockExperimentContext.Experiment.Id,
        ////                ContractExtension.EntitiesProvisioned,
        ////                It.IsAny<CancellationToken>(),
        ////                It.IsAny<string>()))
        ////            .ReturnsAsync(this.TipSessions(TipSessionStatus.Created))
        ////            .Verifiable();

        ////        // Tip client should inject jwt.
        ////        this.mockTipClient.Setup(c => c.ExecuteNodeCommandAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
        ////            .ReturnsAsync((string tipSessionId, string node, string command, TimeSpan timeout, CancellationToken token) => new TipNodeSessionChange()
        ////            {
        ////                TipNodeSessionId = tipSessionId,
        ////                TipNodeSessionChangeId = $"Change-tipSessionId",
        ////                Status = TipNodeSessionChangeStatus.Queued
        ////            })
        ////            .Verifiable();

        ////        // provider should save Tip with updated change id.
        ////        this.mockDataClient.Setup(c => c.UpdateStateItemsAsync<EnvironmentEntity>(
        ////                this.mockExperimentContext.Experiment.Id,
        ////                ContractExtension.EntitiesProvisioned,
        ////                It.IsAny<IEnumerable<EnvironmentEntity>>(),
        ////                It.IsAny<CancellationToken>(),
        ////                It.IsAny<string>()))
        ////            .Returns(Task.CompletedTask)
        ////            .Verifiable();

        ////        // Provider should save state
        ////        this.mockDataClient
        ////            .Setup(c => c.SaveStateAsync<State>(
        ////                this.mockExperimentContext.Experiment.Id,
        ////                $"state-{this.mockExperimentContext.ExperimentStep.Id}",
        ////                It.IsAny<State>(),
        ////                It.IsAny<CancellationToken>(),
        ////                It.IsAny<string>()))
        ////            .Callback<string, string, State, CancellationToken, string>((experimentId, stepId, state, token, stateId) =>
        ////            {
        ////                Assert.IsTrue(state.JwtDropRequested);
        ////                Assert.IsFalse(state.JwtDropConfirmed);
        ////                Assert.IsFalse(state.JwtDropRequestTime == null);
        ////            })
        ////            .Returns(Task.CompletedTask)
        ////            .Verifiable();

        ////        ExecutionResult result = await installHostAgentProvider.ExecuteAsync(this.mockExperimentContext, this.mockExperimentComponent, CancellationToken.None).ConfigureAwait(false);

        ////        // Provider should return inprogress when starting new.
        ////        Assert.AreEqual(ExecutionStatus.InProgress, result.Status);

        ////        this.mockTipClient.Verify(c => c.ExecuteNodeCommandAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        ////        this.mockTipClient.Verify();
        ////        this.mockDataClient.Verify();
        ////    }

        ////    [Test]
        ////    public async Task InstallHostAgentProviderCheckJwtInjectionWillReturnInprogressIfNotFinished()
        ////    {
        ////        var installHostAgentProvider = new InstallHostAgentProvider(this.providerServices);
        ////        State providerState = new State()
        ////        {
        ////            JwtDropRequested = true,
        ////            JwtDropConfirmed = false,
        ////            JwtDropRequestTime = DateTime.UtcNow - TimeSpan.FromMinutes(15),
        ////            AgentInstallationRequested = false,
        ////            AgentHeartbeatConfirmed = false,
        ////            AgentInstallationRequestTime = DateTime.UtcNow - TimeSpan.FromMinutes(5)
        ////        };

        ////        // Return state showing finished for the provider.
        ////        this.mockDataClient
        ////            .Setup(c => c.GetOrCreateStateAsync<State>(
        ////                this.mockExperimentContext.Experiment.Id,
        ////                $"state-{this.mockExperimentContext.ExperimentStep.Id}",
        ////                It.IsAny<CancellationToken>(),
        ////                It.IsAny<string>()))
        ////            .ReturnsAsync(providerState)
        ////            .Verifiable();

        ////        // Return provisioned tip sessions
        ////        this.mockDataClient
        ////            .Setup(c => c.GetOrCreateStateAsync<IEnumerable<EnvironmentEntity>>(
        ////                this.mockExperimentContext.Experiment.Id,
        ////                ContractExtension.EntitiesProvisioned,
        ////                It.IsAny<CancellationToken>(),
        ////                It.IsAny<string>()))
        ////            .ReturnsAsync(this.TipSessions(TipSessionStatus.Created))
        ////            .Verifiable();

        ////        // Tip client return in progress.
        ////        this.mockTipClient.Setup(c => c.GetTipSessionChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
        ////            .ReturnsAsync((string tipSessionId, string changeId, CancellationToken token) => new TipNodeSessionChangeDetails()
        ////            {
        ////                TipNodeSessionId = tipSessionId,
        ////                TipNodeSessionChangeId = $"Change-tipSessionId",
        ////                Result = TipNodeSessionChangeResult.Unknown,
        ////                Status = TipNodeSessionChangeStatus.Queued
        ////            })
        ////            .Verifiable();

        ////        // Provider should save state
        ////        this.mockDataClient
        ////            .Setup(c => c.SaveStateAsync<State>(
        ////                this.mockExperimentContext.Experiment.Id,
        ////                $"state-{this.mockExperimentContext.ExperimentStep.Id}",
        ////                It.IsAny<State>(),
        ////                It.IsAny<CancellationToken>(),
        ////                It.IsAny<string>()))
        ////            .Callback<string, string, State, CancellationToken, string>((experimentId, stepId, state, token, stateId) =>
        ////            {
        ////                Assert.IsTrue(state.JwtDropRequested);
        ////                Assert.IsFalse(state.JwtDropConfirmed);
        ////            })
        ////            .Returns(Task.CompletedTask)
        ////            .Verifiable();

        ////        ExecutionResult result = await installHostAgentProvider.ExecuteAsync(this.mockExperimentContext, this.mockExperimentComponent, CancellationToken.None).ConfigureAwait(false);

        ////        // Provider should return inprogress when heart beat were not received.
        ////        Assert.AreEqual(ExecutionStatus.InProgress, result.Status);

        ////        this.mockTipClient.Verify();
        ////        this.mockDataClient.Verify();
        ////    }

        ////    [Test]
        ////    public async Task InstallHostAgentProviderCheckJwtInjectionWillChangeStateIfInjected()
        ////    {
        ////        var installHostAgentProvider = new InstallHostAgentProvider(this.providerServices);
        ////        State providerState = new State()
        ////        {
        ////            JwtDropRequested = true,
        ////            JwtDropConfirmed = false,
        ////            JwtDropRequestTime = DateTime.UtcNow - TimeSpan.FromMinutes(15),
        ////            AgentInstallationRequested = false,
        ////            AgentHeartbeatConfirmed = false,
        ////            AgentInstallationRequestTime = DateTime.UtcNow - TimeSpan.FromMinutes(5)
        ////        };

        ////        // Return state showing finished for the provider.
        ////        this.mockDataClient
        ////            .Setup(c => c.GetOrCreateStateAsync<State>(
        ////                this.mockExperimentContext.Experiment.Id,
        ////                $"state-{this.mockExperimentContext.ExperimentStep.Id}",
        ////                It.IsAny<CancellationToken>(),
        ////                It.IsAny<string>()))
        ////            .ReturnsAsync(providerState)
        ////            .Verifiable();

        ////        // Return provisioned tip sessions
        ////        this.mockDataClient
        ////            .Setup(c => c.GetOrCreateStateAsync<IEnumerable<EnvironmentEntity>>(
        ////                this.mockExperimentContext.Experiment.Id,
        ////                ContractExtension.EntitiesProvisioned,
        ////                It.IsAny<CancellationToken>(),
        ////                It.IsAny<string>()))
        ////            .ReturnsAsync(this.TipSessions(TipSessionStatus.Created))
        ////            .Verifiable();

        ////        // Tip client return in progress.
        ////        this.mockTipClient.Setup(c => c.GetTipSessionChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
        ////            .ReturnsAsync((string tipSessionId, string changeId, CancellationToken token) => new TipNodeSessionChangeDetails()
        ////            {
        ////                TipNodeSessionId = tipSessionId,
        ////                TipNodeSessionChangeId = $"Change-tipSessionId",
        ////                Result = TipNodeSessionChangeResult.Succeeded,
        ////                Status = TipNodeSessionChangeStatus.Finished
        ////            })
        ////            .Verifiable();

        ////        // Provider should save state
        ////        this.mockDataClient
        ////            .Setup(c => c.SaveStateAsync<State>(
        ////                this.mockExperimentContext.Experiment.Id,
        ////                $"state-{this.mockExperimentContext.ExperimentStep.Id}",
        ////                It.IsAny<State>(),
        ////                It.IsAny<CancellationToken>(),
        ////                It.IsAny<string>()))
        ////            .Callback<string, string, State, CancellationToken, string>((experimentId, stepId, state, token, stateId) =>
        ////            {
        ////                Assert.IsTrue(state.JwtDropRequested);
        ////                Assert.IsTrue(state.JwtDropConfirmed);
        ////            })
        ////            .Returns(Task.CompletedTask)
        ////            .Verifiable();

        ////        ExecutionResult result = await installHostAgentProvider.ExecuteAsync(this.mockExperimentContext, this.mockExperimentComponent, CancellationToken.None).ConfigureAwait(false);

        ////        // Provider should return inprogress when heart beat were not received.
        ////        Assert.AreEqual(ExecutionStatus.InProgress, result.Status);

        ////        this.mockTipClient.Verify();
        ////        this.mockDataClient.Verify();
        ////    }

        ////    [Test]
        ////    public async Task InstallHostAgentProviderBootStrapHostAgentsOnAllTipSessionsAfterToken()
        ////    {
        ////        var installHostAgentProvider = new InstallHostAgentProvider(this.providerServices);
        ////        State providerState = new State()
        ////        {
        ////            JwtDropRequested = true,
        ////            JwtDropConfirmed = true,
        ////            JwtDropRequestTime = DateTime.UtcNow - TimeSpan.FromMinutes(15),
        ////            AgentInstallationRequested = false,
        ////            AgentHeartbeatConfirmed = false,
        ////            AgentInstallationRequestTime = DateTime.UtcNow - TimeSpan.FromMinutes(5)
        ////        };

        ////        // First return null state for the provider.
        ////        this.mockDataClient
        ////            .Setup(c => c.GetOrCreateStateAsync<State>(
        ////                this.mockExperimentContext.Experiment.Id,
        ////                $"state-{this.mockExperimentContext.ExperimentStep.Id}",
        ////                It.IsAny<CancellationToken>(),
        ////                It.IsAny<string>()))
        ////            .ReturnsAsync(providerState)
        ////            .Verifiable();

        ////        // Return provisioned tip sessions
        ////        this.mockDataClient
        ////            .Setup(c => c.GetOrCreateStateAsync<IEnumerable<EnvironmentEntity>>(
        ////                this.mockExperimentContext.Experiment.Id,
        ////                ContractExtension.EntitiesProvisioned,
        ////                It.IsAny<CancellationToken>(),
        ////                It.IsAny<string>()))
        ////            .ReturnsAsync(this.TipSessions(TipSessionStatus.Created))
        ////            .Verifiable();

        ////        // Tip client should apply pilotfish.
        ////        this.mockTipClient.Setup(c => c.ApplyPilotFishServicesAsync(It.IsAny<string>(), It.IsAny<List<KeyValuePair<string, string>>>(), It.IsAny<CancellationToken>()))
        ////            .ReturnsAsync((string tipSessionId, List<KeyValuePair<string, string>> pilotfish, CancellationToken token) => new TipNodeSessionChange()
        ////            {
        ////                TipNodeSessionId = tipSessionId,
        ////                TipNodeSessionChangeId = $"Change-tipSessionId",
        ////                Status = TipNodeSessionChangeStatus.Queued
        ////            })
        ////            .Verifiable();

        ////        // provider should save Tip with updated change id.
        ////        this.mockDataClient.Setup(c => c.UpdateStateItemsAsync<EnvironmentEntity>(
        ////                this.mockExperimentContext.Experiment.Id,
        ////                ContractExtension.EntitiesProvisioned,
        ////                It.IsAny<IEnumerable<EnvironmentEntity>>(),
        ////                It.IsAny<CancellationToken>(),
        ////                It.IsAny<string>()))
        ////            .Returns(Task.CompletedTask)
        ////            .Verifiable();

        ////        // Provider should save state
        ////        this.mockDataClient.Setup(c => c.SaveStateAsync<State>(
        ////                this.mockExperimentContext.Experiment.Id,
        ////                $"state-{this.mockExperimentContext.ExperimentStep.Id}",
        ////                It.IsAny<State>(),
        ////                It.IsAny<CancellationToken>(),
        ////                It.IsAny<string>()))
        ////            .Callback<string, string, State, CancellationToken, string>((experimentId, stepId, state, token, stateId) =>
        ////            {
        ////                Assert.IsTrue(state.AgentInstallationRequested);
        ////                Assert.IsFalse(state.AgentHeartbeatConfirmed);
        ////                Assert.IsFalse(state.AgentInstallationRequestTime == null);
        ////            })
        ////            .Returns(Task.CompletedTask)
        ////            .Verifiable();

        ////        ExecutionResult result = await installHostAgentProvider.ExecuteAsync(this.mockExperimentContext, this.mockExperimentComponent, CancellationToken.None).ConfigureAwait(false);

        ////        // Provider should return inprogress.
        ////        Assert.AreEqual(ExecutionStatus.InProgress, result.Status);

        ////        this.mockTipClient.Verify(c => c.ApplyPilotFishServicesAsync(It.IsAny<string>(), It.IsAny<List<KeyValuePair<string, string>>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        ////        this.mockTipClient.Verify();
        ////        this.mockDataClient.Verify();
        ////    }

        ////    [Test]
        ////    public async Task InstallHostAgentProviderChecksTipChangeStatusAfterBootStrapping()
        ////    {
        ////        var installHostAgentProvider = new InstallHostAgentProvider(this.providerServices);
        ////        State providerState = new State()
        ////        {
        ////            JwtDropRequested = true,
        ////            JwtDropConfirmed = true,
        ////            JwtDropRequestTime = DateTime.UtcNow - TimeSpan.FromMinutes(15),
        ////            AgentInstallationRequested = true,
        ////            AgentInstallationConfirmed = false,
        ////            AgentHeartbeatConfirmed = false,
        ////            AgentInstallationRequestTime = DateTime.UtcNow - TimeSpan.FromMinutes(5)
        ////        };

        ////        // First return null state for the provider.
        ////        this.mockDataClient.Setup(c => c.GetOrCreateStateAsync<State>(
        ////                this.mockExperimentContext.Experiment.Id,
        ////                $"state-{this.mockExperimentContext.ExperimentStep.Id}",
        ////                It.IsAny<CancellationToken>(),
        ////                It.IsAny<string>()))
        ////            .ReturnsAsync(providerState)
        ////            .Verifiable();

        ////        // Return provisioned tip sessions
        ////        this.mockDataClient.Setup(c => c.GetOrCreateStateAsync<IEnumerable<EnvironmentEntity>>(
        ////                this.mockExperimentContext.Experiment.Id,
        ////                ContractExtension.EntitiesProvisioned,
        ////                It.IsAny<CancellationToken>(),
        ////                It.IsAny<string>()))
        ////            .ReturnsAsync(this.TipSessions(TipSessionStatus.Created))
        ////            .Verifiable();

        ////        // Tip client return in progress.
        ////        this.mockTipClient.Setup(c => c.GetTipSessionChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
        ////            .ReturnsAsync((string tipSessionId, string changeId, CancellationToken token) => new TipNodeSessionChangeDetails()
        ////            {
        ////                TipNodeSessionId = tipSessionId,
        ////                TipNodeSessionChangeId = $"Change-tipSessionId",
        ////                Result = TipNodeSessionChangeResult.Succeeded,
        ////                Status = TipNodeSessionChangeStatus.Finished
        ////            })
        ////            .Verifiable();

        ////        // Provider should save state
        ////        this.mockDataClient.Setup(c => c.SaveStateAsync<State>(
        ////                this.mockExperimentContext.Experiment.Id,
        ////                $"state-{this.mockExperimentContext.ExperimentStep.Id}",
        ////                It.IsAny<State>(),
        ////                It.IsAny<CancellationToken>(),
        ////                It.IsAny<string>()))
        ////            .Callback<string, string, State, CancellationToken, string>((experimentId, stepId, state, token, stateId) =>
        ////            {
        ////                Assert.IsTrue(state.AgentInstallationRequested);
        ////                Assert.IsFalse(state.AgentHeartbeatConfirmed);
        ////                Assert.IsFalse(state.AgentInstallationRequestTime == null);
        ////            })
        ////            .Returns(Task.CompletedTask)
        ////            .Verifiable();

        ////        ExecutionResult result = await installHostAgentProvider.ExecuteAsync(this.mockExperimentContext, this.mockExperimentComponent, CancellationToken.None).ConfigureAwait(false);

        ////        // Provider should return inprogress.
        ////        Assert.AreEqual(ExecutionStatus.InProgress, result.Status);

        ////        this.mockTipClient.Verify(c => c.GetTipSessionChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        ////        this.mockTipClient.Verify();
        ////        this.mockDataClient.Verify();
        ////    }

        ////    [Test]
        ////    public async Task InstallHostAgentProviderListenToHeartBeatAndWillReturnInprogressIfNoHeartbeat()
        ////    {
        ////        var installHostAgentProvider = new InstallHostAgentProvider(this.providerServices);
        ////        State providerState = new State()
        ////        {
        ////            JwtDropRequested = true,
        ////            JwtDropConfirmed = true,
        ////            JwtDropRequestTime = DateTime.UtcNow - TimeSpan.FromMinutes(15),
        ////            AgentInstallationRequested = true,
        ////            AgentInstallationConfirmed = true,
        ////            AgentHeartbeatConfirmed = false,
        ////            AgentInstallationRequestTime = DateTime.UtcNow - TimeSpan.FromMinutes(5)
        ////        };

        ////        // Return state showing finished for the provider.
        ////        this.mockDataClient.Setup(c => c.GetOrCreateStateAsync<State>(
        ////                this.mockExperimentContext.Experiment.Id,
        ////                $"state-{this.mockExperimentContext.ExperimentStep.Id}",
        ////                It.IsAny<CancellationToken>(),
        ////                It.IsAny<string>()))
        ////            .ReturnsAsync(providerState)
        ////            .Verifiable();

        ////        // Return provisioned tip sessions
        ////        this.mockDataClient.Setup(c => c.GetOrCreateStateAsync<IEnumerable<EnvironmentEntity>>(
        ////                this.mockExperimentContext.Experiment.Id,
        ////                ContractExtension.EntitiesProvisioned,
        ////                It.IsAny<CancellationToken>(),
        ////                It.IsAny<string>()))
        ////            .ReturnsAsync(this.TipSessions(TipSessionStatus.Created))
        ////            .Verifiable();

        ////        // Return null heartbeat
        ////        this.mockDataClient.Setup(c => c.GetAgentHeartbeatAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
        ////           .Throws(new Exception("Data not found"))
        ////           .Verifiable();

        ////        // Provider should save state
        ////        this.mockDataClient.Setup(c => c.SaveStateAsync<State>(
        ////                this.mockExperimentContext.Experiment.Id,
        ////                $"state-{this.mockExperimentContext.ExperimentStep.Id}",
        ////                It.IsAny<State>(),
        ////                It.IsAny<CancellationToken>(),
        ////                It.IsAny<string>()))
        ////            .Callback<string, string, State, CancellationToken, string>((experimentId, stepId, state, token, stateId) =>
        ////            {
        ////                Assert.IsTrue(state.AgentInstallationRequested);
        ////                Assert.IsFalse(state.AgentHeartbeatConfirmed);
        ////            })
        ////            .Returns(Task.CompletedTask)
        ////            .Verifiable();

        ////        ExecutionResult result = await installHostAgentProvider.ExecuteAsync(this.mockExperimentContext, this.mockExperimentComponent, CancellationToken.None).ConfigureAwait(false);

        ////        // Provider should return inprogress when heart beat were not received.
        ////        Assert.AreEqual(ExecutionStatus.InProgress, result.Status);

        ////        this.mockTipClient.Verify();
        ////        this.mockDataClient.Verify();
        ////    }

        ////    [Test]
        ////    public async Task InstallHostAgentProviderListenToHeartbeatAndReturnSucceedWhenHeartbeatsAreSent()
        ////    {
        ////        var installHostAgentProvider = new InstallHostAgentProvider(this.providerServices);
        ////        State providerState = new State()
        ////        {
        ////            JwtDropRequested = true,
        ////            JwtDropConfirmed = true,
        ////            JwtDropRequestTime = DateTime.UtcNow - TimeSpan.FromMinutes(15),
        ////            AgentInstallationRequested = true,
        ////            AgentInstallationConfirmed = true,
        ////            AgentHeartbeatConfirmed = false,
        ////            AgentInstallationRequestTime = DateTime.UtcNow - TimeSpan.FromMinutes(10)
        ////        };

        ////        // Return state showing finished for the provider.
        ////        this.mockDataClient.Setup(c => c.GetOrCreateStateAsync<State>(
        ////                this.mockExperimentContext.Experiment.Id,
        ////                $"state-{this.mockExperimentContext.ExperimentStep.Id}",
        ////                It.IsAny<CancellationToken>(),
        ////                It.IsAny<string>()))
        ////            .ReturnsAsync(providerState)
        ////            .Verifiable();

        ////        // Return provisioned tip sessions
        ////        this.mockDataClient.Setup(c => c.GetOrCreateStateAsync<IEnumerable<EnvironmentEntity>>(
        ////                this.mockExperimentContext.Experiment.Id,
        ////                ContractExtension.EntitiesProvisioned,
        ////                It.IsAny<CancellationToken>(),
        ////                It.IsAny<string>()))
        ////            .ReturnsAsync(this.TipSessions(TipSessionStatus.Created))
        ////            .Verifiable();

        ////        // Return valid heartbeat
        ////        AgentHeartbeatInstance heartbeat = new AgentHeartbeatInstance("id", "agent", AgentHeartbeatStatus.Running, AgentType.GuestAgent);
        ////        this.mockDataClient.Setup(c => c.GetAgentHeartbeatAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
        ////            .ReturnsAsync(heartbeat)
        ////            .Verifiable();

        ////        // Provider should save state
        ////        this.mockDataClient.Setup(c => c.SaveStateAsync<State>(
        ////                this.mockExperimentContext.Experiment.Id,
        ////                $"state-{this.mockExperimentContext.ExperimentStep.Id}",
        ////                It.IsAny<State>(),
        ////                It.IsAny<CancellationToken>(),
        ////                It.IsAny<string>()))
        ////            .Callback<string, string, State, CancellationToken, string>((experimentId, stepId, state, token, stateId) =>
        ////            {
        ////                Assert.IsTrue(state.AgentInstallationRequested);
        ////                Assert.IsTrue(state.AgentHeartbeatConfirmed);
        ////            })
        ////            .Returns(Task.CompletedTask)
        ////            .Verifiable();

        ////        ExecutionResult result = await installHostAgentProvider.ExecuteAsync(this.mockExperimentContext, this.mockExperimentComponent, CancellationToken.None).ConfigureAwait(false);

        ////        // Provider should return succeed when heartbeast were heard.
        ////        Assert.AreEqual(ExecutionStatus.Succeeded, result.Status);

        ////        this.mockDataClient.Verify(c => c.GetAgentHeartbeatAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        ////        this.mockTipClient.Verify();
        ////        this.mockDataClient.Verify();
        ////    }

        ////    [Test]
        ////    public async Task InstallHostAgentProviderTimesOutIfHeartbeatsAreNotReceivedWithinTimeoutPeriod()
        ////    {
        ////        var installHostAgentProvider = new InstallHostAgentProvider(this.providerServices);
        ////        State providerState = new State()
        ////        {
        ////            JwtDropRequested = true,
        ////            JwtDropConfirmed = true,
        ////            JwtDropRequestTime = DateTime.UtcNow - TimeSpan.FromMinutes(15),
        ////            AgentInstallationRequested = true,
        ////            AgentInstallationConfirmed = true,
        ////            AgentHeartbeatConfirmed = false,
        ////            AgentInstallationRequestTime = DateTime.UtcNow - TimeSpan.FromMinutes(40)
        ////        };

        ////        // Return state showing finished for the provider.
        ////        this.mockDataClient.Setup(c => c.GetOrCreateStateAsync<State>(
        ////                this.mockExperimentContext.Experiment.Id,
        ////                $"state-{this.mockExperimentContext.ExperimentStep.Id}",
        ////                It.IsAny<CancellationToken>(),
        ////                It.IsAny<string>()))
        ////            .ReturnsAsync(providerState)
        ////            .Verifiable();

        ////        // Return provisioned tip sessions
        ////        this.mockDataClient.Setup(c => c.GetOrCreateStateAsync<IEnumerable<EnvironmentEntity>>(
        ////                this.mockExperimentContext.Experiment.Id,
        ////                ContractExtension.EntitiesProvisioned,
        ////                It.IsAny<CancellationToken>(),
        ////                It.IsAny<string>()))
        ////            .ReturnsAsync(this.TipSessions(TipSessionStatus.Created))
        ////            .Verifiable();

        ////        // Return null heartbeat
        ////        this.mockDataClient.Setup(c => c.GetAgentHeartbeatAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
        ////            .Throws(new Exception("Data not found"))
        ////            .Verifiable();

        ////        ExecutionResult result = await installHostAgentProvider.ExecuteAsync(this.mockExperimentContext, this.mockExperimentComponent, CancellationToken.None).ConfigureAwait(false);

        ////        // Provider should fail when timed out.
        ////        Assert.AreEqual(ExecutionStatus.Failed, result.Status);
        ////        Assert.IsTrue(result.Error.Message.Contains("Timing out the experiment."));

        ////        this.mockTipClient.Verify();
        ////        this.mockDataClient.Verify();
        ////    }
        
        [Test]
        public void InstallHostAgentProviderRequestsAutoTriageDiagnosticsOnFailedExperimentsWithDiagnosticsEnabled()
        {
            // Enable Diagnostics flag is on
            this.mockFixture.Component.Parameters[StepParameters.EnableDiagnostics] = true;
            // Execution will fail with invalid resource group deployment
            this.ValidateHostAgentDeployment(TipNodeSessionChangeResult.Failed, ExecutionStatus.Failed);

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
        public void InstallHostAgentProviderDoesNotRequestAutoTriageDiagnosticsOnFailedExperimentsWithoutDiagnosticsEnabled()
        {
            // Enable Diagnostics flag is not enabled
            this.mockFixture.Component.Parameters[StepParameters.EnableDiagnostics] = false;
            // Execution will fail with invalid resource group deployment
            this.ValidateHostAgentDeployment(TipNodeSessionChangeResult.Failed, ExecutionStatus.Failed);

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
        public void InstallHostAgentProviderWithDiagnosticsEnabledDoesNotRequestDiagnosticsFromUnfailedExperiments(TipNodeSessionChangeResult state, ExecutionStatus status)
        {
            // Enable Diagnostics flag is enabled
            this.mockFixture.Component.Parameters[StepParameters.EnableDiagnostics] = true;
            // Testing other resource group deployment execution statuses
            this.ValidateHostAgentDeployment(state, status);

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
        public void InstallHostAgentProviderReturnCorrectStatusonResourceDeployment()
        {
            this.ValidateHostAgentDeployment(TipNodeSessionChangeResult.Unknown, ExecutionStatus.InProgress);
            this.ValidateHostAgentDeployment(TipNodeSessionChangeResult.Failed, ExecutionStatus.Failed);
            this.ValidateHostAgentDeployment(TipNodeSessionChangeResult.Succeeded, ExecutionStatus.Succeeded);
        }

        [Test]
        public async Task InstallHostAgentProviderWillRetryAgentInstallationOnExpectedTiPServiceFailures()
        {
            foreach (string retryableFailureScenario in InstallHostAgentProviderTests.expectedRetryableErrors)
            {
                this.providerState.JwtDropRequested = true;
                this.providerState.JwtDropRequestTime = DateTime.UtcNow.AddSeconds(-15);
                this.providerState.JwtDropConfirmed = true;
                this.providerState.AgentRegistered = true;
                this.providerState.AgentInstallationRequested = true;
                this.providerState.AgentInstallationRetries = 0;

                this.mockTipClient
                    .Setup(client => client.GetTipSessionChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult(new TipNodeSessionChangeDetails
                    {
                        ChangeType = TipNodeSessionChangeType.Create,
                        Result = TipNodeSessionChangeResult.Failed,
                        ErrorMessage = retryableFailureScenario
                    }));

                await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None)
                    .ConfigureAwait(false);

                // The provider should reset the state to cause the agent installation attempt to retry.
                Assert.IsFalse(this.providerState.AgentHeartbeatConfirmed);
                Assert.IsFalse(this.providerState.AgentInstallationConfirmed);
                Assert.IsFalse(this.providerState.AgentInstallationRequested);
                Assert.IsNull(this.providerState.AgentInstallationRequestTime);
                Assert.IsTrue(this.providerState.AgentInstallationRetries > 0);

                // No changes should be made to the access token/JWT state
                Assert.IsTrue(this.providerState.JwtDropConfirmed);
                Assert.IsTrue(this.providerState.JwtDropRequested);
                Assert.IsNotNull(this.providerState.JwtDropRequestTime);

                // No changes should be made to the agent registration state. It does not
                // need to be registered with Juno more than once.
                Assert.IsTrue(this.providerState.AgentRegistered);
            }
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

        private void ValidateHostAgentDeployment(TipNodeSessionChangeResult tipSessionState, ExecutionStatus expectedStatus)
        {
            this.providerState.AgentInstallationRequested = true;
            this.providerState.JwtDropRequested = true;
            this.providerState.JwtDropRequestTime = DateTime.UtcNow.AddSeconds(-15);
            this.providerState.JwtDropConfirmed = true;
            this.providerState.AgentRegistered = true;
            this.providerState.AgentInstallationRetries = 0;

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
                // set the heartbeat to confirmed
                this.providerState.AgentHeartbeatConfirmed = true;
                this.providerState.AgentInstallationConfirmed = true;
                this.mockFixture.DataClient.Setup(c => c.SaveStateAsync<State>(
                        this.mockFixture.ExperimentId,
                        $"state-{this.mockFixture.Context.ExperimentStep.Id}",
                        It.IsAny<State>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<string>()))
                    .Returns(Task.CompletedTask);
            }
            else
            {
                this.mockFixture.DataClient.Setup(c => c.SaveStateAsync<State>(
                        this.mockFixture.ExperimentId,
                        $"state-{this.mockFixture.Context.ExperimentStep.Id}",
                        It.IsAny<State>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<string>()))
                    .Returns(Task.CompletedTask);
            }

            ExecutionResult result = this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None).Result;
            Assert.AreEqual(expectedStatus, result.Status);
        }

        private void InitializeMockDefaultBehaviors()
        {
            this.mockFixture.DataClient.OnGetState<InstallHostAgentProvider.State>()
                .ReturnsAsync(this.providerState);

            this.mockFixture.DataClient.OnGetEntitiesProvisioned()
                .ReturnsAsync(InstallHostAgentProviderTests.TipSessions(TipSessionStatus.Created));
        }
    }
}
