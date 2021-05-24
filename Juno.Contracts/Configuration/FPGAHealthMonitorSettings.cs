namespace Juno.Contracts.Configuration
{
    using System;

    /// <summary>
    /// Represents strongly-typed configuration settings for Juno Host Agent
    /// FPGA health monitoring settings.
    /// </summary>
    public class FpgaHealthMonitorSettings
    {
        /// <summary>
        /// Get or set whether to enable the monitor.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets the interval to monitor for SEL logs on a physical node
        /// where the Juno Host agent is running.
        /// </summary>
        public TimeSpan MonitorInterval { get; set; }
    }
}
