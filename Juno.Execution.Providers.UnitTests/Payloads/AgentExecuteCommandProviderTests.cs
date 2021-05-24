namespace Juno.Execution.Providers.Payloads
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Abstractions;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.Execution.AgentRuntime.Contract;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using NUnit.Framework;
    using State = AgentExecuteCommandProvider.State;

    [TestFixture]
    [Category("Unit")]
    public class AgentExecuteCommandProviderTests
    {
        private ProviderFixture fixture;
        private Mock<IDirectory> mockDirectory;
        private Mock<IFile> mockFile;
        private Mock<IProcessExecution> mockExecution;
        private TestAgentExecuteCommandProvider provider;
        private NodeExecutableSettings mockSettings;

        [OneTimeSetUp]
        public void SetupTests()
        {
            this.fixture = new ProviderFixture(typeof(TestAgentExecuteCommandProvider));
            this.mockDirectory = new Mock<IDirectory>();
            this.mockFile = new Mock<IFile>();
            Mock<IFileSystem> mockSystem = new Mock<IFileSystem>();
            this.mockExecution = new Mock<IProcessExecution>();
            this.mockSettings = new NodeExecutableSettings()
            { 
                Id = "id",
                Payload = "payload",
                ExecutableName = "executableName",
                LogFileName = "logfilename",
                SuccessStrings = new List<string>() { "string1", "string2" },
                RetryableString = new List<string>() { "string3", "string4" },
                Retries = 0,
                MaxExecutionTime = TimeSpan.FromMinutes(10),
                MinExecutionTime = TimeSpan.Zero
            };

            mockSystem.SetupGet(s => s.Directory).Returns(this.mockDirectory.Object);
            mockSystem.SetupGet(s => s.File).Returns(this.mockFile.Object);
            this.fixture.Services.AddSingleton<IFileSystem>(mockSystem.Object);
            this.fixture.Services.AddSingleton<IProcessExecution>(this.mockExecution.Object);
            this.fixture.Services.AddSingleton<NodeExecutableSettings>(this.mockSettings);

            this.provider = new TestAgentExecuteCommandProvider(this.fixture.Services);
        }

        [SetUp]
        public void SetupDefaultBehaviorAsync()
        {
            // Setup data client.
            this.fixture.DataClient.Reset();
            this.fixture.DataClient.Setup(dc => dc.GetOrCreateStateAsync<State>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .ReturnsAsync(new State(DateTime.UtcNow.Add(TimeSpan.FromSeconds(120)), DateTime.UtcNow, 0));
            this.fixture.DataClient.Setup(dc => dc.SaveStateAsync<State>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<State>(), It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Setup process execution.
            this.mockExecution.Reset();
            this.mockExecution.Setup(e => e.CreateProcess(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EventHandler>(), It.IsAny<IList<string>>()))
                .Returns(1234);

            // Setup Directory system
            this.mockDirectory.Reset();
            string directory = $@"C:\App\{this.mockSettings.Payload}\{this.mockSettings.Scenario}";
            this.mockDirectory.Setup(d => d.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
                .Returns(new string[] { $@"{directory}\{this.mockSettings.ExecutableName}" });
            Mock<IDirectoryInfo> directoryInfo = new Mock<IDirectoryInfo>();
            directoryInfo.SetupGet(md => md.FullName).Returns(directory);
            this.mockDirectory.Setup(d => d.GetParent(It.IsAny<string>()))
                .Returns(directoryInfo.Object);

            this.mockFile.Reset();
            this.mockFile.Setup(f => f.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("Some file contents");
        }

        [Test]
        public void StateIsSerializable()
        {
            State state = new State(DateTime.UtcNow, DateTime.UtcNow, 0);
            SerializationAssert.IsJsonSerializable<State>(state);
        }

        [Test]
        public void StateIsSerializableWithOptionalParameters()
        {
            State state = new State(DateTime.UtcNow, DateTime.UtcNow, 0, 1);
            SerializationAssert.IsJsonSerializable<State>(state);
        }

        [Test]
        public void StateIsSerializableWithDefaultSettings()
        {
            State state = new State(DateTime.UtcNow, DateTime.UtcNow, 0);
            SerializationAssert.IsJsonSerializable<State>(state, ContractSerialization.DefaultJsonSerializationSettings);
        }

        [Test]
        public void StateIsSerializableWithDefaultSettingsAndWithOptionalParameters()
        {
            State state = new State(DateTime.UtcNow, DateTime.UtcNow, 0, 1);
            SerializationAssert.IsJsonSerializable<State>(state, ContractSerialization.DefaultJsonSerializationSettings);
        }

        [Test]
        public void IsSuccessReturnsExpectedResultWhenNoSuccessStringsAreOffered()
        {
            this.mockSettings.SuccessStrings = new List<string>();
            string logFile = "Random Text";

            bool result = this.provider.OnIsSuccess.Invoke(logFile);
            Assert.IsTrue(result);
        }

        [Test]
        public void IsSuccessReturnsExpectedResultWhenSuccessStringssAreOfferedButNotFound()
        {
            string successString = "my success string";
            this.mockSettings.SuccessStrings = new List<string>() { successString };
            string logFile = "Random Text";

            bool result = this.provider.OnIsSuccess.Invoke(logFile);
            Assert.IsFalse(result);
        }

        [Test]
        public void IsSuccessReturnsExpectedResultWhenSuccessStringsAreOfferedAndFound()
        {
            string successString = "my success string";
            this.mockSettings.SuccessStrings = new List<string>() { successString };
            string logFile = $"Random Text {successString}";

            bool result = this.provider.OnIsSuccess.Invoke(logFile);
            Assert.IsTrue(result);
        }

        [Test]
        public void IsSuccessReturnsExpectedResutlWhenSuccessStringsAreOfferedButNotAllAreFound()
        {
            string successString1 = "Success string 1";
            string successString2 = "Other success string 2";
            this.mockSettings.SuccessStrings = new List<string>() { successString1, successString2 };
            string logFile = $"Random text {successString2} more text";

            bool result = this.provider.OnIsSuccess.Invoke(logFile);
            Assert.IsFalse(result);
        }

        [Test]
        public void IsSuccessReturnsExpectedResultWhenSuccessStringsAreNull()
        {
            this.mockSettings.SuccessStrings = null;
            bool result = this.provider.OnIsSuccess.Invoke("Random text");
            Assert.IsTrue(result);
        }

        [Test]
        public void IsRetryableReturnsExpectedResultWhenRetryStringsAreNotOffered()
        {
            this.mockSettings.RetryableString = new List<string>();
            string logFile = $"Random text";

            bool result = this.provider.OnIsRetryable.Invoke(logFile);
            Assert.IsFalse(result);
        }

        [Test]
        public void IsRetryableReturnsExpectedResultWhenRetryStringsAreOfferedButNotFound()
        {
            string retryString = "Retryable string";
            this.mockSettings.RetryableString = new List<string>() { retryString };
            string logFile = $"Random text";

            bool result = this.provider.OnIsRetryable.Invoke(logFile);
            Assert.IsFalse(result);
        }

        [Test]
        public void IsRetryableReturnsExpectedResultWhenRetryStringsAreOfferedAndFound()
        {
            string retryString = "Retryable string";
            this.mockSettings.RetryableString = new List<string>() { retryString };
            string logFile = $"Random text {retryString}";

            bool result = this.provider.OnIsRetryable.Invoke(logFile);
            Assert.IsTrue(result);
        }

        [Test]
        public void IsRetryableReturnsExpectedResultWhenRetryStringsAreOfferedButNotAllAreFound()
        {
            string retryString1 = "a retryable string1";
            string retryString2 = "a reetryable string2";
            this.mockSettings.RetryableString = new List<string>() { retryString1, retryString2 };
            string logFile = $"{retryString2} some random text";

            bool result = this.provider.OnIsRetryable.Invoke(logFile);
            Assert.IsTrue(result);
        }

        [Test]
        public async Task ActivateExecutablePassesCorrectArgumentsToGetParentDirectory()
        {
            // Add required parameters to componenet
            this.fixture.Component.Parameters["ConfigurationId"] = "someId";
            // Load node exe settings into private fields.
            await this.provider.ConfigureServicesAsync(this.fixture.Context, this.fixture.Component);
            this.mockDirectory.Setup(d => d.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
                .Callback<string, string, SearchOption>((dir, file, option) =>
                {
                    Assert.AreEqual(AgentRuntimeConstants.AppFolder, dir);
                    Assert.AreEqual(this.mockSettings.ExecutableName, file);
                    Assert.AreEqual(SearchOption.AllDirectories, option);
                }).Returns(new string[] { $@"C:\App\{this.mockSettings.Payload}\{this.mockSettings.Scenario}\{this.mockSettings.ExecutableName}" });

            this.provider.OnActivateExecutable.Invoke(this.fixture.Context, new State(DateTime.UtcNow, DateTime.UtcNow, 0), null, CancellationToken.None);

            this.mockDirectory.Verify(d => d.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()), Times.Once());
        }

        [Test]
        public async Task ActivateExecutablePassesCorrectArgumentsToCreateProcess()
        {
            // Add required parameters to componenet
            this.fixture.Component.Parameters["ConfigurationId"] = "someId";
            // Load node exe settings into private fields.
            await this.provider.ConfigureServicesAsync(this.fixture.Context, this.fixture.Component);
            string expectedParentDirectory = @$"C:\App\{this.mockSettings.Payload}\somesubdir\othersubdir";
            Mock<IDirectoryInfo> info = new Mock<IDirectoryInfo>();
            info.SetupGet(i => i.FullName).Returns(expectedParentDirectory);
            this.mockDirectory.Setup(d => d.GetParent(It.IsAny<string>()))
                .Returns(info.Object);

            string expectedArguments = $"/c {this.mockSettings.ExecutableName} {this.mockSettings.Arguments} 1>{this.mockSettings.LogFileName} 2>&1 `& exit";
            this.mockExecution.Setup(e => e.CreateProcess(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EventHandler>(), It.IsAny<IList<string>>()))
                .Callback<string, string, string, EventHandler, IList<string>>((cmd, args, cwd, handler, redirect) => 
                {
                    Assert.AreEqual("cmd.exe", cmd);
                    Assert.AreEqual(expectedArguments, args);
                    Assert.AreEqual(expectedParentDirectory, cwd);
                    Assert.IsNull(redirect);
                    Assert.IsNotNull(handler);
                });

            this.provider.OnActivateExecutable.Invoke(this.fixture.Context, new State(DateTime.UtcNow, DateTime.UtcNow, 0), null, CancellationToken.None);

            this.mockExecution.Verify(e => e.CreateProcess(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EventHandler>(), It.IsAny<IList<string>>()), Times.Once());
        }

        [Test]
        public async Task ExecuteAsyncPassesCorrectStateWhenExitingStartState()
        {
            // Add required parameters to componenet
            this.fixture.Component.Parameters["ConfigurationId"] = "someId";
            // Load node exe settings into private fields.
            await this.provider.ConfigureServicesAsync(this.fixture.Context, this.fixture.Component);
            this.fixture.DataClient.Setup(dc => dc.GetOrCreateStateAsync<State>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .ReturnsAsync((State)null);
            this.fixture.DataClient.Setup(dc => dc.SaveStateAsync<State>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<State>(), It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .Callback<string, string, State, CancellationToken, string>((id, key, state, token, shared) =>
                {
                    // only need to check state since rest is taken care of by provider framework
                    Assert.AreEqual(0, state.RetryCount);
                    // Give 10 millisecond buffer for asserting equality.
                    Assert.IsTrue(DateTime.UtcNow.Add(this.mockSettings.MaxExecutionTime.Value) <= state.StepTimeout.Add(TimeSpan.FromMilliseconds(1000)));
                    Assert.IsTrue(DateTime.UtcNow.Add(this.mockSettings.MinExecutionTime.Value) <= state.StepLowerBound.Add(TimeSpan.FromMilliseconds(1000)));
                    Assert.IsNull(state.ProcessExitCode);
                }).Returns(Task.CompletedTask);

            ExecutionResult result = await this.provider.ExecuteAsync(this.fixture.Context, this.fixture.Component, CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual(ExecutionStatus.InProgress, result.Status);

            this.fixture.DataClient.Verify(dc => dc.GetOrCreateStateAsync<State>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string>()), Times.Once());
        }

        [Test]
        public async Task ExecuteAsyncReturnsExpectedRestulWhenInIdleState()
        {
            // Add required parameters to componenet
            this.fixture.Component.Parameters["ConfigurationId"] = "someId";
            // Load node exe settings into private fields.
            await this.provider.ConfigureServicesAsync(this.fixture.Context, this.fixture.Component);
            this.fixture.DataClient.Setup(dc => dc.GetOrCreateStateAsync<State>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .ReturnsAsync(new State(DateTime.UtcNow.Add(TimeSpan.FromSeconds(100)), DateTime.UtcNow, 0));

            ExecutionResult result = await this.provider.ExecuteAsync(this.fixture.Context, this.fixture.Component, CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(ExecutionStatus.InProgress, result.Status);
        }

        [Test]
        public async Task ExecuteAsyncReturnsExpectedResultWhenStateHasTimedOut()
        {
            // Add required parameters to componenet
            this.fixture.Component.Parameters["ConfigurationId"] = "someId";
            // Load node exe settings into private fields.
            await this.provider.ConfigureServicesAsync(this.fixture.Context, this.fixture.Component);
            this.fixture.DataClient.Setup(dc => dc.GetOrCreateStateAsync<State>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .ReturnsAsync(new State(DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(100)), DateTime.UtcNow, 0));

            ExecutionResult result = await this.provider.ExecuteAsync(this.fixture.Context, this.fixture.Component, CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(ExecutionStatus.Failed, result.Status);
            Assert.IsInstanceOf<TimeoutException>(result.Error);
        }

        [Test]
        public async Task ExecuteAsyncReturnsExpectedResultWhenProcessHasFinishedButIsShortLived()
        {
            // Add required parameters to componenet
            this.fixture.Component.Parameters["ConfigurationId"] = "someId";
            // Load node exe settings into private fields.
            await this.provider.ConfigureServicesAsync(this.fixture.Context, this.fixture.Component);
            this.fixture.DataClient.Setup(dc => dc.GetOrCreateStateAsync<State>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .ReturnsAsync(new State(DateTime.UtcNow.Add(TimeSpan.FromSeconds(100)), DateTime.UtcNow.Add(TimeSpan.FromSeconds(100)), 0, 0));

            ExecutionResult result = await this.provider.ExecuteAsync(this.fixture.Context, this.fixture.Component, CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(ExecutionStatus.Failed, result.Status);
            Assert.IsInstanceOf<TimeoutException>(result.Error);
        }

        [Test]
        public async Task ExecuteAsyncReturnsExpectedResultWhenProcessExitsAndCanEnterRetryState()
        {
            // Add required parameters to componenet
            this.fixture.Component.Parameters["ConfigurationId"] = "someId";
            // Load node exe settings into private fields.
            await this.provider.ConfigureServicesAsync(this.fixture.Context, this.fixture.Component);
            this.mockSettings.Retries = 3;
            DateTime originalStepTimeout = DateTime.UtcNow.Add(TimeSpan.FromSeconds(100));
            DateTime originalStepLowerbound = DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(100));
            this.fixture.DataClient.Setup(dc => dc.GetOrCreateStateAsync<State>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .ReturnsAsync(new State(originalStepTimeout, originalStepLowerbound, 1, 1));
            this.mockFile.Setup(f => f.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync($"Some file contents {string.Concat(this.mockSettings.RetryableString)}");
            this.fixture.DataClient.Setup(dc => dc.SaveStateAsync<State>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<State>(), It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .Callback<string, string, State, CancellationToken, string>((experimentId, key, state, token, shared) =>
                {
                    Assert.AreEqual(originalStepTimeout, state.StepTimeout);
                    Assert.AreEqual(originalStepLowerbound, state.StepLowerBound);
                    Assert.IsNull(state.ProcessExitCode);
                    Assert.AreEqual(2, state.RetryCount);
                }).Returns(Task.CompletedTask);

            ExecutionResult result = await this.provider.ExecuteAsync(this.fixture.Context, this.fixture.Component, CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(ExecutionStatus.InProgress, result.Status);

            this.fixture.DataClient.Verify(dc => dc.SaveStateAsync<State>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<State>(), It.IsAny<CancellationToken>(), It.IsAny<string>()), Times.Once());
        }

        [Test]
        public async Task ExecuteAsyncReturnsExpectedResultWhenProcessExitsButCanNotEnterRetryState()
        {
            // Add required parameters to componenet
            this.fixture.Component.Parameters["ConfigurationId"] = "someId";
            // Load node exe settings into private fields.
            await this.provider.ConfigureServicesAsync(this.fixture.Context, this.fixture.Component);
            this.mockSettings.Retries = 1;
            DateTime originalStepTimeout = DateTime.UtcNow.Add(TimeSpan.FromSeconds(100));
            DateTime originalStepLowerbound = DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(100));
            this.fixture.DataClient.Setup(dc => dc.GetOrCreateStateAsync<State>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .ReturnsAsync(new State(originalStepTimeout, originalStepLowerbound, 1, 1));
            this.mockFile.Setup(f => f.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync($"Some file contents {string.Concat(this.mockSettings.RetryableString)}");

            ExecutionResult result = await this.provider.ExecuteAsync(this.fixture.Context, this.fixture.Component, CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(ExecutionStatus.Failed, result.Status);
            Assert.IsInstanceOf<ProviderException>(result.Error);
        }

        [Test]
        public async Task ExecuteAsyncReturnsExpectedResultWhenProcessExitsAndIsNotSuccessful()
        {
            // Add required parameters to componenet
            this.fixture.Component.Parameters["ConfigurationId"] = "someId";
            // Load node exe settings into private fields.
            await this.provider.ConfigureServicesAsync(this.fixture.Context, this.fixture.Component);
            this.fixture.DataClient.Setup(dc => dc.GetOrCreateStateAsync<State>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .ReturnsAsync(new State(DateTime.UtcNow.Add(TimeSpan.FromSeconds(100)), DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(100)), 1, 0));
            this.mockFile.Setup(f => f.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync($"Some file contents");

            ExecutionResult result = await this.provider.ExecuteAsync(this.fixture.Context, this.fixture.Component, CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(ExecutionStatus.Failed, result.Status);
            Assert.IsInstanceOf<ProviderException>(result.Error);
        }

        [Test]
        public async Task ExecuteAsyncReturnsExpectedResutlWhenProcessExitsAndIsSuccessful()
        {
            // Add required parameters to componenet
            this.fixture.Component.Parameters["ConfigurationId"] = "someId";
            // Load node exe settings into private fields.
            await this.provider.ConfigureServicesAsync(this.fixture.Context, this.fixture.Component);
            this.fixture.DataClient.Setup(dc => dc.GetOrCreateStateAsync<State>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .ReturnsAsync(new State(DateTime.UtcNow.Add(TimeSpan.FromSeconds(100)), DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(100)), 1, 0));
            this.mockFile.Setup(f => f.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync($"Some file contents {string.Concat(this.mockSettings.SuccessStrings)}");

            ExecutionResult result = await this.provider.ExecuteAsync(this.fixture.Context, this.fixture.Component, CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(ExecutionStatus.Succeeded, result.Status);
        }

        [Test]
        public async Task ExecuteAsyncReturnsExpectedResutlWhenProcessExitsAndIsSuccessfulWhenOverrideParametersArePassed()
        {
            // Add required parameters to componenet
            this.fixture.Component.Parameters["ExecutableName"] = "executableName";
            this.fixture.Component.Parameters["Payload"] = "payload";
            this.fixture.Component.Parameters["LogFileName"] = "logfilename";
            this.fixture.Component.Parameters["MaxExecutionTime"] = "00:10:00";
            // Load node exe settings into private fields.
            await this.provider.ConfigureServicesAsync(this.fixture.Context, this.fixture.Component);
            this.fixture.DataClient.Setup(dc => dc.GetOrCreateStateAsync<State>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .ReturnsAsync(new State(DateTime.UtcNow.Add(TimeSpan.FromSeconds(100)), DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(100)), 1, 0));
            this.mockFile.Setup(f => f.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync($"Some file contents {string.Concat(this.mockSettings.SuccessStrings)}");

            ExecutionResult result = await this.provider.ExecuteAsync(this.fixture.Context, this.fixture.Component, CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(ExecutionStatus.Succeeded, result.Status);
        }

        [ExecutionConstraints(SupportedStepType.Payload, SupportedStepTarget.ExecuteOnNode)]
        private class TestAgentExecuteCommandProvider : AgentExecuteCommandProvider
        {
            public TestAgentExecuteCommandProvider(IServiceCollection services)
                : base(services)
            {
            }

            public Func<string, bool> OnIsSuccess => (logfile) => this.IsSuccess(logfile);

            public Func<string, bool> OnIsRetryable => (logFile) => this.IsRetryable(logFile);

            public Action<ExperimentContext, State, string, CancellationToken> OnActivateExecutable => (context, state, scenario, token) => this.ActivateExecutable(context, state, scenario, token); 
        }
    }
}
