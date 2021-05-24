namespace Juno.Execution.AgentRuntime.Contract
{
    /// <summary>
    /// Properties that represents an Azure node. Cannot be directly accessed by a guest. (eg. VM running GuestAgent)
    /// </summary>
    public enum AzureHostProperty
    {
        /// <summary>
        /// Gets BIOS Manufacturer.
        /// </summary>
        BiosVendor,

        /// <summary>
        ///  Gets BIOS version of physical host from
        ///  Windows registry.
        /// </summary>
        BiosVersion,

        /// <summary>
        /// Gets Cloudclore buildnumber.
        /// </summary>
        CloudCoreBuild,

        /// <summary>
        /// Gets Cloudcore Support buildnumber.
        /// </summary>
        CloudCoreSupportBuild,

        /// <summary>
        ///  Gets Azure clusterId from
        ///  Windows registry
        /// </summary>
        ClusterName,

        /// <summary>
        /// Gets CPU Identifier.
        /// </summary>
        CpuIdentifier,

        /// <summary>
        /// Gets CPU Manufacturer.
        /// </summary>
        CpuManufacturer,

        /// <summary>
        /// Gets Microcodeupdatestatus from
        /// Windows registry.
        /// </summary>
        CpuMicrocodeUpdateStatus,

        /// <summary>
        /// Gets Microcodeversion from
        /// Windows registry.
        /// </summary>
        CpuMicrocodeVersion,

        /// <summary>
        /// Gets CPU model string.
        /// </summary>
        CpuProcessorNameString,

        /// <summary>
        ///  Gets Intel Management Engine version.
        /// </summary>
        IMEVersion,

        /// <summary>
        /// Gets AzureNode Id from
        /// Windows registry.
        /// </summary>
        NodeId,

        /// <summary>
        /// Gets Azurified OS buildnumber.
        /// </summary>
        OsWinAzBuildLabEx,

        /// <summary>
        /// Gets OS buildnumber.
        /// </summary>
        OsWinNtBuildLabEx,

        /// <summary>
        /// Gets OS buildnumber.
        /// </summary>
        OsWinNtCurrentBuildNumber,

        /// <summary>
        /// Gets OS product name.
        /// </summary>
        OsWinNtProductName,

        /// <summary>
        /// Gets OS RelaseBuild Id.
        /// </summary>
        OsWinNtReleaseId,

        /// <summary>
        /// Gets OS build UBR.
        /// </summary>
        OSWinNtUBR,

        /// <summary>
        /// The previous microcode version.
        /// </summary>
        PreviousCpuMicrocodeVersion,

        /// <summary>
        ///  Gets TipSession Id from
        ///  Windows registry.
        /// </summary>
        TipSessionId,

        /// <summary>
        /// The updated microcode version.
        /// </summary>
        UpdatedCpuMicrocodeVersion
    }
}
