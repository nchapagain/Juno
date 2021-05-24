using System;

namespace Juno.Contracts.Configuration
{
    /// <summary>
    /// Represents strongly-typed configuration settings for Juno host agent sel log monitor settings.
    /// </summary>
    public class SelLogMonitorSettings
    {
        /// <summary>
        /// Get or set whether to enable the monitor.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets the path to the ipmiutil.exe on an Azure physical node.
        /// </summary>
        public string IpmiUtilExePath { get; set; }

        /// <summary>
        /// Gets the command to execute on the ipmiutil.exe utility to capture/snapshot
        /// a SEL log on an Azure physical node.
        /// </summary>
        public string IpmiUtilSelLogCommand { get; set; }

        /// <summary>
        /// Gets the interval to monitor for SEL logs on a physical node
        /// where the Juno Host agent is running.
        /// </summary>
        public TimeSpan MonitorInterval { get; set; }
    }
}
