namespace Juno.Execution.AgentRuntime.Windows
{
    using System;
    using System.IO;
    using System.IO.Abstractions;
    using System.Threading.Tasks;
    using Juno.Execution.AgentRuntime.Contract;
    using Microsoft.Azure.CRC;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class FpgaHealthReaderTests
    {
        private Mock<IFileSystem> mockFileSystem = new Mock<IFileSystem>();
        private Mock<IProcessExecution> mockProcessExecution = new Mock<IProcessExecution>();
        private ProcessExecutionResult processExecutionResult = new ProcessExecutionResult();
        private IFirmwareReader<FpgaHealth> fpgaReader;

        [SetUp]
        public void SetupTest()
        {
            this.fpgaReader = new FpgaReader(this.mockProcessExecution.Object, this.mockFileSystem.Object);    
        }

        [Test]
        public void FpgaReaderCanParseHealthStringForFPGAConfig()
        {
            this.SuccessCaseTestSetup();

            FpgaHealth fpgaHealth = this.fpgaReader.Read();

            Assert.AreEqual(fpgaHealth.FPGAConfig.IsStatusOK, true);
            Assert.AreEqual(fpgaHealth.FPGAConfig.BoardName, "LongsPeak");
            Assert.AreEqual(fpgaHealth.FPGAConfig.RoleID, "0x601d");
            Assert.AreEqual(fpgaHealth.FPGAConfig.RoleVersion, "0xca7b030c");
            Assert.AreEqual(fpgaHealth.FPGAConfig.ShellID, "0xbed70c");
            Assert.AreEqual(fpgaHealth.FPGAConfig.ShellVersion, "0x3000c");
            Assert.AreEqual(fpgaHealth.FPGAConfig.IsGolden, true);
        }

        [Test]
        public void FpgaReaderCanParseHealthStringForFPGAId()
        {
            this.SuccessCaseTestSetup();

            FpgaHealth fpgaHealth = this.fpgaReader.Read();

            Assert.AreEqual(fpgaHealth.FPGAID.IsStatusOK, true);
            Assert.AreEqual(fpgaHealth.FPGAID.ChipID, "25299256306433798");
        }

        [Test]
        public void FpgaReaderCanParseHealthStringForFPGAClockReset()
        {
            this.SuccessCaseTestSetup();

            FpgaHealth fpgaHealth = this.fpgaReader.Read();

            Assert.AreEqual(fpgaHealth.FPGAClockReset.IsStatusOK, true);
        }

        [Test]
        public void FpgaReaderCanParseHealthStringForFPGATemperature()
        {
            this.SuccessCaseTestSetup();

            FpgaHealth fpgaHealth = this.fpgaReader.Read();

            Assert.AreEqual(fpgaHealth.FPGATemperature.IsStatusOK, true);
            Assert.AreEqual(fpgaHealth.FPGATemperature.IsTemperatureWarningPresent, false);
            Assert.AreEqual(fpgaHealth.FPGATemperature.Temperature, "68");
        }

        [Test]
        public void FpgaReaderCanParseHealthStringForFPGANetwork()
        {
            this.SuccessCaseTestSetup();

            FpgaHealth fpgaHealth = this.fpgaReader.Read();

            Assert.AreEqual(fpgaHealth.FPGANetwork.IsStatusOK, true);
            Assert.AreEqual(fpgaHealth.FPGANetwork.SoftNetworkStatus, "0x0");
            Assert.AreEqual(fpgaHealth.FPGANetwork.TorMacLanesDeskew, "1");
            Assert.AreEqual(fpgaHealth.FPGANetwork.TorMacLanesStable, "1");
            Assert.AreEqual(fpgaHealth.FPGANetwork.TorMacHardwareError, "0x0");
            Assert.AreEqual(fpgaHealth.FPGANetwork.TorMacPcsHardwareError, "0x0");
            Assert.AreEqual(fpgaHealth.FPGANetwork.TorMacLinkDrops, "0");
            Assert.AreEqual(fpgaHealth.FPGANetwork.TorMacReceiveFcsErrors, "0");
            Assert.AreEqual(fpgaHealth.FPGANetwork.TorMacReceiveCount, "21709436");
            Assert.AreEqual(fpgaHealth.FPGANetwork.TorMacTransferCount, "33743200");
            Assert.AreEqual(fpgaHealth.FPGANetwork.NicMacLanesDeskew, "1");
            Assert.AreEqual(fpgaHealth.FPGANetwork.NicMacLanesStable, "1");
            Assert.AreEqual(fpgaHealth.FPGANetwork.NicMacHardwareError, "0x0");
            Assert.AreEqual(fpgaHealth.FPGANetwork.NicMacPcsHardwareError, "0x0");
            Assert.AreEqual(fpgaHealth.FPGANetwork.NicMacLinkDrops, "0");
            Assert.AreEqual(fpgaHealth.FPGANetwork.NicMacReceiveFcsErrors, "0");
            Assert.AreEqual(fpgaHealth.FPGANetwork.NicMacReceiveCount, "33743200");
            Assert.AreEqual(fpgaHealth.FPGANetwork.NicMacTransferCount, "21709436");
        }

        [Test]
        public void FpgaReaderCanParseHealthStringForFPGAPcie()
        {
            this.SuccessCaseTestSetup();

            FpgaHealth fpgaHealth = this.fpgaReader.Read();

            Assert.AreEqual(fpgaHealth.FPGAPcie.IsStatusOK, true);
        }

        [Test]
        public void FpgaReaderCanParseHealthStringForFPGADram()
        {
            this.SuccessCaseTestSetup();

            FpgaHealth fpgaHealth = this.fpgaReader.Read();

            Assert.AreEqual(fpgaHealth.FPGADram.IsStatusOK, true);
        }

        [Test]
        public void FpgaReaderCanParseHealthStringForFPGACables()
        {
            this.SuccessCaseTestSetup();

            FpgaHealth fpgaHealth = this.fpgaReader.Read();

            Assert.AreEqual(fpgaHealth.FPGACables.IsStatusOK, true);
        }

        [Test]
        public void FpgaReaderCannotFindRequiredDependencies1()
        {
            this.mockFileSystem.Setup(f => f.Directory.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
                .Returns(Array.Empty<string>());

            Assert.Throws<FileNotFoundException>(() => { this.fpgaReader.Read(); });
        }

        private void SuccessCaseTestSetup()
        {
            var fpgaDiagnosticsOutput = @"
[FPGA-ID        ] OK [chipid:25299256306433798,endpoints:1,address:0x0,physical:25299256306433798]
[FPGA-CONFIG    ] OK [golden:1,role_id:0x601d,role_ver:0xca7b030c,shell_id:0xbed70c,shell_ver:0x3000c,sshell_id:0x9a55,sshell_ver:0x20000,crcerr:0,chngset:4014974304,verbmp:0,2020-3-1,clean:1,tfs:1]
[FPGA-CLOCKRESET] OK [pll_locked:1,force_app_reset:0,force_core_reset:0]
[FPGA-TEMP      ] OK [temp_warn:0,temp_shutdown:0,temp_c:68,min_temp_c:63,max_temp_c:72]
[FPGA-NETWORK   ] OK [Soft-Network-Status:0x0] [TOR-MAC,lanes_deskew:1,lanes_stable:1,mac_hw_err:0x0,pcs_hw_err:0x0,linkdrops:0,rx_fcs_errs:0,rx_count:21709436,tx_count:33743200] [NIC-MAC,lanes_deskew:1,lanes_stable:1,mac_hw_err:0x0,pcs_hw_err:0x0,linkdrops:0,rx_fcs_errs:0,rx_count:33743200,tx_count:21709436]
[FPGA-PCIE      ] OK [HIP-0,3x8,id:0x5e00,ver:0x10001,dma_status:0x0] [HIP-1,3x8,id:0x5e00,ver:0x10001,dma_status:0x0]
[FPGA-DRAM      ] OK [DRAM disabled]
[FPGA-DRAMEXT   ] OK [DRAM disabled]
[FPGA-CONFIG-EX ] OK [board:LongsPeak,0xB2,0x18] [golden:1] [asmi:0xb0003.0x3] [shell:0xbed70c,3.12.11-ef4fa560,2020-3-1,Clean] [role:0x601d,0xca7b030c-6fb64219] [sshell:0x9a55,0x20000]
[FPGA-CABLES    ] OK [cables_found:1] [QSFP-0,present:1,done:1,timeout:0,id:0x11,tech:0xa0,vendor_id:00-09-3a,pn:1002971151,rn:0x2020]
FPGA diagnostics total time: 0.02 seconds";
            this.mockFileSystem.Setup(f => f.Directory.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
                .Returns(new string[] { @"C:\App\PayloadFromSME.TipNode_Something\FPGADiagnostics\FPGADiagnostics.exe" });

            Mock<IDirectoryInfo> info = new Mock<IDirectoryInfo>();
            info.SetupGet(i => i.FullName).Returns(@"C:\App\PayloadFromSME.TipNode_Something\");
            this.mockFileSystem.Setup(f => f.Directory.GetParent(It.IsAny<string>()))
                .Returns(info.Object);
            this.processExecutionResult.ExitCode = 0;
            this.processExecutionResult.Output.Add(fpgaDiagnosticsOutput);
            this.mockProcessExecution.Setup(r => r.ExecuteProcessAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<string>(),
                It.IsAny<bool>()))
                .Returns(Task.FromResult<ProcessExecutionResult>(this.processExecutionResult));
        }
    }
}
