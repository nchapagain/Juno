namespace Juno.Execution.AgentRuntime
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;
    using AutoFixture;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using NUnit.Framework;
    using Polly;

    [TestFixture]
    [Category("Unit")]
    public class AgentSystemMonitoringTests
    {
        private TestAgentSystemMonitoring systemMonitoring;
        private Fixture mockFixture;
        private FixtureDependencies fixtureDependencies;
        private AgentClient agentClient;
        private AgentIdentification agentId;
        private IServiceCollection services;
        private EnvironmentSettings settings;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks();
            this.mockFixture.SetupAgentMocks();

            this.fixtureDependencies = new FixtureDependencies();
            this.agentClient = new AgentClient(this.fixtureDependencies.RestClient.Object, new Uri("https://any/uri"));
            this.agentId = this.mockFixture.Create<AgentIdentification>();
            this.services = new ServiceCollection().AddSingleton(new ClientPool<AgentClient>
            {
                [ApiClientType.AgentApi] = this.agentClient,
                [ApiClientType.AgentFileUploadApi] = this.agentClient
            });

            this.settings = this.fixtureDependencies.Settings;
            this.systemMonitoring = new TestAgentSystemMonitoring(this.services, this.settings, this.agentId, AgentType.GuestAgent, Policy.NoOpAsync());
        }

        [Test]
        public void AgentSystemMonitoringConstructorsValidateRequiredParameters()
        {
            Assert.Throws<ArgumentException>(() => new AgentSystemMonitoring(null, this.settings, this.agentId, AgentType.GuestAgent));
            Assert.Throws<ArgumentException>(() => new AgentSystemMonitoring(this.services, null, this.agentId, AgentType.GuestAgent));
            Assert.Throws<ArgumentException>(() => new AgentSystemMonitoring(this.services, this.settings, null, AgentType.GuestAgent));
        }

        [Test]
        public void AgentSystemMonitoringConstructorsSetPropertiesToExpectedValues()
        {
            AgentSystemMonitoring heartbeatTask = new AgentSystemMonitoring(this.services, this.settings, this.agentId, AgentType.GuestAgent);

            EqualityAssert.PropertySet(heartbeatTask, "Services", this.services);
            EqualityAssert.PropertySet(heartbeatTask, "Settings", this.settings);
            EqualityAssert.PropertySet(heartbeatTask, "AgentId", this.agentId);
        }

        [Test]
        public void AgentSystemMonitoringHandlesSelLogUploadsAsExpected()
        {
            ExperimentInstance expectedExperiment = this.mockFixture.Create<ExperimentInstance>();
            ExecutionEventArgs<string> expectedSelLogResults = new ExecutionEventArgs<string>("Any SEL log results");

            // Mock Setup: The system monitoring class should get the experiment for which the agent is
            // running in first. It must have the ID of the experiment to upload the SEL log.
            // It.Is<Uri>(uri => uri.AbsolutePath.Contains("/api/experiments"))
            this.fixtureDependencies.RestClient
                .Setup(client => client.GetAsync(
                    It.Is<Uri>(uri => uri.PathAndQuery == $"/api/experiments?agentId={this.agentId.ToString()}"),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<HttpCompletionOption>()))
                .Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(expectedExperiment.ToJson()) }))
                .Verifiable();

            // Mock Setup:  The system monitoring class will upload the SEL log at that point
            this.fixtureDependencies.RestClient.Setup(client => client.PostAsync(
                It.Is<Uri>(uri =>
                    uri.PathAndQuery.Contains(
                        $"/api/experiments/agent-files" +
                        $"?experimentId={expectedExperiment.Id}&agentType=Host&agentId={HttpUtility.UrlEncode(this.agentId.ToString())}&fileName=sellog")),
                It.Is<HttpContent>(content => content.ReadAsStringAsync().GetAwaiter().GetResult() == expectedSelLogResults.Results),
                It.IsAny<CancellationToken>()))
                .Verifiable();

            this.systemMonitoring.OnSelLogMonitorResult(this, expectedSelLogResults);

            this.fixtureDependencies.RestClient.VerifyAll();
        }

        [Test]
        public void AgentSystemMonitoringHandlesExceptionsThatOccurDuringAnAttemptToUploadASelLog()
        {
            ExecutionEventArgs<string> selLogResults = new ExecutionEventArgs<string>("Any SEL log results");

            this.fixtureDependencies.RestClient
                .Setup(client => client.GetAsync(
                   It.IsAny<Uri>(),
                   It.IsAny<CancellationToken>(),
                   It.IsAny<HttpCompletionOption>()))
                .Throws(new Exception("Any exception"));

            Assert.DoesNotThrow(() => this.systemMonitoring.OnSelLogMonitorResult(this, selLogResults));
        }

        private class TestAgentSystemMonitoring : AgentSystemMonitoring
        {
            public TestAgentSystemMonitoring(IServiceCollection services, EnvironmentSettings settings, AgentIdentification agentId, AgentType type, IAsyncPolicy retryPolicy = null)
                : base(services, settings, agentId, type, retryPolicy)
            {
            }

            public new void OnSelLogMonitorResult(object sender, ExecutionEventArgs<string> e)
            {
                base.OnSelLogMonitorResult(sender, e);
            }
        }
    }
}
