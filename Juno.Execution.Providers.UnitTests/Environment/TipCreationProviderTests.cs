namespace Juno.Execution.Providers.Environment
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Juno.Execution.TipIntegration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;
    using TipGateway.Entities;

    [TestFixture]
    [Category("Unit")]
    public class TipCreationProviderTests
    {
        private ProviderFixture mockFixture;
        private Mock<ITipClient> mockTipClient;
        private Mock<IEnvironmentClient> mockEnvironmentClient;
        private List<TipRack> mockRacks;
        private List<TipSession> mockTipSessions;
        private List<EnvironmentEntity> mockNodes;
        private TestTipCreationProvider provider;
        private TipCreationProvider.State mockState;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new ProviderFixture(typeof(TipCreationProvider));
            this.mockFixture.SetupExperimentMocks(ExperimentType.AB);
            this.mockTipClient = new Mock<ITipClient>();
            this.mockEnvironmentClient = new Mock<IEnvironmentClient>();
            this.mockEnvironmentClient.SetReturnsDefault(new HttpResponseMessage(HttpStatusCode.OK));

            this.mockFixture.Services.AddSingleton(NullLogger.Instance);
            this.mockFixture.Services.AddSingleton(this.mockTipClient.Object);
            this.mockFixture.Services.AddSingleton(this.mockEnvironmentClient.Object);

            this.mockState = new TipCreationProvider.State
            {
                StepTimeout = DateTime.UtcNow.AddMinutes(10),
                Timeout = TimeSpan.FromMinutes(10),
                NodesAttempted = new List<string>()
            };

            this.mockRacks = new List<TipRack>
            {
                new TipRack()
                {
                    RackLocation = "rack1",
                    ClusterName = "cluster1",
                    CpuId = "cpu1",
                    Region = "region1",
                    RemainingTipSessions = 4,
                    PreferredVmSku = "sku-a",
                    SupportedVmSkus = new List<string>() { "sku-a" },
                    NodeList = new List<string>() { "node-a1", "node-a2", "node-a3", "node-a4" },
                    MachinePoolName = "Cluster01MP1"
                },
                new TipRack()
                {
                    RackLocation = "rack2",
                    ClusterName = "cluster2",
                    CpuId = "cpu1",
                    Region = "region2",
                    RemainingTipSessions = 4,
                    PreferredVmSku = "sku-a",
                    SupportedVmSkus = new List<string>() { "sku-a", "sku-b", "sku-c" },
                    NodeList = new List<string>() { "node-b1", "node-b2", "node-b3", "node-b4" },
                    MachinePoolName = "Cluster01MP1"
                }
            };

            this.mockTipSessions = new List<TipSession>
            {
                new TipSession()
                {
                    TipSessionId = "session-node-a1",
                    ClusterName = "cluster1",
                    Region = "region1",
                    GroupName = "Group A",
                    NodeId = "node-a1",
                    ChangeIdList = new List<string>() { "Change-node-a1" },
                    Status = TipSessionStatus.Creating,
                    CreatedTimeUtc = DateTime.UtcNow.Subtract(TimeSpan.FromDays(1)),
                    ExpirationTimeUtc = DateTime.UtcNow.Add(TimeSpan.FromDays(1)),
                    DeletedTimeUtc = DateTime.MaxValue,
                    SupportedVmSkus = new List<string>() { "sku-a" },
                    PreferredVmSku = "sku-a"
                },
                new TipSession()
                {
                    TipSessionId = "session-node-a2",
                    ClusterName = "cluster1",
                    Region = "region1",
                    GroupName = "Group B",
                    NodeId = "node-a2",
                    ChangeIdList = new List<string>() { "Change-node-a2" },
                    Status = TipSessionStatus.Creating,
                    CreatedTimeUtc = DateTime.UtcNow.Subtract(TimeSpan.FromDays(1)),
                    ExpirationTimeUtc = DateTime.UtcNow.Add(TimeSpan.FromDays(1)),
                    DeletedTimeUtc = DateTime.MaxValue,
                    SupportedVmSkus = new List<string>() { "sku-a", "sku-b", "sku-c" },
                    PreferredVmSku = "sku-a"
                }
            };
            this.mockNodes = new List<EnvironmentEntity>();
            this.mockNodes.Add(new EnvironmentEntity(EntityType.Node, "node-a1", "Group A"));
            this.mockNodes.Add(new EnvironmentEntity(EntityType.Node, "node-a2", "Group B"));

            this.provider = new TestTipCreationProvider(this.mockFixture.Services);
            this.SetupMockDefaults();
        }

        [Test]
        public void ProviderSelectsTheExpectedNodesAvailableFromTheRack()
        {
            List<string> rackNodes = new List<string>
            {
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString()
            };

            EnvironmentEntity rackEntity = EnvironmentEntity.Rack("AnyId", "Group A", new Dictionary<string, IConvertible>
            {
                ["NodeList"] = string.Join(";", rackNodes)
            });

            IEnumerable<string> availableNodes = TestTipCreationProvider.GetAvailableNodes(rackEntity, new List<string>());
            CollectionAssert.AreEquivalent(rackNodes, availableNodes);
        }

        [Test]
        public void ProviderSelectsTheExpectedNodesAvailableFromTheRackWhenSomeHaveAlreadyBeenPreviouslyAttempted()
        {
            List<string> rackNodes = new List<string>
            {
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString()
            };

            List<string> nodesAttempted = new List<string>(rackNodes.Take(2));

            EnvironmentEntity rackEntity = EnvironmentEntity.Rack("AnyId", "Group A", new Dictionary<string, IConvertible>
            {
                ["NodeList"] = string.Join(";", rackNodes)
            });

            IEnumerable<string> availableNodes = TestTipCreationProvider.GetAvailableNodes(rackEntity, nodesAttempted);
            CollectionAssert.AreEquivalent(rackNodes.Skip(2), availableNodes);
        }

        [Test]
        public void ProviderSelectsOnlyRacksThatHaveTheMinimumRequiredNodesAvailable()
        {
            List<string> rack1Nodes = new List<string>
            {
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString()
            };

            List<string> rack2Nodes = new List<string>
            {
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString()
            };

            // List<string> nodesAttempted = new List<string>(rackNodes.Take(2));

            List<EnvironmentEntity> rackEntities = new List<EnvironmentEntity>
            {
                EnvironmentEntity.Rack("Rack01", "Group A", new Dictionary<string, IConvertible>
                {
                    ["NodeList"] = string.Join(";", rack1Nodes)
                }),
                EnvironmentEntity.Rack("Rack02", "Group A", new Dictionary<string, IConvertible>
                {
                    ["NodeList"] = string.Join(";", rack2Nodes)
                })
            };

            this.mockFixture.DataClient.OnGetEntityPool().ReturnsAsync(rackEntities);

            IEnumerable<EnvironmentEntity> racksWithAvailableNodes = TestTipCreationProvider.GetRacksWithAvailableNodes(rackEntities, new List<string>(), 2);
            CollectionAssert.AreEquivalent(rackEntities.Select(rack => rack.Id), racksWithAvailableNodes.Select(rack => rack.Id));
        }

        [Test]
        public void ProviderExcludesRacksThatDoNotHaveTheMinimumRequiredNodesAvailable()
        {
            int minimumRequired = 2;
            List<string> rack1Nodes = new List<string>
            {
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString()
            };

            List<string> rack2Nodes = new List<string>
            {
                Guid.NewGuid().ToString(), // node attempted
                Guid.NewGuid().ToString(), // node attempted
                Guid.NewGuid().ToString() // only 1 left, this rack does not meet the minimum required
            };

            List<string> nodesAttempted = new List<string>(rack2Nodes.Take(2));

            List<EnvironmentEntity> rackEntities = new List<EnvironmentEntity>
            {
                EnvironmentEntity.Rack("Rack01", "Group A", new Dictionary<string, IConvertible>
                {
                    ["NodeList"] = string.Join(";", rack1Nodes)
                }),
                EnvironmentEntity.Rack("Rack02", "Group A", new Dictionary<string, IConvertible>
                {
                    ["NodeList"] = string.Join(";", rack2Nodes)
                })
            };

            this.mockFixture.DataClient.OnGetEntityPool().ReturnsAsync(rackEntities);

            IEnumerable<EnvironmentEntity> racksWithAvailableNodes = TestTipCreationProvider.GetRacksWithAvailableNodes(rackEntities, nodesAttempted, minimumRequired);
            CollectionAssert.AreEquivalent(rackEntities.Take(1).Select(rack => rack.Id), racksWithAvailableNodes.Select(rack => rack.Id));
        }

        [Test]
        public void TipCreationProviderRequestsTheExpectedTipSessionsOnFirstExecution()
        {
            int tipSessionsRequested = 0;

            this.mockTipClient.Setup(c => c.CreateTipSessionAsync(It.IsAny<TipParameters>(), It.IsAny<CancellationToken>()))
                .Callback<TipParameters, CancellationToken>((parameters, token) =>
                {
                    tipSessionsRequested++;
                    Assert.IsNotNull(this.mockState.TargetRack);
                    Assert.AreEqual(this.mockState.TargetRack.ClusterName(), parameters.ClusterName);
                    Assert.AreEqual(this.mockState.TargetRack.Region(), parameters.Region);
                    Assert.IsFalse(parameters.IsAmberNodeRequest);
                    Assert.AreEqual(1, parameters.NodeCount);
                    CollectionAssert.IsNotEmpty(parameters.CandidateNodesId);
                    CollectionAssert.IsNotEmpty(parameters.MachinePoolNames);
                    CollectionAssert.IsNotEmpty(this.mockState.TargetRack.NodeList().Intersect(parameters.CandidateNodesId));
                    Assert.AreEqual(this.mockState.TargetRack.MachinePoolName(), parameters.MachinePoolNames.First());
                })
                .ReturnsAsync((TipParameters parameters, CancellationToken token) => TipCreationProviderTests.CreateTipNodeSessionChangeDetail());
            this.provider.ConfigureServicesAsync(this.mockFixture.Context, this.mockFixture.Component);

            this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsTrue(tipSessionsRequested == this.mockFixture.Context.GetExperimentGroups().Count());
        }

        [Test]
        public void TipCreationProviderVerifiesTipSessionCreationRequestsAsExpected()
        {
            // Ensure a target rack has been selected
            this.mockState.TargetRack = TipRack.ToEnvironmentEntity(this.mockRacks.First());

            // Setup the TiP sessions in request
            this.mockTipSessions.ForEach(session => session.Status = TipSessionStatus.Created);
            this.mockFixture.DataClient.OnGetEntitiesProvisioned().ReturnsAsync(TipSession.ToEnvironmentEntities(this.mockTipSessions));

            // Each TiP session request is verified by making a call to the TiP API to get
            // the status of the request.
            int tipSessionsVerified = 0;
            this.mockTipClient.Setup(c => c.GetTipSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, CancellationToken>((tipSessionId, token) =>
                {
                    Assert.AreEqual(tipSessionId, this.mockTipSessions.ElementAt(tipSessionsVerified).TipSessionId);
                    tipSessionsVerified++;
                })
                .ReturnsAsync(TipCreationProviderTests.CreateTipNodeSessionFrom(this.mockTipSessions.First(), TipNodeSessionStatus.Created));
            this.provider.ConfigureServicesAsync(this.mockFixture.Context, this.mockFixture.Component);

            this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsTrue(tipSessionsVerified > 1);
        }

        [Test]
        public void TipCreationProviderAddsSuccessfullyCreatedTipSessionsToTheEntitiesProvisioned()
        {
            // Ensure a target rack has been selected
            this.mockState.TargetRack = TipRack.ToEnvironmentEntity(this.mockRacks.First());

            // Setup the TiP sessions in request
            this.mockTipSessions.ForEach(session => session.Status = TipSessionStatus.Created);
            this.mockFixture.DataClient.OnGetEntitiesProvisioned().ReturnsAsync(TipSession.ToEnvironmentEntities(this.mockTipSessions));

            // Each TiP session request is verified by making a call to the TiP API to get
            // the status of the request.
            this.mockTipClient.Setup(c => c.GetTipSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(TipCreationProviderTests.CreateTipNodeSessionFrom(this.mockTipSessions.First(), TipNodeSessionStatus.Created));

            // When a TiP session is verified created, it is added to the pool of entities provisioned.
            bool entitiesAdded = false;
            this.mockFixture.DataClient.OnUpdateEntitiesProvisioned()
                .Callback<string, string, IEnumerable<EnvironmentEntity>, CancellationToken, string>((experimentId, key, entities, token, stateId) =>
                {
                    entitiesAdded = true;
                    CollectionAssert.AreEquivalent(
                        this.mockTipSessions.Select(session => session.TipSessionId),
                        entities.Where(entity => entity.EntityType == EntityType.TipSession).Select(entity => entity.Id));
                })
                .Returns(Task.CompletedTask);
            this.provider.ConfigureServicesAsync(this.mockFixture.Context, this.mockFixture.Component);

            this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsTrue(entitiesAdded);
        }

        [Test]
        public void TipCreationProviderAddsCorrespondingNodeEntitiesToTheEntitiesProvisionedWhenTipSessionsAreSuccessfullyCreated()
        {
            // Ensure a target rack has been selected
            this.mockState.TargetRack = TipRack.ToEnvironmentEntity(this.mockRacks.First());

            // Setup the TiP sessions in request
            this.mockTipSessions.ForEach(session => session.Status = TipSessionStatus.Created);
            this.mockFixture.DataClient.OnGetEntitiesProvisioned().ReturnsAsync(TipSession.ToEnvironmentEntities(this.mockTipSessions));

            // Each TiP session request is verified by making a call to the TiP API to get
            // the status of the request.
            this.mockTipClient.Setup(c => c.GetTipSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(TipCreationProviderTests.CreateTipNodeSessionFrom(this.mockTipSessions.First(), TipNodeSessionStatus.Created));

            // When a TiP session is verified created, it is added to the pool of entities provisioned.
            bool entitiesAdded = false;
            this.mockFixture.DataClient.OnUpdateEntitiesProvisioned()
                .Callback<string, string, IEnumerable<EnvironmentEntity>, CancellationToken, string>((experimentId, key, entities, token, stateId) =>
                {
                    entitiesAdded = true;
                    CollectionAssert.AreEquivalent(
                        this.mockTipSessions.Select(session => session.TipSessionId),
                        entities.Where(entity => entity.EntityType == EntityType.Node).Select(entity => entity.TipSessionId()));

                    CollectionAssert.AreEquivalent(
                        this.mockTipSessions.Select(session => session.NodeId),
                        entities.Where(entity => entity.EntityType == EntityType.Node).Select(entity => entity.NodeId()));
                })
                .Returns(Task.CompletedTask);
            this.provider.ConfigureServicesAsync(this.mockFixture.Context, this.mockFixture.Component);

            this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsTrue(entitiesAdded);
        }

        [Test]
        public void TipCreationProviderRemovesTipSessionsOfFailedRequestsFromTheEntitiesProvisioned_SingleTipSessionScenario()
        {
            // Ensure a target rack has been selected
            this.mockState.TargetRack = TipRack.ToEnvironmentEntity(this.mockRacks.First());

            // One or more TiP requests have failed
            this.mockTipSessions.ForEach(session => session.Status = TipSessionStatus.Failed);
            this.mockFixture.DataClient.OnGetEntitiesProvisioned().ReturnsAsync(TipSession.ToEnvironmentEntities(this.mockTipSessions));

            // Setup the scenario where the TiP session request failed.
            this.mockTipClient.Setup(c => c.IsTipSessionChangeFailedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // When a TiP session request is failed, it is removed from the pool of entities provisioned.
            bool entitiesRemoved = false;
            this.mockFixture.DataClient.OnRemoveEntitiesProvisioned()
                .Callback<string, string, IEnumerable<EnvironmentEntity>, CancellationToken, string>((experimentId, key, entities, token, stateId) =>
                {
                    entitiesRemoved = true;
                    var entityType = entities.FirstOrDefault().EntityType;
                    if (entityType == EntityType.TipSession)
                    {
                        CollectionAssert.AreEquivalent(
                       this.mockTipSessions.Select(session => session.TipSessionId),
                       entities.Where(entity => entity.EntityType == EntityType.TipSession).Select(entity => entity.Id));
                    }
                    else if (entityType == EntityType.Node)
                    {
                       CollectionAssert.AreEquivalent(
                       this.mockNodes.Select(node => node.Id),
                       entities.Where(entity => entity.EntityType == EntityType.Node).Select(entity => entity.Id));
                    }
                })
                .Returns(Task.CompletedTask);
            this.provider.ConfigureServicesAsync(this.mockFixture.Context, this.mockFixture.Component);

            this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsTrue(entitiesRemoved);
        }

        [Test]
        public void TipCreationProviderRemovesTipSessionsOfFailedRequestsFromTheEntitiesProvisioned_TargetRackChangeScenario()
        {
            // Ensure the target rack initially selected does not have enough remaining/available nodes
            // to satisfy the "same rack" requirement.
            TipRack targetRack = this.mockRacks.First();
            targetRack.NodeList.Clear();
            this.mockState.TargetRack = TipRack.ToEnvironmentEntity(targetRack);

            // One or more TiP requests have failed
            this.mockTipSessions.ForEach(session => session.Status = TipSessionStatus.Failed);
            this.mockFixture.DataClient.OnGetEntitiesProvisioned().ReturnsAsync(TipSession.ToEnvironmentEntities(this.mockTipSessions));

            // Setup the scenario where the TiP session request failed.
            this.mockTipClient.Setup(c => c.IsTipSessionChangeFailedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // When a TiP session request is failed, it is removed from the pool of entities provisioned.
            bool entitiesRemoved = false;
            this.mockFixture.DataClient.OnRemoveEntitiesProvisioned()
                .Callback<string, string, IEnumerable<EnvironmentEntity>, CancellationToken, string>((experimentId, key, entities, token, stateId) =>
                {
                    entitiesRemoved = true;
                    CollectionAssert.AreEquivalent(
                        this.mockTipSessions.Select(session => session.TipSessionId),
                        entities.Where(entity => entity.EntityType == EntityType.TipSession).Select(entity => entity.Id));
                })
                .Returns(Task.CompletedTask);
            this.provider.ConfigureServicesAsync(this.mockFixture.Context, this.mockFixture.Component);

            this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsTrue(entitiesRemoved);
        }

        [Test]
        public void TipCreationProviderChoosesANewRackWhenTipSessionRequestsFailAndThereAreNotEnoughAvailableNodesRemainingOnTheInitialRack()
        {
            // Ensure the target rack initially selected does not have enough remaining/available nodes
            // to satisfy the "same rack" requirement.
            TipRack targetRack = this.mockRacks.First();
            targetRack.NodeList.Clear();
            this.mockState.TargetRack = TipRack.ToEnvironmentEntity(targetRack);

            // One or more TiP requests have failed
            this.mockTipSessions.ForEach(session => session.Status = TipSessionStatus.Failed);
            this.mockFixture.DataClient.OnGetEntitiesProvisioned().ReturnsAsync(TipSession.ToEnvironmentEntities(this.mockTipSessions));

            // Setup the scenario where the TiP session request failed.
            this.mockTipClient.Setup(c => c.IsTipSessionChangeFailedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            this.provider.ConfigureServicesAsync(this.mockFixture.Context, this.mockFixture.Component);

            // First execution resets the state so that the rack selection process will happen again.
            this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None)
                .GetAwaiter().GetResult();

            // Target rack is reset
            Assert.IsNull(this.mockState.TargetRack);
        }

        [Test]
        public void TipCreationProviderDeletesAllPreviouslySucceededTipRequestsWhenItMustSelectANewRack()
        {
            // Ensure the target rack initially selected does not have enough remaining/available nodes
            // to satisfy the "same rack" requirement.
            TipRack targetRack = this.mockRacks.First();
            targetRack.NodeList.Clear();
            this.mockState.TargetRack = TipRack.ToEnvironmentEntity(targetRack);

            // One or more TiP requests have failed
            this.mockTipSessions.ForEach(session => session.Status = TipSessionStatus.Created);
            this.mockTipSessions.Last().Status = TipSessionStatus.Failed;
            this.mockFixture.DataClient.OnGetEntitiesProvisioned().ReturnsAsync(TipSession.ToEnvironmentEntities(this.mockTipSessions));

            this.mockTipClient.Setup(c => c.GetTipSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(TipCreationProviderTests.CreateTipNodeSessionFrom(this.mockTipSessions.First(), TipNodeSessionStatus.Created));

            // Setup the scenario where the TiP session request failed.
            this.mockTipClient.SetupSequence(c => c.IsTipSessionChangeFailedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false)
                .ReturnsAsync(true);
            this.provider.ConfigureServicesAsync(this.mockFixture.Context, this.mockFixture.Component);

            // First execution resets the state so that the rack selection process will happen again.
            this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None)
                .GetAwaiter().GetResult();

            // Should have deleted the TiP session with the status = Created
            this.mockTipClient.Verify(c => c.DeleteTipSessionAsync(this.mockTipSessions.First().TipSessionId, It.IsAny<CancellationToken>()));
        }

        [Test]
        public void TipCreationProviderWillExhaustAllNodesForAllRacksToTryAndSuccessfullyCreateTipSessions()
        {
            // This hashset will not allow duplicate entries.
            HashSet<string> nodesAttempted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // One or more TiP requests have failed
            this.mockTipSessions.ForEach(session => session.Status = TipSessionStatus.Failed);
            this.mockFixture.DataClient.OnGetEntitiesProvisioned().ReturnsAsync(TipSession.ToEnvironmentEntities(this.mockTipSessions));

            this.mockTipClient.Setup(c => c.CreateTipSessionAsync(It.IsAny<TipParameters>(), It.IsAny<CancellationToken>()))
                .Callback<TipParameters, CancellationToken>((parameters, token) =>
                {
                    parameters.CandidateNodesId.ForEach(node => nodesAttempted.Add(node));
                })
                .ReturnsAsync((TipParameters parameters, CancellationToken token) => TipCreationProviderTests.CreateTipNodeSessionChangeDetail());

            this.mockTipClient.Setup(c => c.GetTipSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(TipCreationProviderTests.CreateTipNodeSessionFrom(this.mockTipSessions.First(), TipNodeSessionStatus.Creating));

            // Setup the scenario where the TiP session request failed.
            this.mockTipClient.Setup(c => c.IsTipSessionChangeFailedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            this.provider.ConfigureServicesAsync(this.mockFixture.Context, this.mockFixture.Component);

            for (int i = 0; i < this.mockRacks.Sum(rack => rack.NodeList.Count); i++)
            {
                this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }

            Assert.AreEqual(nodesAttempted.Count, this.mockRacks.Sum(rack => rack.NodeList.Count));
            CollectionAssert.AreEquivalent(nodesAttempted, this.mockRacks.SelectMany(rack => rack.NodeList));
        }

        [Test]
        public void TipCreationProviderReturnsTheExpectedResponseWhileWorkingToGetTipSessionsCreated()
        {
            this.provider.ConfigureServicesAsync(this.mockFixture.Context, this.mockFixture.Component);

            ExecutionResult result = this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.InProgress);
        }

        [Test]
        public void TipCreationProviderReturnsTheExpectedResponseWhenAllExpectedTipSessionsAreCreated()
        {
            this.mockState.TargetRack = TipRack.ToEnvironmentEntity(this.mockRacks.First());

            // All TiP sessions are successfully created
            this.mockTipSessions.ForEach(session => session.Status = TipSessionStatus.Created);
            this.mockFixture.DataClient.OnGetEntitiesProvisioned().ReturnsAsync(TipSession.ToEnvironmentEntities(this.mockTipSessions));

            this.mockTipClient.Setup(c => c.GetTipSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(TipCreationProviderTests.CreateTipNodeSessionFrom(this.mockTipSessions.First(), TipNodeSessionStatus.Created));
            this.provider.ConfigureServicesAsync(this.mockFixture.Context, this.mockFixture.Component);

            ExecutionResult result = this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.Succeeded);
        }

        [Test]
        public void TipCreationProviderThrowsIfTheStepIsTimedOutBeforeTheTipSessionsAreSuccessfullyCreated()
        {
            this.mockState.StepTimeout = DateTime.UtcNow.AddMinutes(-30);
            this.mockState.TargetRack = TipRack.ToEnvironmentEntity(this.mockRacks.First());

            // All TiP sessions are successfully created
            this.mockTipSessions.ForEach(session => session.Status = TipSessionStatus.Creating);
            this.mockFixture.DataClient.OnGetEntitiesProvisioned().ReturnsAsync(TipSession.ToEnvironmentEntities(this.mockTipSessions));

            this.mockTipClient.Setup(c => c.GetTipSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(TipCreationProviderTests.CreateTipNodeSessionFrom(this.mockTipSessions.First(), TipNodeSessionStatus.Creating));
            this.provider.ConfigureServicesAsync(this.mockFixture.Context, this.mockFixture.Component);

            ExecutionResult result = this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.Failed);
            Assert.IsNotNull(result.Error);
            Assert.IsInstanceOf<TimeoutException>(result.Error);
        }

        private static TipNodeSession CreateTipNodeSessionFrom(TipSession tipSession, TipNodeSessionStatus status = TipNodeSessionStatus.Created)
        {
            return new TipNodeSession
            {
                Cluster = tipSession.ClusterName,
                CreatedTimeUtc = DateTime.UtcNow.AddMinutes(-1),
                NodeCount = 1,
                Id = tipSession.NodeId,
                Region = tipSession.Region,
                Status = status
            };
        }

        private static TipNodeSessionChange CreateTipNodeSessionChange(Guid? tipSessionId = null, Guid? tipSessionChangeId = null)
        {
            return new TipNodeSessionChange()
            {
                TipNodeSessionId = tipSessionId?.ToString() ?? Guid.NewGuid().ToString(),
                TipNodeSessionChangeId = tipSessionChangeId?.ToString() ?? Guid.NewGuid().ToString(),
                Status = TipNodeSessionChangeStatus.Queued
            };
        }

        private static TipNodeSessionChangeDetails CreateTipNodeSessionChangeDetail(Guid? tipSessionId = null, Guid? tipSessionChangeId = null)
        {
            return new TipNodeSessionChangeDetails()
            {
                TipNodeSessionId = tipSessionId?.ToString() ?? Guid.NewGuid().ToString(),
                TipNodeSessionChangeId = tipSessionChangeId?.ToString() ?? Guid.NewGuid().ToString(),
                Status = TipNodeSessionChangeStatus.Queued
            };
        }

        private static IEnumerable<TipSession> CreateTipSessions(TipSessionStatus status)
        {
            List<TipSession> sessions = new List<TipSession>();
            sessions.Add(new TipSession()
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
                DeletedTimeUtc = DateTime.MaxValue,
                SupportedVmSkus = new List<string>() { "sku2-a", "sku2-b" },
                PreferredVmSku = "sku2-a"
            });

            sessions.Add(new TipSession()
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
                DeletedTimeUtc = DateTime.MaxValue,
                SupportedVmSkus = new List<string>() { "sku2-a", "sku2-b", "sku3-a" },
                PreferredVmSku = "sku2-a"
            });

            return sessions;
        }

        private void SetupMockDefaults()
        {
            this.mockFixture.DataClient.OnGetState<TipCreationProvider.State>().ReturnsAsync(this.mockState);
            this.mockFixture.DataClient.OnGetEntityPool().ReturnsAsync(TipRack.ToEnvironmentEntities(this.mockRacks.ToList()));
            this.mockFixture.DataClient.OnGetEntitiesProvisioned().ReturnsAsync(new List<EnvironmentEntity>());
            this.mockFixture.DataClient.OnRemoveEntitiesProvisioned().Returns(Task.CompletedTask);
            this.mockFixture.DataClient.OnUpdateEntitiesProvisioned().Returns(Task.CompletedTask);

            // TiP session creation request default
            this.mockTipClient.Setup(c => c.CreateTipSessionAsync(It.IsAny<TipParameters>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((TipParameters parameters, CancellationToken token) => TipCreationProviderTests.CreateTipNodeSessionChangeDetail());

            this.mockTipClient.Setup(c => c.DeleteTipSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(TipCreationProviderTests.CreateTipNodeSessionChange());

            // TiP session creation requests are confirmed succeeded by default.
            this.mockTipClient.Setup(c => c.IsTipSessionChangeFailedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
        }

        private class TestTipCreationProvider : TipCreationProvider
        {
            public TestTipCreationProvider(IServiceCollection services)
                : base(services)
            {
            }

            public static new IEnumerable<EnvironmentEntity> GetRacksWithAvailableNodes(IEnumerable<EnvironmentEntity> rackEntities, IEnumerable<string> nodesAttempted, int minimumAvailableNodes)
            {
                return TipCreationProvider.GetRacksWithAvailableNodes(rackEntities, nodesAttempted, minimumAvailableNodes);
            }

            public static new IEnumerable<string> GetAvailableNodes(EnvironmentEntity rackEntity, IEnumerable<string> nodesAttempted)
            {
                return TipCreationProvider.GetAvailableNodes(rackEntity, nodesAttempted);
            }
        }
    }
}
