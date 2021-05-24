namespace Juno.Execution.AgentRuntime.Contract
{
    /// <summary>
    /// Common exit codes for Juno hosts (e.g. agents, services).
    /// </summary>
    public static class HostExitCode
    {
        /// <summary>
        /// The default exit code.
        /// </summary>
        public const int Default = 0;

        /// <summary>
        /// Exit code representing a failure to install an agent/host.
        /// </summary>
        public const int InstallationFailure = 10;

        /// <summary>
        /// Exit code representing an invalid usage of the host (e.g. invalid command-line arguments).
        /// </summary>
        public const int InvalidUsage = 1;

        /// <summary>
        /// Exit code representing a failure to handle an error usually resulting
        /// in the host crashing.
        /// </summary>
        public const int UnhandledError = 1000;
    }
}
