namespace Juno.EnvironmentSelection.SubscriptionFilters
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Subscriptions;
    using Microsoft.Azure.CRC.Telemetry;

    /// <summary>
    /// Strategy that chooses the best one VmSku + Subscription combo 
    /// based on largest quota.
    /// </summary>
    public class QuotaAggregateStrategy : SelectionStrategy
    {
        /// <inheritdoc/>
        public override IDictionary<string, EnvironmentCandidate> GetEnvironmentCandidates(IList<ServiceLimitAvailibility> limits, int threshold, EventContext telemetryContext)
        {
            limits.ThrowIfNullOrEmpty(nameof(limits));

            // Create the Buckets with key taking the form: Subscription-VmSku
            // and fill the buckets
            const char seperator = '%';
            IDictionary<string, int> buckets = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (ServiceLimitAvailibility sub in limits)
            {
                string key = $"{sub.SubscriptionId}{seperator}{sub.SkuName}";
                if (!buckets.ContainsKey(key))
                {
                    buckets.Add(key, (int)sub.AvailableInstances);
                }
                else
                {
                    buckets[key] += (int)sub.AvailableInstances;
                }
            }

            telemetryContext.AddContext(nameof(buckets), buckets);

            // Find the best sku-sub combo 
            string bestKey = buckets.Aggregate((x, y) => x.Value > y.Value ? x : y).Key;
            string bestSub = bestKey.Split(seperator)[0];
            string bestSku = bestKey.Split(seperator)[1];
            IList<string> regions = new List<string>();

            // Aggregate all the regions that are of the same sub, sku and are above the quota threshold.
            foreach (ServiceLimitAvailibility sub in limits)
            {
                if (sub.SubscriptionId.Equals(bestSub, StringComparison.OrdinalIgnoreCase)
                    && sub.SkuName.Equals(bestSku, StringComparison.OrdinalIgnoreCase)
                    && sub.AvailableInstances >= threshold)
                {
                    regions.Add(sub.Region);
                }
            }

            IDictionary<string, EnvironmentCandidate> result = new Dictionary<string, EnvironmentCandidate>();
            if (!regions.Any())
            {
                return result;
            }

            result.Add(bestSub, SelectionStrategy.CreateEnvironmentCandidate(bestSub, new List<string>() { bestSku }, regions));

            return result;
        }
    }
}
