namespace Juno.Execution.AgentRuntime.Contract
{
    /// <summary>
    /// Info related to the machines Bmc.
    /// </summary>
    public class BmcInfo
    {
        /// <summary>
        /// Initialize a new instance of <see cref="BmcInfo"/>
        /// </summary>
        public BmcInfo(string version)
        {
            this.Version = version;
        }

        /// <summary>
        /// Get the version of the Bmc
        /// </summary>
        public string Version { get; }
    }
}
