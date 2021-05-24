namespace Juno.Execution.AgentRuntime
{
    using System;
    using Juno.Execution.AgentRuntime.Linux;
    using Juno.Execution.AgentRuntime.Windows;

    /// <summary>
    /// Provides monitors to run in the Juno Host agent process for capturing information
    /// about the physical node/system in operation during experiments.
    /// </summary>
    public static class SystemManagerFactory
    {
        /// <summary>
        /// Create the system manager according to the running OS.
        /// </summary>
        /// <returns></returns>
        public static ISystemManager Get()
        {
            ISystemManager manager;
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32Windows:
                case PlatformID.Win32NT:
                case PlatformID.Win32S:
                case PlatformID.WinCE:
                    manager = WindowsSystemManager.Instance;
                    break;

                case PlatformID.Unix:
                    manager = LinuxSystemManager.Instance;
                    break;

                default:
                    throw new NotSupportedException($"OSPlatform '{Environment.OSVersion.Platform.ToString()}' is not supported.");
            }

            return manager;
        }
    }
}