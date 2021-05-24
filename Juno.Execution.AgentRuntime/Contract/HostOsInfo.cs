namespace Juno.Execution.AgentRuntime.Contract
{
    /// <summary>
    /// Defines OS properties.
    /// </summary>
    public class HostOsInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HostOsInfo"/> class.
        /// </summary>
        public HostOsInfo(
            string osWinNtBuildLabEx,
            string osWinNtCurrentBuildNumber,
            string osWinNtReleaseId,
            string oSWinNtUBR,
            string osWinNtProductName,
            string osWinAzBuildLabEx,
            string cloudCoreBuild = null,
            string cloudCoreSupportBuild = null)
        {
            this.OsWinNtBuildLabEx = osWinNtBuildLabEx;
            this.OsWinNtCurrentBuildNumber = osWinNtCurrentBuildNumber;
            this.OsWinNtReleaseId = osWinNtReleaseId;
            this.OSWinNtUBR = oSWinNtUBR;
            this.OsWinNtProductName = osWinNtProductName;
            this.OsWinAzBuildLabEx = osWinAzBuildLabEx;
            this.CloudCoreBuild = cloudCoreBuild;
            this.CloudCoreSupportBuild = cloudCoreSupportBuild;
        }

        /// <summary>
        /// value of HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\BuildLabEx
        /// registry.
        /// </summary>
        public string OsWinNtBuildLabEx { get; }

        /// <summary>
        /// value of  HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\CurrentBuildNumber
        /// registry.
        /// </summary>
        public string OsWinNtCurrentBuildNumber { get; }

        /// <summary>
        /// value of HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ReleaseId
        /// registry.
        /// </summary>
        public string OsWinNtReleaseId { get; }

        /// <summary>
        /// value of HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\UBR
        /// registry.
        /// </summary>
        public string OSWinNtUBR { get; }

        /// <summary>
        /// value of HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\UBR
        /// registry.
        /// </summary>
        public string OsWinNtProductName { get; }

        /// <summary>
        /// value of HKLM\SOFTWARE\Microsoft\Windows Azure\CurrentVersion\BuildLabEx
        /// registry.
        /// </summary>
        public string OsWinAzBuildLabEx { get; }

        /// <summary>
        /// value of HKLM\SOFTWARE\Microsoft\HyperCopy\CloudCore\BuildEx
        /// registry.
        /// </summary>
        public string CloudCoreBuild { get; }

        /// <summary>
        /// value of HKLM\SOFTWARE\Microsoft\HyperCopy\CloudCoreSupport\BuildEx
        /// registry.
        /// </summary>
        public string CloudCoreSupportBuild { get; }
    }

    /// <summary>
    /// Provides required registry keys to get OS properties.
    /// </summary>
    internal static class OSConstants
    {
        /// <summary>
        /// Base registry key for Azure buildlabEx.
        /// </summary>
        public const string WinAzCurrentVersionKey = @"SOFTWARE\Microsoft\Windows Azure\CurrentVersion";

        /// <summary>
        /// Base registry key for Windows NT build properties.
        /// </summary>
        public const string WinNtCurrentVersionKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

        /// <summary>
        /// Base registry key for Azure CloudCore build properties.
        /// </summary>
        public const string CloudCoreKey = @"SOFTWARE\Microsoft\HyperCopy\CloudCore";

        /// <summary>
        /// Base registry key for Azure CloudCoreSupport build properties.
        /// </summary>
        public const string CloudCoreSupprotKey = @"SOFTWARE\Microsoft\HyperCopy\CloudCoreSupport";
    }
}
