namespace Juno.Contracts.Configuration
{
    using System.Collections.Generic;

    /// <summary>
    /// Environment Selection Service Test Integration tests
    /// </summary>
    public class EnvironmentSelectionSettings
    {
        /// <summary>
        /// Example Azure Regions
        /// </summary>
        public IEnumerable<string> Regions { get; set; }
        
        /// <summary>
        /// Example Azure CpuIds
        /// </summary>
        public IEnumerable<string> CpuIds { get; set; }

        /// <summary>
        /// Example Azure HwSku
        /// </summary>
        public IEnumerable<string> HwSkus { get; set; }

        /// <summary>
        /// Example Azure ClusterSkus
        /// </summary>
        public IEnumerable<string> ClusterSkus { get; set; }

        /// <summary>
        /// Example Azure Bios Versions
        /// </summary>
        public IEnumerable<string> BiosVersions { get; set; }
        
        /// <summary>
        /// Example Azure OS Versions
        /// </summary>
        public IEnumerable<string> OSVersions { get; set; }

        /// <summary>
        /// Example Azure SSD Family
        /// </summary>
        public IEnumerable<string> SsdFamilies { get; set; }

        /// <summary>
        /// Example Clusters that target SSDs of interest
        /// </summary>
        public IEnumerable<string> SsdClusters { get; set; }

        /// <summary>
        /// Example azure SSD Models.
        /// </summary>
        public IEnumerable<string> SsdModels { get; set; }

        /// <summary>
        /// Example Azure SSD firmwares.
        /// </summary>
        public IEnumerable<string> SsdFirmwares { get; set; }
        
        /// <summary>
        /// Example Azure VmSkus
        /// </summary>
        public IEnumerable<string> VmSkus { get; set; }
        
        /// <summary>
        /// Example Azure Microcode
        /// </summary>
        public IEnumerable<string> MicrocodeVersions { get; set; }
        
        /// <summary>
        /// Example Clusters in azure fleet.
        /// </summary>
        public IEnumerable<string> ExampleClusters { get; set; }
        
        /// <summary>
        /// Example CpuDescription root string.
        /// </summary>
        public string CpuDescriptionRootString { get; set; }

        /// <summary>
        /// Example SoC Fip firmwares
        /// </summary>
        public IEnumerable<string> SoCFipFirmwares { get; set; }

        /// <summary>
        /// Example SoC Cerberus Firmwares
        /// </summary>
        public IEnumerable<string> SoCCerberusFirmwares { get; set; }

        /// <summary>
        /// Example SoC Nitro firmwares.
        /// </summary>
        public IEnumerable<string> SoCNitroFirmwares { get; set; }

        /// <summary>
        /// Example Experiment Names
        /// </summary>
        public IEnumerable<string> ExperimentNames { get; set; }
    }
}
