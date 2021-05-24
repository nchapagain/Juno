namespace Juno.Execution.AgentRuntime.Windows
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Abstractions;
    using System.Linq;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Execution.AgentRuntime.Contract;
    using Microsoft.Azure.CRC;
    using Moq;
    using Newtonsoft.Json;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class SsdReaderTests
    {
        // These string literals must be the exact same as in SsdreaderSmartCtl
        private readonly string appPath = @"D:\App\";
        private readonly string smartctlBinary = "smartctl.exe";
        private readonly string mockHwMonPath = @"JunoSsdPayload.TipNode_2020_11_10_10001\";
        private readonly string listFlag = "--scan";
        private readonly string getInfoFlag = "-a";
        private readonly string jsonFlag = "-j";
        private Mock<IPath> mockPath;
        private Mock<IDirectory> mockDir;
        private Mock<IProcessExecution> mockExecuter;
        private SsdReader reader;
        private RunTimeContractFixture fixture;

        [OneTimeSetUp]
        public void SetupTests()
        {
            Mock<IFileSystem> fileSystem = new Mock<IFileSystem>();
            this.mockPath = new Mock<IPath>();
            this.mockDir = new Mock<IDirectory>();
            fileSystem.SetupGet(fs => fs.Path).Returns(this.mockPath.Object);
            fileSystem.SetupGet(fs => fs.Directory).Returns(this.mockDir.Object);
            this.mockExecuter = new Mock<IProcessExecution>();
            this.fixture = new RunTimeContractFixture();
            this.fixture.Register<SsdDrives>(() => this.CreateSsdDrives());
            this.fixture.Register<SsdDrive>(() => SsdReaderTests.CreateSsdDrive());
            this.fixture.Register<SsdInfo>(() => this.fixture.Create<SataInfo>());

            this.reader = new SsdReader(fileSystem.Object, this.mockExecuter.Object);
        }

        [SetUp]
        public void SetupDefaultMocks()
        {
            this.mockDir.Setup(dr => dr.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>())).Returns(new string[] { string.Concat(this.appPath, this.mockHwMonPath, this.smartctlBinary) });

            ProcessExecutionResult listResult = new ProcessExecutionResult() { ExitCode = 0 };
            listResult.Output.Add(JsonConvert.SerializeObject(this.fixture.Create<SsdDrives>()));
            ProcessExecutionResult getResult = new ProcessExecutionResult() { ExitCode = 0 };
            getResult.Output.Add(JsonConvert.SerializeObject(this.fixture.Create<SsdInfo>()));
            this.mockExecuter.SetupSequence(e => e.ExecuteProcessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(Task.FromResult(listResult))
                .Returns(Task.FromResult(getResult));
        }

        [Test]
        public void ReadThrowsErrorWhenPathToBinaryIsNotFound()
        {
            this.mockDir.Setup(dr => dr.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
                .Returns(Array.Empty<string>());
            Assert.Throws<FileNotFoundException>(() => this.reader.Read());
        }

        [Test]
        public void ReadThrowsErrorWhenPathToBinaryDoesNotMatchHwMonExpression()
        {
            this.mockDir.Setup(dr => dr.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
                .Returns(new string[] { string.Concat(this.appPath, this.smartctlBinary) });
            Assert.Throws<FileNotFoundException>(() => this.reader.Read());
        }

        [Test]
        public void ReadPassesCorrectParametersToExecuteProcessList()
        {
            ProcessExecutionResult listResult = new ProcessExecutionResult() { ExitCode = 0 };
            listResult.Output.Add(JsonConvert.SerializeObject(this.fixture.Create<SsdDrives>()));
            ProcessExecutionResult getResult = new ProcessExecutionResult() { ExitCode = 0 };
            getResult.Output.Add(JsonConvert.SerializeObject(this.fixture.Create<SsdInfo>()));
            this.mockExecuter.Setup(e => e.ExecuteProcessAsync(It.IsAny<string>(), It.Is<string>(s => s.Contains(this.listFlag)), It.IsAny<TimeSpan?>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Callback<string, string, TimeSpan?, string, bool>((file, args, ttl, cwd, redirect) =>
                {
                    Assert.AreEqual(string.Concat(this.appPath, this.mockHwMonPath, this.smartctlBinary), file);
                    Assert.AreEqual(string.Join(" ", this.listFlag, this.jsonFlag), args);
                }).Returns(Task.FromResult(listResult));
            this.mockExecuter.Setup(e => e.ExecuteProcessAsync(It.IsAny<string>(), It.Is<string>(s => s.Contains(this.getInfoFlag)), It.IsAny<TimeSpan?>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(Task.FromResult(getResult));

            _ = this.reader.Read();

            this.mockExecuter.Verify(e => e.ExecuteProcessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<string>(), It.IsAny<bool>()), Times.AtLeastOnce());
        }

        [Test]
        public void ReadPassedCorrectParametersToExecuteProcessGetInfo()
        {
            SsdDrives drives = this.fixture.Create<SsdDrives>();
            string drive = drives.First().Name;
            ProcessExecutionResult listResult = new ProcessExecutionResult() { ExitCode = 0 };
            listResult.Output.Add(JsonConvert.SerializeObject(drives));
            ProcessExecutionResult getResult = new ProcessExecutionResult() { ExitCode = 0 };
            getResult.Output.Add(JsonConvert.SerializeObject(this.fixture.Create<SsdInfo>()));

            this.mockExecuter.Setup(e => e.ExecuteProcessAsync(It.IsAny<string>(), It.Is<string>(s => s.Contains(this.listFlag)), It.IsAny<TimeSpan?>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(Task.FromResult(listResult));

            this.mockExecuter.Setup(e => e.ExecuteProcessAsync(It.IsAny<string>(), It.Is<string>(s => s.Contains(this.getInfoFlag)), It.IsAny<TimeSpan?>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Callback<string, string, TimeSpan?, string, bool>((file, args, ttl, cwd, redirect) =>
                {
                    Assert.AreEqual(string.Concat(this.appPath, this.mockHwMonPath, this.smartctlBinary), file);
                    Assert.AreEqual(string.Join(" ", this.getInfoFlag, this.jsonFlag, drive), args);
                }).Returns(Task.FromResult(getResult));

            _ = this.reader.Read();

            this.mockExecuter.Verify(e => e.ExecuteProcessAsync(It.IsAny<string>(), It.Is<string>(s => s.Contains(this.listFlag)), It.IsAny<TimeSpan?>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Once());
            this.mockExecuter.Verify(e => e.ExecuteProcessAsync(It.IsAny<string>(), It.Is<string>(s => s.Contains(this.getInfoFlag)), It.IsAny<TimeSpan?>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Once());
        }

        [Test]
        public void ReadThrowsErrorWhenListDrivesReturnsNonZeroExitCode()
        {
            this.mockExecuter.Setup(e => e.ExecuteProcessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(Task.FromResult(new ProcessExecutionResult() { ExitCode = 1 }));

            Assert.Throws<ProcessExecutionException>(() => this.reader.Read());
        }

        [Test]
        public void ReadThrowsErrorWhenGetInfoDrivesReturnsNonZeroExitCode()
        {
            this.mockExecuter.Setup(e => e.ExecuteProcessAsync(It.IsAny<string>(), It.Is<string>(s => s.Contains(this.getInfoFlag)), It.IsAny<TimeSpan?>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(Task.FromResult(new ProcessExecutionResult() { ExitCode = 1 }));

            Assert.Throws<AggregateException>(() => this.reader.Read());
        }

        [Test]
        public void ReadThrowsErrorWhenFailedToDeserializeSsdInfo()
        {
            ProcessExecutionResult result = new ProcessExecutionResult() { ExitCode = 0 };
            result.Output.Add("Not an ssd info object");
            this.mockExecuter.Setup(e => e.ExecuteProcessAsync(It.IsAny<string>(), It.Is<string>(s => s.Contains(this.getInfoFlag)), It.IsAny<TimeSpan?>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(Task.FromResult(result));

            Assert.Throws<AggregateException>(() => this.reader.Read());
        }

        [Test]
        public void ReadReturnsExpectedResultWhenDeviceTypeIsSata()
        {
            ProcessExecutionResult getResult = new ProcessExecutionResult() { ExitCode = 0 };
            SataInfo expectedInfo = this.fixture.Create<SataInfo>();
            getResult.Output.Add(JsonConvert.SerializeObject(expectedInfo));
            this.mockExecuter.Setup(e => e.ExecuteProcessAsync(It.IsAny<string>(), It.Is<string>(s => s.Contains(this.getInfoFlag)), It.IsAny<TimeSpan?>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(Task.FromResult(getResult));

            IEnumerable<SsdInfo> actualResult = this.reader.Read();
            Assert.IsNotNull(actualResult);
            Assert.IsNotEmpty(actualResult);
            Assert.AreEqual(new SsdInfo[] { expectedInfo }, actualResult);
        }

        [Test]
        public void ReadReturnsExpectedResultWhenDeviceTypeIsNvme()
        {
            ProcessExecutionResult listResult = new ProcessExecutionResult() { ExitCode = 0 };
            listResult.Output.Add(JsonConvert.SerializeObject(this.CreateSsdDrives("nvme")));
            this.mockExecuter.Setup(e => e.ExecuteProcessAsync(It.IsAny<string>(), It.Is<string>(s => s.Contains(this.listFlag)), It.IsAny<TimeSpan?>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(Task.FromResult(listResult));

            ProcessExecutionResult getResult = new ProcessExecutionResult() { ExitCode = 0 };
            NvmeInfo expectedInfo = this.fixture.Create<NvmeInfo>();
            getResult.Output.Add(JsonConvert.SerializeObject(expectedInfo));
            this.mockExecuter.Setup(e => e.ExecuteProcessAsync(It.IsAny<string>(), It.Is<string>(s => s.Contains(this.getInfoFlag)), It.IsAny<TimeSpan?>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(Task.FromResult(getResult));

            IEnumerable<SsdInfo> actualResult = this.reader.Read();
            Assert.IsNotNull(actualResult);
            Assert.IsNotEmpty(actualResult);
        }

        private static SsdDrive CreateSsdDrive(string type = null)
        {
            return new SsdDrive(
                Guid.NewGuid().ToString(),
                type ?? "ATA",
                Guid.NewGuid().ToString());
        }

        private SsdDrives CreateSsdDrives(string type = null)
        {
            return new SsdDrives(new List<SsdDrive>()
            {
               SsdReaderTests.CreateSsdDrive(type)
            });
        }
    }
}
