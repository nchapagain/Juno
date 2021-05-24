namespace Juno.EnvironmentSelection.SubscriptionFilters
{
    using System;
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
    /// Provider that filters out subscriptions that exceed the set number of IP addresses for each subscription
    /// </summary>
    [SupportedFilter(Name = Constants.IncludeSubscription, Type = typeof(string), Required = false)]
    [SupportedFilter(Name = Constants.PublicIpAddressLimit, Type = typeof(string), Required = false)]
    public class PublicIPAddressFilterProvider : EnvironmentSelectionProvider
    {
        /// <summary>
        /// The Public IP Address limit set by Azure (plus a buffer):
        /// https://docs.microsoft.com/en-us/azure/azure-resource-manager/management/azure-subscription-service-limits?toc=/azure/virtual-network/toc.json#publicip-address
        /// </summary>
        public const int DefaultPublicIPAddressLimit = 980;

        /// <inheritdoc/>
        public PublicIPAddressFilterProvider(IServiceCollection services, IConfiguration configuration, ILogger logger)
            : base(services, CacheConstants.PublicIpAddressTtl, configuration, logger)
        { 
        }

        /// <summary>
        /// Local Cache used for storing results from data source.
        /// </summary>
        protected IMemoryCache<IDictionary<string, EnvironmentCandidate>> LocalCache { get; private set; }

        /// <summary>
        /// Service Limit client used for querying a subscriptions usages.
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
        /// Gather information about given subscription usages and filter out
        /// subscriptions that do not fit criteria.
        /// </summary>
        /// <param name="filter">Filter that offers parameters for execution</param>
        /// <param name="telemetryContext">Event Context object used for logging telemetry.</param>
        /// <param name="token">Cancellation Token used to cancel current thread of execution.</param>
        /// <returns><see cref="IDictionary{TKey, TValue}"/> in which the key is the subscription ID.</returns>
        protected async override Task<IDictionary<string, EnvironmentCandidate>> ExecuteAsync(EnvironmentFilter filter, EventContext telemetryContext, CancellationToken token)
        {
            filter.ThrowIfNull(nameof(filter));
            IDictionary<string, EnvironmentCandidate> result = new Dictionary<string, EnvironmentCandidate>();
            if (!token.IsCancellationRequested)
            {
                result = await this.LocalCache.GetOrAddAsync(filter.ToString(), this.TTL, async () => 
                {
                    IList<string> subscriptionIds = null;
                    if (filter.Parameters.TryGetValue(Constants.IncludeSubscription, out IConvertible entry))
                    {
                        subscriptionIds = entry.ToString().ToList(',', ';');
                    }

                    int ipLimit = filter.Parameters.GetValue<int>(Constants.PublicIpAddressLimit, PublicIPAddressFilterProvider.DefaultPublicIPAddressLimit);

                    // Grab the Usages from the SLC and map to a dictionary where [subscriptionId] = count of public IPs
                    IEnumerable<AzureIPUsage> usages = await this.Client.GetAllAzureIpUsageAsync(token, subscriptionIds).ConfigureAwait(false);
                    Dictionary<string, int> usageMapping = usages.GroupBy(usage => usage.SubscriptionId).ToDictionary(group => group.Key, group => group.Count());

                    telemetryContext.AddContext(nameof(usageMapping), usageMapping);

                    // Filter out only Subscriptions that are below the azure limit, then translate to an ESS expected dictionary.
                    return usageMapping.Where(usage => usage.Value < ipLimit).ToDictionary(usage => usage.Key, usage => new EnvironmentCandidate(subscription: usage.Key));
                }).ConfigureAwait(false);
            }

            return result;
        }

        private class Constants
        {
            internal const string JunoTag = "juno-";
            internal const string IncludeSubscription = nameof(Constants.IncludeSubscription);
            internal const string PublicIpAddressLimit = nameof(Constants.PublicIpAddressLimit);
        }
    }
}
