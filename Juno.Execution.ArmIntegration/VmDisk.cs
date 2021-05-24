namespace Juno.Execution.ArmIntegration
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    /// <summary>
    /// Virtual machine disk.
    /// </summary>
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class VmDisk
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="VmDisk"/> class.
        /// </summary>
        /// <param name="lun">Disk index</param>
        /// <param name="sku">Disk sku</param>
        /// <param name="storageAccountType">Disk storage account type</param>
        /// <param name="diskSizeGB">Disk size in GB</param>
        public VmDisk(int lun, string sku, int diskSizeGB, string storageAccountType)
        {
            this.Lun = lun;
            this.Sku = sku;
            this.StorageAccountType = storageAccountType;
            this.DiskSizeGB = diskSizeGB;
        }

        /// <summary>
        /// Get the disk Specifies the logical unit number of the data disk. 
        /// This value is used to identify data disks within the VM and therefore must be unique 
        /// for each data disk attached to a VM.
        /// https://docs.microsoft.com/en-us/azure/templates/microsoft.compute/2019-07-01/virtualmachines#DataDisk
        /// </summary>
        public int Lun { get; }

        /// <summary>
        /// Get The sku name. - Standard_LRS, Premium_LRS, StandardSSD_LRS, UltraSSD_LRS
        /// https://docs.microsoft.com/en-us/azure/templates/microsoft.compute/2019-07-01/disks#DiskSku
        /// </summary>
        public string Sku { get; }

        /// <summary>
        /// Get storage account type
        /// Specifies the storage account type for the managed disk. NOTE: 
        /// UltraSSD_LRS can only be used with data disks, it cannot be used with OS Disk. 
        /// - Standard_LRS, Premium_LRS, StandardSSD_LRS, UltraSSD_LRS
        /// https://docs.microsoft.com/en-us/azure/templates/microsoft.compute/2019-07-01/virtualmachines#ManagedDiskParameters
        /// </summary>
        public string StorageAccountType { get; }

        /// <summary>
        ///  Get disk size in GB
        /// </summary>
        public int DiskSizeGB { get; }

        /// <summary>
        /// Create CreateVirtualMachineDisk collections
        /// </summary>
        /// <param name="diskCount">Disk count</param>
        /// <param name="diskSku">Disk sku in ARM.</param>
        /// <param name="diskSizeInGB">Disk size in GB.</param>
        /// <param name="storageAccountType">Storage account type</param>
        /// <returns></returns>
        public static IList<VmDisk> CreateVirtualMachineDisk(int diskCount, string diskSku, int diskSizeInGB, string storageAccountType)
        {
            IList<VmDisk> vDisks = new List<VmDisk>();

            for (int i = 0; i < diskCount; i++)
            {
                vDisks.Add(new VmDisk(i, diskSku, diskSizeInGB, storageAccountType));
            }

            return vDisks;
        }

        /// <summary>
        /// Parses the disk information from the delimited/formatted string.
        /// </summary>
        /// <param name="diskInfo">
        /// The delimited/formatted disk information string (e.g. storageAccountType=Standard_LRS,sku=Standard_LRS,lun=0,sizeInGB=32).
        /// </param>
        public static VmDisk ParseDisk(string diskInfo)
        {
            diskInfo.ThrowIfNullOrWhiteSpace(nameof(diskInfo));

            VmDisk disk = null;
            Match match = Regex.Match(
                diskInfo,
                $"storageAccountType=([a-z0-9_]+),sku=([a-z0-9_]+),lun=([0-9]+),sizeInGB=([1-9]+)",
                RegexOptions.IgnoreCase);

            if (match.Success)
            {
                disk = new VmDisk(
                    int.Parse(match.Groups[3].Value),
                    match.Groups[2].Value,
                    int.Parse(match.Groups[4].Value),
                    match.Groups[1].Value);
            }

            return disk;
        }

        /// <summary>
        /// Parses the disk information from the delimited/formatted string.
        /// </summary>
        /// <param name="diskInfo">
        /// The delimited/formatted disk information string (e.g. storageAccountType=Standard_LRS,sku=Standard_LRS,lun=0,sizeInGB=32).
        /// </param>
        public static IEnumerable<VmDisk> ParseDisks(string diskInfo)
        {
            diskInfo.ThrowIfNullOrWhiteSpace(nameof(diskInfo));

            List<VmDisk> disks = new List<VmDisk>();
            string[] individualDisks = diskInfo.Split(new char[] { '|' }, System.StringSplitOptions.RemoveEmptyEntries);

            if (individualDisks?.Any() == true)
            {
                individualDisks.ToList().ForEach(disk => disks.Add(VmDisk.ParseDisk(disk)));
            }

            return disks;
        }

        /// <summary>
        /// Returns a delimited string representation of the VM disk 
        /// (ex: storageAccountType=Standard_LRS,sku=Standard_LRS,lun=0,sizeInGB=32).
        /// </summary>
        public static string ToString(VmDisk disk)
        {
            disk.ThrowIfNull(nameof(disk));
            return $"storageAccountType={disk.StorageAccountType},sku={disk.Sku},lun={disk.Lun},sizeInGB={disk.DiskSizeGB}";
        }

        /// <summary>
        /// Returns a delimited string representation of the VM disk 
        /// (ex: storageAccountType=Standard_LRS,sku=Standard_LRS,lun=0,sizeInGB=32).
        /// </summary>
        public static string ToString(IEnumerable<VmDisk> disks)
        {
            disks.ThrowIfNull(nameof(disks));

            List<string> diskInfo = new List<string>();
            disks.ToList().ForEach(disk => diskInfo.Add(VmDisk.ToString(disk)));

            return string.Join("|", diskInfo);
        }
    }
}