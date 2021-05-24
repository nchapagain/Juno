namespace Juno.Contracts.Configuration
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents strongly-typed configuration settings for Garbage Collector
    /// </summary>
    public class GarbageCollectorSettings
    {
        /// <summary>
        /// Gets the URI to the Juno Experiment API for the environment.
        /// </summary>
        public Uri ExperimentsApiUri { get; set; }

        /// <summary>
        /// Gets the storage account that is used by azure to manage the Garbage Collector webjob
        /// </summary>
        public string GarbageCollectorStorageAccount { get; set; }

        /// <summary>
        /// Gets the set of AAD principals used by the execution service in the
        /// environment.
        /// </summary>
        public IEnumerable<AadPrincipalSettings> AadPrincipals { get; set; }

        /// <summary>
        /// Gets the set of enabled subscriptionIDs for the environment
        /// </summary>
        public IEnumerable<string> EnabledSubscriptionIds { get; set; }
    }
}
