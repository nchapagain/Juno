namespace Juno.EnvironmentSelection
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Constants used throughout the Environment Selection Providers assembly.
    /// </summary>
    public static class ProviderConstants
    {
        /// <summary>
        /// A logical container for resources allocated to an azure account
        /// </summary>
        public const string Subscription = nameof(ProviderConstants.Subscription);

        /// <summary>
        /// Identifier for a unique Cluster.
        /// </summary>
        public const string ClusterId = nameof(ProviderConstants.ClusterId);

        /// <summary>
        /// Name of the machine pool for the node. A cluster consists of multiple machine pools
        /// </summary>
        public const string MachinePoolName = nameof(ProviderConstants.MachinePoolName);

        /// <summary>
        /// Unique identifier for a node in the azure cloud
        /// </summary>
        public const string NodeId = nameof(ProviderConstants.NodeId);

        /// <summary>
        /// An azure defined Region. There are three representations of region CRP, Fabric and External.
        /// </summary>
        public const string Region = nameof(ProviderConstants.Region);

        /// <summary>
        /// The specific Sku of Virtual machine
        /// </summary>
        public const string VmSku = nameof(ProviderConstants.VmSku);

        /// <summary>
        /// The specific hardware Sku of a node.
        /// </summary>
        public const string HwSku = nameof(ProviderConstants.HwSku);

        /// <summary>
        /// The specific Sku of a cluster (app, str, fpg, etc).
        /// </summary>
        public const string ClusterSku = nameof(ProviderConstants.ClusterSku);

        /// <summary>
        /// The Id of the CPU. This has a rough mapping to generation of CPU
        /// (i.e. 50654 -> Gen6)
        /// </summary>
        public const string CpuId = nameof(ProviderConstants.CpuId);

        /// <summary>
        /// The physical rack in  the azure cloud. Can contain several nodes. 
        /// </summary>
        public const string Rack = nameof(ProviderConstants.Rack);

        /// <summary>
        /// The version of the Bios on a physical node.
        /// </summary>
        public const string BiosVersion = nameof(ProviderConstants.BiosVersion);

        /// <summary>
        /// The family of SSD a certain node has.
        /// </summary>
        public const string SsdFamily = nameof(ProviderConstants.SsdFamily);

        /// <summary>
        /// The firmware of an SSD.
        /// </summary>
        public const string SsdFirmware = nameof(ProviderConstants.SsdFirmware);

        /// <summary>
        /// The model number of an SSD.
        /// </summary>
        public const string SsdModel = nameof(ProviderConstants.SsdModel);

        /// <summary>
        /// Either System Drive or Data Drive
        /// </summary>
        public const string SsdDriveType = nameof(ProviderConstants.SsdDriveType);

        /// <summary>
        /// The firmware on SoC
        /// </summary>
        public const string SoCFip = nameof(ProviderConstants.SoCFip);

        /// <summary>
        /// The firmware of cerberus on soc
        /// </summary>
        public const string SoCCerberus = nameof(ProviderConstants.SoCCerberus);

        /// <summary>
        /// Firmware of Nitro on soc
        /// </summary>
        public const string SoCNitro = nameof(ProviderConstants.SoCNitro);

        /// <summary>
        /// The description of the CPU.
        /// </summary>
        public const string CpuDescription = nameof(ProviderConstants.CpuDescription);

        /// <summary>
        /// Constant that can be applied to operations that use a contains.
        /// (i.e. the root string in: Cpudescription contains 8171M is 8171M)
        /// Used for mapping values onto the cache.
        /// </summary>
        public const string IncludeRootString = nameof(ProviderConstants.IncludeRootString);

        /// <summary>
        /// Constant that can be applied to operations that use a contains.
        /// (i.e. the root string in: Cpudescription !contains 8171M is 8171M)
        /// Used for mapping values onto the cache.
        /// </summary>
        public const string ExcludeRootString = nameof(ProviderConstants.ExcludeRootString);

        /// <summary>
        /// The numbe of remaining tip sessions a cluster can support.
        /// </summary>
        public const string RemainingTipSessions = nameof(ProviderConstants.RemainingTipSessions);

        /// <summary>
        /// Constant that supports the mapping of filters to cache and the kusto results to 
        /// cache values.
        /// </summary>
        public const string TipSessionLowerBound = nameof(ProviderConstants.TipSessionLowerBound);

        /// <summary>
        /// Version of microcode currently on a node.
        /// </summary>
        public const string Microcode = nameof(ProviderConstants.Microcode);

        /// <summary>
        /// The OS build currently occupying a physical node
        /// </summary>
        public const string OsBuildUbr = nameof(ProviderConstants.OsBuildUbr);

        /// <summary>
        /// Constant that supports the mapping of filters to cache and the kusto results to 
        /// cache values.
        /// </summary>
        public const string MinVmCount = nameof(ProviderConstants.MinVmCount);

        /// <summary>
        /// Specifies an experiment name parameter.
        /// </summary>
        public const string ExperimentName = nameof(ProviderConstants.ExperimentName);
    }
}
