namespace Juno.EnvironmentSelection.Service
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Class that chooses Nodes and Subscriptions that satisfy
    /// user constraints.
    /// </summary>
    public class EnvironmentSelectionService : IEnvironmentSelectionService
    {
        private static Random randomSeed = new Random();
        private static Dictionary<NodeAffinity, Func<EnvironmentCandidate, string>> affinityIdGenerators = new Dictionary<NodeAffinity, Func<EnvironmentCandidate, string>>()
        {
            [NodeAffinity.Any] = (candidate) => candidate.NodeId,
            [NodeAffinity.SameRack] = (candidate) => string.Concat(candidate.Rack, candidate.ClusterId, candidate.MachinePoolName),
            [NodeAffinity.SameCluster] = (candidate) => candidate.ClusterId,
            [NodeAffinity.DifferentCluster] = (candidate) => candidate.Region
        };

        private readonly IEnumerable<IAccountable> accountables;

        /// <summary>
        /// Manager class for the Environment Selection Service.
        /// </summary>
        /// <param name="services">List of services used for dependency injection</param>
        /// <param name="configuration">Configuration of current executon environment</param>
        /// <param name="logger"><see cref="ILogger"/></param>
        public EnvironmentSelectionService(IServiceCollection services, IConfiguration configuration, ILogger logger)
        {
            services.ThrowIfNull(nameof(services));
            configuration.ThrowIfNull(nameof(configuration));
            logger.ThrowIfNull(nameof(logger));

            if (services.TryGetService<IEnumerable<IAccountable>>(out IEnumerable<IAccountable> accountables)) 
            {
                this.accountables = accountables;
            }

            this.Services = services;
            this.Configuration = configuration;
            this.Logger = logger;
        }

        private IServiceCollection Services { get; }

        private IConfiguration Configuration { get; }

        private ILogger Logger { get; }

        /// <summary>
        /// Query the entire envrionment space to retrieve environment candidates that satisfy the given filters
        /// </summary>
        /// <param name="query">The filters to apply to the search space</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns>
        /// A list of <see cref="EnvironmentCandidate"/>.
        /// If Subscription filters were given, and none were found throws <see cref="EnvironmentSelectionException"/>
        /// If node filters were given, and none were found throws <see cref="EnvironmentSelectionException"/>
        /// </returns>
        public async Task<IEnumerable<EnvironmentCandidate>> GetEnvironmentCandidatesAsync(EnvironmentQuery query, CancellationToken cancellationToken)
        {
            query.ThrowIfNull(nameof(query));

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(Constants.CandidateQueryId, query.Name);

            return await this.Logger.LogTelemetryAsync($"{nameof(EnvironmentSelectionService)}.GetEnvrionmentCandidates", telemetryContext, async () =>
            {
                if (query.HasExternalReferences())
                {
                    query = query.ReplaceExternalReferences();
                }

                // First get subscription we need
                EnvironmentCandidate subscription = await this.GetSubscriptionAsync(query, cancellationToken)
                    .ConfigureDefaults();

                if (subscription == null)
                {
                    throw new EnvironmentSelectionException("No Subscriptions were found with the given filters");
                }

                telemetryContext.AddContext(nameof(subscription), subscription);

                // Pass the new info. I.E. Regions etc to the node/cluster filters.
                if (query.HasSubscriptionReferences())
                {
                    // Feed forward the addtionalinfo to the node/cluster selection space
                    foreach (KeyValuePair<string, string> additonalInfo in subscription.AdditionalInfo)
                    {
                        KeyValuePair<string, IConvertible> translation = new KeyValuePair<string, IConvertible>(additonalInfo.Key, additonalInfo.Value);
                        query.Parameters.Add(translation);
                    }

                    query = query.ReplaceSubscriptionReferences();
                }

                IEnumerable<EnvironmentCandidate> clusters = await this.GetClustersAsync(query, cancellationToken)
                    .ConfigureDefaults();

                if (clusters == null)
                {
                    throw new EnvironmentSelectionException("No Clusters were found with the given filters");
                }

                if (query.HasClusterReference())
                {
                    IEnumerable<string> clusterStrings = clusters.Select(cluster => cluster.ClusterId);
                    HashSet<string> set = new HashSet<string>(clusterStrings);

                    // The only info there is to pass are the clusters
                    query.Parameters.Add(new KeyValuePair<string, IConvertible>(Constants.Clusters, string.Join(",", set)));
                    query = query.ReplaceClusterReferences();
                }

                // Retrieve the results from the nodes
                IEnumerable<EnvironmentCandidate> candidates = await this.GetNodesAsync(query, cancellationToken)
                    .ConfigureDefaults();

                if (candidates == null)
                {
                    throw new EnvironmentSelectionException("No Nodes were found with the given filters");
                }

                telemetryContext.AddContext(nameof(candidates), candidates);

                if (candidates?.Any() == true && clusters?.Any() == true)
                {
                    List<EnvironmentCandidate> candidatesWithVms = new List<EnvironmentCandidate>();
                    foreach (EnvironmentCandidate candidate in candidates)
                    {
                        EnvironmentCandidate cluster = clusters.FirstOrDefault(c => c.ClusterId == candidate.ClusterId);
                        candidatesWithVms.Add(new EnvironmentCandidate(
                            candidate.Subscription,
                            candidate.ClusterId,
                            candidate.Region,
                            candidate.MachinePoolName,
                            candidate.Rack,
                            candidate.NodeId,
                            cluster.VmSku,
                            candidate.CpuId,
                            candidate.AdditionalInfo));
                    }

                    candidates = candidatesWithVms;
                }

                // Have candidates and subscription (or default subscription, the projection is safe, and wont overwrite anything.)
                if (candidates?.Any() == true)
                {
                    return subscription.ProjectSubscription(candidates);
                }

                // No candidates but have clusters and may have subscription.
                else if (clusters?.Any() == true)
                {
                    return subscription.ProjectSubscription(clusters);
                }

                // No nodes but have subscription    
                return new List<EnvironmentCandidate> { subscription };
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Retrieves the set of Nodes that satisfy the given filters
        /// </summary>
        /// <param name="query">List of filters that include attributes the nodes must have</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns>
        /// A list of eligible nodes. If there are nodes found.
        /// null, if node filters were given, but NO nodes were found.
        /// An empty list if no filters were given.
        /// </returns>
        public async Task<IEnumerable<EnvironmentCandidate>> GetNodesAsync(EnvironmentQuery query, CancellationToken cancellationToken)
        {
            query.ThrowIfNull(nameof(query));
            IList<EnvironmentFilter> filters = query.Filters.Where(entity => entity.GetProviderCategory() == FilterCategory.Node).ToList();

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(nameof(filters), string.Join(", ", filters.Select(f => f.Type)));

            return await this.Logger.LogTelemetryAsync($"{nameof(EnvironmentSelectionService)}.GetNodes", telemetryContext, async () =>
            {
                // If we dont have any filters for nodes thats ok, just return an empty list.
                if (filters == null || !filters?.Any() == true)
                {
                    return new List<EnvironmentCandidate>();
                }

                IEnumerable<EnvironmentCandidate> intersectionResults = await this.GetSetIntersectionAsync(filters, telemetryContext, cancellationToken)
                    .ConfigureDefaults();

                // If we had filters, but no results, thats not ok, return null.
                if (!intersectionResults?.Any() == true || intersectionResults == null)
                {
                    return null;
                }

                // Shuffle to prevent from sticking to any particular, cluster, region, etc...
                IList<EnvironmentCandidate> candidateSet = intersectionResults.ToList();

                IList<EnvironmentCandidate> trimmedSet = new List<EnvironmentCandidate>();

                Func<EnvironmentCandidate, string> parityIdGenerator = EnvironmentSelectionService.affinityIdGenerators[query.NodeAffinity];
                string parityUid = null;
                List<string> selectedPartition = new List<string>();
                for (int i = 0; trimmedSet.Count < query.NodeCount && i < candidateSet.Count; i++)
                {
                    int randomIndex = EnvironmentSelectionService.randomSeed.Next(candidateSet.Count - 1);
                    EnvironmentCandidate current = candidateSet[randomIndex];

                    // If any level of affinity is established, simply add the node we are looking at and continue
                    if (query.NodeAffinity == NodeAffinity.Any || query.NodeCount == 1)
                    {
                        trimmedSet.Add(current);
                        candidateSet.Remove(current);
                    }

                    // If an affinity is established, add only pairs of nodes that establish this affinity.
                    else
                    {
                        parityUid = parityIdGenerator.Invoke(current);

                        if (!selectedPartition.Contains(parityUid))
                        {
                            // Find its other rack mates that also have not been chosen. 
                            // (note this list will also include the original "current" candidate)
                            IList<EnvironmentCandidate> rackMates = candidateSet.Where(e => parityIdGenerator.Invoke(e)
                            .Equals(parityUid, StringComparison.OrdinalIgnoreCase)).ToList();

                            if (rackMates.Count > 1)
                            {
                                // now we can add 2 potential rackmates to the trimmed set.
                                const int PairCount = 2;
                                for (int j = 0; j < PairCount && j < rackMates.Count && trimmedSet.Count < query.NodeCount; j++)
                                {
                                    // 1. Grab the candidate, 2. Add the Candidate to the trimmed set
                                    // 3. Remove it from the pool of potential candidates 4. Mark it as occupied over a long duration.
                                    // 5. Mark this partition as chosen from and move to another.
                                    EnvironmentCandidate candidate = rackMates[j];
                                    trimmedSet.Add(candidate);
                                    candidateSet.Remove(candidate);
                                    selectedPartition.Add(parityUid);
                                }
                            }
                        }
                    }
                }

                telemetryContext.AddContext(nameof(trimmedSet), trimmedSet);

                // If the trimmed set is empty, that means we have asked for nodes, but have found none.
                // Thus the appropiate response is to return null.
                return trimmedSet.Any() ? trimmedSet : null;
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Retrieve Clusters with attributes defined in the Environment Query
        /// </summary>
        /// <param name="query">The envrionment query that defines cluster filters.</param>
        /// <param name="cancellationToken">Token used for cancellation of task.</param>
        /// <returns>List of environment candidates.</returns>
        public async Task<IEnumerable<EnvironmentCandidate>> GetClustersAsync(EnvironmentQuery query, CancellationToken cancellationToken)
        {
            query.ThrowIfNull(nameof(query));
            List<EnvironmentFilter> filters = query.Filters.Where(entity => entity.GetProviderCategory() == FilterCategory.Cluster).ToList();

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(nameof(filters), filters);

            return await this.Logger.LogTelemetryAsync($"{nameof(EnvironmentSelectionService)}.GetClusters", telemetryContext, async () =>
            {
                if (filters == null || !filters?.Any() == true)
                {
                    // If we dont have any filters, thats ok, just return default candidate
                    return new List<EnvironmentCandidate> { };
                }

                IEnumerable<EnvironmentCandidate> clusters = await this.GetSetIntersectionAsync(filters, telemetryContext, cancellationToken)
                    .ConfigureDefaults();

                return clusters?.Any() == true ? clusters : null;
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Filter all of the subscription requirements
        /// </summary>
        /// <returns>
        /// One Environment Candidate which outlines the Subscription and a list of regions where it is
        /// supported
        /// </returns>
        public async Task<EnvironmentCandidate> GetSubscriptionAsync(EnvironmentQuery query, CancellationToken cancellationToken)
        {
            query.ThrowIfNull(nameof(query));
            IList<EnvironmentFilter> filters = query.Filters.Where(entity => entity.GetProviderCategory() == FilterCategory.Subscription).ToList();

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(nameof(filters), filters);

            return await this.Logger.LogTelemetryAsync($"{nameof(EnvironmentSelectionService)}.GetSubsriptions", telemetryContext, async () =>
            {
                if (filters == null || !filters?.Any() == true)
                {
                    // If we dont have any filters, thats ok, just return default candidate
                    return new EnvironmentCandidate();
                }

                ICollection<EnvironmentCandidate> subscriptions = await this.GetSetIntersectionAsync(filters, telemetryContext, cancellationToken)
                    .ConfigureDefaults();

                // If we had filters, but no results, thats not ok, return null.
                return subscriptions?.Any() == true ? subscriptions.Shuffle().First() : null;
            }).ConfigureDefaults();
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<EnvironmentCandidate>> ReserveEnvironmentCandidatesAsync(IEnumerable<EnvironmentCandidate> reservedCandidates, TimeSpan reservationDuration, CancellationToken cancellationToken)
        {
            EventContext telemetryContext = EventContext.Persisted();
            return await this.Logger.LogTelemetryAsync($"{nameof(EnvironmentSelectionService)}.ReserveCandidates", telemetryContext, async () =>
            {
                Func<EnvironmentCandidate, TimeSpan, CancellationToken, IEnumerable<Task<bool>>> reportFunction = (candidate, duration, token) =>
                {
                    return this.accountables.Select(a => a.ReserveCandidateAsync(candidate, duration, token));
                };

                return await this.UpdateAccountablesAsync(reservedCandidates, reservationDuration, telemetryContext, reportFunction, cancellationToken).ConfigureAwait(false);
            }).ConfigureDefaults();
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<EnvironmentCandidate>> DeleteReservationsAsync(IEnumerable<EnvironmentCandidate> reservedCandidates, CancellationToken cancellationToken)
        {
            EventContext telemetryContext = EventContext.Persisted();
            return await this.Logger.LogTelemetryAsync($"{nameof(EnvironmentSelectionService)}.DeleteCandidates", telemetryContext, async () =>
            {
                Func<EnvironmentCandidate, TimeSpan, CancellationToken, IEnumerable<Task<bool>>> reportFunction = (candidate, duration, token) =>
                {
                    return this.accountables.Select(a => a.DeleteReservationAsync(candidate, token));
                };

                return await this.UpdateAccountablesAsync(reservedCandidates, TimeSpan.Zero, telemetryContext, reportFunction, cancellationToken).ConfigureAwait(false);
            }).ConfigureDefaults();
        }

        private async Task<IEnumerable<EnvironmentCandidate>> UpdateAccountablesAsync(
            IEnumerable<EnvironmentCandidate> candidates, 
            TimeSpan duration, 
            EventContext telemetryContext, 
            Func<EnvironmentCandidate, TimeSpan, CancellationToken, IEnumerable<Task<bool>>> accountableFunction, 
            CancellationToken cancellationToken)
        {
            candidates.ThrowIfNull(nameof(candidates));

            telemetryContext.AddContext(nameof(candidates), candidates);

            List<EnvironmentCandidate> updated = new List<EnvironmentCandidate>();
            List<EnvironmentCandidate> notUpdated = new List<EnvironmentCandidate>();
            if (!cancellationToken.IsCancellationRequested)
            {
                foreach (EnvironmentCandidate candidate in candidates)
                {
                    IEnumerable<Task<bool>> updateTasks = accountableFunction.Invoke(candidate, duration, cancellationToken);
                    bool updateSuccess = (await Task.WhenAll(updateTasks).ConfigureDefaults()).All(t => t);

                    if (updateSuccess)
                    {
                        updated.Add(candidate);
                    }
                    else
                    {
                        notUpdated.Add(candidate);
                    }
                }
            }

            telemetryContext.AddContext(nameof(updated), updated);
            telemetryContext.AddContext(nameof(notUpdated), notUpdated);

            return updated;
        }

        private async Task<ICollection<EnvironmentCandidate>> GetSetIntersectionAsync(IList<EnvironmentFilter> filters, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            telemetryContext.AddContext(nameof(filters), filters);
            IEnumerable<string> filterTypes = filters.Select(filter => filter.Type);
            telemetryContext.AddContext(nameof(filterTypes), filterTypes);

            IDictionary<string, int> intersectionResults = new Dictionary<string, int>();

            IList<Task<KeyValuePair<string, IDictionary<string, EnvironmentCandidate>>>> providerTasks = new List<Task<KeyValuePair<string, IDictionary<string, EnvironmentCandidate>>>>();
            foreach (EnvironmentFilter filter in filters)
            {
                providerTasks.Add(Task.Run(() => this.GetProviderResultAsync(filter, cancellationToken)));
            }

            Task<KeyValuePair<string, IDictionary<string, EnvironmentCandidate>>> baseSetPairTask = await Task.WhenAny(providerTasks).ConfigureDefaults();
            providerTasks.Remove(baseSetPairTask);
            KeyValuePair<string, IDictionary<string, EnvironmentCandidate>> baseSetPair = await baseSetPairTask.ConfigureDefaults();
            string intersectionFilters = baseSetPair.Key;
            IDictionary<string, EnvironmentCandidate> baseSet = baseSetPair.Value;

            intersectionResults.Add(intersectionFilters, baseSet.Count);

            while (providerTasks.Any())
            {
                var filterResultTask = await Task.WhenAny(providerTasks).ConfigureDefaults();
                providerTasks.Remove(filterResultTask);

                var filterResult = await filterResultTask.ConfigureDefaults();
                IDictionary<string, EnvironmentCandidate> currentCandidateSet = filterResult.Value;
                intersectionFilters = string.Join(", ", intersectionFilters, filterResult.Key);
                baseSet = baseSet.Intersection(currentCandidateSet);

                intersectionResults.Add(intersectionFilters, baseSet.Count);
            }

            telemetryContext.AddContext(nameof(intersectionResults), intersectionResults);

            return baseSet.Values;
        }

        private async Task<KeyValuePair<string, IDictionary<string, EnvironmentCandidate>>> GetProviderResultAsync(EnvironmentFilter filter, CancellationToken cancellationToken)
        {
            IEnvironmentSelectionProvider provider = EnvironmentSelectionProviderFactory.CreateEnvironmentFilterProvider(filter, this.Services, this.Configuration, this.Logger);
            IDictionary<string, EnvironmentCandidate> result = await provider.ExecuteAsync(filter, cancellationToken).ConfigureDefaults();

            return new KeyValuePair<string, IDictionary<string, EnvironmentCandidate>>(provider.GetType().Name, result);
        }

        private static class Constants
        {
            internal const string ProviderName = "providerName";
            internal const string CandidateQueryId = "candidateQueryId";
            internal const string Clusters = "clusters";
        }
    }
}