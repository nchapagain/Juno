namespace Juno.Execution.ArmIntegration
{
    /// <summary>
    /// Constants that represent the OS platform the VMs use.
    /// </summary>
    public static class VmPlatform
    {
        /// <summary>
        /// Windows family of Guest OS
        /// </summary>
        public const string WinX64 = "win-x64";

        /// <summary>
        /// Linux family of Guest OS
        /// </summary>
        public const string LinuxX64 = "linux-x64";

        /// <summary>
        /// Windows family of Guest OS
        /// </summary>
        public const string WinArm64 = "win-arm64";

        /// <summary>
        /// Linux family of Guest OS
        /// </summary>
        public const string LinuxArm64 = "linux-arm64";

        /// <summary>
        /// Return true if the platform is linux.
        /// </summary>
        /// <param name="platform">Platform runtime identifier.</param>
        /// <returns>If the platform is linux</returns>
        public static bool IsLinux(string platform)
        {
            return (platform == VmPlatform.LinuxArm64 || platform == VmPlatform.LinuxX64);
        }
    }
}
