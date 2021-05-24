namespace Juno.Execution.Management
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Repository.KeyVault;
    using Microsoft.Azure.CRC.Rest;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NuGet.Protocol;
    using NUnit.Framework;
    using Polly;

    [TestFixture]
    [Category("Unit")]
    public class AgentExecutionManagerTests
    {
        private Fixture mockFixture;
        private AgentExecutionManager executionManager;
        private AgentClient mockAgentApiClient;
        private Mock<IRestClient> mockRestClient;
        private Mock<IAzureKeyVault> mockKeyVaultClient;
        private Mock<IProviderDataClient> mockProviderDataClient;
        private ExperimentStepFactory stepFactory;

        // The following mock objects are used in the default setup of the mock REST client. By defining
        // them as simple variables, it makes them easy to change before setting up the mock REST client.
        // And that makes it easy to make small changes to the behaviors of small parts of the larger workflow
        // in order to validate a specific behavior of the experiment manager.
        private AgentIdentification agentId;

        private ExperimentInstance mockExperiment;
        private List<ExperimentStepInstance> mockExperimentSteps;
        private ExperimentStepInstance exampleParentStep;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture
                .SetupExperimentMocks();
            this.mockRestClient = new Mock<IRestClient>();
            this.mockKeyVaultClient = new Mock<IAzureKeyVault>();
            this.mockProviderDataClient = new Mock<IProviderDataClient>();
            this.agentId = new AgentIdentification("Cluster01,Node01,VM01");
            this.exampleParentStep = this.mockFixture.Create<ExperimentStepInstance>();
            this.stepFactory = new ExperimentStepFactory();
            this.mockAgentApiClient = new AgentClient(
                this.mockRestClient.Object,
                new Uri("https://anyjunoenvironment.execution"),
                Policy.NoOpAsync());
            this.executionManager = new AgentExecutionManager(
                new ServiceCollection()
                    .AddSingleton<ILogger>(NullLogger.Instance)
                    .AddSingleton<AgentIdentification>(this.agentId)
                    .AddSingleton<AgentClient>(this.mockAgentApiClient)
                    .AddSingleton<IProviderDataClient>(this.mockProviderDataClient.Object)
                    .AddSingleton<IAzureKeyVault>(this.mockKeyVaultClient.Object),
                new ConfigurationBuilder().Build(),
                Policy.NoOpAsync());

            // General Mock Pattern
            // When there are a lot of dependency interactions in a given method/workflow, it is often
            // helpful to follow the "happy path" mock setup strategy.  This involves setting up ALL mock
            // dependencies to a singular, known good behavior (e.g. the happy path). Then in each individual
            // unit test method, you change ONLY the one or two dependency behaviors required to test the specific
            // high level behavior (i.e. of you class) required.
            this.mockExperiment = this.mockFixture.Create<ExperimentInstance>();

            // Ensure agent steps are in a valid state and have proper sequences for the
            // default code path.
            this.mockExperimentSteps = this.CreateAgentSteps(
                ExecutionStatus.Pending,
                ExecutionStatus.InProgressContinue);
        }

        [Test]
        [TestCase(null)]
        public void AgentExecutionManagerConstructorValidatesParameters(object invalidParam)
        {
            // Required Parameter: Services
            Assert.Throws<ArgumentException>(() => new AgentExecutionManager(
                invalidParam as IServiceCollection,
                It.IsAny<IConfiguration>(),
                It.IsAny<IAsyncPolicy>()));

            // Required Parameter: Configuration
            Assert.Throws<ArgumentException>(() => new AgentExecutionManager(
                It.IsAny<IServiceCollection>(),
                invalidParam as IConfiguration,
                It.IsAny<IAsyncPolicy>()));
        }

        [Test]
        public void AgentExecutionManagerProvidesTheExpectedServiceDependenciesToExperimentProviders()
        {
            Assert.IsNotNull(this.executionManager.Services);

            // Expected dependencies
            Assert.IsTrue(this.executionManager.Services.Count == 5);

            ILogger loggerDependency;
            IProviderDataClient dataClientDependency;
            IAzureKeyVault keyVaultClientDependency;
            AgentClient agentClientDependency;
            AgentIdentification agentIdDependency;
            Assert.IsTrue(this.executionManager.Services.TryGetService<ILogger>(out loggerDependency));
            Assert.IsTrue(this.executionManager.Services.TryGetService<IProviderDataClient>(out dataClientDependency));
            Assert.IsTrue(this.executionManager.Services.TryGetService<AgentClient>(out agentClientDependency));
            Assert.IsTrue(this.executionManager.Services.TryGetService<IAzureKeyVault>(out keyVaultClientDependency));
            Assert.IsTrue(this.executionManager.Services.TryGetService<AgentIdentification>(out agentIdDependency));
        }

        // Expected Workflow
        // 1. Get all steps for agents with status: InProgress, InProgressContinue or pending
        // 2. Select steps to execute.
        //     2.1 Get all steps which is inprogress or inprogresscontinue, and add them to selected steps collection
        //     2.2 Check if selected steps contains any step with status=inprogress, if no add the next pending step to collection based on sequence ranking.
        // 3  Execute all the selected states in parallel
        [Test]
        public void AgentExecutionManagerExecutionFollowsTheExpectedWorkflowAsync()
        {
            this.SetupRestClientMockBehaviors();

            this.executionManager.ExecuteAsync(CancellationToken.None)
                .GetAwaiter().GetResult();

            // 1) Get all steps for agents with status: InProgress, InProgressContinue or pending
            string encodedFilter = "(Status+eq+%27InProgress%27)+or+(Status+eq+%27InProgressContinue%27)+or+(Status+eq+%27Pending%27)";
            this.mockRestClient
                .Verify(client => client.GetAsync(
                    It.Is<Uri>(uri => uri.PathAndQuery.Equals($"/api/experiments/agent-steps?agentId={this.agentId}&filter={encodedFilter}")),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<HttpCompletionOption>()),
                    Times.Once);

            // 2. Get all agent steps for the current experiment (i.e. the latest experiment).
            this.mockRestClient
                .Verify(client => client.GetAsync(
                It.Is<Uri>(uri => uri.PathAndQuery.Equals($"/api/experiments/{this.mockExperiment.Id}")),
                It.IsAny<CancellationToken>(),
                It.IsAny<HttpCompletionOption>()),
                Times.Once);

            // 3  Execute all the selected states
            foreach (ExperimentStepInstance step in this.mockExperimentSteps)
            {
                this.mockRestClient
                    .Verify(client => client.PutAsync(
                        It.Is<Uri>(uri => uri.AbsoluteUri.Contains($"/api/experiments/agent-steps/{step.Id}")),
                        It.IsAny<HttpContent>(),
                        It.IsAny<CancellationToken>()),
                        Times.Once);
            }

            this.mockRestClient.VerifyNoOtherCalls();
        }

        [Test]
        [TestCase(ExecutionStatus.Pending)]
        [TestCase(ExecutionStatus.InProgress)]
        [TestCase(ExecutionStatus.InProgressContinue)]
        public void AgentExecutionManagerExecutesParallelAgentStepsConcurrently(ExecutionStatus status)
        {
            // Parallel Steps should execute concurrently
            this.mockExperimentSteps = this.CreateAgentSteps(
                status,
                status);

            // Set the sequence to 100 after the parent step sequence
            int expectedSequence = this.exampleParentStep.Sequence + 100;

            // assign this value to all steps for parallel execution
            this.mockExperimentSteps.ForEach(step => step.Sequence = expectedSequence);

            // Execute the Agent Steps specified above
            ConcurrentBag<ExperimentStepInstance> stepsExecuted = this.AgentExecutionManagerExecutesProvidedAgentSteps();

            Assert.AreEqual(this.mockExperimentSteps.Count, stepsExecuted.Count);
            // Validate parallel execution by the start time of the agent steps
            Assert.AreEqual(
                stepsExecuted.First().StartTime.ToString().TrimEnd(),
                stepsExecuted.Last().StartTime.ToString().TrimEnd());

            // Validate parallel execution by the start time of the agent steps
            Assert.AreEqual(
                stepsExecuted.First().EndTime.ToString().TrimEnd(),
                stepsExecuted.Last().EndTime.ToString().TrimEnd());
            CollectionAssert.AreEquivalent(this.mockExperimentSteps.Select(step => step.Id), stepsExecuted.Select(step => step.Id));
        }

        [Test]
        [TestCase(ExecutionStatus.InProgress, ExecutionStatus.InProgress)]
        [TestCase(ExecutionStatus.InProgressContinue, ExecutionStatus.InProgressContinue)]
        [TestCase(ExecutionStatus.InProgress, ExecutionStatus.InProgressContinue)]
        [TestCase(ExecutionStatus.InProgressContinue, ExecutionStatus.InProgress)]
        public void AgentExecutionManagerExecutesReentrantAgentSteps(ExecutionStatus reentrantStepA, ExecutionStatus reentrantStepB)
        {
            // Reentrant steps are those that are in a status of InProgress or InProgressContinue
            this.mockExperimentSteps = this.CreateAgentSteps(
                reentrantStepA,
                reentrantStepB);

            // Execute the Agent Steps specified above
            ConcurrentBag<ExperimentStepInstance> stepsExecuted = this.AgentExecutionManagerExecutesProvidedAgentSteps();

            // Validate that all reentrant agent steps execute successfully
            Assert.IsTrue(stepsExecuted.All(step => step.Status == ExecutionStatus.Succeeded));
            Assert.AreEqual(this.mockExperimentSteps.Count, stepsExecuted.Count);
            CollectionAssert.AreEquivalent(
                this.mockExperimentSteps.Select(step => step.Id),
                stepsExecuted.Select(step => step.Id));
        }

        [Test]
        public void AgentExecutionManagerExecutesInProgressContinueAgentStepsAlongWithPending()
        {
            // InProgressContinue and Pending steps can be executed together
            this.mockExperimentSteps = this.CreateAgentSteps(
                ExecutionStatus.InProgressContinue,
                ExecutionStatus.Pending);

            // Execute the steps specified above
            ConcurrentBag<ExperimentStepInstance> stepsExecuted = this.AgentExecutionManagerExecutesProvidedAgentSteps();

            // Validate that the steps executed successfully
            Assert.IsTrue(stepsExecuted.All(step => step.Status == ExecutionStatus.Succeeded));
            Assert.AreEqual(this.mockExperimentSteps.Count, stepsExecuted.Count);
            CollectionAssert.AreEquivalent(
                this.mockExperimentSteps.Select(step => step.Id),
                stepsExecuted.Select(step => step.Id));
        }

        [Test]
        [TestCase(ExecutionStatus.InProgress, ExecutionStatus.InProgressContinue)]
        [TestCase(ExecutionStatus.InProgressContinue, ExecutionStatus.InProgress)]
        public void AgentExecutionManagerDoesNotExecuteAnyPendingAgentStepsWhenOneOrMoreAgentStepsAreStatusInProgress(ExecutionStatus reentrantStepA, ExecutionStatus reentrantStepB)
        {
            // Agent Execution Manager will not execute Pending steps if any step has an InProgress status
            this.mockExperimentSteps = this.CreateAgentSteps(
                reentrantStepA,
                reentrantStepB,
                ExecutionStatus.Pending);

            // Execute the Agent Steps specified above
            ConcurrentBag<ExperimentStepInstance> stepsExecuted = this.AgentExecutionManagerExecutesProvidedAgentSteps();

            // Verify that the Pending Agent Step did not execute with the InProgress step
            Assert.AreNotEqual(this.mockExperimentSteps.Count, stepsExecuted.Count);
            CollectionAssert.AreEquivalent(
                this.mockExperimentSteps
                    .Where(step => step.Status != ExecutionStatus.Pending)
                    .Select(step => step.Id),
                stepsExecuted.Select(step => step.Id));
        }

        [Test]
        public void AgentExecutionManagerExecutesExpectedStepsToStartAnExperiment()
        {
            // When all steps are in status 'Pending', we expect to execute the very first
            // one (by sequence).
            this.mockExperimentSteps = this.CreateAgentSteps(
                ExecutionStatus.Pending,
                ExecutionStatus.Pending);

            // Execute the Agent Steps specified above
            ConcurrentBag<ExperimentStepInstance> stepsExecuted = this.AgentExecutionManagerExecutesProvidedAgentSteps();

            // The Execution Manager will only execute the first pending step in sequence
            Assert.AreNotEqual(this.mockExperimentSteps.Count, stepsExecuted.Count);
        }

        [Test]
        [TestCase(ExecutionStatus.Cancelled)]
        [TestCase(ExecutionStatus.Failed)]
        [TestCase(ExecutionStatus.Succeeded)]
        public void AgentExecutionManagerWillNotExecuteTerminalAgentSteps(ExecutionStatus terminalStatus)
        {
            // Terminal statuses (Cancelled, Failed, Succeeded) are not queued for execution
            this.mockExperimentSteps = this.CreateAgentSteps(
                terminalStatus,
                terminalStatus);

            // Execute the Agent Steps specified above
            ConcurrentBag<ExperimentStepInstance> stepsExecuted = this.AgentExecutionManagerExecutesProvidedAgentSteps();

            // Validate that the Agent Steps with terminal statuses were not executed
            Assert.IsEmpty(stepsExecuted);
        }

        [Test]
        [TestCase(ExecutionStatus.Cancelled)]
        [TestCase(ExecutionStatus.Failed)]
        public void AgentExecutionManagerWillNotExecutePendingAgentStepsWithTerminalAgentSteps(ExecutionStatus terminalStatus)
        {
            // Terminal statuses (Cancelled, Failed) will terminate the experiment
            this.mockExperimentSteps = this.CreateAgentSteps(
                terminalStatus,
                ExecutionStatus.Pending);

            // Execute the Agent Steps specified above
            ConcurrentBag<ExperimentStepInstance> stepsExecuted = this.AgentExecutionManagerExecutesProvidedAgentSteps();

            // No pending steps should be executed if the experiment has been terminated
            Assert.IsEmpty(stepsExecuted);
        }

        private static HttpResponseMessage CreateResponseMessage(HttpStatusCode expectedStatusCode, object expectedContent = null)
        {
            HttpResponseMessage mockResponse = new HttpResponseMessage(expectedStatusCode)
            {
                Content = new StringContent(expectedContent.ToJson())
            };

            return mockResponse;
        }

        private ConcurrentBag<ExperimentStepInstance> AgentExecutionManagerExecutesProvidedAgentSteps()
        {
            // Set up the mock
            this.SetupRestClientMockBehaviors();

            // We can verify the steps that are executed by the fact that they are updated after
            // execution.
            ConcurrentBag<ExperimentStepInstance> stepsExecuted = new ConcurrentBag<ExperimentStepInstance>();
            foreach (ExperimentStepInstance step in this.mockExperimentSteps)
            {
                this.mockRestClient
                    .Setup(client => client.PutAsync(
                        It.Is<Uri>(uri => uri.AbsoluteUri.Contains($"/api/experiments/agent-steps/{step.Id}")),
                        It.IsAny<HttpContent>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<Uri, HttpContent, CancellationToken>((uri, content, token) =>
                    {
                        ExperimentStepInstance stepExecuted = content.ReadAsStringAsync()
                            .GetAwaiter().GetResult().FromJson<ExperimentStepInstance>();

                        stepsExecuted.Add(stepExecuted);
                    });
            }

            this.executionManager.ExecuteAsync(CancellationToken.None)
                .GetAwaiter().GetResult();

            // returns the steps executed
            return stepsExecuted;
        }

        private List<ExperimentStepInstance> CreateAgentSteps(params ExecutionStatus[] orderedStatuses)
        {
            List<ExperimentComponent> components = new List<ExperimentComponent>();
            foreach (ExecutionStatus status in orderedStatuses)
            {
                components.Add(FixtureExtensions.CreateExperimentComponent(typeof(TestWorkloadExecutesOnVm)));
            }

            int expectedSequence = this.exampleParentStep.Sequence;
            List<ExperimentStepInstance> steps = this.stepFactory.CreateAgentSteps(
                components,
                this.agentId.ToString(),
                this.exampleParentStep.Id,
                this.mockExperiment.Id,
                expectedSequence += 100) as List<ExperimentStepInstance>;

            for (int i = 0; i < orderedStatuses.Length; i++)
            {
                steps.ElementAt(i).Status = orderedStatuses.ElementAt(i);
            }

            return steps;
        }

        /// <summary>
        /// 1. Get all steps for agents with status: InProgress, InProgressContinue or pending
        /// 2. Select steps to execute.
        ///     2.1 Get all steps which is inprogress or inprogresscontinue, and add them to selected steps collection
        ///     2.2 Check if selected steps contains any step with status=inprogress, if no add the next pending step to collection based on sequence ranking.
        /// 3  Execute all the selected states in parallel
        /// </summary>
        private void SetupRestClientMockBehaviors()
        {
            HttpResponseMessage getAgentExperimentResponse = AgentExecutionManagerTests.CreateResponseMessage(HttpStatusCode.OK, this.mockExperiment);
            HttpResponseMessage getStepsResponse = AgentExecutionManagerTests.CreateResponseMessage(HttpStatusCode.OK, this.mockExperimentSteps);
            HttpResponseMessage updatedStepsResponse = AgentExecutionManagerTests.CreateResponseMessage(HttpStatusCode.OK, this.mockExperimentSteps);

            //// 1. Get all steps for agents with status: InProgress, InProgressContinue or pending
            this.mockRestClient
                .Setup(client => client.GetAsync(
                   It.Is<Uri>(uri => uri.PathAndQuery.Contains("/api/experiments/agent")),
                   It.IsAny<CancellationToken>(),
                   It.IsAny<HttpCompletionOption>()))
               .ReturnsAsync(getStepsResponse);

            // 2) Get experiment related to the agent and steps.
            this.mockRestClient
                .Setup(client => client.GetAsync(
                    It.Is<Uri>(uri => uri.PathAndQuery.Equals($"/api/experiments/{this.mockExperiment.Id}")),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<HttpCompletionOption>()))
                .ReturnsAsync(getAgentExperimentResponse);

            // 3) Get the next steps in-sequence to execute and process/execute the next steps.
            int currentStepNumber = 0;
            this.mockRestClient
                .Setup(client => client.PutAsync(
                    It.Is<Uri>(uri => uri.AbsoluteUri.Contains($"/api/experiments/agent-steps/{this.mockExperimentSteps[currentStepNumber].Id}")),
                    It.IsAny<HttpContent>(),
                    It.IsAny<CancellationToken>()))
                .Callback<Uri, HttpContent, CancellationToken>((uri, content, token) => currentStepNumber++) // Step number must be incremented in the callback
                .Returns(Task.Run(() =>
                {
                    // We want to imitate the behavior where the step is updated.
                    return AgentExecutionManagerTests.CreateResponseMessage(HttpStatusCode.OK, this.mockExperimentSteps[currentStepNumber]);
                }));
        }
    }

    public class TestProvider : ExperimentProvider
    {
        public TestProvider(IServiceCollection services)
            : base(services)
        {
        }

        protected override Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            return Task.FromResult(new ExecutionResult(ExecutionStatus.Succeeded));
        }
    }

    [ExecutionConstraints(SupportedStepType.Workload, SupportedStepTarget.ExecuteOnVirtualMachine)]
    public class TestWorkloadExecutesOnVm : TestProvider
    {
        public TestWorkloadExecutesOnVm(IServiceCollection services)
            : base(services)
        {
        }
    }
}