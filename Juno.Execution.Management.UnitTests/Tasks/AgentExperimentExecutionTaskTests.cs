namespace Juno.Execution.Management.Tasks
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
    using Juno.Providers;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Repository.KeyVault;
    using Microsoft.Azure.CRC.Rest;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using NUnit.Framework;
    using Polly;

    [TestFixture]
    [Category("Unit")]
    public class AgentExperimentExecutionTaskTests
    {
        private Fixture mockFixture;
        private FixtureDependencies mockDependencies;
        private Mock<IRestClient> mockRestClient;
        private Mock<IProviderDataClient> mockProviderDataClient;

        private IServiceCollection services;
        private EnvironmentSettings settings;
        private AgentExecutionManager executionManager;
        private AgentIdentification agentId;
        private AgentType agentType;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupAgentMocks();

            this.mockDependencies = new FixtureDependencies();
            this.mockRestClient = new Mock<IRestClient>();
            this.mockProviderDataClient = new Mock<IProviderDataClient>();
            this.agentId = this.mockFixture.Create<AgentIdentification>();
            this.agentType = AgentType.HostAgent;

            AgentClient mockAgentApiClient = new AgentClient(this.mockRestClient.Object, new Uri("https://anyjunoenvironment.execution"), Policy.NoOpAsync());

            this.services = new ServiceCollection()
                .AddSingleton<AgentIdentification>(this.agentId)
                .AddSingleton<AgentClient>(mockAgentApiClient)
                .AddSingleton<IProviderDataClient>(this.mockProviderDataClient.Object)
                .AddSingleton<IAzureKeyVault>(this.mockDependencies.KeyVault.Object);

            this.executionManager = new AgentExecutionManager(this.services, new ConfigurationBuilder().Build());

            // The task uses an agent client to communicate with the Juno Agent API.
            this.services.AddSingleton(this.executionManager);

            this.mockDependencies.RestClient.SetReturnsDefault(new HttpResponseMessage(HttpStatusCode.OK));

            this.settings = this.mockDependencies.Settings;
        }

        [Test]
        public void AgentExperimentExecutionTaskConstructorsValidateRequiredParameters()
        {
            Assert.Throws<ArgumentException>(() => new AgentExperimentExecutionTask(null, this.settings, this.agentId, this.agentType));
            Assert.Throws<ArgumentException>(() => new AgentExperimentExecutionTask(this.services, null, this.agentId, this.agentType));
            Assert.Throws<ArgumentException>(() => new AgentExperimentExecutionTask(this.services, this.settings, null, this.agentType));
        }

        [Test]
        public void AgentExperimentExecutionTaskConstructorsSetPropertiesToExpectedValues()
        {
            AgentExperimentExecutionTask executionTask = new AgentExperimentExecutionTask(this.services, this.settings, this.agentId, this.agentType);

            EqualityAssert.PropertySet(executionTask, "Services", this.services);
            EqualityAssert.PropertySet(executionTask, "Settings", this.settings);
            EqualityAssert.PropertySet(executionTask, "AgentId", this.agentId);
            EqualityAssert.PropertySet(executionTask, "AgentType", this.agentType);
        }

        [Test]
        public void AgentExperimentExecutionTaskExecutesExperimentProcessing()
        {
            TestAgentExperimentExecutionTask executionTask = new TestAgentExperimentExecutionTask(
                this.services, this.settings, this.agentId, this.agentType);

            bool executionOccurred = false;
            executionTask.OnProcessExperimentAsync = (executionManager, token) => executionOccurred = true;
            executionTask.ExecuteAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(executionOccurred);
        }

        [Test]
        public void AgentExperimentExecutionTaskHandlesExceptionsThatOccurDuringAnAttemptToProcessExperimentSteps()
        {
            TestAgentExperimentExecutionTask executionTask = new TestAgentExperimentExecutionTask(
               this.services, this.settings, this.agentId, this.agentType);

            executionTask.OnProcessExperimentAsync = (executionManager, token) => throw new Exception("Any error");
            Assert.DoesNotThrow(() => executionTask.ExecuteAsync(CancellationToken.None)
                .GetAwaiter().GetResult());
        }

        private class TestAgentExperimentExecutionTask : AgentExperimentExecutionTask
        {
            public TestAgentExperimentExecutionTask(IServiceCollection services, EnvironmentSettings settings, AgentIdentification agentId, AgentType agentType)
                : base(services, settings, agentId, agentType)
            {
            }

            public Action<AgentExecutionManager, CancellationToken> OnProcessExperimentAsync { get; set; }

            protected override Task ProcessExperimentAsync(AgentExecutionManager executionManager, CancellationToken cancellationToken)
            {
                this.OnProcessExperimentAsync?.Invoke(executionManager, cancellationToken);
                return Task.CompletedTask;
            }
        }
    }
}
