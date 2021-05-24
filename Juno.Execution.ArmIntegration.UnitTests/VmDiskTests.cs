namespace Juno.Execution.ArmIntegration
{
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class VmDiskTests
    {
        [Test]
        public void VmDiskToStringCreatesTheExpectedFormattedStringRepresentation()
        {
            string expectedDiskInfo = "storageAccountType=Premium_LRS,sku=Standard_LRS,lun=1,sizeInGB=32";
            VmDisk disk = new VmDisk(1, "Standard_LRS", 32, "Premium_LRS");

            string actualDiskInfo = VmDisk.ToString(disk);
            Assert.AreEqual(expectedDiskInfo, actualDiskInfo);
        }

        [Test]
        public void VmDiskParseDiskCreatesTheExpectedVmDiskFromAFormattedStringRepresentation()
        {
            string diskInfo = "storageAccountType=Premium_LRS,sku=Standard_LRS,lun=1,sizeInGB=32";
            VmDisk disk = VmDisk.ParseDisk(diskInfo);

            Assert.AreEqual(disk.DiskSizeGB, 32);
            Assert.AreEqual(disk.Lun, 1);
            Assert.AreEqual(disk.Sku, "Standard_LRS");
            Assert.AreEqual(disk.StorageAccountType, "Premium_LRS");
        }
    }
}
