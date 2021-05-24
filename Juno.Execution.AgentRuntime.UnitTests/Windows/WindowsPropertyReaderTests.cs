namespace Juno.Execution.AgentRuntime.Windows
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Juno.Execution.AgentRuntime.Contract;
    using Microsoft.Win32;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class WindowsPropertyReaderTests
    {
        private const string BiosKey = @"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\BIOS";

        private const string CpuKey = @"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0";

        private const string TipSessionKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\AzureTipNode";

        private const string AzNodeKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\AzureHL\NodeProperties";

        private const string WinAzCurrentVersionKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Azure\CurrentVersion";

        private const string WinNtCurrentVersionKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion";

        private WindowsPropertyReader testReader;
        private Mock<IRegistry> mockRegistry;

        [SetUp]
        public void SetUp()
        {
            this.mockRegistry = new Mock<IRegistry>();
            this.testReader = new WindowsPropertyReader(this.mockRegistry.Object);

            this.mockRegistry.Setup(r => r.Read<IConvertible>(It.IsAny<RegistryHive>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IConvertible>())).Returns(string.Empty);

            this.mockRegistry.Setup(r => r.Read<dynamic>(
                WindowsPropertyReaderTests.BiosKey,
                Constants.BiosVersion,
                It.IsAny<string>())).Returns("C2010.10.3F32.GN1")
                .Verifiable();

            this.mockRegistry.Setup(r => r.Read<dynamic>(
                WindowsPropertyReaderTests.BiosKey,
                Constants.BiosVendor,
                It.IsAny<string>())).Returns("AMI")
                .Verifiable();

            this.mockRegistry.Setup(r => r.Read<dynamic>(
                WindowsPropertyReaderTests.CpuKey,
                Constants.CpuIdentifier,
                It.IsAny<string>())).Returns("Intel Xeon E5-7633")
                .Verifiable();

            this.mockRegistry.Setup(r => r.Read<dynamic>(
                WindowsPropertyReaderTests.CpuKey,
                Constants.CpuManufacturer,
                It.IsAny<string>())).Returns("Intel")
                .Verifiable();

            this.mockRegistry.Setup(r => r.Read<dynamic>(
                WindowsPropertyReaderTests.CpuKey,
                Constants.MicrocodeUpdateStatus,
                int.MinValue)).Returns(0).ToString();

            this.mockRegistry.Setup(r => r.Read<dynamic>(
                WindowsPropertyReaderTests.CpuKey,
                Constants.CpuProcessorNameString,
                It.IsAny<string>())).Returns("Intel Xeon E5-7633")
                .Verifiable();

            this.mockRegistry.Setup(r => r.Read<dynamic>(
                WindowsPropertyReaderTests.AzNodeKey,
                Constants.NodeId,
                It.IsAny<string>())).Returns("9999-0000-1111")
                .Verifiable();

            this.mockRegistry.Setup(r => r.Read<dynamic>(
                WindowsPropertyReaderTests.AzNodeKey,
                Constants.ClusterName,
                It.IsAny<string>())).Returns("b22602803")
                .Verifiable();

            this.mockRegistry.Setup(r => r.Read<dynamic>(
                WindowsPropertyReaderTests.TipSessionKey,
                Constants.TipSessionId,
                It.IsAny<string>())).Returns("9999-0000-1111")
                .Verifiable();

            this.mockRegistry.Setup(r => r.Read<dynamic>(
                WindowsPropertyReaderTests.WinNtCurrentVersionKey,
                Constants.BuildLabEx,
                It.IsAny<string>())).Returns("amd64-9011")
                .Verifiable();

            this.mockRegistry.Setup(r => r.Read<dynamic>(
                WindowsPropertyReaderTests.WinNtCurrentVersionKey,
                Constants.CurrentBuildNumber,
                It.IsAny<string>())).Returns("144939")
                .Verifiable();

            this.mockRegistry.Setup(r => r.Read<dynamic>(
                WindowsPropertyReaderTests.WinNtCurrentVersionKey,
                Constants.Ubr,
                It.IsAny<int>())).Returns(295).ToString();

            this.mockRegistry.Setup(r => r.Read<dynamic>(
                WindowsPropertyReaderTests.WinNtCurrentVersionKey,
                Constants.ProductName,
                It.IsAny<string>())).Returns("Windows server 2019");

            this.mockRegistry.Setup(r => r.Read<dynamic>(
                WindowsPropertyReaderTests.WinNtCurrentVersionKey,
                Constants.ReleaseId,
                It.IsAny<string>())).Returns("1990");

            this.mockRegistry.Setup(r => r.Read<dynamic>(
                WindowsPropertyReaderTests.WinAzCurrentVersionKey,
                Constants.BuildLabEx,
                It.IsAny<string>())).Returns("amd64-1990");
        }

        [Test]
        public void WindowsPropertyPropertyReaderReadsBiosPropertiesFromExpectedLocation()
        {
            string biosVersion = this.testReader.Read(AzureHostProperty.BiosVersion);
            string biosVendor = this.testReader.Read(AzureHostProperty.BiosVendor);
            Assert.AreEqual("C2010.10.3F32.GN1", biosVersion);
            Assert.AreEqual("AMI", biosVendor);
        }

        [Test]
        public void WindowsPropertyPropertyReaderReadsCpuPropertiesFromExpectedLocation()
        {
            string cpuIdentifier = this.testReader.Read(AzureHostProperty.CpuIdentifier);
            string cpuManufacturer = this.testReader.Read(AzureHostProperty.CpuManufacturer);
            string cpuMicrocodeUpdateStatus = this.testReader.Read(AzureHostProperty.CpuMicrocodeUpdateStatus);
            string cpuProcessorNameString = this.testReader.Read(AzureHostProperty.CpuProcessorNameString);

            Assert.AreEqual("Intel Xeon E5-7633", cpuProcessorNameString);
            Assert.AreEqual(int.MinValue.ToString(), cpuMicrocodeUpdateStatus);
            Assert.AreEqual("Intel", cpuManufacturer);
            Assert.AreEqual("Intel Xeon E5-7633", cpuIdentifier);
        }

        [Test]
        public void WindowsPropertyPropertyReaderReadsOsPropertiesFromExpectedLocation()
        {
            string osWinNtBuildLabEx = this.testReader.Read(AzureHostProperty.OsWinNtBuildLabEx);
            string osWinNtCurrentBuildNumber = this.testReader.Read(AzureHostProperty.OsWinNtCurrentBuildNumber);
            string osWinNtReleaseId = this.testReader.Read(AzureHostProperty.OsWinNtReleaseId);
            string oSWinNtUBR = this.testReader.Read(AzureHostProperty.OSWinNtUBR);
            string osWinNtProductName = this.testReader.Read(AzureHostProperty.OsWinNtProductName);
            string osWinAzBuildLabEx = this.testReader.Read(AzureHostProperty.OsWinAzBuildLabEx);

            Assert.AreEqual("amd64-9011", osWinNtBuildLabEx);
            Assert.AreEqual("144939", osWinNtCurrentBuildNumber);
            Assert.AreEqual("1990", osWinNtReleaseId);
            Assert.AreEqual(int.MinValue.ToString(), oSWinNtUBR);
            Assert.AreEqual("Windows server 2019", osWinNtProductName);
            Assert.AreEqual("amd64-1990", osWinAzBuildLabEx);
        }

        [Test]
        public void WindowsPropertyPropertyReaderReadsNodeIdetifierPropertiesFromExpectedLocation()
        {
            string nodeId = this.testReader.Read(AzureHostProperty.NodeId);
            string tipSessionId = this.testReader.Read(AzureHostProperty.TipSessionId);
            string clusterId = this.testReader.Read(AzureHostProperty.ClusterName);

            Assert.AreEqual("9999-0000-1111", tipSessionId);
            Assert.AreEqual("9999-0000-1111", nodeId);
            Assert.AreEqual("b22602803", clusterId);
        }

        [Test]
        public void WindowsPropertyPropertyReaderReadsExpectedPreviousCpuMicrocodeVersionInformation()
        {
            this.mockRegistry.Setup(r => r.Read<byte[]>(
                WindowsPropertyReaderTests.CpuKey,
                Constants.PreviousMicrocodeVersion,
                It.IsAny<byte[]>())).Returns(new byte[] { 0, 0, 0, 0, 36, 0, 0, 2 });

            string microcodeVersion = this.testReader.Read(AzureHostProperty.PreviousCpuMicrocodeVersion);

            Assert.AreEqual("0200002400000000", microcodeVersion);
        }

        [Test]
        public void WindowsPropertyPropertyReaderReadsExpectedUpdatedCpuMicrocodeVersionInformation()
        {
            this.mockRegistry.Setup(r => r.Read<byte[]>(
                WindowsPropertyReaderTests.CpuKey,
                Constants.UpdatedMicrocodeVersion,
                It.IsAny<byte[]>())).Returns(new byte[] { 0, 0, 0, 0, 36, 0, 0, 2 });

            string microcodeVersion = this.testReader.Read(AzureHostProperty.UpdatedCpuMicrocodeVersion);

            Assert.AreEqual("0200002400000000", microcodeVersion);
        }

        private static class Constants
        {
            internal const string Unknown = "Unknown";
            internal const string NodeId = "NodeId";
            internal const string TipSessionId = "TipNodeSessionId";
            internal const string ClusterName = "ClusterName";
            internal const string MicrocodeUpdateStatus = "Update Status";
            internal const string UpdatedMicrocodeVersion = "Update Revision";
            internal const string PreviousMicrocodeVersion = "Previous Update Revision";
            internal const string BuildLabEx = "BuildLabEx";
            internal const string ProductName = "ProductName";
            internal const string Ubr = "UBR";
            internal const string CurrentBuildNumber = "CurrentBuildNumber";
            internal const string ReleaseId = "ReleaseId";
            internal const string CpuManufacturer = "VendorIdentifier";
            internal const string CpuIdentifier = "Identifier";
            internal const string CpuProcessorNameString = "ProcessorNameString";
            internal const string BiosVersion = "BIOSVersion";
            internal const string BiosVendor = "BIOSVendor";
            internal const string VirtualMachineName = "VirtualMachineName";
        }
    }
}
