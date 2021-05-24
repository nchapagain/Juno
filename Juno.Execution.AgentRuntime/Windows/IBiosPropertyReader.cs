namespace Juno.Execution.AgentRuntime.Windows
{
    using Juno.Execution.AgentRuntime.Contract;

    /// <summary>
    /// Provides method for capturing BiosSettings of physical host.
    /// </summary>
    public interface IBiosPropertyReader
    {
        /// <summary>
        /// method to read all BIOS Settings
        /// </summary>
        /// <returns></returns>
        string ReadBiosSettings();

        /// <summary>
        /// method to read BIOS properties e.g. sps version,bios version.
        /// </summary>
        /// <returns></returns>
        string ReadBiosProperties(AzureHostProperty azureHostProperty);

        /// <summary>
        /// method to read the Intel SPS Version
        /// </summary>
        /// <returns>Intel SPS version</returns>
        string GetIntelSPSVersion();

        /// <summary>
        /// Reads the bios version after reboot. This script and setup
        /// is provided by the BIOS team
        /// </summary>
        /// <returns>Version check result</returns>
        string ReadBiosVersionAfterBoot();

        /// <summary>
        /// Reads the blade's BIOS health. This script and setup is 
        /// provided by the BIOS team
        /// </summary>
        /// <returns>Node BIOS health check result</returns>
        string ReadNodeBiosHealth();

    }
}
