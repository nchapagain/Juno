namespace Juno.Execution.AgentRuntime.Tasks
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Repository.KeyVault;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using NUnit.Framework;
    using Polly;

    [TestFixture]
    [Category("Unit")]
    public class VirtualClientMonitorTaskTests
    {
        private ExperimentFixture mockFixture;
        private FixtureDependencies mockDependencies;
        private Mock<IProcessManager> mockProcessManager;
        private EventContext eventContext;
        private TestVirtualClientTask virtualClientMonitorTask;
        private VCMonitorSettings vCMonitorSettings;
        private AgentIdentification agentIdentification;
        private AgentClient agentClient;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new ExperimentFixture();
            this.mockFixture.Setup();
            this.mockDependencies = new FixtureDependencies();

            List<KeyValuePair<string, object>> contextProperties = new List<KeyValuePair<string, object>>();
            contextProperties.Add(new KeyValuePair<string, object>("containerId", "a container id"));
            this.eventContext = new EventContext(Guid.NewGuid(), contextProperties);

            this.agentIdentification = new AgentIdentification("Cluster_01", "Node_01", "VM-01", Guid.NewGuid().ToString());
            this.mockProcessManager = new Mock<IProcessManager>();
            this.vCMonitorSettings = new VCMonitorSettings()
            {
                Enabled = true,
                MonitorInterval = TimeSpan.Zero
            };
            this.mockDependencies.Settings.AgentSettings.AgentMonitoringSettings.VCMonitorSettings = this.vCMonitorSettings;

            // avoid adding the event hub telemetry argument for now
            this.mockDependencies.Settings.EventHubSettings = this.mockDependencies.Settings.EventHubSettings.Where(s => s.Id != nameof(Setting.VirtualClientTelemetry));
            this.mockDependencies.Settings.EventHubSettings = this.mockDependencies.Settings.EventHubSettings.Concat(new List<EventHubSettings>() 
            { 
                new EventHubSettings() 
                { 
                    Id = nameof(Setting.VirtualClientTelemetry), 
                    ConnectionString = string.Empty 
                } 
            });

            this.agentClient = new AgentClient(this.mockDependencies.RestClient.Object, new Uri("https://any/uri"));
            IServiceCollection services = new ServiceCollection()
                .AddSingleton<AgentIdentification>(this.agentIdentification)
                // .AddSingleton<IAzureKeyVault>(this.mockDependencies.KeyVault.Object)
                .AddSingleton(new ClientPool<AgentClient>
                {
                    [ApiClientType.AgentApi] = this.agentClient,
                    [ApiClientType.AgentFileUploadApi] = this.agentClient
                });

            this.virtualClientMonitorTask = new TestVirtualClientTask(services, this.mockDependencies.Settings);
            this.virtualClientMonitorTask.FindsProcess = false;
            this.virtualClientMonitorTask.FoundProcessManager = null;
        }

        [Test]
        public async Task VirtualClientMonitorTaskCreatesProcessManagerAndStartsItsProcess()
        {
            bool calledOnCreate = false;
            this.virtualClientMonitorTask.OnCreateAndStartProcessManager = () =>
            {
                calledOnCreate = true;
            };
            bool calledOnStart = false;
            this.virtualClientMonitorTask.OnStartProcessManager = () =>
            {
                calledOnStart = true;
            };

            await this.virtualClientMonitorTask.ExecuteAsync(CancellationToken.None);
            Assert.IsNotNull(this.virtualClientMonitorTask.GetProcessManager());
            Assert.IsTrue(calledOnCreate);
            Assert.IsTrue(calledOnStart);
        }

        [Test]
        public async Task VirtualClientMonitorTaskPicksUpExistingProcess()
        {
            IProcessProxy expectedProcessProxy = new ProcessProxy(new System.Diagnostics.Process());
            this.virtualClientMonitorTask.FindsProcess = true;
            this.virtualClientMonitorTask.FoundProcessManager = new ProcessManager(expectedProcessProxy);

            await this.virtualClientMonitorTask.ExecuteAsync(CancellationToken.None);

            Assert.AreEqual(this.virtualClientMonitorTask.GetProcessManager(), this.virtualClientMonitorTask.FoundProcessManager);
        }

        [Test]
        public async Task VirtualClientMonitorTaskRestartsProcessWhenNotRunning()
        {
            this.virtualClientMonitorTask = new TestVirtualClientTask(new ServiceCollection(), this.mockDependencies.Settings, processManager: this.mockProcessManager.Object);
            bool calledProcessManagerIsProcessRunning = false;
            this.mockProcessManager.Setup(pm => pm.IsProcessRunning())
                .Callback(() =>
                {
                    calledProcessManagerIsProcessRunning = true;
                })
                .Returns(false);

            bool calledOnStartProcessManager = false;
            this.virtualClientMonitorTask.OnStartProcessManager = () =>
            {
                calledOnStartProcessManager = true;
            };

            await this.virtualClientMonitorTask.ExecuteAsync(CancellationToken.None);

            Assert.True(calledProcessManagerIsProcessRunning);
            Assert.True(calledOnStartProcessManager);
        }

        [Test]
        public async Task VirtualClientMonitorTaskAppliesTheRetryPolicyDefinedOnFailureToManageProcessSuccessfully()
        {
            int expectedRetryCount = 3;
            int attempts = 0;
            IAsyncPolicy expectedRetryPolicy = Policy.Handle<Exception>().RetryAsync(retryCount: expectedRetryCount);

            this.virtualClientMonitorTask = new TestVirtualClientTask(new ServiceCollection(), this.mockDependencies.Settings, expectedRetryPolicy);
            this.virtualClientMonitorTask.OnCreateAndStartProcessManager = () =>
            {
                attempts++;
                throw new Exception();
            };

            await this.virtualClientMonitorTask.ExecuteAsync(CancellationToken.None);

            // 1 attempt + 3 retries -> expectedRetryCount + 1
            Assert.IsTrue(attempts == expectedRetryCount + 1);
        }

        [Test]
        public async Task VirtualClientMonitorTaskGathersTheExpectedArguments()
        {
            IDictionary<string, string> actualMetadata = this.virtualClientMonitorTask.CreateMetadataTest(this.mockFixture.ExperimentInstance, this.agentIdentification, this.eventContext);
            string metadataArguments = string.Join(",,,", actualMetadata.Select(entry => $"{entry.Key}={entry.Value}"));
            string expectedArguments = $"--profile=RealTimeDataMonitor.json --platform=Juno --timeout={(int)TimeSpan.FromDays(7).TotalMinutes} --multipleInstances=true --metadata=\"{metadataArguments}\"";

            this.virtualClientMonitorTask.TestCurrentExperimentInstance = this.mockFixture.ExperimentInstance;
            string actualArguments = await this.virtualClientMonitorTask.CallGetCommandArgumentsAsync(this.eventContext, CancellationToken.None).ConfigureDefaults();

            Assert.IsNotNull(actualMetadata);
            Assert.NotZero(actualMetadata.Count);
            Assert.AreEqual(actualMetadata["containerId"], this.eventContext.Properties["containerId"]);
            Assert.AreEqual(actualMetadata["agentId"], this.agentIdentification.ToString());
            Assert.AreEqual(actualMetadata["tipSessionId"], this.agentIdentification.Context);
            Assert.AreEqual(actualMetadata["nodeId"], this.agentIdentification.NodeName);
            Assert.AreEqual(actualMetadata["nodeName"], this.agentIdentification.NodeName);
            Assert.AreEqual(actualMetadata["experimentId"], this.mockFixture.ExperimentInstance.Id);
            Assert.AreEqual(actualMetadata["virtualMachineName"], this.agentIdentification.VirtualMachineName);
            Assert.AreEqual(actualMetadata["clusterName"], this.agentIdentification.ClusterName);
            Assert.AreEqual(expectedArguments, actualArguments);
        }

        [Test]
        public void VirtualClientMonitorTaskGathersTheExpectedExperiment()
        {
            // Mock Setup: The system monitoring class should get the experiment for which the agent is
            // running in first. It must have the ID of the experiment to provide command argument metadata.
            // It.Is<Uri>(uri => uri.AbsolutePath.Contains("/api/experiments"))
            this.mockDependencies.RestClient
                .Setup(client => client.GetAsync(
                    It.Is<Uri>(uri => uri.PathAndQuery == $"/api/experiments?agentId={this.agentIdentification.ToString()}"),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<HttpCompletionOption>()))
                .Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(this.mockFixture.ExperimentInstance.ToJson()) }))
                .Verifiable();

            bool foundExperiment = this.virtualClientMonitorTask.CallGetExperimentInstance(CancellationToken.None);

            this.mockDependencies.RestClient.VerifyAll();
            Assert.AreEqual(this.virtualClientMonitorTask.GetExperiment(), this.mockFixture.ExperimentInstance);
            Assert.True(foundExperiment);
        }

        private class TestVirtualClientTask : VirtualClientMonitorTask
        {
            public TestVirtualClientTask(IServiceCollection services, EnvironmentSettings settings, IAsyncPolicy retryPolicy = null, IProcessManager processManager = null)
               : base(services, settings, retryPolicy, processManager)
            {
            }

            public ExperimentInstance TestCurrentExperimentInstance
            { 
                get
                {
                    return this.CurrentExperiment;
                }

                set
                {
                    this.CurrentExperiment = value;
                }
            }

            public bool FindsProcess { get; set; }

            public IProcessManager FoundProcessManager { get; set; }

            public Action OnStartProcessManager { get; set; }

            public Action OnCreateAndStartProcessManager { get; set; }

            public IDictionary<string, string> CreateMetadataTest(ExperimentInstance experiment, AgentIdentification agentId, EventContext telemetryContext)
            {
                return VirtualClientMonitorTask.CreateMetadata(experiment, agentId, telemetryContext);
            }

            public IProcessManager GetProcessManager()
            {
                return VirtualClientMonitorTask.VCProcessManager;
            }

            public ExperimentInstance GetExperiment()
            {
                return this.CurrentExperiment;
            }

            public Task<string> CallGetCommandArgumentsAsync(EventContext telemetryContext, CancellationToken cancellationToken)
            {
                return base.GetCommandArgumentsAsync(telemetryContext, cancellationToken);
            }

            public bool CallGetExperimentInstance(CancellationToken cancellationToken)
            {
                return base.GetExperimentInstance(cancellationToken);
            }

            protected override bool TryGetVirtualClientProcess(out IProcessManager processManager)
            {
                processManager = this.FoundProcessManager;
                return this.FindsProcess;
            }

            protected override Task CreateAndStartProcessManagerAsync(EventContext telemetryContext, CancellationToken cancellationToken)
            {
                this.OnCreateAndStartProcessManager?.Invoke();
                return base.CreateAndStartProcessManagerAsync(telemetryContext, cancellationToken);
            }

            protected override Task StartProcessAsync(EventContext telemetryContext, CancellationToken cancellationToken)
            {
                this.OnStartProcessManager?.Invoke();
                return Task.CompletedTask;
            }

            protected override Task<string> GetCommandArgumentsAsync(EventContext telemetryContext, CancellationToken cancellationToken)
            {
                return Task.FromResult("arguments");
            }

            protected override bool GetExperimentInstance(CancellationToken cancellationToken)
            {
                return true;
            }
        }
    }
}
