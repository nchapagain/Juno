namespace Juno.EnvironmentSelection.SubscriptionFilters
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Subscriptions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    /// <summary>
    /// Environment Filter used to find Subscriptions that have not 
    /// exceeded the resource group allocation limit.
    /// </summary>
    [SupportedFilter(Name = Constants.IncludeSubscription, Type = typeof(string), Required = false)]
    [SupportedFilter(Name = Constants.ResourceGroupLimit, Type = typeof(string), Required = false)]
    public class ResourceGroupFilterProvider : EnvironmentSelectionProvider
    {
        /// <summary>
        /// Hard set number by Azure
        /// </summary>
        private const int ResourceGroupLimit = 960;

        /// <summary>
        /// Initailizes a <see cref="ResourceGroupFilterProvider"/>
        /// </summary>
        /// <param name="services">List of services used for dependency injection</param>
        /// <param name="configuration">Current configuration of execution</param>
        /// <param name="logger">Logger used to capture telemetry</param>
        public ResourceGroupFilterProvider(IServiceCollection services, IConfiguration configuration, ILogger logger)
            : base(services, CacheConstants.ResourceGroupTtl, configuration, logger)
        { 
        }

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

            return base.ConfigureServicesAsync();
        }

        /// <summary>
        /// Filters out any subscription that is above the Azure Resource Group Limit of 980
        /// </summary>
        /// <param name="filter">The filter that contains the set of parameters required for successful execution.</param>
        /// <param name="telemetryContext">Event context used for capturing telemetry.</param>
        /// <param name="token"><see cref="CancellationToken"/></param>
        /// <returns>
        /// <see cref="IDictionary{String, EnvironmentCandidate}"/> whose key is the subscription id and the value
        /// is contextual infomation for the subscription.
        /// </returns>
        protected async override Task<IDictionary<string, EnvironmentCandidate>> ExecuteAsync(EnvironmentFilter filter, EventContext telemetryContext, CancellationToken token)
        {
            filter.ThrowIfNull(nameof(filter));

            if (token.IsCancellationRequested)
            {
                return null;
            }

            IList<string> subscriptionHints = null; 
            if (filter.Parameters.TryGetValue(Constants.IncludeSubscription, out IConvertible subscriptionString))
            { 
                subscriptionHints = subscriptionString.ToString().ToList(',', ';');
            }

            int resourceGroupLimit = ResourceGroupFilterProvider.ResourceGroupLimit;
            if (filter.Parameters.TryGetValue(Constants.ResourceGroupLimit, out IConvertible givenLimit))
            {
                resourceGroupLimit = Convert.ToInt32(givenLimit);
            }

            IEnumerable<AzureResourceGroup> resourceGroups = await this.Client.GetAllResourceGroupUsageAsync(token, subscriptionHints)
                .ConfigureDefaults();

            IDictionary<string, int> usages = new Dictionary<string, int>();
            foreach (AzureResourceGroup resourceGroup in resourceGroups)
            {
                string currentSubscription = resourceGroup.SubscriptionId;
                if (!usages.ContainsKey(currentSubscription))
                {
                    usages.Add(currentSubscription, 1);
                }
                else
                {
                    usages[currentSubscription]++;
                }
            }

            telemetryContext.AddContext(nameof(usages), usages);

            IEnumerable<string> overUsedSubs = usages.Where(usage => usage.Value >= resourceGroupLimit)
                .Select(usage => usage.Key.Trim());

            // Add transient error, so that this can have a monitor placed on top of.
            if (overUsedSubs.Any())
            {
                telemetryContext.AddError(new EnvironmentSelectionException($"Subscriptions have reached the resource group limit. Subscriptions: {overUsedSubs}"));
            }
            
            IDictionary<string, EnvironmentCandidate> result = new Dictionary<string, EnvironmentCandidate>();
            foreach (KeyValuePair<string, int> usage in usages)
            {
                if (usage.Value < resourceGroupLimit)
                {
                    result.Add(usage.Key, this.CreateEnvironmentCandidate(usage));
                }
            }

            return result;
        }

        private EnvironmentCandidate CreateEnvironmentCandidate(KeyValuePair<string, int> usage)
        {
            IDictionary<string, string> additionalInfo = new Dictionary<string, string>()
            { [Constants.ResourceGroupUsage] = usage.Value.ToString() };

            return new EnvironmentCandidate(subscription: usage.Key, additionalInfo: additionalInfo);
        }

        private class Constants
        {
            internal const string JunoTag = "juno-";
            internal const string IncludeSubscription = "includeSubscription";
            internal const string ResourceGroupLimit = "resourceGroupLimit";
            internal const string ResourceGroupUsage = "resourceGroupUsage";
        }
    }
}
