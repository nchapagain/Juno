namespace Juno.Execution.Providers.Workloads
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Abstractions;
    using System.Security;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Execution.AgentRuntime;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Repository.KeyVault;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class HostWorkloadProviderTests
    {
        private ProviderFixture mockFixture;
        private TestHostWorkloadProvider provider;
        private HostWorkloadProvider.State providerState;
        private Mock<IFileSystem> mockFileSystem;
        private Mock<IAzureKeyVault> mockKeyVault;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new ProviderFixture(typeof(HostWorkloadProvider));            
            this.providerState = new HostWorkloadProvider.State();
            this.providerState.StepTimeout = DateTime.UtcNow.Add(TimeSpan.FromMinutes(20));

            this.mockKeyVault = new Mock<IAzureKeyVault>();
            string expectedConnectionString = "Endpoint=sb://something.servicebus.windows.net/;SharedAccessKeyName=keyname;SharedAccessKey=verysecurekey";
            SecureString secretString = expectedConnectionString.ToSecureString();
            this.mockKeyVault.Setup(kv => kv.GetSecretAsync("VirtualClientEventHubConnectionString", It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(secretString));

            this.mockFileSystem = new Mock<IFileSystem>();
            this.mockFileSystem.Setup(f => f.Path.GetPathRoot(It.IsAny<string>()))
                .Returns(@"C:\");
            this.mockFileSystem.Setup(f => f.Directory.GetDirectories(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new string[] { @"C:\App\HostWorkloads.TipNode_something" });
            this.mockFileSystem.Setup(f => f.Directory.GetFiles(It.IsAny<string>(), It.IsAny<string>(), SearchOption.AllDirectories))
                .Returns(new string[] { @"C:\App\HostWorkloads.TipNode_something\VirtualClient\VirtualClient.exe" });

            this.mockFixture.Component.Parameters.Clear();
            this.mockFixture.Component.Parameters.AddRange(new Dictionary<string, IConvertible>
            {
                { "commandArguments", "--profile=RealtimeDataMonitor.json --platform=Juno" },
                { "duration", "5:00:00" },
                { "eventHubConnectionString", "[secret:keyvault]=VirtualClientEventHubConnectionString" }
            });

            this.mockFixture.Services.AddSingleton<IFileSystem>(this.mockFileSystem.Object);
            this.mockFixture.Services.AddSingleton<IAzureKeyVault>(this.mockKeyVault.Object);
            this.provider = new TestHostWorkloadProvider(this.mockFixture.Services);
            this.provider.ResetVirtualClientProcess();
            this.provider.ConfigureServicesAsync(this.mockFixture.Context, this.mockFixture.Component)
                .GetAwaiter()
                .GetResult();
        }

        [Test]
        public async Task HostWorkloadProviderSetsDependenciesInstalledStateToTrueAndReturnsResultInProgressContinueIfRequiredDependenciesAreFound()
        {
            this.mockFixture.DataClient.OnSaveState<HostWorkloadProvider.State>()
                .Callback((string x, string y, HostWorkloadProvider.State state, CancellationToken c, string s) =>
                {
                    this.providerState = state;
                })
                .Returns(Task.CompletedTask);

            ExecutionResult result = await this.provider.ExecuteAsync(
                this.mockFixture.Context,
                this.mockFixture.Component,
                CancellationToken.None);

            Assert.AreEqual(true, this.providerState.DependenciesInstalled);
            Assert.AreEqual(ExecutionStatus.InProgressContinue, result.Status);
        }

        [Test]
        public async Task HostWorkloadProviderReturnsFailedIfRequiredDependenciesAreNotFoundWithinTheStepTimeout()
        {
            this.providerState.StepTimeout = DateTime.UtcNow;
            this.mockFileSystem.Setup(f => f.Directory.GetDirectories(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Array.Empty<string>());
            this.mockFixture.DataClient.OnGetState<HostWorkloadProvider.State>()
                .Returns(Task.FromResult(this.providerState));
            this.mockFixture.DataClient.OnSaveState<HostWorkloadProvider.State>()
                .Callback((string x, string y, HostWorkloadProvider.State state, CancellationToken c, string s) =>
                {
                    this.providerState = state;
                })
                .Returns(Task.CompletedTask);

            ExecutionResult result = await this.provider.ExecuteAsync(
                this.mockFixture.Context,
                this.mockFixture.Component,
                CancellationToken.None);

            Assert.AreEqual(ExecutionStatus.Failed, result.Status);
            Assert.AreEqual("Timeout expired. The VirtualClient process could not be started within the time range.", result.Error.Message);
        }

        [Test]
        public async Task HostWorkloadProviderSetsDependenciesInstalledStateToFalseAndReturnsResultInProgressContinueIfRequiredDependenciesAreNotFound()
        {
            this.mockFileSystem.Setup(f => f.Directory.GetDirectories(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Array.Empty<string>());
            this.mockFixture.DataClient.OnSaveState<HostWorkloadProvider.State>()
                .Callback((string x, string y, HostWorkloadProvider.State state, CancellationToken c, string s) =>
                {
                    this.providerState = state;
                })
                .Returns(Task.CompletedTask);

            ExecutionResult result = await this.provider.ExecuteAsync(
                this.mockFixture.Context,
                this.mockFixture.Component,
                CancellationToken.None);

            Assert.AreEqual(false, this.providerState.DependenciesInstalled);
            Assert.AreEqual(ExecutionStatus.InProgressContinue, result.Status);
        }

        [Test]
        public async Task HostWorkloadProviderSetsProcessRunningStateToTrueAndReturnsResultInProgressContinueIfProcessIsAbleToStartCorrectly()
        {
            this.providerState.DependenciesInstalled = true;
            this.mockFixture.DataClient.OnGetState<HostWorkloadProvider.State>()
                .Returns(Task.FromResult(this.providerState));
            this.mockFixture.DataClient.OnSaveState<HostWorkloadProvider.State>()
                .Callback((string x, string y, HostWorkloadProvider.State state, CancellationToken c, string s) =>
                {
                    this.providerState = state;
                })
                .Returns(Task.CompletedTask);

            ExecutionResult result = await this.provider.ExecuteAsync(
                this.mockFixture.Context,
                this.mockFixture.Component,
                CancellationToken.None);

            Assert.AreEqual(true, this.providerState.ProcessRunning);
            Assert.AreEqual(ExecutionStatus.InProgressContinue, result.Status);
        }

        [Test]
        public async Task HostWorkloadProviderDoesNothingIfProcessIsRunning()
        {
            this.providerState.DependenciesInstalled = true;
            this.providerState.ProcessRunning = true;
            this.providerState.ProcessEndTime = DateTime.UtcNow.AddHours(1);
            this.provider.SetVirtualClientProcess();
            this.mockFixture.DataClient.OnGetState<HostWorkloadProvider.State>()
                .Returns(Task.FromResult(this.providerState));
            this.mockFixture.DataClient.OnSaveState<HostWorkloadProvider.State>()
                .Callback((string x, string y, HostWorkloadProvider.State state, CancellationToken c, string s) =>
                {
                    this.providerState = state;
                })
                .Returns(Task.CompletedTask);

            ExecutionResult result = await this.provider.ExecuteAsync(
                this.mockFixture.Context,
                this.mockFixture.Component,
                CancellationToken.None);

            Assert.AreEqual(ExecutionStatus.InProgressContinue, result.Status);
        }

        [Test]
        public async Task HostWorkloadProviderTriesToRestartTheProcessIfItHasCrashed()
        {
            this.providerState.DependenciesInstalled = true;
            this.providerState.ProcessRunning = true;
            this.providerState.RestartCount = 0;
            this.provider.IsVirtualClientProcessRunning = false;
            this.mockFixture.DataClient.OnGetState<HostWorkloadProvider.State>()
                .Returns(Task.FromResult(this.providerState));
            this.mockFixture.DataClient.OnSaveState<HostWorkloadProvider.State>()
                .Callback((string x, string y, HostWorkloadProvider.State state, CancellationToken c, string s) =>
                {
                    this.providerState = state;
                })
                .Returns(Task.CompletedTask);

            ExecutionResult result = await this.provider.ExecuteAsync(
                this.mockFixture.Context,
                this.mockFixture.Component,
                CancellationToken.None);

            Assert.AreEqual(ExecutionStatus.InProgressContinue, result.Status);
            Assert.AreEqual(1, this.providerState.RestartCount);
        }

        [Test]
        public async Task HostWorkloadProviderDoesNothingIfItJustLostTheProcessManager()
        {
            this.providerState.DependenciesInstalled = true;
            this.providerState.ProcessRunning = true;
            this.providerState.RestartCount = 0;
            this.provider.IsVirtualClientProcessRunning = true;
            this.mockFixture.DataClient.OnGetState<HostWorkloadProvider.State>()
                .Returns(Task.FromResult(this.providerState));
            this.mockFixture.DataClient.OnSaveState<HostWorkloadProvider.State>()
                .Callback((string x, string y, HostWorkloadProvider.State state, CancellationToken c, string s) =>
                {
                    this.providerState = state;
                })
                .Returns(Task.CompletedTask);

            ExecutionResult result = await this.provider.ExecuteAsync(
                this.mockFixture.Context,
                this.mockFixture.Component,
                CancellationToken.None);

            Assert.AreEqual(ExecutionStatus.InProgressContinue, result.Status);
            Assert.AreEqual(0, this.providerState.RestartCount);
        }

        [Test]
        public async Task HostWorkloadProviderFailsIfProcessHasRestartedTooManyTimes()
        {
            this.providerState.DependenciesInstalled = true;
            this.providerState.ProcessRunning = true;
            this.providerState.RestartCount = 51;
            this.mockFixture.DataClient.OnGetState<HostWorkloadProvider.State>()
                .Returns(Task.FromResult(this.providerState));
            this.mockFixture.DataClient.OnSaveState<HostWorkloadProvider.State>()
                .Callback((string x, string y, HostWorkloadProvider.State state, CancellationToken c, string s) =>
                {
                    this.providerState = state;
                })
                .Returns(Task.CompletedTask);

            ExecutionResult result = await this.provider.ExecuteAsync(
                this.mockFixture.Context,
                this.mockFixture.Component,
                CancellationToken.None);
     
            Assert.AreEqual(ExecutionStatus.Failed, result.Status);
            Assert.AreEqual("Process restarted too many times.", result.Error.Message);
        }

        [Test]
        public async Task HostWorkloadProviderReturnsSucceededIfTheProcessDurationHasExpired()
        {
            this.providerState.DependenciesInstalled = true;
            this.providerState.ProcessRunning = true;
            this.providerState.ProcessEndTime = DateTime.UtcNow;
            this.provider.SetVirtualClientProcess();

            this.mockFixture.DataClient.OnGetState<HostWorkloadProvider.State>()
                .Returns(Task.FromResult(this.providerState));
            this.mockFixture.DataClient.OnSaveState<HostWorkloadProvider.State>()
                .Callback((string x, string y, HostWorkloadProvider.State state, CancellationToken c, string s) =>
                {
                    this.providerState = state;
                })
                .Returns(Task.CompletedTask);

            ExecutionResult result = await this.provider.ExecuteAsync(
                this.mockFixture.Context,
                this.mockFixture.Component,
                CancellationToken.None);

            Assert.AreEqual(ExecutionStatus.Succeeded, result.Status);
        }

        private class TestHostWorkloadProvider : HostWorkloadProvider
        {
            public TestHostWorkloadProvider(IServiceCollection services)
                : base(services)
            {
            }

            public bool IsVirtualClientProcessRunning { get; set; }

            public void ResetVirtualClientProcess()
            {
                HostWorkloadProvider.VirtualClientProcess = null;
            }

            public void SetVirtualClientProcess()
            {
                HostWorkloadProvider.VirtualClientProcess = TestHostWorkloadProvider.SetupMockProcessManager();
            }

            protected override IProcessManager CreateBreakoutProcessManager(string commandFullPath, string commandArguments)
            {
                return TestHostWorkloadProvider.SetupMockProcessManager();
            }

            protected override IProcessManager CreateBreakoutProcessManager(IProcessProxy process)
            {
                return TestHostWorkloadProvider.SetupMockProcessManager();
            }

            protected override bool TryGetVirtualClientProcess(out IProcessManager processManager)
            {
                processManager = new Mock<IProcessManager>().Object;
                return this.IsVirtualClientProcessRunning;
            }

            private static IProcessManager SetupMockProcessManager()
            {
                var processManager = new Mock<IProcessManager>();
                Mock<IProcessProxy> mockProcess = new Mock<IProcessProxy>();
                IProcessProxy process = mockProcess.Object;
                int? exitCode = 0;

                processManager.Setup(pm => pm.StartProcessAsync(It.IsAny<EventContext>(), It.IsAny<CancellationToken>()))
                        .Returns(Task.CompletedTask);
                processManager.Setup(pm => pm.StopProcessAsync(It.IsAny<EventContext>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);
                processManager.Setup(pm => pm.IsProcessRunning())
                    .Returns(true);
                processManager.Setup(pm => pm.TryGetProcess(out process))
                    .Returns(true);
                processManager.Setup(pm => pm.TryGetProcessExitCode(out exitCode))
                    .Returns(true);

                return processManager.Object;
            }
        }
    }
}
