namespace Juno.Execution.AgentRuntime.Contract
{
    /// <summary>
    /// Defines BIOS properties.
    /// </summary>
    public class BiosInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BiosInfo"/> class.
        /// </summary>
        public BiosInfo(string biosVersion, string biosVendor, string biosSpsVersion)
        {
            this.BiosVersion = biosVersion;
            this.BiosVendor = biosVendor;
            this.SpsVersion = biosSpsVersion;
        }

        /// <summary>
        /// BIOS version.
        /// </summary>
        public string BiosVersion { get; }

        /// <summary>
        /// BIOS developer.
        /// </summary>
        public string BiosVendor { get;  }

        /// <summary>
        /// IntelME version.
        /// </summary>
        public string SpsVersion { get; }
    }

    /// <summary>
    /// Provides required registry keys to get BIOS properties.
    /// </summary>
    internal static class BiosConstants
    {
        /// <summary>
        /// Registry key for BIOS properties.
        /// </summary>
        public const string BiosKey = @"HARDWARE\DESCRIPTION\System\BIOS";
    }
}
