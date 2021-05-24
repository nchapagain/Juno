namespace Juno.EnvironmentSelection.NodeSelectionFilters
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Extensions.AspNetCore;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Filter that retrieves healthy-tippable nodes
    /// </summary>
    [KustoColumn(Name = ProviderConstants.MachinePoolName, AdditionalInfo = false)]
    public class HealthyNodeProvider : NodeSelectionFilter, IAccountable
    {
        private const string DefaultNodeId = "*";

        /// <inheritdoc />
        public HealthyNodeProvider(IServiceCollection services, IConfiguration configuration, ILogger logger)
            : base(services, CacheConstants.HealthyNodeTtl, configuration, logger, Properties.Resources.HealthyNodeQuery)
        {
        }

        /// <summary>
        /// Local cache that keeps a ledger of nodes reserved.
        /// </summary>
        protected IMemoryCache<bool> ReservedNodes { get; private set; }

        /// <inheritdoc/>
        public override Task ConfigureServicesAsync()
        {
            if (!this.Services.TryGetService<IMemoryCache<bool>>(out IMemoryCache<bool> cache))
            {
                cache = new MemoryCache<bool>();
                this.Services.AddSingleton<IMemoryCache<bool>>(cache);
            }

            this.ReservedNodes = cache;
            return base.ConfigureServicesAsync();
        }

        /// <inheritdoc />
        public Task<bool> DeleteReservationAsync(EnvironmentCandidate candidate, CancellationToken cancellationToken)
        {
            candidate.ThrowIfNull(nameof(candidate));
            return this.DeleteCandidateReservationAsync(candidate, cancellationToken);
        }
        
        /// <inheritdoc />
        public Task<bool> ReserveCandidateAsync(EnvironmentCandidate candidate, TimeSpan reservationDuration, CancellationToken cancellationToken)
        {
            candidate.ThrowIfNull(nameof(candidate));
            return this.UpdateCandidateStateAsync(candidate, reservationDuration, false, cancellationToken);
        }

        /// <summary>
        /// Further filter out nodes that have been marked occupied
        /// </summary>
        /// <param name="filter">EnvironmentFilter that offers parameters for successful execution.</param>
        /// <param name="telemetryContext">Event context used for capturing telemetry</param>
        /// <param name="token">Token used for cancelling the current thread of execution.</param>
        /// <returns></returns>
        protected async override Task<IDictionary<string, EnvironmentCandidate>> ExecuteAsync(EnvironmentFilter filter, EventContext telemetryContext, CancellationToken token)
        {
            IDictionary<string, EnvironmentCandidate> baseResults = await base.ExecuteAsync(filter, telemetryContext, token).ConfigureDefaults();
            ParallelQuery<KeyValuePair<string, EnvironmentCandidate>> query = from candidate in baseResults.AsParallel().AsUnordered()
                                                                              where !this.ReservedNodes.Contains(candidate.Key)
                                                                              select candidate;
            ConcurrentDictionary<string, EnvironmentCandidate> bag = new ConcurrentDictionary<string, EnvironmentCandidate>();
            query.ForAll(candidate => bag.TryAdd(candidate.Key, candidate.Value));

            return bag;
        }

        private async Task<bool> UpdateCandidateStateAsync(EnvironmentCandidate candidate, TimeSpan reservationDuration, bool reported, CancellationToken cancellationToken)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                await this.ConfigureServicesAsync().ConfigureDefaults();
                string nodeId = candidate.NodeId;
                // Attempting to reserve a candidate that this provider does not handle.
                if (nodeId.Equals(HealthyNodeProvider.DefaultNodeId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (this.ReservedNodes.Contains(nodeId))
                {
                    try
                    {
                        await this.ReservedNodes.ChangeTimeToLiveAsync(nodeId, reservationDuration).ConfigureDefaults();
                    }
                    catch (KeyNotFoundException)
                    {
                        return false;
                    }

                    return true;
                }

                try
                {
                    _ = await this.ReservedNodes.AddAsync(nodeId, reservationDuration, () => reported).ConfigureDefaults();
                }
                catch (KeyNotFoundException)
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        private async Task<bool> DeleteCandidateReservationAsync(EnvironmentCandidate candidate, CancellationToken cancellationToken)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                await this.ConfigureServicesAsync().ConfigureDefaults();

                string nodeId = candidate.NodeId;
                if (this.ReservedNodes.Contains(nodeId))
                {
                    try
                    {
                        await this.ReservedNodes.RemoveAsync(nodeId).ConfigureDefaults();
                    }
                    catch (KeyNotFoundException)
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }
    }
}
