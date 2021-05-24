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
    public class TipCreationProvider2Tests
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
                Timeout = TimeSpan.FromMinutes(10)
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
                })
            };

            this.mockEntitiesProvisioned = new List<EnvironmentEntity>();
            this.provider = new TipCreationProvider2(this.mockFixture.Services);
            this.SetupMockDefaults();
        }

        [Test]
        public async Task ProviderStepTimeoutMatchesExpectedDefaultValue()
        {
            TimeSpan expectedTimeout = TimeSpan.FromHours(2);
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            Assert.AreEqual(expectedTimeout, this.mockState.Timeout);
        }

        [Test]
        public async Task ProviderStepTimeoutMatchesExpectedValueWhenDefined()
        {
            TimeSpan expectedTimeout = TimeSpan.FromMinutes(49);
            this.mockFixture.Component.Parameters[StepParameters.Timeout] = expectedTimeout.ToString();
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            Assert.AreEqual(expectedTimeout, this.mockState.Timeout);
        }

        [Test]
        public void ProviderSupportsExpectedParameters()
        {
            this.mockFixture.Component.Parameters[StepParameters.FeatureFlag] = "AnyFlag";
            this.mockFixture.Component.Parameters[StepParameters.NodeAffinity] = NodeAffinity.Any.ToString();
            this.mockFixture.Component.Parameters[StepParameters.IsAmberNodeRequest] = true;
            this.mockFixture.Component.Parameters[StepParameters.Timeout] = TimeSpan.FromHours(1).ToString();

            Assert.DoesNotThrowAsync(() => this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None));
        }

        [Test]
        public async Task ProviderTelemetryEventsUseTheExpectedPrefix()
        {
            this.mockLogger.OnLog = (level, eventId, message, exc) =>
            {
                Assert.IsTrue(
                    eventId.Name.StartsWith(nameof(TipCreationProvider)),
                    $"Event '{eventId.Name}' does not have the prefix expected.");
            };

            // Typical code paths
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            // Exception/Error code paths
            this.mockTipClient.OnGetTipSession()
               .Throws(new TimeoutException());

            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            this.mockTipClient.OnGetTipSession()
                .Throws(new ProviderException(ErrorReason.ExpectedEnvironmentEntitiesNotFound));

            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            this.mockTipClient.OnGetTipSession()
                .Throws(new Exception("Any other errors"));

            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);
        }

        [Test]
        public async Task TipCreationProviderThrowsIfTheStepIsTimedOutBeforeTheRequiredTipSessionsAreAllSuccessfullyCreated()
        {
            // Mimic scenario where we are awaiting TiP node creation and then we timeout.
            this.mockEntityPool.First().TipSessionStatus(TipSessionStatus.Creating.ToString());

            // Setup the step to timeout
            this.mockState.StepTimeout = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(1));
            ExecutionResult result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            Assert.IsTrue(result.Status == ExecutionStatus.Failed);
            Assert.IsNotNull(result.Error);
            Assert.IsInstanceOf<TimeoutException>(result.Error);
        }

        [Test]
        public async Task TipCreationProviderThrowsIfAllNodeOptionsAreExpendedBeforeTheRequiredTipSessionsAreCreated()
        {
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

            Assert.IsTrue(result.Status == ExecutionStatus.Failed);
            Assert.IsNotNull(result.Error);
            Assert.IsInstanceOf<ProviderException>(result.Error);
            Assert.IsTrue((result.Error as ProviderException).Reason == ErrorReason.ExpectedEnvironmentEntitiesNotFound);
        }

        [Test]
        public async Task ProviderDefaultNodeAffinityIsSameRackAffinity()
        {
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            IEnumerable<EnvironmentEntity> selectedTipNodes = this.mockFixture.EntityManager.EntityPool
                .GetNodes()
                .Where(node => !string.IsNullOrEmpty(node.TipSessionId()));

            Assert.IsNotNull(selectedTipNodes);
            Assert.IsTrue(selectedTipNodes.Select(tipNode => tipNode.ClusterName()).Distinct().Count() == 1);
            Assert.IsTrue(selectedTipNodes.Select(tipNode => tipNode.RackLocation()).Distinct().Count() == 1);
        }

        [Test]
        public async Task ProviderAddsTheExpectedTipSessionAndNodeEntitiesToThePoolOfEntitiesProvisionedWhenTipSessionsAreSuccessfullyCreated()
        {
            // Stage a couple of TiP sessions in the entity pool. The provider will add the TipSession entities
            // to the entity pool when the initial request is made.
            IEnumerable<EnvironmentEntity> expectedTipNodes = this.mockEntityPool.GetNodes().GroupBy(node => node.EnvironmentGroup)
                .Select(nodes => nodes.First());

            IEnumerable<EnvironmentEntity> expectedTipSessions = expectedTipNodes.Select(node => TipCreationProvider2Tests.CreateTipSessionEntityFrom(node));
            this.mockEntityPool.AddRange(expectedTipSessions);

            // First execution selects the TiP nodes. The second execution confirms the TiP node statuses.
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            IEnumerable<EnvironmentEntity> tipSessionsCreated = this.mockFixture.EntityManager.EntitiesProvisioned.GetTipSessions();
            IEnumerable<EnvironmentEntity> tipNodesUsed = this.mockFixture.EntityManager.EntitiesProvisioned.GetNodes();

            // The entities provisioned should contain the Node entites associated with the TiP sessions.
            Assert.IsNotNull(tipNodesUsed);
            Assert.IsTrue(tipNodesUsed.Count() == 2);
            CollectionAssert.AllItemsAreUnique(tipNodesUsed);
            CollectionAssert.AllItemsAreUnique(tipNodesUsed.Select(tipNode => tipNode.EnvironmentGroup));
            CollectionAssert.AllItemsAreUnique(tipNodesUsed.Select(tipNode => tipNode.TipSessionId()));

            // The TiP session IDs and node IDs should match
            CollectionAssert.AreEquivalent(tipSessionsCreated.Select(tip => tip.TipSessionId()), tipNodesUsed.Select(node => node.TipSessionId()));
            CollectionAssert.AreEquivalent(tipSessionsCreated.Select(tip => tip.NodeId()), tipNodesUsed.Select(node => node.Id));
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
        public async Task ProviderDoesNotAddTipSessionEntitiesOrRelatedNodesToThePoolOfEntitiesProvisionedWhenTipSessionCreationFails()
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

            IEnumerable<EnvironmentEntity> expectedTipSessions = expectedTipNodes.Select(node => TipCreationProvider2Tests.CreateTipSessionEntityFrom(node));
            this.mockEntityPool.AddRange(expectedTipSessions);

            // First execution selects the TiP nodes. The second execution confirms the TiP node statuses.
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            // The entities provisioned should contain the Node entites associated with the TiP sessions.
            CollectionAssert.IsEmpty(this.mockFixture.EntityManager.EntitiesProvisioned.GetTipSessions());
            CollectionAssert.IsEmpty(this.mockFixture.EntityManager.EntitiesProvisioned.GetNodes());
        }

        [Test]
        public async Task ProviderAttemptsToCreateTipSessionsSelectingOtherNodesWhenTheInitialAttemptsFail()
         {
            // Stage a couple of TiP sessions in the entity pool. The provider will add the TipSession entities
            // to the entity pool when the initial request is made.
            IEnumerable<EnvironmentEntity> originalTipNodesSelected = this.mockEntityPool.GetNodes().GroupBy(node => node.EnvironmentGroup)
                .Select(nodes => nodes.First());

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
                    if (newTipSessionsRequested > 2)
                    {
                        // The nodes referenced should be 2 brand new nodes.
                        Assert.IsFalse(parameters.CandidateNodesId.Intersect(originalTipNodesSelected.Select(tipNode => tipNode.Id)).Any());
                    }
                })
                .ReturnsAsync(() => TipCreationProvider2Tests.CreateTipNodeSessionChange());

            // First execution selects the TiP nodes. The second execution confirms the TiP node statuses.
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            Assert.IsTrue(newTipSessionsRequested == 2);
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
                .ReturnsAsync(true) // ...but subsequent TiP nodes that match the same affinity fail to get created (i.e. Node02, Node03)
                .ReturnsAsync(false); // 2nd TiP node is created successfully and within the same node affinity set (i.e. Node04).

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
                this.mockEntityPool.Take(1).Union(this.mockEntityPool.Skip(3).Take(1)).Select(node => node.Id),
                this.mockFixture.EntityManager.EntitiesProvisioned.GetNodes().Select(node => node.Id));
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
                .ReturnsAsync(() => TipCreationProvider2Tests.CreateTipNodeSessionChange());

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

            Assert.IsTrue(attempts == 6); // based on the setup of the mock entity pool
            Assert.IsTrue(result.Status == ExecutionStatus.Failed);
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
                .ReturnsAsync(true);

            int attempts = 0;
            this.mockTipClient.OnCreateTipSession()
                .Callback<TipParameters, CancellationToken>((parameters, token) => attempts++)
                .ReturnsAsync(() => TipCreationProvider2Tests.CreateTipNodeSessionChange());

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

            Assert.IsTrue(attempts == 6); // based on the setup of the mock entity pool
            Assert.IsTrue(result.Status == ExecutionStatus.Failed);
            Assert.IsNotNull(result.Error);
            Assert.IsInstanceOf<ProviderException>(result.Error);
            Assert.IsTrue((result.Error as ProviderException).Reason == ErrorReason.ExpectedEnvironmentEntitiesNotFound);
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
                .ReturnsAsync(true) // ...but subsequent TiP nodes that match the same affinity fail to get created (i.e. Node02, Node03, Node04)
                .ReturnsAsync(true)
                .ReturnsAsync(true)
                .ReturnsAsync(false) // TiP nodes that have a different matching affinity are created successfully at the end (i.e. Node05, Node06)
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
                .ReturnsAsync(TipCreationProvider2Tests.CreateTipNodeSessionChange());

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
            CollectionAssert.AreEquivalent(this.mockEntityPool.Take(1).Select(node => node.Id), tipNodeSessionsDeleted.Select(node => node.Id));
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
                .ReturnsAsync(true); // ...but subsequent TiP nodes that match the same affinity fail to get created (i.e. Node02, Node03, Node04)

            // Track which TiP sessions are deleted at the end.
            List<EnvironmentEntity> tipNodeSessionsDeleted = new List<EnvironmentEntity>();
            this.mockTipClient.OnDeleteTipSession()
                .Callback<string, CancellationToken>((tipSessionId, token) =>
                {
                    EnvironmentEntity tipNode = this.mockEntityPool.FirstOrDefault(node => node.TipSessionId() == tipSessionId);
                    Assert.IsNotNull(tipNode);
                    tipNodeSessionsDeleted.Add(tipNode);
                })
                .ReturnsAsync(TipCreationProvider2Tests.CreateTipNodeSessionChange());

            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            // Setup the step to timeout
            this.mockState.StepTimeout = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(1));
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            CollectionAssert.IsEmpty(this.mockFixture.EntityManager.EntitiesProvisioned);
            CollectionAssert.IsNotEmpty(tipNodeSessionsDeleted);
            Assert.IsTrue(tipNodeSessionsDeleted.Count == 1);
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

        private static EnvironmentEntity CreateTipSessionEntityFrom(EnvironmentEntity node)
        {
            node.TipSessionId(Guid.NewGuid().ToString());
            node.TipSessionRequestChangeId(Guid.NewGuid().ToString());
            node.TipSessionStatus(TipSessionStatus.Created.ToString());

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
                .ReturnsAsync(() => TipCreationProvider2Tests.CreateTipNodeSessionChange());

            this.mockTipClient.OnDeleteTipSession()
                .ReturnsAsync(TipCreationProvider2Tests.CreateTipNodeSessionChange());

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

                    confirmedTipSession = TipCreationProvider2Tests.CreateTipNodeSessionFrom(expectedTipNode);
                })
                .ReturnsAsync(() => confirmedTipSession);
        }
    }
}
