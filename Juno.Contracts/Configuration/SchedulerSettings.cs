namespace Juno.Contracts.Configuration
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents strongly-typed configuration settings for Juno scheduler
    /// </summary>
    public class SchedulerSettings
    {
        /// <summary>
        /// Gets the URI to the Juno Experiment API for the environment.
        /// </summary>
        public Uri ExperimentsApiUri { get; set; }

        /// <summary>
        /// Gets the storage account that is used by azure to manage the scheduler webjob
        /// </summary>
        public string SchedulerStorageAccount { get; set; }

        /// <summary>
        /// Gets the set of AAD principals used by the execution service in the
        /// environment.
        /// </summary>
        public IEnumerable<AadPrincipalSettings> AadPrincipals { get; set; }
    }
}
