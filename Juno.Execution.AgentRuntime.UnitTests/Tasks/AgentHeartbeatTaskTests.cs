namespace Juno.Execution.AgentRuntime.Tasks
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class AgentHeartbeatTaskTests
    {
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
            this.mockFixture.SetupAgentMocks();

            this.fixtureDependencies = new FixtureDependencies();
            this.agentClient = new AgentClient(this.fixtureDependencies.RestClient.Object, new Uri("https://any/uri"));
            this.agentId = this.mockFixture.Create<AgentIdentification>();

            this.fixtureDependencies.RestClient.SetReturnsDefault(new HttpResponseMessage(HttpStatusCode.OK));
            
            // The task uses an agent client to communicate with the Juno Agent API.
            this.services = new ServiceCollection()
                .AddSingleton(new ClientPool<AgentClient>
                {
                    [ApiClientType.AgentHeartbeatApi] = this.agentClient
                });

            this.settings = this.fixtureDependencies.Settings;
        }

        [Test]
        public void AgentHeartbeatTaskConstructorsValidateRequiredParameters()
        {
            AgentType validAgentType = AgentType.HostAgent;
            Assert.Throws<ArgumentException>(() => new AgentHeartbeatTask(null, this.settings, this.agentId, validAgentType));
            Assert.Throws<ArgumentException>(() => new AgentHeartbeatTask(this.services, null, this.agentId, validAgentType));
            Assert.Throws<ArgumentException>(() => new AgentHeartbeatTask(this.services, this.settings, null, validAgentType));
        }

        [Test]
        public void AgentHeartbeatTaskConstructorsSetPropertiesToExpectedValues()
        {
            AgentType agentType = AgentType.HostAgent;
            AgentHeartbeatTask heartbeatTask = new AgentHeartbeatTask(this.services, this.settings, this.agentId, agentType);

            EqualityAssert.PropertySet(heartbeatTask, "Services", this.services);
            EqualityAssert.PropertySet(heartbeatTask, "Settings", this.settings);
            EqualityAssert.PropertySet(heartbeatTask, "AgentId", this.agentId);
            EqualityAssert.PropertySet(heartbeatTask, "AgentType", agentType);
        }

        [Test]
        public void AgentHeartbeatTaskSendsTheExpectedHeartbeat()
        {
            AgentType agentType = AgentType.HostAgent;
            AgentIdentification agentId = new AgentIdentification("Cluster01,Node01");
            AgentHeartbeatTask heartbeatTask = new AgentHeartbeatTask(this.services, this.fixtureDependencies.Settings, agentId, agentType);

            bool heartbeatSent = false;

            this.fixtureDependencies.RestClient
                .Setup(client => client.PostAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<HttpContent>(),
                    It.IsAny<CancellationToken>()))
                .Callback<Uri, HttpContent, CancellationToken>((uri, content, token) =>
                {
                    heartbeatSent = true;
                    string requestBody = content.ReadAsStringAsync().GetAwaiter().GetResult();
                    Assert.IsNotNull(requestBody);

                    AgentHeartbeat actualHeartbeat = requestBody.FromJson<AgentHeartbeat>();

                    Assert.IsTrue(actualHeartbeat.AgentIdentification.Equals(agentId)
                        && actualHeartbeat.AgentType == agentType
                        && actualHeartbeat.Status == AgentHeartbeatStatus.Running);
                });

            heartbeatTask.ExecuteAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(heartbeatSent);
        }

        [Test]
        public void AgentHeartbeatTaskHandlesExceptionsThatOccurDuringAnAttemptToProcessExperimentSteps()
        {
            AgentType agentType = AgentType.HostAgent;
            AgentIdentification agentId = new AgentIdentification("Cluster01,Node01");
            AgentHeartbeatTask heartbeatTask = new AgentHeartbeatTask(this.services, this.fixtureDependencies.Settings, agentId, agentType);

            this.fixtureDependencies.RestClient
                .Setup(client => client.PostAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<HttpContent>(),
                    It.IsAny<CancellationToken>()))
                .Throws(new Exception("Any error"));

            Assert.DoesNotThrow(() => heartbeatTask.ExecuteAsync(CancellationToken.None)
                .GetAwaiter().GetResult());
        }

        private class TestSystemMonitor : AgentMonitorTask<string>
        {
            public TestSystemMonitor(ServiceCollection services, EnvironmentSettings settings)
                : base(services, settings)
            {
            }

            public override Task ExecuteAsync(CancellationToken cancellationToken)
            {
                this.OnResults(new ExecutionEventArgs<string>("Snapshot event data"));
                return Task.CompletedTask;
            }
        }
    }
}
