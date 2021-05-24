namespace Juno.Execution.Providers.Environment
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Execution.TipIntegration;
    using Juno.Providers;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Moq;
    using NUnit.Framework;
    using TipGateway.Entities;
    using TipGateway.FabricApi.Requests;

    [TestFixture]
    [Category("Unit")]
    public class TipStateProviderTests
    {
        private ProviderFixture mockFixture;
        private Mock<ITipClient> mockTipClient;
        private TestLogger mockLogger;
        private List<EnvironmentEntity> mockEntitiesProvisioned;
        private TipStateProvider provider;
        private TipStateProvider.State mockState;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new ProviderFixture(typeof(TipStateProvider));
            this.mockFixture.SetupExperimentMocks(ExperimentType.AB);
            this.mockTipClient = new Mock<ITipClient>();
            this.mockLogger = new TestLogger();

            this.mockFixture.Component.Parameters.Add(StepParameters.NodeState, NodeState.Excluded.ToString());
            this.mockFixture.Services.AddSingleton<ILogger>(this.mockLogger);
            this.mockFixture.Services.AddSingleton<ITipClient>(this.mockTipClient.Object);
            this.mockFixture.Services.AddSingleton<EntityManager>(this.mockFixture.EntityManager);

            this.mockState = new TipStateProvider.State
            {
                StepTimeout = DateTime.UtcNow.AddMinutes(10)
            };

            this.mockEntitiesProvisioned = new List<EnvironmentEntity>
            {
                EnvironmentEntity.TipSession("TipSession01", "Group A", new Dictionary<string, IConvertible>
                {
                    [nameof(EnvironmentEntityExtensions.ClusterName)] = "Cluster01",
                    [nameof(EnvironmentEntityExtensions.RackLocation)] = "Rack01",
                    [nameof(EnvironmentEntityExtensions.Region)] = "Region01",
                    [nameof(EnvironmentEntityExtensions.MachinePoolName)] = "Cluster01-MP01",
                    [nameof(EnvironmentEntityExtensions.TipSessionId)] = Guid.NewGuid().ToString(),
                    [nameof(EnvironmentEntityExtensions.NodeId)] = Guid.NewGuid().ToString()
                }),
                EnvironmentEntity.TipSession("TipSession02", "Group B", new Dictionary<string, IConvertible>
                {
                    [nameof(EnvironmentEntityExtensions.ClusterName)] = "Cluster01",
                    [nameof(EnvironmentEntityExtensions.RackLocation)] = "Rack01",
                    [nameof(EnvironmentEntityExtensions.Region)] = "Region01",
                    [nameof(EnvironmentEntityExtensions.MachinePoolName)] = "Cluster01-MP01",
                    [nameof(EnvironmentEntityExtensions.TipSessionId)] = Guid.NewGuid().ToString(),
                    [nameof(EnvironmentEntityExtensions.NodeId)] = Guid.NewGuid().ToString(),
                })
            };

            this.provider = new TipStateProvider(this.mockFixture.Services);
            this.SetupMockDefaults();
        }

        [Test]
        public void TipStateProviderSupportsExpectedParameters()
        {
            this.mockFixture.Component.Parameters[StepParameters.FeatureFlag] = "AnyFlag";
            this.mockFixture.Component.Parameters[StepParameters.NodeState] = NodeState.Excluded.ToString();
            this.mockFixture.Component.Parameters[StepParameters.Timeout] = TimeSpan.FromHours(1).ToString();

            Assert.DoesNotThrowAsync(() => this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None));
        }

        [Test]
        public async Task TipStateProviderTelemetryEventsUseTheExpectedPrefix()
        {
            this.mockLogger.OnLog = (level, eventId, message, exc) =>
            {
                Assert.IsTrue(
                    eventId.Name.StartsWith(nameof(TipStateProvider)),
                    $"Event '{eventId.Name}' does not have the prefix expected.");
            };

            // Typical code paths
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            // Exception/Error code paths
            this.mockTipClient.Setup(c => c.GetTipSessionChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .Throws(new TimeoutException());

            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);
        }

        [Test]
        public async Task TipStateProviderThrowsIfTheStepIsTimedOutBeforeTheRequiredTipNodeStateChangeIsCompleted()
        {
            // Setup the step to timeout
            this.mockState.StepTimeout = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(1));
            ExecutionResult result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            Assert.IsTrue(result.Status == ExecutionStatus.Failed);
            Assert.IsNotNull(result.Error);
            Assert.IsInstanceOf<TimeoutException>(result.Error);
        }

        [Test]
        public async Task TipStateProviderSavesStateAtTheEndOfEachRoundOfExecution()
        {
            int executionAttempts = 0;
            this.mockFixture.DataClient.OnSaveState<TipStateProvider.State>()
                .Callback<string, string, TipStateProvider.State, CancellationToken, string>((experimentId, key, state, token, stateId) => executionAttempts++)
                .Returns(Task.CompletedTask);

            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            Assert.IsTrue(executionAttempts == 2);
        }

        [Test]
        public async Task TipStateProviderSavesTheEntitiesProvisionedChangesAtTheEndOfEachRoundOfExecution()
        {
            int executionAttempts = 0;
            this.mockFixture.DataClient.OnSaveEntitiesProvisioned()
                .Callback<string, string, IEnumerable<EnvironmentEntity>, CancellationToken, string>((experimentId, key, state, token, stateId) => executionAttempts++)
                .Returns(Task.CompletedTask);

            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            Assert.IsTrue(executionAttempts == 2);
        }

        [Test]
        [TestCase(NodeState.Excluded)]
        [TestCase(NodeState.Raw)]
        public async Task TipStateProviderSavesTheNodeStateMetadataToTheEntitiesProvisionedOnSuccessfulChanges(NodeState expectedState)
        {
            // Ensure we target both Group A and Group B
            this.mockFixture.Setup(ExperimentType.AB, "*");
            this.mockFixture.Component.Parameters[StepParameters.NodeState] = expectedState.ToString();

            // First execution requests the node state changes.
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            // Second executions confirms the node state changes.
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            Assert.IsTrue(this.mockEntitiesProvisioned.GetTipSessions().Select(tip => tip.NodeState()).Distinct().Count() == 1);
            Assert.AreEqual(expectedState.ToString(), this.mockEntitiesProvisioned.GetTipSessions().First().NodeState());
        }

        [Test]
        [TestCase(NodeState.Excluded)]
        [TestCase(NodeState.Raw)]
        public async Task TipStateProviderRequestsTheExpectedNodeStateChange_Scenario1(NodeState expectedState)
        {
            this.mockFixture.Setup(ExperimentType.AB, "Group B");

            // In this scenario, the environment groups are explicity defined (e.g. Group A, Group B).
            this.mockFixture.Component.Parameters[StepParameters.NodeState] = expectedState.ToString();

            TipNodeSessionChange changeResponse = new TipNodeSessionChange
            {
                Status = TipNodeSessionChangeStatus.Finished,
                TipNodeSessionChangeId = Guid.NewGuid().ToString(),
                TipNodeSessionId = Guid.NewGuid().ToString()
            };

            List<EnvironmentEntity> tipSessionsTargeted = new List<EnvironmentEntity>();

            this.mockTipClient
                .Setup(c => c.SetNodeStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NodeState>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, NodeState, CancellationToken>((tipSessionId, tipNodeId, actualState, token) =>
                {
                    Assert.AreEqual(expectedState, actualState);

                    EnvironmentEntity tipSession = this.mockEntitiesProvisioned.FirstOrDefault(tip => tip.TipSessionId() == tipSessionId);
                    Assert.IsNotNull(tipSession);
                    Assert.AreEqual(tipSession.NodeId(), tipNodeId);

                    tipSessionsTargeted.Add(tipSession);
                })
                .ReturnsAsync(() => changeResponse);

            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            Assert.IsTrue(tipSessionsTargeted.Count == 1);
            Assert.IsTrue(tipSessionsTargeted.First().EnvironmentGroup == "Group B");
        }

        [Test]
        [TestCase(NodeState.Excluded)]
        [TestCase(NodeState.Raw)]
        public async Task TipStateProviderRequestsTheExpectedNodeStateChange_Scenario2(NodeState expectedState)
        {
            this.mockFixture.Setup(ExperimentType.AB, "*");

            // In this scenario, the global wildard is used to define the environment groups '*'.
            this.mockFixture.Component.Parameters[StepParameters.NodeState] = expectedState.ToString();

            TipNodeSessionChange changeResponse = new TipNodeSessionChange
            {
                Status = TipNodeSessionChangeStatus.Finished,
                TipNodeSessionChangeId = Guid.NewGuid().ToString(),
                TipNodeSessionId = Guid.NewGuid().ToString()
            };

            List<EnvironmentEntity> tipSessionsTargeted = new List<EnvironmentEntity>();

            this.mockTipClient
                .Setup(c => c.SetNodeStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NodeState>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, NodeState, CancellationToken>((tipSessionId, tipNodeId, actualState, token) =>
                {
                    Assert.AreEqual(expectedState, actualState);

                    EnvironmentEntity tipSession = this.mockEntitiesProvisioned.FirstOrDefault(tip => tip.TipSessionId() == tipSessionId);
                    Assert.IsNotNull(tipSession);
                    Assert.AreEqual(tipSession.NodeId(), tipNodeId);

                    tipSessionsTargeted.Add(tipSession);
                })
                .ReturnsAsync(() => changeResponse);

            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            Assert.IsTrue(tipSessionsTargeted.Count == this.mockEntitiesProvisioned.Count);
            CollectionAssert.AreEquivalent(this.mockEntitiesProvisioned.Select(e => e.EnvironmentGroup), tipSessionsTargeted.Select(tip => tip.EnvironmentGroup));
        }

        [Test]
        public async Task TipStateProviderConfirmsTheSuccessfulCompletionOfNodeStateChangeRequests_Scenario1()
        {
            this.mockFixture.Setup(ExperimentType.AB, "Group B");

            // In this scenario, the environment groups are explicity defined (e.g. Group A, Group B).
            this.mockFixture.Component.Parameters[StepParameters.NodeState] = NodeState.Excluded.ToString();

            TipNodeSessionChangeDetails changeStatus = new TipNodeSessionChangeDetails
            {
                Status = TipNodeSessionChangeStatus.Finished,
                TipNodeSessionChangeId = Guid.NewGuid().ToString(),
                TipNodeSessionId = Guid.NewGuid().ToString()
            };

            List<EnvironmentEntity> tipSessionsVerified = new List<EnvironmentEntity>();

            this.mockTipClient
                .Setup(c => c.GetTipSessionChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, CancellationToken>((tipSessionId, tipChangeId, token) =>
                {
                    EnvironmentEntity tipSession = this.mockEntitiesProvisioned.FirstOrDefault(tip => tip.TipSessionId() == tipSessionId);
                    Assert.IsNotNull(tipSession);
                    tipSessionsVerified.Add(tipSession);
                })
                .ReturnsAsync(() => changeStatus);

            // During the first round, the initial state change requests are made.
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            // During the second round, the state change request outcomes are confirmed.
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            Assert.IsTrue(tipSessionsVerified.Count == 1);
            Assert.IsTrue(tipSessionsVerified.First().EnvironmentGroup == "Group B");
        }

        [Test]
        public async Task TipStateProviderConfirmsTheSuccessfulCompletionOfNodeStateChangeRequests_Scenario2()
        {
            this.mockFixture.Setup(ExperimentType.AB, "*");

            // In this scenario, the environment groups are explicity defined (e.g. Group A, Group B).
            this.mockFixture.Component.Parameters[StepParameters.NodeState] = NodeState.Excluded.ToString();

            TipNodeSessionChangeDetails changeStatus = new TipNodeSessionChangeDetails
            {
                Status = TipNodeSessionChangeStatus.Finished,
                TipNodeSessionChangeId = Guid.NewGuid().ToString(),
                TipNodeSessionId = Guid.NewGuid().ToString()
            };

            List<EnvironmentEntity> tipSessionsVerified = new List<EnvironmentEntity>();

            this.mockTipClient
                .Setup(c => c.GetTipSessionChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, CancellationToken>((tipSessionId, tipChangeId, token) =>
                {
                    EnvironmentEntity tipSession = this.mockEntitiesProvisioned.FirstOrDefault(tip => tip.TipSessionId() == tipSessionId);
                    Assert.IsNotNull(tipSession);
                    tipSessionsVerified.Add(tipSession);
                })
                .ReturnsAsync(() => changeStatus);

            // During the first round, the initial state change requests are made.
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            // During the second round, the state change request outcomes are confirmed.
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            Assert.IsTrue(tipSessionsVerified.Count == this.mockEntitiesProvisioned.Count);
            CollectionAssert.AreEquivalent(this.mockEntitiesProvisioned.Select(e => e.EnvironmentGroup), tipSessionsVerified.Select(tip => tip.EnvironmentGroup));
        }

        [Test]
        public async Task TipStateProviderFailsTheStepIfTheStateChangeRequestFails()
        {
            this.mockFixture.Setup(ExperimentType.AB, "*");

            // In this scenario, the environment groups are explicity defined (e.g. Group A, Group B).
            this.mockFixture.Component.Parameters[StepParameters.NodeState] = NodeState.Excluded.ToString();

            TipNodeSessionChangeDetails changeStatus = new TipNodeSessionChangeDetails
            {
                Status = TipNodeSessionChangeStatus.Executing,
                TipNodeSessionChangeId = Guid.NewGuid().ToString(),
                TipNodeSessionId = Guid.NewGuid().ToString()
            };

            // Mimic the TiP service indicating that the node state change request failed.
            this.mockTipClient
                .Setup(c => c.IsTipSessionChangeFailedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            this.mockTipClient
                .Setup(c => c.GetTipSessionChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => changeStatus);

            // During the first round, the initial state change requests are made.
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            // During the second round, the state change request outcomes are confirmed.
            ExecutionResult result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            Assert.IsTrue(result.Status == ExecutionStatus.Failed);
            Assert.IsNotNull(result.Error);
            Assert.IsInstanceOf<ProviderException>(result.Error);
            Assert.AreEqual(ErrorReason.TipRequestFailure, (result.Error as ProviderException).Reason);
        }

        [Test]
        public async Task TipStateProviderReturnsTheExpectedResponseWhenNodeStateChangeRequestsSucceed()
        {
            this.mockFixture.Setup(ExperimentType.AB, "*");

            // In this scenario, the environment groups are explicity defined (e.g. Group A, Group B).
            this.mockFixture.Component.Parameters[StepParameters.NodeState] = NodeState.Excluded.ToString();

            // During the first round, the initial state change requests are made.
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            // During the second round, the state change request outcomes are confirmed.
            ExecutionResult result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            Assert.IsTrue(result.Status == ExecutionStatus.Succeeded);
        }

        private void SetupMockDefaults()
        {
            // The EntityManager will make calls to the API through the IProviderDataClient to manage
            // entities (e.g. entityPool, entitiesProvisioned).
            this.mockFixture.DataClient.OnGetState<TipStateProvider.State>().ReturnsAsync(() => this.mockState);
            this.mockFixture.DataClient.OnGetEntitiesProvisioned().ReturnsAsync(this.mockEntitiesProvisioned);

            // TiP session creation requests are confirmed succeeded by default.
            this.mockTipClient
                .Setup(c => c.IsTipSessionChangeFailedAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            TipNodeSession confirmedTipSession = null;
            this.mockTipClient.OnGetTipSession()
                .Callback<string, CancellationToken>((tipSessionId, token) =>
                {
                    EnvironmentEntity tipSession = this.mockEntitiesProvisioned.First(t => t.TipSessionId() == tipSessionId);
                    confirmedTipSession = new TipNodeSession
                    {
                        Id = tipSessionId,
                        Cluster = tipSession.ClusterName(),
                        CreatedBy = "Anybody",
                        NodeCount = 1,
                        CreatedTimeUtc = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(10)),
                        Region = tipSession.Region(),
                        Status = TipNodeSessionStatus.Created
                    };
                })
                .ReturnsAsync(() => confirmedTipSession);

            // The default response indicates that the node state change request completed successfully.
            TipNodeSessionChange changeResponse = null;

            this.mockTipClient
                .Setup(c => c.SetNodeStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NodeState>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, NodeState, CancellationToken>((tipSessionId, tipNodeId, state, token) =>
                {
                    changeResponse = new TipNodeSessionChange
                    {
                        Status = TipNodeSessionChangeStatus.Finished,
                        TipNodeSessionChangeId = Guid.NewGuid().ToString(),
                        TipNodeSessionId = tipSessionId
                    };
                })
                .ReturnsAsync(() => changeResponse);

            TipNodeSessionChangeDetails changeDetails = null;

            this.mockTipClient
               .Setup(c => c.GetTipSessionChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .Callback<string, string, CancellationToken>((tipSessionId, tipChangeId, token) =>
               {
                   changeDetails = new TipNodeSessionChangeDetails
                   {
                       Status = TipNodeSessionChangeStatus.Finished,
                       TipNodeSessionChangeId = tipChangeId,
                       TipNodeSessionId = tipSessionId
                   };
               })
               .ReturnsAsync(() => changeDetails);
        }
    }
}
