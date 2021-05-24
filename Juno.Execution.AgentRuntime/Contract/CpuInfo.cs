namespace Juno.Execution.AgentRuntime.Contract
{
    /// <summary>
    /// Defines CPU properties.
    /// </summary>
    public class CpuInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CpuInfo"/> class.
        /// </summary>
        public CpuInfo(
            string cpuIdentifier,
            string cpuManufacturer,
            string cpuMicrocodeUpdateStatus,
            string cpuPreviousMicrocode,
            string cpuProcessorNameString,
            string cpuUpdatedMicrocode,
            string msr0x17F = null,
            string msr0x102 = null)
        {
            this.CpuIdentifier = cpuIdentifier;
            this.CpuManufacturer = cpuManufacturer;
            this.CpuMicrocodeUpdateStatus = cpuMicrocodeUpdateStatus;
            this.CpuPreviousMicrocode = cpuPreviousMicrocode;
            this.CpuProcessorName = cpuProcessorNameString;
            this.CpuUpdatedMicrocode = cpuUpdatedMicrocode;
            this.Msr0x17F = msr0x17F;
            this.Msr0x102 = msr0x102;
        }

        /// <summary>
        /// Cpu Identifier
        /// </summary>
        public string CpuIdentifier { get; }

        /// <summary>
        /// CPU Manufacturer.
        /// </summary>
        public string CpuManufacturer { get; }

        /// <summary>
        /// Microcode update status.
        /// </summary>
        public string CpuMicrocodeUpdateStatus { get; }

        /// <summary>
        /// Microcode version before update.
        /// </summary>
        public string CpuPreviousMicrocode { get; }

        /// <summary>
        /// Processor Name
        /// </summary>
        public string CpuProcessorName { get; }

        /// <summary>
        /// Microcode version after successfull update.
        /// </summary>
        public string CpuUpdatedMicrocode { get; }

        /// <summary>
        /// Defines Patrol scrub feature is updated.
        /// </summary>
        public string Msr0x17F { get; }

        /// <summary>
        /// Defines JCC feature is disabled.
        /// </summary>
        public string Msr0x102 { get; }
    }

    /// <summary>
    /// Provides required registry keys to get CPU properties.
    /// </summary>
    internal static class CpuConstants
    {
        /// <summary>
        /// Registry key to access cpu properties.
        /// </summary>
        public const string CpuKey = @"HARDWARE\DESCRIPTION\System\CentralProcessor\0";

        /// <summary>
        /// MSR register number to read Patrol scrub feature properties.
        /// </summary>
        public const string PatrolScrubMsrReg = "0x17F";

        /// <summary>
        /// MSR register number to read JCC property.
        /// </summary>
        public const string JccMsrReg = "0x102";
    }
}
