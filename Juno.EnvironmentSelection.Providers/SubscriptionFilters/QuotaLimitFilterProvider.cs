namespace Juno.EnvironmentSelection.SubscriptionFilters
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.Extensions.AspNetCore;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Subscriptions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Environment Filter used to find the best subscription and region. 
    /// </summary>
    [SupportedFilter(Name = Constants.IncludeVmSkus, Type = typeof(string), Required = true)]
    [SupportedFilter(Name = Constants.IncludeRegion, Type = typeof(string), Required = false)]
    [SupportedFilter(Name = Constants.ExcludeRegion, Type = typeof(string), Required = false)]
    [SupportedFilter(Name = Constants.RequiredInstances, Type = typeof(int), Required = false)]
    [SupportedFilter(Name = Constants.IncludeSubscription, Type = typeof(string), Required = false)]
    [SupportedFilter(Name = Constants.SelectionStrategy, Type = typeof(string), Required = false)]
    public class QuotaLimitFilterProvider : EnvironmentSelectionProvider
    {
        private const int DefaultThreshold = 2;
        private static readonly ConcurrentDictionary<string, string> RegionMapping = new ConcurrentDictionary<string, string>();
        private readonly Type defaultStrategy = typeof(RegionMaximizationStrategy);

        /// <summary>
        /// Initializes a <see cref="QuotaLimitFilterProvider"/>
        /// </summary>
        /// <param name="services">List of services used for dependency injection</param>
        /// <param name="configuration">Current configuration of execution</param>
        /// <param name="logger">Logger used to capture telemetry</param>
        public QuotaLimitFilterProvider(IServiceCollection services, IConfiguration configuration, ILogger logger)
            : base(services, CacheConstants.QuotaLimitTtl, configuration, logger)
        {
        }

        /// <summary>
        /// Local Cache used for storing results from data source.
        /// </summary>
        protected IMemoryCache<IDictionary<string, EnvironmentCandidate>> LocalCache { get; private set; }

        /// <summary>
        /// Service Limit client used for  
        /// </summary>
        protected IServiceLimitClient Client { get; private set; }
        
        /// <inheritdoc />
        public override Task ConfigureServicesAsync()
        {
            // Allow for dependency injection for testing, but do not add back in.
            IServiceLimitClient client;
            if (!this.Services.TryGetService<IServiceLimitClient>(out client))
            {
                EnvironmentSettings settings = EnvironmentSettings.Initialize(this.Configuration);
                string crcEnvironment = settings.Environment.Replace(Constants.JunoTag, string.Empty, StringComparison.OrdinalIgnoreCase);
                client = new ServiceLimitClient(crcEnvironment);
            }

            this.Client = client;

            IMemoryCache<IDictionary<string, EnvironmentCandidate>> cache;
            if (!this.Services.TryGetService<IMemoryCache<IDictionary<string, EnvironmentCandidate>>>(out cache))
            {
                cache = new MemoryCache<IDictionary<string, EnvironmentCandidate>>();
                this.Services.AddSingleton(cache);
            }

            this.LocalCache = cache;

            return base.ConfigureServicesAsync();
        }

        /// <summary>
        /// Decides the best subscription, set of regions and vmsku to be used.
        /// </summary>
        /// <param name="filter">The filter that contains the set of parameters required for successful execution.</param>
        /// <param name="telemetryContext">Event context used for capturing telemetry.</param>
        /// <param name="token"><see cref="CancellationToken"/></param>
        /// <returns>A dicitonary where the key is the subsctipition Id and the candidate contains other supporting information.</returns>
        protected async override Task<IDictionary<string, EnvironmentCandidate>> ExecuteAsync(EnvironmentFilter filter, EventContext telemetryContext, CancellationToken token)
        {
            filter.ThrowIfNull(nameof(filter));

            if (token.IsCancellationRequested)
            {
                return null;
            }

            return await this.LocalCache.GetOrAddAsync(filter.ToString(), this.TTL, async () =>
            {
                IList<string> subIds = null;
                if (filter.Parameters.TryGetValue(Constants.IncludeSubscription, out IConvertible subscriptions))
                {
                    subIds = subscriptions.ToString().ToList(';', ',');
                }

                if (!QuotaLimitFilterProvider.RegionMapping.Any())
                {
                    IEnumerable<AzureRegion> regions = await this.Client.GetAllSupportedRegionsAsync(token, subIds).ConfigureDefaults();
                    QuotaLimitFilterProvider.RegionMapping.AddRange(regions.Select(r => new KeyValuePair<string, string>(r.ExternalName, r.InternalName)).Distinct());
                    QuotaLimitFilterProvider.RegionMapping.AddRange(regions.Where(r => r.InternalName != r.ExternalName).Select(r => new KeyValuePair<string, string>(r.InternalName, r.ExternalName)).Distinct());
                }

                int threshold = QuotaLimitFilterProvider.DefaultThreshold;
                if (filter.Parameters.TryGetValue(Constants.RequiredInstances, out IConvertible convertible))
                {
                    threshold = Convert.ToInt32(convertible);
                }

                IList<string> includeRegions = null;
                if (filter.Parameters.TryGetValue(Constants.IncludeRegion, out IConvertible value))
                {
                    includeRegions = value.ToString().ToList(',', ';');
                    if (includeRegions.Any(r => !QuotaLimitFilterProvider.RegionMapping.ContainsKey(r)))
                    {
                        throw new ArgumentException($"Given regions: {string.Join(", ", includeRegions.Where(r => !QuotaLimitFilterProvider.RegionMapping.ContainsKey(r)))} are incorrect/not supported. Please" +
                            $"verify their spelling/existence.");
                    }

                    // Map external region into CRP region.
                    includeRegions = QuotaLimitFilterProvider.RegionMapping.Select(rm => rm.Value).ToList();
                }

                IList<string> vmSkuHints = filter.Parameters.GetValue<string>(Constants.IncludeVmSkus).ToList(',', ';');

                IList<ServiceLimitAvailibility> subs = await this.Client.GetServiceLimitAvailabilityForVmSkuAsync(telemetryContext, token, subscriptionIds: subIds, vmSku: vmSkuHints, regions: includeRegions)
                    .ConfigureDefaults();

                if (!subs.Any())
                {
                    // This means that our data source returned nothing, so we do not have any Subscription/Region/VmSku combo
                    // for the subscription and vms given.
                    return new Dictionary<string, EnvironmentCandidate>();
                }

                IList<string> excludeRegion = new List<string>();
                if (filter.Parameters.TryGetValue(Constants.ExcludeRegion, out value))
                {
                    excludeRegion = value.ToString().ToList(',', ';');
                }

                IList<ServiceLimitAvailibility> mappedSubs = new List<ServiceLimitAvailibility>();
                foreach (ServiceLimitAvailibility sub in subs)
                {
                    if (!QuotaLimitFilterProvider.RegionMapping.TryGetValue(sub.Region, out string externalRegion))
                    {
                        this.Logger.LogWarning($"{nameof(QuotaLimitFilterProvider)}.Mapping", $"{sub.Region} does not have mapping to an external region");
                        continue;
                    }

                    if (!excludeRegion.Contains(externalRegion))
                    {
                        mappedSubs.Add(new ServiceLimitAvailibility()
                        {
                            SubscriptionId = sub.SubscriptionId,
                            SkuFamilyName = sub.SkuFamilyName,
                            SkuName = sub.SkuName,
                            VirtualCPU = sub.VirtualCPU,
                            Region = externalRegion,
                            Limit = sub.Limit,
                            Usage = sub.Usage
                        });
                    }
                }

                subs = mappedSubs;

                ISelectionStrategy strategy = this.Services.TryGetService<ISelectionStrategy>(out ISelectionStrategy injectedStrategy)
                    ? injectedStrategy
                    : this.GetStrategy(filter);

                return strategy.GetEnvironmentCandidates(subs, threshold, telemetryContext);
            }).ConfigureDefaults();
        }

        private ISelectionStrategy GetStrategy(EnvironmentFilter filter)
        {
            string strategy = this.defaultStrategy.FullName;
            if (filter.Parameters.TryGetValue(Constants.SelectionStrategy, out IConvertible parameterValue))
            {
                strategy = parameterValue.ToString().Trim();
            }

            Type matchingType = Type.GetType(strategy, throwOnError: false);
            if (matchingType == null)
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();

                matchingType = assemblies.FirstOrDefault(assembly => assembly.GetType(strategy, throwOnError: false) != null)
                    ?.GetType(strategy);
            }

            if (matchingType == null)
            {
                throw new TypeLoadException($"{nameof(ISelectionStrategy)} provided: {strategy} is not a valid {nameof(ISelectionStrategy)}");
            }

            ISelectionStrategy selectionStrategy = (ISelectionStrategy)Activator.CreateInstance(matchingType);

            return selectionStrategy;
        }

        private class Constants
        {
            internal const string IncludeVmSkus = "includeVmSku";
            internal const string IncludeRegion = "includeRegion";
            internal const string ExcludeRegion = "excludeRegion";
            internal const string IncludeSubscription = "includeSubscription";
            internal const string RequiredInstances = "requiredInstances";
            internal const string SelectionStrategy = "strategy";
            internal const string JunoTag = "juno-";
        }
    }
}
