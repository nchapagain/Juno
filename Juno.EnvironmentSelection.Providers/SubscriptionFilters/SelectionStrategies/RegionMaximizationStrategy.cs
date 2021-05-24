namespace Juno.EnvironmentSelection.SubscriptionFilters
{ 
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Juno.Contracts;
    using Kusto.Cloud.Platform.Utils;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Subscriptions;
    using Microsoft.Azure.CRC.Telemetry;

    /// <summary>
    /// Returns subscriptions organized such that we try to maximize the regions included
    /// </summary>
    public class RegionMaximizationStrategy : SelectionStrategy
    {
        /// <inheritdoc/>
        public override IDictionary<string, EnvironmentCandidate> GetEnvironmentCandidates(IList<ServiceLimitAvailibility> limits, int threshold, EventContext telemetryContext)
        {
            limits.ThrowIfNullOrEmpty(nameof(limits));

            IEnumerable<string> returnedSubIds = limits.Select(sub => sub.SubscriptionId).Distinct();

            IDictionary<string, EnvironmentCandidate> result = new Dictionary<string, EnvironmentCandidate>();
            foreach (string subId in returnedSubIds)
            {
                IDictionary<string, HashSet<string>> skuRegion = null;
                IDictionary<string, HashSet<string>> regionSku = null;
                HashSet<string> regions = null;
                HashSet<string> skus = null;
                IEnumerable<ServiceLimitAvailibility> currentSet = limits.Where(sub => sub.SubscriptionId.Equals(subId, StringComparison.OrdinalIgnoreCase));
                currentSet = currentSet.Where(sub => sub.AvailableInstances >= threshold);
                if (currentSet.Any())
                {
                    this.GenerateGraphForSubscription(currentSet, out skuRegion, out regionSku);
                    this.RetrieveBestRegionSkuSet(skuRegion, regionSku, out skus, out regions);
                    result.Add(subId, SelectionStrategy.CreateEnvironmentCandidate(subId, skus, regions));
                }
            }

            return result;
        }

        private void GenerateGraphForSubscription(
            IEnumerable<ServiceLimitAvailibility> resources,
            out IDictionary<string, HashSet<string>> skuRegion,
            out IDictionary<string, HashSet<string>> regionSku)
        {
            skuRegion = new Dictionary<string, HashSet<string>>();
            regionSku = new Dictionary<string, HashSet<string>>();

            foreach (ServiceLimitAvailibility resource in resources)
            {
                string vmSku = resource.SkuName;
                string region = resource.Region;

                if (!skuRegion.ContainsKey(vmSku))
                {
                    skuRegion.Add(vmSku, new HashSet<string>() { region });
                }
                else
                {
                    skuRegion[vmSku].Add(region);
                }

                if (!regionSku.ContainsKey(region))
                {
                    regionSku.Add(region, new HashSet<string> { vmSku });
                }
                else
                {
                    regionSku[region].Add(vmSku);
                }
            }
        }

        private void RetrieveBestRegionSkuSet(
            IDictionary<string, HashSet<string>> skuRegion,
            IDictionary<string, HashSet<string>> regionSku,
            out HashSet<string> selectedSkus,
            out HashSet<string> selectedRegions)
        {
            selectedSkus = new HashSet<string>();
            selectedRegions = new HashSet<string>();

            // 1. Find the VmSku with the largest out degree.
            string startingSku = skuRegion.Aggregate((x, y) => x.Value.Count > y.Value.Count ? x : y).Key;
            selectedSkus.Add(startingSku);

            // 2. Find all the regions that support the sku
            HashSet<string> regionList = skuRegion[startingSku];
            selectedRegions.AddRange(regionList);

            // 3. Find all skus that ALL regions support.
            IEnumerable<KeyValuePair<string, HashSet<string>>> regionLists = regionSku.Where(region => regionList.Contains(region.Key));
            HashSet<string> intersectionList = regionLists.First().Value;
            foreach (KeyValuePair<string, HashSet<string>> currentSetPair in regionLists)
            {
                HashSet<string> currentSet = currentSetPair.Value;
                HashSet<string> previousIntersection = new HashSet<string>(intersectionList);
                intersectionList.IntersectWith(currentSet);

                // Avoid bad regions where it supports the original vm sku but maybe not a lot of others.
                // rewind the interseciton and remove the region from the region list.
                if (intersectionList.Count < previousIntersection.Count * 0.75)
                {
                    intersectionList = previousIntersection;
                    selectedRegions.Remove(currentSetPair.Key);
                }
            }

            // 4. Add all newly found skus.
            selectedSkus.AddRange(intersectionList);
        }
    }
}
