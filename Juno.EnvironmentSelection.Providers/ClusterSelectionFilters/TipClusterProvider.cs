namespace Juno.EnvironmentSelection.ClusterSelectionFilters
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Extensions.AspNetCore;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Filter that retrieves healthy-tippable Clusters
    /// </summary>
    [SupportedFilter(Name = FilterParameters.TipSessionsRequired, Type = typeof(int), Required = false, CacheLabel = ProviderConstants.TipSessionLowerBound, Default = "2")]
    [KustoColumn(Name = ProviderConstants.RemainingTipSessions, AdditionalInfo = true)]
    [KustoColumn(Name = ProviderConstants.TipSessionLowerBound, AdditionalInfo = true, ComposesCacheKey = true)]
    public class TipClusterProvider : ClusterSelectionFilter, IAccountable
    {
        // Need high level granularity of the cache to perform LINQ operations.
        // do not need synchronization object for each key.
        private const int DefaultTipSessions = 0;
        private const string WildCard = "*";
        private static readonly TimeSpan DefaultTimeSpan = TimeSpan.FromHours(12);

        private IMemoryCache<ClusterAccount> clusterLedger;

        /// <inheritdoc />
        public TipClusterProvider(IServiceCollection services, IConfiguration configuration, ILogger logger)
            : base(services, CacheConstants.HealthyNodeTtl, configuration, logger, Properties.Resources.TipClusterQuery)
        {
        }

        /// <inheritdoc/>
        public override Task ConfigureServicesAsync()
        {
            if (!this.Services.TryGetService(out IMemoryCache<ClusterAccount> cache))
            {
                cache = new MemoryCache<ClusterAccount>();
                this.Services.AddSingleton<IMemoryCache<ClusterAccount>>(cache);
            }

            this.clusterLedger = cache;

            return base.ConfigureServicesAsync();
        }

        /// <inheritdoc/>
        public Task<bool> DeleteReservationAsync(EnvironmentCandidate candidate, CancellationToken cancellationToken)
        {
            candidate.ThrowIfNull(nameof(candidate));
            Action<ClusterAccount, string, DateTime> updateFunction = (account, nodeId, expiration) =>
            {
                ClusterReservation originalReservation = account.Reservations.FirstOrDefault(r => r.NodeId.Equals(nodeId, StringComparison.OrdinalIgnoreCase));
                account.Reservations.Remove(originalReservation);
            };
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(false);
            }

            return this.UpdateCacheEntryAsync(candidate.ClusterId, candidate.NodeId, DateTime.Now, updateFunction);
        }

        /// <inheritdoc/>
        public Task<bool> ReserveCandidateAsync(EnvironmentCandidate candidate, TimeSpan reservationDuration, CancellationToken cancellationToken)
        {
            candidate.ThrowIfNull(nameof(candidate));
            Action<ClusterAccount, string, DateTime> updateFunction = (account, nodeId, expiration) =>
            {
                ClusterReservation originalResrvation = account.Reservations.FirstOrDefault(r => r.NodeId.Equals(nodeId, StringComparison.OrdinalIgnoreCase));
                account.Reservations.Remove(originalResrvation);
                account.Reservations.Add(new ClusterReservation(nodeId, expiration));
            };
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(false);
            }

            return this.UpdateCacheEntryAsync(candidate.ClusterId, candidate.NodeId, DateTime.UtcNow.Add(reservationDuration), updateFunction);
        }

        /// <inheritdoc/>
        protected override async Task<IDictionary<string, EnvironmentCandidate>> ExecuteAsync(EnvironmentFilter filter, EventContext telemetryContext, CancellationToken token)
        {
            // Grab the base set
            IDictionary<string, EnvironmentCandidate> baseSet = await base.ExecuteAsync(filter, telemetryContext, token).ConfigureDefaults();

            return await this.Logger.LogTelemetryAsync($"{this.GetType().Name}.ExecuteExtension", telemetryContext, async () =>
            {
                // Grab Source information: [ClusterId] = TipSessionsRemaining
                // If base set was pulled from cache this information may not be present. This results in skipping the update cache and go straight to filtering.
                IEnumerable<KeyValuePair<string, int>> tipSessions = baseSet.Where(candidate => candidate.Value.AdditionalInfo.ContainsKey(ProviderConstants.RemainingTipSessions))
                    .Select(c => new KeyValuePair<string, int>(c.Key, int.Parse(c.Value.AdditionalInfo[ProviderConstants.RemainingTipSessions])));

                // Update the cache entries.
                await this.UpdateCacheEntriesAsync(tipSessions).ConfigureDefaults();

                // Update tipsessions to now use the values from the udpated cache.
                tipSessions = await Task.WhenAll(baseSet.Keys.Select(async b =>
                {
                    int ts = await this.GetClustersTipSessionRemainingAsync(b).ConfigureDefaults();
                    return new KeyValuePair<string, int>(b, ts);
                })).ConfigureDefaults();

                Dictionary<string, int> tipSessionDictionary = tipSessions.ToDictionary(c => c.Key, c => c.Value);

                // Filter out clusters that are full.
                return baseSet.Where(c => tipSessionDictionary[c.Key] > 0).ToDictionary(c => c.Key, c => c.Value);
            }).ConfigureDefaults();
        }

        private Task UpdateCacheEntriesAsync(IEnumerable<KeyValuePair<string, int>> tipSessions)
        {
            Func<KeyValuePair<string, int>, Task> updateFunction = async (tipSession) =>
            {
                // Case 1: Already present in the cache. update values and prune increment/decrement list.
                // Case 2: Not present in the cache. Create new cache entry.
                ClusterAccount clusterAccount = new ClusterAccount(tipSession.Value);
                if (this.clusterLedger.Contains(tipSession.Key))
                {
                    ClusterAccount exisitingAccount = await this.clusterLedger.GetAsync(tipSession.Key).ConfigureDefaults();
                    clusterAccount = new ClusterAccount(tipSession.Value, exisitingAccount.PruneAccount().Reservations);
                    await this.clusterLedger.RemoveAsync(tipSession.Key).ConfigureDefaults();
                }

                await this.clusterLedger.AddAsync(tipSession.Key, TipClusterProvider.DefaultTimeSpan, () => clusterAccount).ConfigureAwait(false);
            };

            // Update Cluster Ledger
            return Task.WhenAll(tipSessions.Select(ts => updateFunction.Invoke(ts)));
        }

        private async Task<bool> UpdateCacheEntryAsync(string clusterId, string nodeId, DateTime expiration, Action<ClusterAccount, string, DateTime> updateAction)
        {
            // An envionrment Candidate that this provider should not handle.
            if (clusterId == TipClusterProvider.WildCard || nodeId == TipClusterProvider.WildCard)
            {
                return true;
            }

            await this.ConfigureServicesAsync().ConfigureDefaults();

            // Case 1: ID is not present. Create new entry and place default value for source tip sessions.
            ClusterAccount account = new ClusterAccount(TipClusterProvider.DefaultTipSessions);
            if (this.clusterLedger.Contains(clusterId))
            {
                // Case 2: ID is present just need to update the list of reservations.
                account = await this.clusterLedger.GetAsync(clusterId).ConfigureDefaults();
                await this.clusterLedger.RemoveAsync(clusterId).ConfigureDefaults();
            }

            updateAction.Invoke(account, nodeId, expiration);
            await this.clusterLedger.AddAsync(clusterId, TipClusterProvider.DefaultTimeSpan, () => account).ConfigureDefaults();

            return true;
        }

        private async Task<int> GetClustersTipSessionRemainingAsync(string key)
        {
            if (this.clusterLedger.Contains(key))
            {
                return (await this.clusterLedger.GetAsync(key).ConfigureDefaults()).TipSessionsAllowed;
            }

            // If the cluster is not in the cache this would be a very precarious position, since we would have just updated the cache
            // before calling this method.
            return 0;
        }

        internal static class FilterParameters
        {
            internal const string TipSessionsRequired = "tipSessionsRequired";
        }
    }
}
