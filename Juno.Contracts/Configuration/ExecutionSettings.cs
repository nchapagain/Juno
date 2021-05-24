namespace Juno.Contracts.Configuration
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents strongly-typed configuration settings for services or
    /// agents in the Juno system.
    /// </summary>
    public class ExecutionSettings : SettingsBase
    {
        /// <summary>
        /// Gets the URI to the Juno Execution API for the environment.
        /// </summary>
        public Uri ExecutionApiUri { get; set; }

        /// <summary>
        /// Gets the URI to the Juno Environments API for the environment.
        /// </summary>
        public Uri EnvironmentApiUri { get; set; }

        /// <summary>
        /// Gets the name of the queue on which notices-of-work are placed
        /// for experiments to be executed.
        /// </summary>
        public string WorkQueueName { get; set; }

        /// <summary>
        /// Gets the polling interval (in-seconds) at which the execution service
        /// will poll for new experiment steps/work.
        /// </summary>
        public TimeSpan WorkPollingInterval { get; set; }

        /// <summary>
        /// Gets the set of AAD principals used by the execution service in the
        /// environment.
        /// </summary>
        public IEnumerable<AadPrincipalSettings> AadPrincipals { get; set; }

        /// <summary>
        /// Gets the set of installers used to install agents on VMs running
        /// in the system.
        /// </summary>
        public IEnumerable<InstallerSettings> Installers { get; set; }

        /// <summary>
        /// Gets the set of NuGet feeds from which agent packages are downloaded.
        /// </summary>
        public IEnumerable<NuGetFeedSettings> NuGetFeeds { get; set; }

        /// <summary>
        /// Gets the set of Authorization Settings used by the execution service in the
        /// environment.
        /// </summary>
        public IEnumerable<AuthorizationSettings> AuthorizationSettings { get; set; }
    }
}
