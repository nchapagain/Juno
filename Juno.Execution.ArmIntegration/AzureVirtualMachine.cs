namespace Juno.Execution.ArmIntegration
{
    using System;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    /// <summary>
    /// Encapsulate VM specifications and the number of VM with given specification
    /// </summary>
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class AzureVirtualMachine
    {
        /// <summary>
        /// Azure VM specifications
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public AzureVmSpecification VmSpecification { get; set; }

        /// <summary>
        /// The number of Vm with given Vm Specification
        /// </summary>
        public int Count { get; set; }
    }

    /// <summary>
    ///  Encapsulate Azure VM specifications
    /// </summary>
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class AzureVmSpecification : ICloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AzureVmSpecification"/> class.
        /// </summary>
        /// <param name="osDiskStorageAccountType">Specifies the storage account type for the os disk. </param>
        /// <param name="vmSize">Sizes for Windows virtual machines in Azure</param>
        /// <param name="osPublisher">The image publisher.</param>
        /// <param name="osOffer">Specifies the offer of the platform image or marketplace image used to create the virtual machine.</param>
        /// <param name="osSku">The image SKU.</param>
        /// <param name="osVersion">Specifies the version of the platform image or marketplace image used to create the virtual machine.</param>
        /// <param name="dataDiskCount">Data disk count</param>
        /// <param name="dataDiskSizeInGB">Data disk size in GB</param>
        /// <param name="dataDiskStorageAccountType">Data disk storage account type</param>
        /// <param name="enableAcceleratedNetworking">should the VM use accelerated networking?</param>
        /// <param name="sigImageReference">Image reference for shared image gallery</param>
        /// <param name="dataDiskSku">Data disk sku</param>
        [JsonConstructor]
        public AzureVmSpecification(
            string osDiskStorageAccountType,
            string vmSize,
            string osPublisher = null,
            string osOffer = null,
            string osSku = null,
            string osVersion = null,
            int? dataDiskCount = null,
            string dataDiskSku = null,
            int? dataDiskSizeInGB = null,
            string dataDiskStorageAccountType = null,
            bool enableAcceleratedNetworking = false,
            string sigImageReference = null)
        {
            this.OsDiskStorageAccountType = osDiskStorageAccountType;
            this.VmSize = vmSize;
            this.OsPublisher = osPublisher;
            this.OsOffer = osOffer;
            this.OsSku = osSku;
            this.OsVersion = osVersion;

            if (dataDiskCount != null)
            {
                this.DataDiskCount = dataDiskCount.Value;
            }

            if (dataDiskSizeInGB != null)
            {
                this.DataDiskSizeInGB = dataDiskSizeInGB.Value;
            }

            if (!string.IsNullOrEmpty(sigImageReference))
            {
                this.SigImageReference = sigImageReference;
            }

            this.DataDiskSku = dataDiskSku;
            this.DataDiskStorageAccountType = dataDiskStorageAccountType;
            this.EnableAcceleratedNetworking = enableAcceleratedNetworking;
        }

        /// <summary>
        /// Get or set osType
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public string OsDiskStorageAccountType { get; set; }

        /// <summary>
        /// Get or set vmSize
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public string VmSize { get; set; }

        /// <summary>
        /// Get or set OsPublisher
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public string OsPublisher { get; }

        /// <summary>
        /// Get or set osOffer
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public string OsOffer { get; }

        /// <summary>
        /// Get or set osSku
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public string OsSku { get; }

        /// <summary>
        /// Get or set osVersion
        /// </summary>
        [JsonProperty(Required = Required.Default)]
        public string OsVersion { get; }

        /// <summary>
        /// Get or set sig image reference
        /// </summary>
        [JsonProperty(Required = Required.Default)]
        public string SigImageReference { get; }

        /// <summary>
        /// Get or set diskCount
        /// </summary>
        [JsonProperty(Required = Required.Default)]
        public int DataDiskCount { get; }

        /// <summary>
        /// Get or set data disk type
        /// </summary>
        [JsonProperty(Required = Required.Default)]
        public string DataDiskSku { get; }

        /// <summary>
        /// Get or set data disk size in GB
        /// </summary>
        [JsonProperty(Required = Required.Default)]
        public int DataDiskSizeInGB { get; }

        /// <summary>
        /// Enables accelerated networking on the VM
        /// </summary>
        [JsonProperty(Required = Required.Default)]
        public bool EnableAcceleratedNetworking { get; }

        /// <summary>
        ///  Get or set data storage account type 
        /// </summary>
        public string DataDiskStorageAccountType { get; private set; }

        /// <inheritdoc/>
        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }
}
