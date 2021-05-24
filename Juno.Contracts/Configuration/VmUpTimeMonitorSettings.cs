using System;

namespace Juno.Contracts.Configuration
{
    /// <summary>
    /// Represents strongly-typed configuration settings for Juno guest agent uptime monitor settings.
    /// </summary>
    public class VmUptimeMonitorSettings
    {
        /// <summary>
        /// Get or set whether to enable the monitor.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets the interval to monitor for vm uptime.
        /// </summary>
        public TimeSpan MonitorInterval { get; set; }
    }
}