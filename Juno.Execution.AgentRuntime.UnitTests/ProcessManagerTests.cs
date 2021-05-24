namespace Juno.Execution.AgentRuntime
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;
    using Polly;

    [TestFixture]
    [Category("Unit")]
    public class ProcessManagerTests
    {
        private ProcessManager processManager;
        private Mock<IProcessProxy> mockProcessProxy;
        private ILogger logger;
        private string expectedFileName;
        private string expectedWorkingDirectory;
        private string expectedFullPath;
        private string expectedArguments;

        [SetUp]
        public void SetUp()
        {
            this.expectedFileName = "expectedFileName.exe";
            this.expectedWorkingDirectory = "expectedPath";
            this.expectedFullPath = $"{this.expectedWorkingDirectory}\\{this.expectedFileName}";
            this.expectedArguments = "expectedArguments";
            this.logger = NullLogger.Instance;
            this.processManager = new ProcessManager(this.expectedFullPath, this.expectedArguments, logger: this.logger);
            this.mockProcessProxy = new Mock<IProcessProxy>();
        }

        [TearDown]
        public void TearDown()
        {
            this.processManager.CurrentProcess.Dispose();
            this.processManager.CurrentProcess = null;
            this.processManager = null;
        }

        [Test]
        public void ProcessManagerCreatesTheExpectedProcess()
        {
            Assert.IsNotNull(this.processManager);
            Assert.IsNotNull(this.processManager.CurrentProcess);
            Assert.IsNotNull(this.processManager.CurrentProcess.StartInfo);
            Assert.AreEqual(this.expectedFullPath, this.processManager.CurrentProcess.StartInfo.FileName);
            Assert.AreEqual(this.expectedWorkingDirectory, this.processManager.CurrentProcess.StartInfo.WorkingDirectory);
            Assert.IsTrue(this.processManager.CurrentProcess.StartInfo.CreateNoWindow);
            Assert.IsFalse(this.processManager.CurrentProcess.StartInfo.UseShellExecute);
            Assert.IsTrue(this.processManager.CurrentProcess.StartInfo.RedirectStandardError);
            Assert.IsFalse(this.processManager.CurrentProcess.StartInfo.RedirectStandardOutput);
        }

        [Test]
        public void ProcessManagerCorrectlyDetectsWhenAProcessIsRunning()
        {
            bool hasExitedReferenced = false;
            this.mockProcessProxy.Setup(p => p.HasExited)
                .Callback(() =>
                {
                    hasExitedReferenced = true;
                })
                .Returns(true);

            this.processManager.CurrentProcess = this.mockProcessProxy.Object;

            Assert.IsFalse(hasExitedReferenced);
            Assert.False(this.processManager.IsProcessRunning());
            Assert.IsTrue(hasExitedReferenced);

            hasExitedReferenced = false;
            this.mockProcessProxy.Setup(p => p.HasExited)
                .Callback(() =>
                {
                    hasExitedReferenced = true;
                })
                .Returns(false);

            Assert.IsTrue(this.processManager.IsProcessRunning());
            Assert.IsTrue(hasExitedReferenced);
        }

        [Test]
        public async Task ProcessManagerStartsAGivenProcessAsExpected()
        {
            bool hasStarted = false;
            bool startInfoReferenced = false;
            bool hasBegunReadingOutput = false;

            this.mockProcessProxy.Setup(p => p.StartInfo)
                .Callback(() =>
                {
                    startInfoReferenced = true;
                })
                .Returns(new System.Diagnostics.ProcessStartInfo());

            this.mockProcessProxy.Setup(p => p.Start())
                .Callback(() =>
                {
                    hasStarted = true;
                })
                .Returns(true);

            this.mockProcessProxy.Setup(p => p.BeginReadingOutput(It.IsAny<bool>(), It.IsAny<bool>()))
                .Callback(() =>
                {
                    hasBegunReadingOutput = true;
                });

            this.processManager.CurrentProcess = this.mockProcessProxy.Object;
            await this.processManager.StartProcessAsync(new EventContext(Guid.NewGuid()), CancellationToken.None);

            Assert.IsTrue(startInfoReferenced);
            Assert.IsTrue(hasStarted);
            Assert.IsTrue(hasBegunReadingOutput);
        }

        [Test]
        public async Task ProcessManagerStopsAGivenProcessAsExpected()
        {
            bool hasBeenDisposed = false;
            bool startInfoReferenced = false;
            bool hasExitedReferenced = false;
            bool hasBeenKilled = false;

            this.mockProcessProxy.Setup(p => p.StartInfo)
                .Callback(() =>
                {
                    startInfoReferenced = true;
                })
                .Returns(new System.Diagnostics.ProcessStartInfo());

            this.mockProcessProxy.Setup(p => p.Dispose())
                .Callback(() =>
                {
                    hasBeenDisposed = true;
                });

            this.mockProcessProxy.Setup(p => p.Kill())
                .Callback(() =>
                {
                    hasBeenKilled = true;
                });

            this.mockProcessProxy.Setup(p => p.HasExited)
                .Callback(() =>
                {
                    hasExitedReferenced = true;
                })
                .Returns(false);

            this.processManager.CurrentProcess = this.mockProcessProxy.Object;
            await this.processManager.StopProcessAsync(new EventContext(Guid.NewGuid()), CancellationToken.None);

            Assert.IsTrue(startInfoReferenced);
            Assert.IsTrue(hasExitedReferenced);
            Assert.IsTrue(hasBeenDisposed);
            Assert.IsTrue(hasBeenKilled);
        }
    }
}