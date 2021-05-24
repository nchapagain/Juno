namespace Juno.EnvironmentSelection.SubscriptionFilters
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Juno.Contracts;
    using Kusto.Cloud.Platform.Utils;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Subscriptions;
    using Microsoft.Azure.CRC.Telemetry;

    /// <summary>
    /// Abstract class that requires derived class to implement a strategy to find
    /// the best region and vm sku combos for each subscription.
    /// </summary>
    public abstract class SelectionStrategy : ISelectionStrategy
    {
        /// <inheritdoc/>
        public abstract IDictionary<string, EnvironmentCandidate> GetEnvironmentCandidates(IList<ServiceLimitAvailibility> limits, int threshold, EventContext telemetryContext);

        /// <summary>
        /// Offer a way for each strategy to hand back a normalized and expected
        /// Environment Candidate form
        /// </summary>
        /// <param name="subId">Subscription id</param>
        /// <param name="skus">Supported VM Skus</param>
        /// <param name="regions">Supported Regions</param>
        /// <returns></returns>
        protected static EnvironmentCandidate CreateEnvironmentCandidate(string subId, IEnumerable<string> skus, IEnumerable<string> regions)
        {
            subId.ThrowIfNullOrWhiteSpace(nameof(subId));
            skus.ThrowIfNull(nameof(skus));
            regions.ThrowIfNull(nameof(regions));

            IDictionary<string, string> additionalInfo = new Dictionary<string, string>()
            {
                [Constants.RegionSearchSpace] = regions.StringJoin(";", region => region.Trim()),
                [Constants.VmSkuSearchSpace] = skus.StringJoin(";", region => region.Trim())
            };

            return new EnvironmentCandidate(subId, additionalInfo: additionalInfo);
        }

        private class Constants
        {
            internal const string RegionSearchSpace = "regionSearchSpace";
            internal const string VmSkuSearchSpace = "vmSkuSearchSpace";
        }
    }
}
