namespace Juno.GarbageCollector
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;

    /// <summary>
    /// Interface to assist with Garbage Collector Actions
    /// i.e. Getting leaked resources, Cleaning Leaked resources.
    /// </summary>
    public interface IGarbageCollector
    {
        /// <summary>
        /// Helper method to getting Juno Leaked Resource
        /// </summary>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns> Key-Value Pair of ResourceId and  <see cref="LeakedResource"/></returns>
        Task<IDictionary<string, LeakedResource>> GetLeakedResourcesAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Helper method to Clean Leaked Resources.
        /// </summary>
        /// <param name="leakedResources">Key-Value Pair of ResourceID and <see cref="LeakedResource"/></param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns>Key Value Pair of (ResourceId, ExperimentID) </returns>
        Task<IDictionary<string, string>> CleanupLeakedResourcesAsync(IDictionary<string, LeakedResource> leakedResources, CancellationToken cancellationToken);
    }
}
