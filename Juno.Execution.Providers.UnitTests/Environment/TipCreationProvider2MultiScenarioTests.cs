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
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Moq;
    using NUnit.Framework;
    using TipGateway.Entities;

    [TestFixture]
    [Category("Unit")]
    public class TipCreationProvider2MultiScenarioTests
    {
        private ProviderFixture mockFixture;
        private Mock<ITipClient> mockTipClient;
        private TestLogger mockLogger;
        private List<EnvironmentEntity> mockEntityPool;
        private List<EnvironmentEntity> mockEntitiesProvisioned;
        private TipCreationProvider2 provider;
        private TipCreationProvider2.State mockState;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new ProviderFixture(typeof(TipCreationProvider));
            this.mockFixture.SetupExperimentMocks(ExperimentType.AB);
            this.mockTipClient = new Mock<ITipClient>();
            this.mockLogger = new TestLogger();

            this.mockFixture.Services.AddSingleton<ILogger>(this.mockLogger);
            this.mockFixture.Services.AddSingleton<ITipClient>(this.mockTipClient.Object);
            this.mockFixture.Services.AddSingleton<EntityManager>(this.mockFixture.EntityManager);

            this.mockState = new TipCreationProvider2.State
            {
                StepTimeout = DateTime.UtcNow.AddMinutes(10),
                Timeout = TimeSpan.FromMinutes(10),
                CountPerGroup = 2
            };

            this.mockEntityPool = new List<EnvironmentEntity>
            {
                // The set of node entities below is meant to represent a scenario where there nodes
                // that have different location characteristics in order to validate both selection and
                // selection retry scenarios in the provider. It is important that the set have enough nodes
                // defined to represent ALL possible node affinities. This introduces complexity to the handling
                // of the node selection and the provider MUST be able to handle this complexity. The default
                // provider affinity is SameRack affinity. These tests evaluate the correct behavior of the provider
                // irrespective of the affinity
                //
                // Given the Nodes Defined Below:
                // 1) SameRack Affinity
                //    Given the node options below there are only 2 racks that can support
                //    the requirement of same rack affinity for an A/B experiment.
                //    - Rack01 - 4 options
                //    - Rack02 - 2 options
                EnvironmentEntity.Node("Cluster01-Rack01-Node01", "Group A", new Dictionary<string, IConvertible>
                {
                    [nameof(EnvironmentEntityExtensions.ClusterName)] = "Cluster01",
                    [nameof(EnvironmentEntityExtensions.RackLocation)] = "Rack01",
                    [nameof(EnvironmentEntityExtensions.Region)] = "Region01",
                    [nameof(EnvironmentEntityExtensions.MachinePoolName)] = "Cluster01-MP01"
                }),
                EnvironmentEntity.Node("Cluster01-Rack01-Node02", "Group B", new Dictionary<string, IConvertible>
                {
                    [nameof(EnvironmentEntityExtensions.ClusterName)] = "Cluster01",
                    [nameof(EnvironmentEntityExtensions.RackLocation)] = "Rack01",
                    [nameof(EnvironmentEntityExtensions.Region)] = "Region01",
                    [nameof(EnvironmentEntityExtensions.MachinePoolName)] = "Cluster01-MP01"
                }),
                EnvironmentEntity.Node("Cluster01-Rack01-Node03", "Group A", new Dictionary<string, IConvertible>
                {
                    [nameof(EnvironmentEntityExtensions.ClusterName)] = "Cluster01",
                    [nameof(EnvironmentEntityExtensions.RackLocation)] = "Rack01",
                    [nameof(EnvironmentEntityExtensions.Region)] = "Region01",
                    [nameof(EnvironmentEntityExtensions.MachinePoolName)] = "Cluster01-MP01"
                }),
                EnvironmentEntity.Node("Cluster01-Rack01-Node04", "Group B", new Dictionary<string, IConvertible>
                {
                    [nameof(EnvironmentEntityExtensions.ClusterName)] = "Cluster01",
                    [nameof(EnvironmentEntityExtensions.RackLocation)] = "Rack01",
                    [nameof(EnvironmentEntityExtensions.Region)] = "Region01",
                    [nameof(EnvironmentEntityExtensions.MachinePoolName)] = "Cluster01-MP01"
                }),
                EnvironmentEntity.Node("Cluster01-Rack01-Node05", "Group A", new Dictionary<string, IConvertible>
                {
                    [nameof(EnvironmentEntityExtensions.ClusterName)] = "Cluster01",
                    [nameof(EnvironmentEntityExtensions.RackLocation)] = "Rack01",
                    [nameof(EnvironmentEntityExtensions.Region)] = "Region01",
                    [nameof(EnvironmentEntityExtensions.MachinePoolName)] = "Cluster01-MP01"
                }),
                EnvironmentEntity.Node("Cluster01-Rack01-Node06", "Group B", new Dictionary<string, IConvertible>
                {
                    [nameof(EnvironmentEntityExtensions.ClusterName)] = "Cluster01",
                    [nameof(EnvironmentEntityExtensions.RackLocation)] = "Rack01",
                    [nameof(EnvironmentEntityExtensions.Region)] = "Region01",
                    [nameof(EnvironmentEntityExtensions.MachinePoolName)] = "Cluster01-MP01"
                }),
                EnvironmentEntity.Node("Cluster01-Rack02-Node05", "Group A", new Dictionary<string, IConvertible>
                {
                    [nameof(EnvironmentEntityExtensions.ClusterName)] = "Cluster01",
                    [nameof(EnvironmentEntityExtensions.RackLocation)] = "Rack02",
                    [nameof(EnvironmentEntityExtensions.Region)] = "Region01",
                    [nameof(EnvironmentEntityExtensions.MachinePoolName)] = "Cluster01-MP01"
                }),
                EnvironmentEntity.Node("Cluster01-Rack02-Node06", "Group B", new Dictionary<string, IConvertible>
                {
                    [nameof(EnvironmentEntityExtensions.ClusterName)] = "Cluster01",
                    [nameof(EnvironmentEntityExtensions.RackLocation)] = "Rack02",
                    [nameof(EnvironmentEntityExtensions.Region)] = "Region01",
                    [nameof(EnvironmentEntityExtensions.MachinePoolName)] = "Cluster01-MP01"
                }),
                EnvironmentEntity.Node("Cluster01-Rack02-Node07", "Group A", new Dictionary<string, IConvertible>
                {
                    [nameof(EnvironmentEntityExtensions.ClusterName)] = "Cluster01",
                    [nameof(EnvironmentEntityExtensions.RackLocation)] = "Rack02",
                    [nameof(EnvironmentEntityExtensions.Region)] = "Region01",
                    [nameof(EnvironmentEntityExtensions.MachinePoolName)] = "Cluster01-MP01"
                }),
                EnvironmentEntity.Node("Cluster01-Rack02-Node08", "Group B", new Dictionary<string, IConvertible>
                {
                    [nameof(EnvironmentEntityExtensions.ClusterName)] = "Cluster01",
                    [nameof(EnvironmentEntityExtensions.RackLocation)] = "Rack02",
                    [nameof(EnvironmentEntityExtensions.Region)] = "Region01",
                    [nameof(EnvironmentEntityExtensions.MachinePoolName)] = "Cluster01-MP01"
                })
            };

            this.mockEntitiesProvisioned = new List<EnvironmentEntity>();
            this.provider = new TipCreationProvider2(this.mockFixture.Services);
            this.SetupMockDefaults();
        }

        [Test]
        public async Task ProviderCreatesTheExpectedTipSessionsBySelectingOtherNodesWhenTheInitialAttemptsFail()
        {
            // Scenario 1 involves the case where a TiP node is created successfully for the node affinity, then
            // the next 1 or more nodes that meet that affinity do not have successful TiP session creations. However,
            // the final node option results in a successful TiP session creation.
            // for those.
            this.mockTipClient
                .SetupSequence(c => c.IsTipSessionChangeFailedAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false) // 1st TiP node is created successfully (i.e. Node01)
                .ReturnsAsync(true) // ...but subsequent TiP nodes that match the same affinity fail to get created (i.e. Node02)
                .ReturnsAsync(false) // GroupA-Node03 success
                .ReturnsAsync(false) // GroupB-Node04 success
                .ReturnsAsync(false); // Only Node06 in Rack01 is there for groupB. GroupB-Node06 success.

            ExecutionResult result = null;
            int executionAttempts = 0;
            while (executionAttempts < 10)
            {
                result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);
                if (result.Status == ExecutionStatus.Succeeded)
                {
                    break;
                }

                executionAttempts++; // need a short-circuit in case to ensure we don't loop forever if the logic is wrong in the provider.
            }

            CollectionAssert.IsNotEmpty(this.mockFixture.EntityManager.EntitiesProvisioned);

            CollectionAssert.AreEquivalent(
                this.mockEntityPool.Take(1).Union(this.mockEntityPool.Skip(2).Take(2)).Union(this.mockEntityPool.Skip(5).Take(1)).Select(node => node.Id),
                this.mockFixture.EntityManager.EntitiesProvisioned.GetNodes().Select(node => node.Id));
        }

        [Test]
        public async Task ProviderAddsTheExpectedTipSessionAndNodeEntitiesToThePoolOfEntitiesProvisionedWhenTipSessionsAreSuccessfullyCreated()
        {
            // Stage a couple of TiP sessions in the entity pool. The provider will add the TipSession entities
            // to the entity pool when the initial request is made.
            IEnumerable<EnvironmentEntity> expectedTipNodes = this.mockEntityPool.GetNodes().Take(4);

            IEnumerable<EnvironmentEntity> expectedTipSessions = expectedTipNodes.Select(node => TipCreationProvider2MultiScenarioTests.CreateTipSessionEntityFrom(node));
            // this.mockEntityPool.AddRange(expectedTipSessions);

            // First execution selects the TiP nodes. The second execution confirms the TiP node statuses.
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            IEnumerable<EnvironmentEntity> tipSessionsCreated = this.mockFixture.EntityManager.EntitiesProvisioned.GetTipSessions();
            IEnumerable<EnvironmentEntity> tipNodesUsed = this.mockFixture.EntityManager.EntitiesProvisioned.GetNodes();

            // The entities provisioned should contain the Node entites associated with the TiP sessions.
            Assert.IsNotNull(tipNodesUsed);
            Assert.AreEqual(4, tipNodesUsed.Count());
            CollectionAssert.AllItemsAreUnique(tipNodesUsed);
            CollectionAssert.AllItemsAreUnique(tipNodesUsed.Select(tipNode => tipNode.TipSessionId()));

            // The TiP session IDs and node IDs should match
            CollectionAssert.AreEquivalent(tipSessionsCreated.Select(tip => tip.TipSessionId()), tipNodesUsed.Select(node => node.TipSessionId()));
            CollectionAssert.AreEquivalent(tipSessionsCreated.Select(tip => tip.NodeId()), tipNodesUsed.Select(node => node.Id));
        }

        [Test]
        public async Task ProviderDeletesAnyTipSessionsPreviouslyCreatedThatCannotBeUsedAsPartOfTheExperiment()
        {
            // Scenario 1 involves the case where a TiP node is created successfully for the node affinity but
            // no other nodes matching that node affinity have successful TiP sessions created. The logic has to move
            // on to a completely new set of nodes meeting the node affinity and successfully creates TiP sessions
            // for those.
            this.mockTipClient
                .SetupSequence(c => c.IsTipSessionChangeFailedAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false) // 1st TiP node is created successfully (i.e. Node01)
                .ReturnsAsync(false) // ...but subsequent TiP nodes that match the same affinity fail to get created (i.e. Node03, 04, 05, 06 on rack 1)
                .ReturnsAsync(true)
                .ReturnsAsync(true)
                .ReturnsAsync(true)
                .ReturnsAsync(true)
                .ReturnsAsync(false) // Provider has to start over on Rack02
                .ReturnsAsync(false)
                .ReturnsAsync(false) // All rack02 succeed
                .ReturnsAsync(false);

            // Track which TiP sessions are deleted at the end.
            List<EnvironmentEntity> tipNodeSessionsDeleted = new List<EnvironmentEntity>();
            this.mockTipClient.OnDeleteTipSession()
                .Callback<string, CancellationToken>((tipSessionId, token) =>
                {
                    EnvironmentEntity tipNode = this.mockEntityPool.FirstOrDefault(node => node.TipSessionId() == tipSessionId);
                    Assert.IsNotNull(tipNode);
                    tipNodeSessionsDeleted.Add(tipNode);
                })
                .ReturnsAsync(TipCreationProvider2MultiScenarioTests.CreateTipNodeSessionChange());

            ExecutionResult result = null;
            int executionAttempts = 0;
            while (executionAttempts < 10)
            {
                result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);
                if (result.Status == ExecutionStatus.Succeeded)
                {
                    break;
                }

                executionAttempts++; // need a short-circuit in case to ensure we don't loop forever if the logic is wrong in the provider.
            }

            CollectionAssert.IsNotEmpty(this.mockFixture.EntityManager.EntitiesProvisioned);
            CollectionAssert.IsNotEmpty(tipNodeSessionsDeleted);
            CollectionAssert.AreEquivalent(this.mockEntityPool.Take(2).Select(node => node.Id), tipNodeSessionsDeleted.Select(node => node.Id));
        }

        [Test]
        public async Task ProviderMarksNodeEntitiesInTheEntityPoolAsDiscardedWhenTipSessionCreationFails()
        {
            // Cause the TiP session request to be failed
            this.mockTipClient
                .Setup(c => c.IsTipSessionChangeFailedAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Stage a couple of TiP sessions in the entity pool. The provider will add the TipSession entities
            // to the entity pool when the initial request is made.
            IEnumerable<EnvironmentEntity> expectedTipNodes = this.mockEntityPool.GetNodes().GroupBy(node => node.EnvironmentGroup)
                .Select(nodes => nodes.First());

            // First execution selects the TiP nodes. The second execution confirms the TiP node statuses.
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            // The entities provisioned should contain the Node entites associated with the TiP sessions.
            Assert.IsTrue(expectedTipNodes.All(tip => tip.Discarded()));
        }

        [Test]
        public async Task ProviderAttemptsToCreateTipSessionsSelectingOtherNodesWhenTheInitialAttemptsFail()
         {
            // Stage a couple of TiP sessions in the entity pool. The provider will add the TipSession entities
            // to the entity pool when the initial request is made.
            IEnumerable<EnvironmentEntity> originalTipNodesSelected = this.mockEntityPool.GetNodes().Take(4);

            // Cause the TiP session request to be failed
            this.mockTipClient
                .Setup(c => c.IsTipSessionChangeFailedAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            int newTipSessionsRequested = 0;
            this.mockTipClient.OnCreateTipSession()
                .Callback<TipParameters, CancellationToken>((parameters, token) =>
                {
                    newTipSessionsRequested++;
                    if (newTipSessionsRequested == 8)
                    {
                        // The nodes referenced should be 4 brand new nodes.
                        Assert.IsFalse(parameters.CandidateNodesId.Intersect(originalTipNodesSelected.Select(tipNode => tipNode.Id)).Any());
                    }
                })
                .ReturnsAsync(() => TipCreationProvider2MultiScenarioTests.CreateTipNodeSessionChange());

            // First execution selects the TiP nodes. The second execution confirms the TiP node statuses.
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);
            // Third one will try 4 new nodes
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            Assert.AreEqual(8, newTipSessionsRequested);
        }

        [Test]
        public async Task ProviderWillAttemptToCreateTheRequisiteTipSessionsUntilItRunsOutOfViableOptions_Scenario1()
        {
            // Scenario 1 involves the case where no TiP nodes are ever created successfully.
            this.mockTipClient
                .Setup(c => c.IsTipSessionChangeFailedAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            int attempts = 0;
            this.mockTipClient.OnCreateTipSession()
                .Callback<TipParameters, CancellationToken>((parameters, token) => attempts++)
                .ReturnsAsync(() => TipCreationProvider2MultiScenarioTests.CreateTipNodeSessionChange());

            ExecutionResult result = null;
            int executionAttempts = 0;
            while (executionAttempts < 10)
            {
                result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);
                if (result.Status == ExecutionStatus.Failed)
                {
                    break;
                }

                executionAttempts++; // need a short-circuit in case to ensure we don't loop forever if the logic is wrong in the provider.
            }

            Assert.AreEqual(10, attempts); // based on the setup of the mock entity pool
            Assert.AreEqual(ExecutionStatus.Failed, result.Status);
            Assert.IsNotNull(result.Error);
            Assert.IsInstanceOf<ProviderException>(result.Error);
            Assert.IsTrue((result.Error as ProviderException).Reason == ErrorReason.ExpectedEnvironmentEntitiesNotFound);
        }

        [Test]
        public async Task ProviderWillAttemptToCreateTheRequisiteTipSessionsUntilItRunsOutOfViableOptions_Scenario2()
        {
            // Scenario 2 involves the case where at least 1 TiP node is created successfully. There are 6 nodes based
            // upon the mock entities defined at the top of the test. We want to have 1 TiP session created but the 
            // rest fail to get created.
            this.mockTipClient
                .SetupSequence(c => c.IsTipSessionChangeFailedAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false)
                .ReturnsAsync(true)
                .ReturnsAsync(true)
                .ReturnsAsync(true)
                .ReturnsAsync(true)
                .ReturnsAsync(true)
                .ReturnsAsync(true)
                .ReturnsAsync(true)
                .ReturnsAsync(true)
                .ReturnsAsync(true);

            int attempts = 0;
            this.mockTipClient.OnCreateTipSession()
                .Callback<TipParameters, CancellationToken>((parameters, token) => attempts++)
                .ReturnsAsync(() => TipCreationProvider2MultiScenarioTests.CreateTipNodeSessionChange());

            ExecutionResult result = null;
            int executionAttempts = 0;
            while (executionAttempts < 10)
            {
                result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);
                if (result.Status == ExecutionStatus.Failed)
                {
                    break;
                }

                executionAttempts++; // need a short-circuit in case to ensure we don't loop forever if the logic is wrong in the provider.
            }

            Assert.AreEqual(10, attempts); // based on the setup of the mock entity pool.
            Assert.AreEqual(ExecutionStatus.Failed, result.Status);
            Assert.IsNotNull(result.Error);
            Assert.IsInstanceOf<ProviderException>(result.Error);
            Assert.IsTrue((result.Error as ProviderException).Reason == ErrorReason.ExpectedEnvironmentEntitiesNotFound);
        }

        [Test]
        public async Task ProviderDeletesAllTipSessionsPreviouslyCreatedWhenTheStepTimesOutBeforeCompletion()
        {
            // Scenario 1 involves the case where a TiP node is created successfully for the node affinity but
            // no other nodes matching that node affinity have successful TiP sessions created. The logic has to move
            // on to a completely new set of nodes meeting the node affinity and successfully creates TiP sessions
            // for those.
            this.mockTipClient
                .SetupSequence(c => c.IsTipSessionChangeFailedAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false) // 1st TiP node is created successfully (i.e. Node01)
                .ReturnsAsync(true) // ...but subsequent TiP nodes that match the same affinity fail to get created (i.e. Node02, Node03, Node04)
                .ReturnsAsync(true)
                .ReturnsAsync(true)
                .ReturnsAsync(true)
                .ReturnsAsync(true)
                .ReturnsAsync(true)
                .ReturnsAsync(true)
                .ReturnsAsync(true)
                .ReturnsAsync(true);

            // Track which TiP sessions are deleted at the end.
            List<EnvironmentEntity> tipNodeSessionsDeleted = new List<EnvironmentEntity>();
            this.mockTipClient.OnDeleteTipSession()
                .Callback<string, CancellationToken>((tipSessionId, token) =>
                {
                    EnvironmentEntity tipNode = this.mockEntityPool.FirstOrDefault(node => node.TipSessionId() == tipSessionId);
                    Assert.IsNotNull(tipNode);
                    tipNodeSessionsDeleted.Add(tipNode);
                })
                .ReturnsAsync(TipCreationProvider2MultiScenarioTests.CreateTipNodeSessionChange());

            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            // Setup the step to timeout
            this.mockState.StepTimeout = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(1));
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            CollectionAssert.IsEmpty(this.mockFixture.EntityManager.EntitiesProvisioned);
            CollectionAssert.IsNotEmpty(tipNodeSessionsDeleted);
            Assert.AreEqual(1, tipNodeSessionsDeleted.Count);
            Assert.AreEqual(this.mockEntityPool.First().Id, tipNodeSessionsDeleted.First().Id);
        }

        [Test]
        public async Task ProviderSavesTheEntityPoolAtTheEndOfEachRoundOfExecution()
        {
            int executionAttempts = 0;
            this.mockFixture.DataClient.OnSaveEntityPool()
                .Callback<string, string, IEnumerable<EnvironmentEntity>, CancellationToken, string>((experimentId, key, state, token, stateId) => executionAttempts++)
                .Returns(Task.CompletedTask);

            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            Assert.IsTrue(executionAttempts == 2);
        }

        [Test]
        public async Task ProviderSavesTheEntitiesProvisionedAtTheEndOfEachRoundOfExecution()
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
        public async Task ProviderSavesStateAtTheEndOfEachRoundOfExecution()
        {
            int executionAttempts = 0;
            this.mockFixture.DataClient.OnSaveState<TipCreationProvider2.State>()
                .Callback<string, string, TipCreationProvider2.State, CancellationToken, string>((experimentId, key, state, token, stateId) => executionAttempts++)
                .Returns(Task.CompletedTask);

            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            Assert.IsTrue(executionAttempts == 2);
        }

        private static EnvironmentEntity CreateTipSessionEntityFrom(EnvironmentEntity node, TipSessionStatus tipStatus = TipSessionStatus.Created)
        {
            node.TipSessionId(Guid.NewGuid().ToString());
            node.TipSessionRequestChangeId(Guid.NewGuid().ToString());
            node.TipSessionStatus(tipStatus.ToString());

            EnvironmentEntity tipSession = EnvironmentEntity.TipSession(node.TipSessionId(), node.EnvironmentGroup, node.Metadata);
            tipSession.NodeId(node.Id);

            return tipSession;
        }

        private static TipNodeSession CreateTipNodeSessionFrom(EnvironmentEntity tipNodeEntity, TipNodeSessionStatus status = TipNodeSessionStatus.Created)
        {
            return new TipNodeSession
            {
                Cluster = tipNodeEntity.ClusterName(),
                CreatedTimeUtc = DateTime.UtcNow.AddMinutes(-1),
                NodeCount = 1,
                Id = tipNodeEntity.Id,
                Region = tipNodeEntity.Region(),
                Status = status
            };
        }

        private static TipNodeSessionChange CreateTipNodeSessionChange()
        {
            return new TipNodeSessionChange()
            {
                TipNodeSessionId = Guid.NewGuid().ToString(),
                TipNodeSessionChangeId = Guid.NewGuid().ToString(),
                Status = TipNodeSessionChangeStatus.Queued
            };
        }

        private void SetupMockDefaults()
        {
            // The EntityManager will make calls to the API through the IProviderDataClient to manage
            // entities (e.g. entityPool, entitiesProvisioned).
            this.mockFixture.DataClient.OnGetState<TipCreationProvider2.State>().ReturnsAsync(() => this.mockState);
            this.mockFixture.DataClient.OnGetEntityPool().ReturnsAsync(this.mockEntityPool);
            this.mockFixture.DataClient.OnGetEntitiesProvisioned().ReturnsAsync(this.mockEntitiesProvisioned);

            // Mimic the addition of entities to the entities provisioned that will be saved to a backing store
            // on subsequent provider executions.
            this.mockFixture.DataClient.OnSaveEntityPool()
                .Callback<string, string, IEnumerable<EnvironmentEntity>, CancellationToken, string>((experimentId, key, state, token, stateId) =>
                {
                    state.ToList().ForEach(entity =>
                    {
                        if (!this.mockEntityPool.Contains(entity))
                        {
                            this.mockEntityPool.Add(entity);
                        }
                    });
                })
                .Returns(Task.CompletedTask);

            // Mimic the addition of entities to the entities provisioned that will be saved to a backing store
            // on subsequent provider executions.
            this.mockFixture.DataClient.OnSaveEntitiesProvisioned()
                .Callback<string, string, IEnumerable<EnvironmentEntity>, CancellationToken, string>((experimentId, key, state, token, stateId) =>
                {
                    state.ToList().ForEach(entity =>
                    {
                        if (!this.mockEntitiesProvisioned.Contains(entity))
                        {
                            this.mockEntitiesProvisioned.Add(entity);
                        }
                    });
                })
                .Returns(Task.CompletedTask);

            // TiP session creation request default
            this.mockTipClient.OnCreateTipSession()
                .ReturnsAsync(() => TipCreationProvider2MultiScenarioTests.CreateTipNodeSessionChange());

            this.mockTipClient.OnDeleteTipSession()
                .ReturnsAsync(TipCreationProvider2MultiScenarioTests.CreateTipNodeSessionChange());

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
                    EnvironmentEntity expectedTipNode = this.mockFixture.EntityManager.EntityPool.GetNodes()
                        ?.FirstOrDefault(e => e.TipSessionId() == tipSessionId);

                    confirmedTipSession = TipCreationProvider2MultiScenarioTests.CreateTipNodeSessionFrom(expectedTipNode);
                })
                .ReturnsAsync(() => confirmedTipSession);
        }
    }
}
