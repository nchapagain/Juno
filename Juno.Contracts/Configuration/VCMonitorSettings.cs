using System;

namespace Juno.Contracts.Configuration
{
    /// <summary>
    /// Represents strongly-typed configuration settings for Juno host agent virtual client lite monitor settings.
    /// </summary>
    public class VCMonitorSettings
    {
        /// <summary>
        /// Get or set whether to enable the monitor.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets the interval to monitor for virtual client lite on a physical node
        /// where the Juno Host agent is running.
        /// </summary>
        public TimeSpan MonitorInterval { get; set; }
    }
}
