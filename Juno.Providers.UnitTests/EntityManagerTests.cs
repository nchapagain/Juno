namespace Juno.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Execution.Providers.Demo;
    using Microsoft.Azure.CRC;
    using Moq;
    using NUnit.Framework;
    using Polly;

    [TestFixture]
    [Category("Unit")]
    public class EntityManagerTests
    {
        private ProviderFixture mockFixture;
        private EntityManager entityManager;
        private IEnumerable<EnvironmentEntity> mockEntities;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new ProviderFixture(typeof(ExampleTipCreationProvider));
            this.mockFixture.SetupExperimentMocks();
            this.entityManager = new EntityManager(this.mockFixture.DataClient.Object);

            this.mockEntities = new List<EnvironmentEntity>
            {
                // We need to allow for SameRack, SameCluster, DifferentCluster scenarios.
                // SameRack
                EnvironmentEntity.Node("Cluster01-Rack01-Node01", "Group A", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster01",
                    ["RackLocation"] = "Rack01"
                }),
                EnvironmentEntity.Node("Cluster01-Rack01-Node02", "Group A", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster01",
                    ["RackLocation"] = "Rack01"
                }),
                EnvironmentEntity.Node("Cluster01-Rack01-Node03", "Group B", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster01",
                    ["RackLocation"] = "Rack01"
                }),
                EnvironmentEntity.Node("Cluster01-Rack01-Node04", "Group B", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster01",
                    ["RackLocation"] = "Rack01"
                }),

                // SameCluster
                EnvironmentEntity.Node("Cluster01-Rack02-Node05", "Group A", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster01",
                    ["RackLocation"] = "Rack02"
                }),
                EnvironmentEntity.Node("Cluster01-Rack02-Node06", "Group B", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster01",
                    ["RackLocation"] = "Rack02"
                }),

                 // DifferentCluster
                EnvironmentEntity.Node("Cluster02-Rack03-Node07", "Group A", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster02",
                    ["RackLocation"] = "Rack03"
                }),
                EnvironmentEntity.Node("Cluster01-Rack03-Node08", "Group B", new Dictionary<string, IConvertible>
                {
                    ["ClusterName"] = "Cluster02",
                    ["RackLocation"] = "Rack03"
                })
            };
        }

        [Test]
        public void EntityManagerConstructorsValidateRequiredParameters()
        {
            Assert.Throws<ArgumentException>(() => new EntityManager(null));
        }

        [Test]
        public void EntityManagerConstructorsSetPropertiesToExpectedValues()
        {
            EntityManager manager = new EntityManager(this.mockFixture.DataClient.Object);
            EqualityAssert.PropertySet(manager, "DataClient", this.mockFixture.DataClient.Object);
        }

        [Test]
        public async Task EntityManagerPreventsDuplicatesFromBeingAddedToTheEntityPoolCollection()
        {
            // The entity pool must be loaded before it can be operated on.
            this.mockFixture.DataClient.OnGetEntityPool()
               .ReturnsAsync(this.mockEntities);

            await this.entityManager.LoadEntityPoolAsync(this.mockFixture.ExperimentId, CancellationToken.None);

            // Now attempt to add the same entities again. None of these should be added/duplicated.
            this.mockEntities.ToList().ForEach(e => this.entityManager.EntityPool.Add(e));

            Assert.IsTrue(this.mockEntities.Count() == this.entityManager.EntityPool.Count);
            CollectionAssert.AreEquivalent(this.mockEntities.Select(e => e.GetHashCode()), this.entityManager.EntityPool.Select(e => e.GetHashCode()));
        }

        [Test]
        public async Task EntityManagerPreventsDuplicatesFromBeingAddedToTheEntitiesProvisionedCollection()
        {
            // The entity pool must be loaded before it can be operated on.
            this.mockFixture.DataClient.OnGetEntitiesProvisioned()
               .ReturnsAsync(this.mockEntities);

            await this.entityManager.LoadEntitiesProvisionedAsync(this.mockFixture.ExperimentId, CancellationToken.None);

            // Now attempt to add the same entities again. None of these should be added/duplicated.
            this.mockEntities.ToList().ForEach(e => this.entityManager.EntitiesProvisioned.Add(e));

            Assert.IsTrue(this.mockEntities.Count() == this.entityManager.EntitiesProvisioned.Count);
            CollectionAssert.AreEquivalent(this.mockEntities.Select(e => e.GetHashCode()), this.entityManager.EntitiesProvisioned.Select(e => e.GetHashCode()));
        }

        [Test]
        public async Task EntityManagerLoadsTheExpectedEntitiesForTheEntityPool()
        {
            this.mockFixture.DataClient.OnGetEntityPool()
                .Callback<string, string, CancellationToken, string>((experimentId, stateKey, token, stateId) =>
                {
                    Assert.AreEqual(this.mockFixture.Context.Experiment.Id, experimentId);
                    Assert.AreEqual("entityPool", stateKey);
                    Assert.IsNull(stateId); // saved in the global state object.
                })
                .ReturnsAsync(this.mockEntities);

            await this.entityManager.LoadEntityPoolAsync(this.mockFixture.ExperimentId, CancellationToken.None);

            CollectionAssert.AreEqual(this.mockEntities, this.entityManager.EntityPool);
        }

        [Test]
        public async Task EntityManagerEntityPoolIsNotAccessableUntilTheEntitiesAreLoaded()
        {
            // Until the entities are loaded, the entity pool is null/inaccessible
            Assert.IsNull(this.entityManager.EntityPool);

            this.mockFixture.DataClient.OnGetEntityPool()
                .ReturnsAsync(this.mockEntities);

            await this.entityManager.LoadEntityPoolAsync(this.mockFixture.ExperimentId, CancellationToken.None);
            Assert.IsNotEmpty(this.entityManager.EntityPool);
        }

        [Test]
        public async Task EntityManagerLoadsTheExpectedEntitiesForTheEntitiesProvisioned()
        {
            this.mockFixture.DataClient.OnGetEntitiesProvisioned()
                .Callback<string, string, CancellationToken, string>((experimentId, stateKey, token, stateId) =>
                {
                    Assert.AreEqual(this.mockFixture.Context.Experiment.Id, experimentId);
                    Assert.AreEqual("entitiesProvisioned", stateKey);
                    Assert.IsNull(stateId); // saved in the global state object.
                })
                .ReturnsAsync(this.mockEntities);

            await this.entityManager.LoadEntitiesProvisionedAsync(this.mockFixture.ExperimentId, CancellationToken.None);

            CollectionAssert.AreEqual(this.mockEntities, this.entityManager.EntitiesProvisioned);
        }

        [Test]
        public async Task EntityManagerEntitiesProvisionedAreNotAccessableUntilTheEntitiesAreLoaded()
        {
            // Until the entities are loaded, the entities provisioned /isare null/inaccessible
            Assert.IsNull(this.entityManager.EntitiesProvisioned);

            this.mockFixture.DataClient.OnGetEntitiesProvisioned()
                .ReturnsAsync(this.mockEntities);

            await this.entityManager.LoadEntitiesProvisionedAsync(this.mockFixture.ExperimentId, CancellationToken.None);
            Assert.IsNotEmpty(this.entityManager.EntitiesProvisioned);
        }

        [Test]
        public void EntityManagerThrowsWhenAttemptingToSaveTheEntityPoolBeforeItIsLoaded()
        {
            Assert.ThrowsAsync<DataException>(async () => await this.entityManager.SaveEntityPoolAsync(this.mockFixture.ExperimentId, CancellationToken.None));
        }

        [Test]
        public void EntityManagerThrowsWhenAttemptingToSaveTheEntitiesProvisionedBeforeTheyAreLoaded()
        {
            Assert.ThrowsAsync<DataException>(async () => await this.entityManager.SaveEntitiesProvisionedAsync(this.mockFixture.ExperimentId, CancellationToken.None));
        }

        [Test]
        public async Task EntityManagerSavesTheEntityPoolToTheExpectedLocationInTheBackingStore()
        {
            this.mockFixture.DataClient.OnGetEntityPool()
                .ReturnsAsync(this.mockEntities);

            this.mockFixture.DataClient.OnSaveState<IEnumerable<EnvironmentEntity>>()
                .Callback<string, string, IEnumerable<EnvironmentEntity>, CancellationToken, string>((experimentId, stateKey, state, token, stateId) =>
                {
                    Assert.AreEqual(this.mockFixture.ExperimentId, experimentId);
                    Assert.AreEqual("entityPool", stateKey);
                    Assert.IsNull(stateId); // global state, null = global/shared
                })
                .Returns(Task.CompletedTask);

            // Ensure the entity pool has been loaded. It cannot be operated on until it is initially loaded.
            await this.entityManager.LoadEntityPoolAsync(this.mockFixture.ExperimentId, CancellationToken.None);

            // Ensure a save/change is required.
            this.entityManager.EntityPool.Add(EnvironmentEntity.Node("AnyNode01", "Group A"));

            await this.entityManager.SaveEntityPoolAsync(this.mockFixture.ExperimentId, CancellationToken.None, Policy.NoOpAsync());
        }

        [Test]
        public async Task EntityManagerSavesTheEntitiesProvisionedToTheExpectedLocationInTheBackingStore()
        {
            this.mockFixture.DataClient.OnGetEntitiesProvisioned()
                .ReturnsAsync(this.mockEntities);

            this.mockFixture.DataClient.OnSaveState<IEnumerable<EnvironmentEntity>>()
                .Callback<string, string, IEnumerable<EnvironmentEntity>, CancellationToken, string>((experimentId, stateKey, state, token, stateId) =>
                {
                    Assert.AreEqual(this.mockFixture.ExperimentId, experimentId);
                    Assert.AreEqual("entityPool", stateKey);
                    Assert.IsNull(stateId); // global state, null = global/shared
                })
                .Returns(Task.CompletedTask);

            // Ensure the entity pool has been loaded. It cannot be operated on until it is initially loaded.
            await this.entityManager.LoadEntityPoolAsync(this.mockFixture.ExperimentId, CancellationToken.None);

            // Ensure a save/change is required.
            this.entityManager.EntityPool.Add(EnvironmentEntity.Node("AnyNode01", "Group A"));

            await this.entityManager.SaveEntityPoolAsync(this.mockFixture.ExperimentId, CancellationToken.None, Policy.NoOpAsync());
        }

        [Test]
        public async Task EntityManagerSavesTheExpectedEntitiesToTheEntityPoolInTheBackingStoreWhenNewEntitiesAreAdded()
        {
            // The existing entities are retrieved from the backing data store. Any new
            // entities in the entity manager will be merged into these before updating the
            // entire set in the backing store again.
            this.mockFixture.DataClient.OnGetEntityPool()
                .ReturnsAsync(this.mockEntities);

            List<EnvironmentEntity> newEntities = new List<EnvironmentEntity>
            {
                EnvironmentEntity.Node("AnyNode01", "Group A"),
                EnvironmentEntity.Node("AnyNode02", "Group B")
            };

            this.mockFixture.DataClient.OnSaveState<IEnumerable<EnvironmentEntity>>()
                .Callback<string, string, IEnumerable<EnvironmentEntity>, CancellationToken, string>((experimentId, stateKey, state, token, stateId) =>
                {
                    CollectionAssert.AreEquivalent(this.mockEntities.Union(newEntities).Select(e => e.GetHashCode()), state.Select(e => e.GetHashCode()));
                })
                .Returns(Task.CompletedTask);

            // Ensure the entity pool has been loaded. It cannot be operated on until it is initially loaded.
            await this.entityManager.LoadEntityPoolAsync(this.mockFixture.ExperimentId, CancellationToken.None);

            // Add new entities.
            newEntities.ForEach(e => this.entityManager.EntityPool.Add(e));

            await this.entityManager.SaveEntityPoolAsync(this.mockFixture.ExperimentId, CancellationToken.None, Policy.NoOpAsync());
        }

        [Test]
        public async Task EntityManagerSavesTheExpectedEntitiesToTheEntityPoolInTheBackingStoreWhenEntitiesAreRemoved()
        {
            // The existing entities are retrieved from the backing data store. Any entities
            // removed from the entity manager will be removed from the existing set in the backing store.
            this.mockFixture.DataClient.OnGetEntityPool()
                .ReturnsAsync(this.mockEntities);

            this.mockFixture.DataClient.OnSaveState<IEnumerable<EnvironmentEntity>>()
                .Callback<string, string, IEnumerable<EnvironmentEntity>, CancellationToken, string>((experimentId, stateKey, state, token, stateId) =>
                {
                    CollectionAssert.AreEquivalent(this.mockEntities.Skip(2).Select(e => e.GetHashCode()), state.Select(e => e.GetHashCode()));
                })
                .Returns(Task.CompletedTask);

            // Ensure the entity pool has been loaded. It cannot be operated on until it is initially loaded.
            await this.entityManager.LoadEntityPoolAsync(this.mockFixture.ExperimentId, CancellationToken.None);

            // remove entities.
            this.entityManager.EntityPool.Remove(this.entityManager.EntityPool.ElementAt(0));
            this.entityManager.EntityPool.Remove(this.entityManager.EntityPool.ElementAt(0));

            await this.entityManager.SaveEntityPoolAsync(this.mockFixture.ExperimentId, CancellationToken.None, Policy.NoOpAsync());
        }

        [Test]
        public async Task EntityManagerSavesTheExpectedEntitiesToTheEntityPoolInTheBackingStoreWhenEntitiesAreReplaced_Scenario1()
        {
            // The existing entities are retrieved from the backing data store.
            this.mockFixture.DataClient.OnGetEntityPool()
                .ReturnsAsync(this.mockEntities);

            // Ensure the entity pool has been loaded. It cannot be operated on until it is initially loaded.
            await this.entityManager.LoadEntityPoolAsync(this.mockFixture.ExperimentId, CancellationToken.None);

            // Replace entities.
            EnvironmentEntity entityToReplace1 = this.entityManager.EntityPool.ElementAt(0);
            EnvironmentEntity entityToReplace2 = this.entityManager.EntityPool.ElementAt(1);
            EnvironmentEntity replacement1 = new EnvironmentEntity(entityToReplace1.EntityType, entityToReplace1.Id, entityToReplace1.EnvironmentGroup);
            EnvironmentEntity replacement2 = new EnvironmentEntity(entityToReplace2.EntityType, entityToReplace2.Id, entityToReplace2.EnvironmentGroup);

            this.mockFixture.DataClient.OnSaveState<IEnumerable<EnvironmentEntity>>()
                .Callback<string, string, IEnumerable<EnvironmentEntity>, CancellationToken, string>((experimentId, stateKey, state, token, stateId) =>
                {
                    Assert.IsTrue(state.Contains(replacement1));
                    Assert.IsTrue(state.Contains(replacement2));
                    Assert.IsFalse(state.Contains(entityToReplace1));
                    Assert.IsFalse(state.Contains(entityToReplace2));
                })
                .Returns(Task.CompletedTask);

            // Scenario 1 involves removing the existing entities before adding the replacements.
            this.entityManager.EntityPool.Remove(entityToReplace1);
            this.entityManager.EntityPool.Remove(entityToReplace2);
            this.entityManager.EntityPool.Add(replacement1);
            this.entityManager.EntityPool.Add(replacement2);

            await this.entityManager.SaveEntityPoolAsync(this.mockFixture.ExperimentId, CancellationToken.None, Policy.NoOpAsync());
        }

        [Test]
        public async Task EntityManagerSavesTheExpectedEntitiesToTheEntityPoolInTheBackingStoreWhenEntitiesAreReplaced_Scenario2()
        {
            // The existing entities are retrieved from the backing data store.
            this.mockFixture.DataClient.OnGetEntityPool()
                .ReturnsAsync(this.mockEntities);

            // Ensure the entity pool has been loaded. It cannot be operated on until it is initially loaded.
            await this.entityManager.LoadEntityPoolAsync(this.mockFixture.ExperimentId, CancellationToken.None);

            // Replace entities.
            EnvironmentEntity entityToReplace1 = this.entityManager.EntityPool.ElementAt(0);
            EnvironmentEntity entityToReplace2 = this.entityManager.EntityPool.ElementAt(1);
            EnvironmentEntity replacement1 = new EnvironmentEntity(entityToReplace1.EntityType, entityToReplace1.Id, entityToReplace1.EnvironmentGroup);
            EnvironmentEntity replacement2 = new EnvironmentEntity(entityToReplace2.EntityType, entityToReplace2.Id, entityToReplace2.EnvironmentGroup);

            this.mockFixture.DataClient.OnSaveState<IEnumerable<EnvironmentEntity>>()
                .Callback<string, string, IEnumerable<EnvironmentEntity>, CancellationToken, string>((experimentId, stateKey, state, token, stateId) =>
                {
                    Assert.IsTrue(state.Contains(replacement1));
                    Assert.IsTrue(state.Contains(replacement2));
                    Assert.IsFalse(state.Contains(entityToReplace1));
                    Assert.IsFalse(state.Contains(entityToReplace2));
                })
                .Returns(Task.CompletedTask);

            // Scenario 2 involves adding the replacement entities before removing the the existing entities.
            this.entityManager.EntityPool.Add(replacement1);
            this.entityManager.EntityPool.Add(replacement2);
            this.entityManager.EntityPool.Remove(entityToReplace1);
            this.entityManager.EntityPool.Remove(entityToReplace2);

            await this.entityManager.SaveEntityPoolAsync(this.mockFixture.ExperimentId, CancellationToken.None, Policy.NoOpAsync());
        }

        [Test]
        public async Task EntityManagerSavesTheExpectedEntitiesToTheEntityPoolInTheBackingStoreWhenEntitiesHaveMetadataUpdates_Scenario1()
        {
            await this.SetupDefaultMockBehaviorsAsync();

            EnvironmentEntity updatedEntity1 = this.entityManager.EntityPool.ElementAt(0);
            EnvironmentEntity updatedEntity2 = this.entityManager.EntityPool.ElementAt(1);

            // Scenario 1 involves changing existing metadata but there aren't any metadata properties
            // that exist in the backing store entity that don't exist in the local entity.
            updatedEntity1.Metadata["ClusterName"] = "AnyNewCluster01";
            updatedEntity2.Metadata["RackLocation"] = "AnyNewRackLocation01";

            this.mockFixture.DataClient.OnSaveState<IEnumerable<EnvironmentEntity>>()
                .Callback<string, string, IEnumerable<EnvironmentEntity>, CancellationToken, string>((experimentId, stateKey, state, token, stateId) =>
                {
                    Assert.IsTrue(state.Contains(updatedEntity1));
                    Assert.IsTrue(state.Contains(updatedEntity2));
                    Assert.AreEqual("AnyNewCluster01", state.First(entity => entity.Equals(updatedEntity1)).Metadata["ClusterName"]);
                    Assert.AreEqual("AnyNewRackLocation01", state.First(entity => entity.Equals(updatedEntity2)).Metadata["RackLocation"]);
                })
                .Returns(Task.CompletedTask);

            await this.entityManager.SaveEntityPoolAsync(this.mockFixture.ExperimentId, CancellationToken.None, Policy.NoOpAsync());
        }

        [Test]
        public async Task EntityManagerSavesTheExpectedEntitiesToTheEntityPoolInTheBackingStoreWhenEntitiesHaveMetadataUpdates_Scenario2()
        {
            await this.SetupDefaultMockBehaviorsAsync();

            EnvironmentEntity updatedEntity1 = this.entityManager.EntityPool.ElementAt(0);
            EnvironmentEntity updatedEntity2 = this.entityManager.EntityPool.ElementAt(1);

            // Scenario 2 involves adding new metadata to the local entity that does not exist in
            // the backing store entity.
            updatedEntity1.Metadata.Add("NewMetadata", 123);
            updatedEntity2.Metadata.Add("NewMetadata", 456);

            this.mockFixture.DataClient.OnSaveState<IEnumerable<EnvironmentEntity>>()
                .Callback<string, string, IEnumerable<EnvironmentEntity>, CancellationToken, string>((experimentId, stateKey, state, token, stateId) =>
                {
                    Assert.IsTrue(state.Contains(updatedEntity1));
                    Assert.IsTrue(state.Contains(updatedEntity2));
                    Assert.AreEqual(123, state.First(entity => entity.Equals(updatedEntity1)).Metadata["NewMetadata"]);
                    Assert.AreEqual(456, state.First(entity => entity.Equals(updatedEntity2)).Metadata["NewMetadata"]);
                })
                .Returns(Task.CompletedTask);

            await this.entityManager.SaveEntityPoolAsync(this.mockFixture.ExperimentId, CancellationToken.None, Policy.NoOpAsync());
        }

        [Test]
        public async Task EntityManagerSavesTheExpectedEntitiesToTheEntityPoolInTheBackingStoreWhenEntitiesHaveMetadataUpdates_Scenario3()
        {
            await this.SetupDefaultMockBehaviorsAsync();

            EnvironmentEntity updatedEntity1 = this.entityManager.EntityPool.ElementAt(0);
            EnvironmentEntity updatedEntity2 = this.entityManager.EntityPool.ElementAt(1);

            // Scenario 3 involves adding new metadata to the local entity that was added by another process to
            // the backing store entity.
            updatedEntity1.Metadata.Remove("ClusterName");
            updatedEntity2.Metadata.Remove("ClusterName");

            this.mockFixture.DataClient.OnSaveState<IEnumerable<EnvironmentEntity>>()
                .Callback<string, string, IEnumerable<EnvironmentEntity>, CancellationToken, string>((experimentId, stateKey, state, token, stateId) =>
                {
                    Assert.IsTrue(state.Contains(updatedEntity1));
                    Assert.IsTrue(state.Contains(updatedEntity2));
                    Assert.IsTrue(state.First(entity => entity.Equals(updatedEntity1)).Metadata.ContainsKey("ClusterName"));
                    Assert.IsTrue(state.First(entity => entity.Equals(updatedEntity2)).Metadata.ContainsKey("ClusterName"));
                })
                .Returns(Task.CompletedTask);

            await this.entityManager.SaveEntityPoolAsync(this.mockFixture.ExperimentId, CancellationToken.None, Policy.NoOpAsync());
        }

        [Test]
        public async Task EntityManagerSavesTheExpectedEntitiesToTheEntitiesProvisionedInTheBackingStoreWhenNewEntitiesAreAdded()
        {
            // The existing entities are retrieved from the backing data store. Any new
            // entities in the entity manager will be merged into these before updating the
            // entire set in the backing store again.
            this.mockFixture.DataClient.OnGetEntitiesProvisioned()
                .ReturnsAsync(this.mockEntities);

            List<EnvironmentEntity> newEntities = new List<EnvironmentEntity>
            {
                EnvironmentEntity.Node("AnyNode01", "Group A"),
                EnvironmentEntity.Node("AnyNode02", "Group B")
            };

            this.mockFixture.DataClient.OnSaveState<IEnumerable<EnvironmentEntity>>()
                .Callback<string, string, IEnumerable<EnvironmentEntity>, CancellationToken, string>((experimentId, stateKey, state, token, stateId) =>
                {
                    CollectionAssert.AreEquivalent(this.mockEntities.Union(newEntities).Select(e => e.GetHashCode()), state.Select(e => e.GetHashCode()));
                })
                .Returns(Task.CompletedTask);

            // Ensure the entities provisioned have been loaded. They cannot be operated on until it is initially loaded.
            await this.entityManager.LoadEntitiesProvisionedAsync(this.mockFixture.ExperimentId, CancellationToken.None);

            // Add new entities.
            newEntities.ForEach(e => this.entityManager.EntitiesProvisioned.Add(e));

            await this.entityManager.SaveEntitiesProvisionedAsync(this.mockFixture.ExperimentId, CancellationToken.None, Policy.NoOpAsync());
        }

        [Test]
        public async Task EntityManagerSavesTheExpectedEntitiesToTheEntitiesProvisionedInTheBackingStoreWhenEntitiesAreRemoved()
        {
            // The existing entities are retrieved from the backing data store. Any entities
            // removed from the entity manager will be removed from the existing set in the backing store.
            this.mockFixture.DataClient.OnGetEntitiesProvisioned()
                .ReturnsAsync(this.mockEntities);

            this.mockFixture.DataClient.OnSaveState<IEnumerable<EnvironmentEntity>>()
                .Callback<string, string, IEnumerable<EnvironmentEntity>, CancellationToken, string>((experimentId, stateKey, state, token, stateId) =>
                {
                    CollectionAssert.AreEquivalent(this.mockEntities.Skip(2).Select(e => e.GetHashCode()), state.Select(e => e.GetHashCode()));
                })
                .Returns(Task.CompletedTask);

            // Ensure the entities provisioned have been loaded. They cannot be operated on until it is initially loaded.
            await this.entityManager.LoadEntitiesProvisionedAsync(this.mockFixture.ExperimentId, CancellationToken.None);

            // remove entities.
            this.entityManager.EntitiesProvisioned.Remove(this.entityManager.EntitiesProvisioned.ElementAt(0));
            this.entityManager.EntitiesProvisioned.Remove(this.entityManager.EntitiesProvisioned.ElementAt(0));

            await this.entityManager.SaveEntitiesProvisionedAsync(this.mockFixture.ExperimentId, CancellationToken.None, Policy.NoOpAsync());
        }

        [Test]
        public async Task EntityManagerSavesTheExpectedEntitiesToTheEntitiesProvisionedInTheBackingStoreWhenEntitiesAreReplaced_Scenario1()
        {
            await this.SetupDefaultMockBehaviorsAsync();

            // Replace entities.
            EnvironmentEntity entityToReplace1 = this.entityManager.EntitiesProvisioned.ElementAt(0);
            EnvironmentEntity entityToReplace2 = this.entityManager.EntitiesProvisioned.ElementAt(1);
            EnvironmentEntity replacement1 = new EnvironmentEntity(entityToReplace1.EntityType, entityToReplace1.Id, entityToReplace1.EnvironmentGroup);
            EnvironmentEntity replacement2 = new EnvironmentEntity(entityToReplace2.EntityType, entityToReplace2.Id, entityToReplace2.EnvironmentGroup);

            this.mockFixture.DataClient.OnSaveState<IEnumerable<EnvironmentEntity>>()
                .Callback<string, string, IEnumerable<EnvironmentEntity>, CancellationToken, string>((experimentId, stateKey, state, token, stateId) =>
                {
                    Assert.IsTrue(state.Contains(replacement1));
                    Assert.IsTrue(state.Contains(replacement2));
                    Assert.IsFalse(state.Contains(entityToReplace1));
                    Assert.IsFalse(state.Contains(entityToReplace2));
                })
                .Returns(Task.CompletedTask);

            // Scenario 1 involves removing the existing entities before adding the replacements.
            this.entityManager.EntitiesProvisioned.Remove(entityToReplace1);
            this.entityManager.EntitiesProvisioned.Remove(entityToReplace2);
            this.entityManager.EntitiesProvisioned.Add(replacement1);
            this.entityManager.EntitiesProvisioned.Add(replacement2);

            await this.entityManager.SaveEntitiesProvisionedAsync(this.mockFixture.ExperimentId, CancellationToken.None, Policy.NoOpAsync());
        }

        [Test]
        public async Task EntityManagerSavesTheExpectedEntitiesToTheEntitiesProvisionedInTheBackingStoreWhenEntitiesAreReplaced_Scenario2()
        {
            await this.SetupDefaultMockBehaviorsAsync();

            // Replace entities.
            EnvironmentEntity entityToReplace1 = this.entityManager.EntitiesProvisioned.ElementAt(0);
            EnvironmentEntity entityToReplace2 = this.entityManager.EntitiesProvisioned.ElementAt(1);
            EnvironmentEntity replacement1 = new EnvironmentEntity(entityToReplace1.EntityType, entityToReplace1.Id, entityToReplace1.EnvironmentGroup);
            EnvironmentEntity replacement2 = new EnvironmentEntity(entityToReplace2.EntityType, entityToReplace2.Id, entityToReplace2.EnvironmentGroup);

            this.mockFixture.DataClient.OnSaveState<IEnumerable<EnvironmentEntity>>()
                .Callback<string, string, IEnumerable<EnvironmentEntity>, CancellationToken, string>((experimentId, stateKey, state, token, stateId) =>
                {
                    Assert.IsTrue(state.Contains(replacement1));
                    Assert.IsTrue(state.Contains(replacement2));
                    Assert.IsFalse(state.Contains(entityToReplace1));
                    Assert.IsFalse(state.Contains(entityToReplace2));
                })
                .Returns(Task.CompletedTask);

            // Scenario 2 involves adding the replacement entities before removing the the existing entities.
            this.entityManager.EntitiesProvisioned.Add(replacement1);
            this.entityManager.EntitiesProvisioned.Add(replacement2);
            this.entityManager.EntitiesProvisioned.Remove(entityToReplace1);
            this.entityManager.EntitiesProvisioned.Remove(entityToReplace2);

            await this.entityManager.SaveEntitiesProvisionedAsync(this.mockFixture.ExperimentId, CancellationToken.None, Policy.NoOpAsync());
        }

        [Test]
        public async Task EntityManagerSavesTheExpectedEntitiesToTheEntitiesProvisionedInTheBackingStoreWhenEntitiesHaveMetadataUpdates_Scenario1()
        {
            await this.SetupDefaultMockBehaviorsAsync();

            EnvironmentEntity updatedEntity1 = this.entityManager.EntitiesProvisioned.ElementAt(0);
            EnvironmentEntity updatedEntity2 = this.entityManager.EntitiesProvisioned.ElementAt(1);

            // Scenario 1 involves changing existing metadata but there aren't any metadata properties
            // that exist in the backing store entity that don't exist in the local entity.
            updatedEntity1.Metadata["ClusterName"] = "AnyNewCluster01";
            updatedEntity2.Metadata["RackLocation"] = "AnyNewRackLocation01";

            this.mockFixture.DataClient.OnSaveState<IEnumerable<EnvironmentEntity>>()
                .Callback<string, string, IEnumerable<EnvironmentEntity>, CancellationToken, string>((experimentId, stateKey, state, token, stateId) =>
                {
                    Assert.IsTrue(state.Contains(updatedEntity1));
                    Assert.IsTrue(state.Contains(updatedEntity2));
                    Assert.AreEqual("AnyNewCluster01", state.First(entity => entity.Equals(updatedEntity1)).Metadata["ClusterName"]);
                    Assert.AreEqual("AnyNewRackLocation01", state.First(entity => entity.Equals(updatedEntity2)).Metadata["RackLocation"]);
                })
                .Returns(Task.CompletedTask);

            await this.entityManager.SaveEntitiesProvisionedAsync(this.mockFixture.ExperimentId, CancellationToken.None, Policy.NoOpAsync());
        }

        [Test]
        public async Task EntityManagerSavesTheExpectedEntitiesToTheEntitiesProvisionedInTheBackingStoreWhenEntitiesHaveMetadataUpdates_Scenario2()
        {
            await this.SetupDefaultMockBehaviorsAsync();

            EnvironmentEntity updatedEntity1 = this.entityManager.EntitiesProvisioned.ElementAt(0);
            EnvironmentEntity updatedEntity2 = this.entityManager.EntitiesProvisioned.ElementAt(1);

            // Scenario 2 involves adding new metadata to the local entity that does not exist in
            // the backing store entity.
            updatedEntity1.Metadata.Add("NewMetadata", 123);
            updatedEntity2.Metadata.Add("NewMetadata", 456);

            this.mockFixture.DataClient.OnSaveState<IEnumerable<EnvironmentEntity>>()
                .Callback<string, string, IEnumerable<EnvironmentEntity>, CancellationToken, string>((experimentId, stateKey, state, token, stateId) =>
                {
                    Assert.IsTrue(state.Contains(updatedEntity1));
                    Assert.IsTrue(state.Contains(updatedEntity2));
                    Assert.AreEqual(123, state.First(entity => entity.Equals(updatedEntity1)).Metadata["NewMetadata"]);
                    Assert.AreEqual(456, state.First(entity => entity.Equals(updatedEntity2)).Metadata["NewMetadata"]);
                })
                .Returns(Task.CompletedTask);

            await this.entityManager.SaveEntitiesProvisionedAsync(this.mockFixture.ExperimentId, CancellationToken.None, Policy.NoOpAsync());
        }

        [Test]
        public async Task EntityManagerSavesTheExpectedEntitiesToTheEntitiesProvisionedInTheBackingStoreWhenEntitiesHaveMetadataUpdates_Scenario3()
        {
            await this.SetupDefaultMockBehaviorsAsync();

            EnvironmentEntity updatedEntity1 = this.entityManager.EntitiesProvisioned.ElementAt(0);
            EnvironmentEntity updatedEntity2 = this.entityManager.EntitiesProvisioned.ElementAt(1);

            // Scenario 3 involves adding new metadata to the local entity that was added by another process to
            // the backing store entity.
            updatedEntity1.Metadata.Remove("ClusterName");
            updatedEntity2.Metadata.Remove("ClusterName");

            this.mockFixture.DataClient.OnSaveState<IEnumerable<EnvironmentEntity>>()
                .Callback<string, string, IEnumerable<EnvironmentEntity>, CancellationToken, string>((experimentId, stateKey, state, token, stateId) =>
                {
                    Assert.IsTrue(state.Contains(updatedEntity1));
                    Assert.IsTrue(state.Contains(updatedEntity2));
                    Assert.IsTrue(state.First(entity => entity.Equals(updatedEntity1)).Metadata.ContainsKey("ClusterName"));
                    Assert.IsTrue(state.First(entity => entity.Equals(updatedEntity2)).Metadata.ContainsKey("ClusterName"));
                })
                .Returns(Task.CompletedTask);

            await this.entityManager.SaveEntitiesProvisionedAsync(this.mockFixture.ExperimentId, CancellationToken.None, Policy.NoOpAsync());
        }

        private static IEnumerable<EnvironmentEntity> CloneEntities(IEnumerable<EnvironmentEntity> entities)
        {
            List<EnvironmentEntity> clones = new List<EnvironmentEntity>();
            foreach (EnvironmentEntity entity in entities)
            {
                clones.Add(new EnvironmentEntity(entity.EntityType, entity.Id, entity.EnvironmentGroup, entity.Metadata));
            }

            return clones;
        }

        private async Task SetupDefaultMockBehaviorsAsync()
        {
            // We want to imitate the behavior where changes where made to the local entity metadata while some other
            // process may have updated the entity metadata at the same time.
            this.mockFixture.DataClient.OnGetEntityPoolSequence()
                .ReturnsAsync(this.mockEntities) // on first load
                .ReturnsAsync(EntityManagerTests.CloneEntities(this.mockEntities)); // just before saving the entities to the backing store.

            this.mockFixture.DataClient.OnGetEntitiesProvisionedSequence()
                .ReturnsAsync(this.mockEntities) // on first load
                .ReturnsAsync(EntityManagerTests.CloneEntities(this.mockEntities)); // just before saving the entities to the backing store.

            // Ensure the entity pool has been loaded. It cannot be operated on until it is initially loaded.
            await this.entityManager.LoadEntityPoolAsync(this.mockFixture.ExperimentId, CancellationToken.None);

            // Ensure the entity pool has been loaded. It cannot be operated on until it is initially loaded.
            await this.entityManager.LoadEntitiesProvisionedAsync(this.mockFixture.ExperimentId, CancellationToken.None);
        }
    }
}
