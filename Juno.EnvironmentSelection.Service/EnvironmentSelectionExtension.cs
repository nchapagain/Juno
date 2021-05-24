namespace Juno.EnvironmentSelection.Service
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Extensions;

    /// <summary>
    /// Extension methods for the environment selection service.
    /// </summary>
    public static class EnvironmentSelectionExtension
    {
        /// <summary>
        /// Finds the intersection between two Dictionaries of Environment Candidates
        /// </summary>
        /// <param name="left">One Dictionary to perform the intersection on</param>
        /// <param name="right">The other Dictionary to perform the intersection on</param>
        /// <returns>A new Dictionary containing the intersection of the given lists.</returns>
        internal static IDictionary<string, EnvironmentCandidate> Intersection(this IDictionary<string, EnvironmentCandidate> left, IDictionary<string, EnvironmentCandidate> right)
        {
            left.ThrowIfNull(nameof(left));
            right.ThrowIfNull(nameof(right));

            IDictionary<string, EnvironmentCandidate> intersection = new Dictionary<string, EnvironmentCandidate>();
            ParallelQuery<KeyValuePair<string, EnvironmentCandidate>> query = from leftCandidate in left.AsParallel().AsUnordered()
                        where right.ContainsKey(leftCandidate.Key)
                        select EnvironmentSelectionExtension.JoinCandidates(leftCandidate.Value, right[leftCandidate.Key], leftCandidate.Key);

            ConcurrentDictionary<string, EnvironmentCandidate> bag = new ConcurrentDictionary<string, EnvironmentCandidate>();
            query.ForAll(e => bag.TryAdd(e.Key, e.Value));

            return bag;
        }

        /// <summary>
        /// Projects the subscription values onto a set of nodes
        /// </summary>
        /// <param name="subscription">The Environment Candidate that contains info about the subscription</param>
        /// <param name="nodes">A list of envrionment candidates that contain info about the nodes selected</param>
        /// <returns></returns>
        internal static IEnumerable<EnvironmentCandidate> ProjectSubscription(this EnvironmentCandidate subscription, IEnumerable<EnvironmentCandidate> nodes)
        {
            subscription.ThrowIfNull(nameof(subscription));
            nodes.ThrowIfNull(nameof(nodes));

            IList<EnvironmentCandidate> result = new List<EnvironmentCandidate>();
            foreach (EnvironmentCandidate candidate in nodes)
            {
                if (subscription?.AdditionalInfo != null)
                {
                    candidate.AdditionalInfo.AddRange(subscription.AdditionalInfo);
                }

                result.Add(new EnvironmentCandidate(
                    subscription != null ? subscription.Subscription : candidate.Subscription,
                    candidate.ClusterId,
                    candidate.Region,
                    candidate.MachinePoolName,
                    candidate.Rack,
                    candidate.NodeId,
                    candidate.VmSku,
                    candidate.CpuId,
                    candidate.AdditionalInfo));
            }

            return result;
        }

        private static KeyValuePair<string, EnvironmentCandidate> JoinCandidates(EnvironmentCandidate left, EnvironmentCandidate right, string key)
        {
            return new KeyValuePair<string, EnvironmentCandidate>(key, new EnvironmentCandidate(
                        EnvironmentSelectionExtension.GetNonWildcardValue(left.Subscription, right.Subscription),
                        EnvironmentSelectionExtension.GetNonWildcardValue(left.ClusterId, right.ClusterId),
                        EnvironmentSelectionExtension.GetNonWildcardValue(left.Region, right.Region),
                        EnvironmentSelectionExtension.GetNonWildcardValue(left.MachinePoolName, right.MachinePoolName),
                        EnvironmentSelectionExtension.GetNonWildcardValue(left.Rack, right.Rack),
                        EnvironmentSelectionExtension.GetNonWildcardValue(left.NodeId, right.NodeId),
                        EnvironmentSelectionExtension.GetNonWildcardValue(left.VmSku, right.VmSku),
                        EnvironmentSelectionExtension.GetNonWildcardValue(left.CpuId, right.CpuId),
                        left.AdditionalInfo.Union(right.AdditionalInfo).ToDictionary(entry => entry.Key, entry => entry.Value)));
        }

        private static string GetNonWildcardValue(string left, string right)
        {
            const string wildcard = "*";
            return !left.Equals(wildcard, StringComparison.OrdinalIgnoreCase) ? left : !right.Equals(wildcard, StringComparison.OrdinalIgnoreCase) ? right : wildcard;
        }

        private static IList<string> GetNonWildcardValue(IList<string> left, IList<string> right)
        {
            return left.Count == 0 ? right : left;
        }
    }
}
