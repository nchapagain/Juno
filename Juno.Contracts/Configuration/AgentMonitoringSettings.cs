namespace Juno.Contracts.Configuration
{
    /// <summary>
    /// Represents strongly-typed configuration settings for Juno agent
    /// system monitoring settings.
    /// </summary>
    public class AgentMonitoringSettings
    {
        /// <summary>
        /// SEL log monitoring settings.
        /// </summary>
        public SelLogMonitorSettings SelLogMonitorSettings { get; set; }

        /// <summary>
        /// System info monitoring settings (e.g. node, BIOS, CPU).
        /// </summary>
        public SystemInfoMonitorSettings SystemInfoMonitorSettings { get; set; }

        /// <summary>
        /// VM Uptime monitoring settings.
        /// </summary>
        public VmUptimeMonitorSettings VmUptimeMonitorSettings { get; set; }

        /// <summary>
        /// FPGA Health monitoring settings.
        /// </summary>
        public FpgaHealthMonitorSettings FpgaHealthMonitorSettings { get; set; }

        /// <summary>
        /// Ssd Health Monitor Settings
        /// </summary>
        public SsdHealthMonitorSettings SsdHealthMonitorSettings { get; set; }

        /// <summary>
        /// Virtual Client Lite monitoring settings.
        /// </summary>
        public VCMonitorSettings VCMonitorSettings { get; set; }
    }
}
