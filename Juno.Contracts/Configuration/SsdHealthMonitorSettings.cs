namespace Juno.Contracts.Configuration
{
    using System;

    /// <summary>
    /// Represents strongly-typed configuration settings for Juno Host Agent
    /// SSD health monitoring settings.
    /// </summary>
    public class SsdHealthMonitorSettings
    {
        /// <summary>
        /// Get or set whether to enable the monitor.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets the interval to monitor for SSD Health on a physical node
        /// where the Juno Host agent is running.
        /// </summary>
        public TimeSpan MonitorInterval { get; set; }
    }
}
