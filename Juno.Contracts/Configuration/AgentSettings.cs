namespace Juno.Contracts.Configuration
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents strongly-typed configuration settings for the Juno Guest
    /// agent.
    /// </summary>
    public class AgentSettings
    {
        /// <summary>
        /// Gets the certificate name that should be installed in HostAgent.
        /// </summary>
        public string HostAgentCertificateName { get; set; }

        /// <summary>
        /// Gets the certificate name that should be installed in GuestAgent.
        /// </summary>
        public string GuestAgentCertificateName { get; set; }

        /// <summary>
        /// Gets the interval at which the Juno Guest agent
        /// will send heartbeats.
        /// </summary>
        public TimeSpan HeartbeatInterval { get; set; }

        /// <summary>
        /// Gets the polling interval (in-seconds) at which the Juno Guest agent
        /// will poll for new agent experiment steps/work.
        /// </summary>
        public TimeSpan WorkPollingInterval { get; set; }

        /// <summary>
        /// Gets the settings to use for agent system monitoring operations
        /// that occur in Juno agent processes.
        /// </summary>
        public AgentMonitoringSettings AgentMonitoringSettings { get; set; }

        /// <summary>
        /// Gets the set of API/service endpoints used by agents to communicate
        /// with the Juno system.
        /// </summary>
        public IEnumerable<ApiSettings> Apis { get; set; }

        /// <summary>
        /// Gets the set of AAD principals used by the Juno Guest agent in the
        /// environment.
        /// </summary>
        public IEnumerable<AadPrincipalSettings> AadPrincipals { get; set; }
    }
}
