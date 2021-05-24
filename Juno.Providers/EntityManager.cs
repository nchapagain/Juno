namespace Juno.Providers
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.Data;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Polly;

    /// <summary>
    /// Provides the functionality required to manage experiment entities
    /// (e.g. entity pool, entities provisioned).
    /// </summary>
    public class EntityManager
    {
        private static readonly SemaphoreSlim SemaphoreLock = new SemaphoreSlim(1, 1);
        private static readonly Random RandomGen = new Random();

        private static readonly IAsyncPolicy RetryPolicy = Policy.WrapAsync(
            Policy.Handle<Exception>().WaitAndRetryAsync(retryCount: 10, (retries) =>
            {
                return TimeSpan.FromSeconds(retries + 10);
            }),
            Policy.Handle<ProviderException>(exc => exc.Reason == ErrorReason.DataAlreadyExists || exc.Reason == ErrorReason.DataETagMismatch)
            .WaitAndRetryAsync(retryCount: 100, (retries) =>
            {
                // Some amount of randomization in the retry wait time/delta backoff helps address failures
                // related to data conflict/eTag mismatch issues with state/context object updates.
                return TimeSpan.FromSeconds(retries + EntityManager.RandomGen.Next(2, 10));
            }));

        private ConcurrentDictionary<EnvironmentEntity, EntityState> entityPoolChanges;
        private ConcurrentDictionary<EnvironmentEntity, EntityState> entitiesProvisionedChanges;

        /// <summary>
        /// Initializes a new instance of the <see cref="EntityManager"/> class.
        /// </summary>
        /// <param name="dataClient">A client for interacting with Juno system APIs.</param>
        public EntityManager(IProviderDataClient dataClient)
        {
            dataClient.ThrowIfNull(nameof(dataClient));
            this.DataClient = dataClient;

            this.entityPoolChanges = new ConcurrentDictionary<EnvironmentEntity, EntityState>();
            this.entitiesProvisionedChanges = new ConcurrentDictionary<EnvironmentEntity, EntityState>();
        }

        private enum EntityState
        {
            Added,
            Deleted
        }

        /// <summary>
        /// The set of entities available for use in the experiment.
        /// </summary>
        public ObservableHashSet<EnvironmentEntity> EntityPool { get; private set; }

        /// <summary>
        /// The set of entities that have been provisioned (created, in use) for the experiment.
        /// </summary>
        public ObservableHashSet<EnvironmentEntity> EntitiesProvisioned { get; private set; }

        /// <summary>
        /// Client provides methods for interacting with the API services to get
        /// entity sets.
        /// </summary>
        protected IProviderDataClient DataClient { get; }

        /// <summary>
        /// Loads the entity pool instances/objects for the experiment.
        /// </summary>
        /// <param name="experimentId">The ID of the experiment with which the entities are associated.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        public async Task LoadEntityPoolAsync(string experimentId, CancellationToken cancellationToken)
        {
            await EntityManager.SemaphoreLock.WaitAsync().ConfigureDefaults();

            try
            {
                IEnumerable<EnvironmentEntity> entityPool = await this.DataClient.GetOrCreateStateAsync<IEnumerable<EnvironmentEntity>>(
                    experimentId,
                    ContractExtension.EntityPool,
                    cancellationToken).ConfigureDefaults();

                ObservableHashSet<EnvironmentEntity> refreshedEntityPool = new ObservableHashSet<EnvironmentEntity>();

                if (entityPool?.Any() == true)
                {
                    entityPool.ToList().ForEach(e => refreshedEntityPool.Add(e));
                }

                this.EntityPool = refreshedEntityPool;
                this.EntityPool.CollectionChanged += this.OnEntityPoolCollectionChanged;
            }
            finally
            {
                EntityManager.SemaphoreLock.Release();
            }
        }

        /// <summary>
        /// Loads the entities provisioned instances/objects for the experiment.
        /// </summary>
        /// <param name="experimentId">The ID of the experiment with which the entities are associated.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        public async Task LoadEntitiesProvisionedAsync(string experimentId, CancellationToken cancellationToken)
        {
            await EntityManager.SemaphoreLock.WaitAsync().ConfigureDefaults();

            try
            {
                IEnumerable<EnvironmentEntity> entitiesProvisioned = await this.DataClient.GetOrCreateStateAsync<IEnumerable<EnvironmentEntity>>(
                    experimentId,
                    ContractExtension.EntitiesProvisioned,
                    cancellationToken).ConfigureDefaults();

                ObservableHashSet<EnvironmentEntity> refreshedEntitiesProvisioned = new ObservableHashSet<EnvironmentEntity>();

                if (entitiesProvisioned?.Any() == true)
                {
                    entitiesProvisioned.ToList().ForEach(e => refreshedEntitiesProvisioned.Add(e));
                }

                this.EntitiesProvisioned = refreshedEntitiesProvisioned;
                this.EntitiesProvisioned.CollectionChanged += this.OnEntitiesProvisionedCollectionChanged;
            }
            finally
            {
                EntityManager.SemaphoreLock.Release();
            }
        }

        /// <summary>
        /// Saves the entity pool instances/objects for the experiment to the backing store
        /// adding/merging in any new entries defined in the entity manager.
        /// </summary>
        /// <param name="experimentId">The ID of the experiment with which the entities are associated.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="retryPolicy">
        /// An optional retry policy that can be used to override the default policy when saving the entities to the backing store.
        /// </param>
        public async Task SaveEntityPoolAsync(string experimentId, CancellationToken cancellationToken, IAsyncPolicy retryPolicy = null)
        {
            if (this.EntityPool == null)
            {
                throw new DataException(
                   "The entity pool set is not loaded. The entity set must be loaded before any operations are executed against the it.");
            }

            await EntityManager.SemaphoreLock.WaitAsync().ConfigureDefaults();

            try
            {
                await (retryPolicy ?? EntityManager.RetryPolicy).ExecuteAsync(async () =>
                {
                    IEnumerable<EnvironmentEntity> existingEntityPool = await this.DataClient.GetOrCreateStateAsync<IEnumerable<EnvironmentEntity>>(
                        experimentId,
                        ContractExtension.EntityPool,
                        cancellationToken).ConfigureDefaults();

                    HashSet<EnvironmentEntity> updatedEntityPool = new HashSet<EnvironmentEntity>();
                    if (existingEntityPool?.Any() == true)
                    {
                        existingEntityPool.ToList().ForEach(e => updatedEntityPool.Add(e));
                    }

                    EntityManager.AddEntities(
                        updatedEntityPool,
                        this.entityPoolChanges.Where(entry => entry.Value == EntityState.Added)?.Select(entry => entry.Key));

                    EntityManager.RemoveEntities(
                        updatedEntityPool,
                        this.entityPoolChanges.Where(entry => entry.Value == EntityState.Deleted)?.Select(entry => entry.Key));

                    EntityManager.ConvergeEntities(updatedEntityPool, this.EntityPool);

                    await this.DataClient.SaveStateAsync(
                        experimentId,
                        ContractExtension.EntityPool,
                        updatedEntityPool.OrderBy(entity => entity.EntityType.ToString()).ThenBy(entity => entity.EnvironmentGroup),
                        cancellationToken,
                        null).ConfigureDefaults();

                    this.entityPoolChanges.Clear();
                }).ConfigureDefaults();
            }
            finally
            {
                EntityManager.SemaphoreLock.Release();
            }
        }

        /// <summary>
        /// Saves the entities provisioned instances/objects for the experiment to the backing store
        /// adding/merging in any new entries defined in the entity manager.
        /// </summary>
        /// <param name="experimentId">The ID of the experiment with which the entities are associated.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="retryPolicy">
        /// An optional retry policy that can be used to override the default policy when saving the entities to the backing store.
        /// </param>
        public async Task SaveEntitiesProvisionedAsync(string experimentId, CancellationToken cancellationToken, IAsyncPolicy retryPolicy = null)
        {
            if (this.EntitiesProvisioned == null)
            {
                throw new DataException(
                    "The entities provisioned set is not loaded. The entity set must be loaded before any operations are executed against the it.");
            }

            await EntityManager.SemaphoreLock.WaitAsync().ConfigureDefaults();

            try
            {
                await (retryPolicy ?? EntityManager.RetryPolicy).ExecuteAsync(async () =>
                {
                    IEnumerable<EnvironmentEntity> existingEntitiesProvisioned = await this.DataClient.GetOrCreateStateAsync<IEnumerable<EnvironmentEntity>>(
                        experimentId,
                        ContractExtension.EntitiesProvisioned,
                        cancellationToken).ConfigureDefaults();

                    HashSet<EnvironmentEntity> updatedEntitiesProvisioned = new HashSet<EnvironmentEntity>();
                    if (existingEntitiesProvisioned?.Any() == true)
                    {
                        existingEntitiesProvisioned.ToList().ForEach(e => updatedEntitiesProvisioned.Add(e));
                    }

                    EntityManager.AddEntities(
                        updatedEntitiesProvisioned,
                        this.entitiesProvisionedChanges.Where(entry => entry.Value == EntityState.Added)?.Select(entry => entry.Key));

                    EntityManager.RemoveEntities(
                        updatedEntitiesProvisioned,
                        this.entitiesProvisionedChanges.Where(entry => entry.Value == EntityState.Deleted)?.Select(entry => entry.Key));

                    EntityManager.ConvergeEntities(updatedEntitiesProvisioned, this.EntitiesProvisioned);

                    await this.DataClient.SaveStateAsync(
                        experimentId,
                        ContractExtension.EntitiesProvisioned,
                        updatedEntitiesProvisioned.OrderBy(entity => entity.EntityType.ToString()).ThenBy(entity => entity.EnvironmentGroup),
                        cancellationToken,
                        null).ConfigureDefaults();

                    this.entitiesProvisionedChanges.Clear();
                }).ConfigureDefaults();
            }
            finally
            {
                EntityManager.SemaphoreLock.Release();
            }
        }

        private static void AddEntities(HashSet<EnvironmentEntity> existingEntities, IEnumerable<EnvironmentEntity> entitiesToAdd)
        {
            // The entity manager tracks entities that are explicitly added. This makes it easier to determine new
            // entities that should be added to the collection in the backing store.

            if (entitiesToAdd?.Any() == true)
            {
                // A HashSet is a special collection that will check the hash code for an object
                // before adding it. Thus it provides a natural deduplication mechanic by preventing
                // duplicates in the first place.
                entitiesToAdd.ToList().ForEach(e => existingEntities.Add(e));
            }
        }

        private static void ConvergeEntities(HashSet<EnvironmentEntity> existingEntities, IEnumerable<EnvironmentEntity> changedEntities)
        {
            // Some changes that are made to the entity pool are not explicit adds or removes. They are changes to metadata properties.
            // We want to update those entities in the backing store as well.
            List<Tuple<EnvironmentEntity, EnvironmentEntity>> entitesToConverge = new List<Tuple<EnvironmentEntity, EnvironmentEntity>>();
            if (existingEntities?.Any() == true && changedEntities?.Any() == true)
            {
                StringComparison ignoreCase = StringComparison.OrdinalIgnoreCase;
                foreach (EnvironmentEntity entity in existingEntities)
                {
                    // The entities have the same ID and environment group but are not equal. This means that the metadata values
                    // have been changed.
                    EnvironmentEntity matchingEntity = changedEntities.FirstOrDefault(e => e.Id.Equals(entity.Id, ignoreCase)
                        && e.EnvironmentGroup.Equals(entity.EnvironmentGroup, ignoreCase));

                    if (matchingEntity != null && matchingEntity.Equals(entity) != true)
                    {
                        // The metadata does not match
                        entitesToConverge.Add(new Tuple<EnvironmentEntity, EnvironmentEntity>(entity, matchingEntity));
                    }
                }
            }

            if (entitesToConverge.Any())
            {
                // We want to ensure that any metadata that exists in the original entity from the backing store
                // but that does NOT exist in the changed entity locally is added to the local entity. For metadata
                // that exists between both the original entity and the local entity, the local entity value will take
                // precedence.
                foreach (Tuple<EnvironmentEntity, EnvironmentEntity> entityPair in entitesToConverge)
                {
                    EnvironmentEntity originalEntity = entityPair.Item1;
                    EnvironmentEntity changedEntity = entityPair.Item2;

                    foreach (KeyValuePair<string, IConvertible> entry in originalEntity.Metadata)
                    {
                        if (!changedEntity.Metadata.ContainsKey(entry.Key))
                        {
                            changedEntity.Metadata.Add(entry);
                        }
                    }

                    existingEntities.Remove(originalEntity);
                    existingEntities.Add(changedEntity);
                }
            }
        }

        private static void RemoveEntities(HashSet<EnvironmentEntity> existingEntities, IEnumerable<EnvironmentEntity> entitiesToRemove)
        {
            // The entity manager tracks entities that are explicitly deleted. This makes it easier to determine new
            // entities that should be added to the collection in the backing store. Without this, it would be hard for
            // example to determine if an entity that existed in the backing data store but that did not exist in the local
            // entity manager was deleted by the entity manager or put in the backing store by some other process.
            if (entitiesToRemove?.Any() == true)
            {
                foreach (EnvironmentEntity entity in entitiesToRemove)
                {
                    existingEntities.Remove(entity);
                }
            }
        }

        private void OnEntityPoolCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (object entityAdded in e.NewItems)
                {
                    EnvironmentEntity entity = entityAdded as EnvironmentEntity;
                    if (entity != null)
                    {
                        this.entityPoolChanges[entity] = EntityState.Added;
                    }
                }
            }

            if (e.OldItems != null)
            {
                foreach (object entityRemoved in e.OldItems)
                {
                    EnvironmentEntity entity = entityRemoved as EnvironmentEntity;
                    if (entity != null)
                    {
                        this.entityPoolChanges[entity] = EntityState.Deleted;
                    }
                }
            }
        }

        private void OnEntitiesProvisionedCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (object entityAdded in e.NewItems)
                {
                    EnvironmentEntity entity = entityAdded as EnvironmentEntity;
                    if (entity != null)
                    {
                        this.entitiesProvisionedChanges[entity] = EntityState.Added;
                    }
                }
            }

            if (e.OldItems != null)
            {
                foreach (object entityRemoved in e.OldItems)
                {
                    EnvironmentEntity entity = entityRemoved as EnvironmentEntity;
                    if (entity != null)
                    {
                        this.entitiesProvisionedChanges[entity] = EntityState.Deleted;
                    }
                }
            }
        }
    }
}
