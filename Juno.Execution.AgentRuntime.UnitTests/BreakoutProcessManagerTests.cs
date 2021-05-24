namespace Juno.Execution.AgentRuntime
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Execution.Providers;
    using Juno.Execution.Providers.Payloads;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;
    using Polly;

    [TestFixture]
    [Category("Unit")]
    public class BreakoutProcessManagerTests
    {
        private TestBreakoutProcessManager processManager;
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
            this.processManager = new TestBreakoutProcessManager(this.expectedFullPath, this.expectedArguments, logger: this.logger);
            this.mockProcessProxy = new Mock<IProcessProxy>();
        }

        [TearDown]
        public void TearDown()
        {
            if (this.processManager.CurrentProcess != null)
            {
                this.processManager.CurrentProcess.Dispose();
                this.processManager.CurrentProcess = null;
            }

            this.processManager = null;
        }

        [Test]
        [TestCase("/folder/anotherfolder/folder with space/virtualclient.exe", null, false)]
        [TestCase(null, "--option=blue --option2=cat,bird", false)]
        [TestCase("/folder/anotherfolder/folder with space/virtualclient.exe", "", false)]
        [TestCase("", "--option=blue --option2=cat,bird", false)]
        [TestCase("/folder/anotherfolder/folder with space/virtualclient.exe", "--option=blue --option2=cat,bird", false)]
        [TestCase("/folder/anotherfolder/folder with space/virtualclient.exe", "--option=\"blue, green\" --option2=cat,bird", false)]
        [TestCase("/folder/anotherfolder/folderwithnospace/virtualclient.exe", "--option=\"blue, green\" --option2=\"cat\"", false)]
        [TestCase("\"/folder/anotherfolder/folderwithnospace/virtualclient.exe\"", "--option=\"blue, green\" --option2=\"cat\"", false)]
        [TestCase("\"/folder/anotherfolder/folderwithnospace/virtualclient.exe\"", "--option=\"blue, green\" --option2=cat", false)]
        [TestCase("\"/folder/anotherfolder/folderwithnospace/virtualclient.exe\"", "--option=blue --option2=cat", false)]
        [TestCase("/folder/anotherfolder/folderwithnospace/virtualclient.exe", "--option=blue,green --option2=\"cat, bird\"", true)]
        [TestCase("/folder/anotherfolder/folderwithnospace/virtualclient.exe", "--option=blue,green --option2=\"cat,bird\"", true)]
        [TestCase("/folder/anotherfolder/folderwithnospace/virtualclient.exe", "--option=\"blue, green\" --option2=cat,bird", true)]
        [TestCase("/folder/anotherfolder/folderwithnospace/virtualclient.exe", "--option=\"blue,green\" --option2=cat,bird", true)]
        public void ProcessManagerEscapesAndValidatesCommandArgumentsFullPath(string path, string arguments, bool shouldValidate)
        {
            bool threwError = false;

            try
            {
                TestBreakoutProcessManager processManager = new TestBreakoutProcessManager(path, arguments);
            }
            catch (ArgumentException)
            {
                threwError = true;
            }

            if (threwError)
            {
                Assert.IsFalse(shouldValidate);
            }
            else
            {
                Assert.IsTrue(shouldValidate);
            }
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
        public async Task ProcessManagerTriesToStartExpectedProcesses()
        {
            string expectedCreateCommandStart = $@"create ";
            string expectedCreateCommandEnd = $@" binpath= ""cmd /c start {this.expectedFullPath} {this.expectedArguments}""";
            string expectedStartCommand = $@"start ";
            string expectedDeleteCommand = $@"delete ";

            int processesStarted = 0;
            bool receivedCreateCommand = false;
            bool receivedStartCommand = false;
            bool receivedDeleteCommand = false;

            this.mockProcessProxy.Setup(p => p.Start())
                .Callback(() =>
                {
                    processesStarted++;
                    if (this.processManager.CreatedCommandFullPath == "sc")
                    {
                        if (this.processManager.CreatedCommandArguments.StartsWith(expectedCreateCommandStart) 
                            && this.processManager.CreatedCommandArguments.EndsWith(expectedCreateCommandEnd))
                        {
                            receivedCreateCommand = true;
                        }
                        else if (this.processManager.CreatedCommandArguments.StartsWith(expectedStartCommand))
                        {
                            receivedStartCommand = true;
                        }
                        else if (this.processManager.CreatedCommandArguments.StartsWith(expectedDeleteCommand))
                        {
                            receivedDeleteCommand = true;
                        }
                    }
                })
                .Returns(false);

            this.processManager.CreatedProcess = this.mockProcessProxy.Object;
            await this.processManager.StartProcessAsync(new EventContext(Guid.NewGuid()), CancellationToken.None);

            Assert.AreEqual(3, processesStarted);
            Assert.IsTrue(receivedCreateCommand);
            Assert.IsTrue(receivedStartCommand);
            Assert.IsTrue(receivedDeleteCommand);
        }

        [Test]
        public async Task ProcessManagerStopsAGivenProcessAsExpected()
        {
            bool hasBeenDisposed = false;
            bool hasExitedReferenced = false;
            bool hasBeenKilled = false;

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

            Assert.IsTrue(hasExitedReferenced);
            Assert.IsTrue(hasBeenDisposed);
            Assert.IsTrue(hasBeenKilled);
        }

        private class TestBreakoutProcessManager : BreakoutProcessManager
        {
            public TestBreakoutProcessManager(string commandFullPath, string commandArguments, IAsyncPolicy retryPolicy = null, ILogger logger = null)
                : base(commandFullPath, commandArguments, retryPolicy, logger)
            {
            }

            public TestBreakoutProcessManager(IProcessProxy processProxy, IAsyncPolicy retryPolicy = null, ILogger logger = null)
                : base(processProxy, retryPolicy, logger)
            {
            }

            public IProcessProxy CreatedProcess { get; set; }

            public string CreatedCommandFullPath { get; set; }

            public string CreatedCommandArguments { get; set; }

            public override IProcessProxy CreateProcess(string commandFullPath, string commandArguments)
            {
                this.CreatedCommandFullPath = commandFullPath;
                this.CreatedCommandArguments = commandArguments;
                return this.CreatedProcess;
            }
        }
    }
}
