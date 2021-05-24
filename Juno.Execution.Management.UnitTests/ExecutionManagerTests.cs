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
    using Juno;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Juno.Providers;
    using Juno.Providers.Certification;
    using Juno.Providers.Diagnostics;
    using Juno.Providers.Environment;
    using Juno.Providers.Payloads;
    using Juno.Providers.Workloads;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Repository.KeyVault;
    using Microsoft.Azure.CRC.Rest;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;
    using Polly;

    [TestFixture]
    [Category("Unit")]
    public class ExecutionManagerTests
    {
        private Fixture mockFixture;
        private FixtureDependencies mockDependencies;
        private TestExecutionManager executionManager;
        private Mock<IRestClient> mockRestClient;
        private Mock<IProviderDataClient> mockProviderDataClient;
        private Mock<IExperimentNoticeManager> mockNoticeManager;

        // The following mock objects are used in the default setup of the mock REST client. By defining
        // them as simple variables, it makes them easy to change before setting up the mock REST client.
        // And that makes it easy to make small changes to the behaviors of small parts of the larger workflow
        // in order to validate a specific behavior of the experiment manager.
        private ExperimentInstance mockExperiment;

        private ExperimentMetadataInstance mockNotice;
        private List<ExperimentStepInstance> mockExperimentSteps;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks();
            this.mockDependencies = new FixtureDependencies();
            this.mockRestClient = new Mock<IRestClient>();
            this.mockProviderDataClient = new Mock<IProviderDataClient>();
            this.mockNoticeManager = new Mock<IExperimentNoticeManager>();

            ExecutionClient apiClient = new ExecutionClient(
                this.mockRestClient.Object,
                new Uri("https://anyjunoenvironment.execution"),
                Policy.NoOpAsync());

            IServiceCollection services = new ServiceCollection()
                .AddSingleton<ILogger>(NullLogger.Instance)
                .AddSingleton<ExecutionClient>(apiClient)
                .AddSingleton<IProviderDataClient>(this.mockProviderDataClient.Object)
                .AddSingleton<IAzureKeyVault>(this.mockDependencies.KeyVault.Object)
                .AddSingleton<IExperimentNoticeManager>(this.mockNoticeManager.Object);

            this.executionManager = new TestExecutionManager(services, this.mockDependencies.Configuration, Policy.NoOpAsync());

            // General Mock Pattern
            // When there are a lot of dependency interactions in a given method/workflow, it is often
            // helpful to follow the "happy path" mock setup strategy.  This involves setting up ALL mock
            // dependencies to a singular, known good behavior (e.g. the happy path). Then in each individual
            // unit test method, you change ONLY the one or two dependency behaviors required to test the specific
            // high level behavior (i.e. of you class) required.
            this.mockExperiment = this.mockFixture.Create<ExperimentInstance>();
            this.mockNotice = this.mockFixture.Create<ExperimentMetadataInstance>();

            // Ensure all steps are in a Pending state and have proper sequences for the
            // default code path.
            this.mockExperimentSteps = this.CreateSteps(
                ExecutionStatus.Pending,
                ExecutionStatus.Pending);
        }

        [Test]
        public void ExecutionManagerProvidesTheExpectedServiceDependenciesToExperimentProviders()
        {
            Assert.IsNotNull(this.executionManager.Services);

            // Expected dependencies
            Assert.IsTrue(this.executionManager.Services.Count >= 4);

            ILogger loggerDependency;
            IAzureKeyVault keyVaultClientDependency;
            IProviderDataClient dataClientDependency;
            ExecutionClient executionClientDependency;
            Assert.IsTrue(this.executionManager.Services.TryGetService<ILogger>(out loggerDependency));
            Assert.IsTrue(this.executionManager.Services.TryGetService<IAzureKeyVault>(out keyVaultClientDependency));
            Assert.IsTrue(this.executionManager.Services.TryGetService<IProviderDataClient>(out dataClientDependency));
            Assert.IsTrue(this.executionManager.Services.TryGetService<ExecutionClient>(out executionClientDependency));
        }

        [Test]
        public void ExecutionManagerExecutionFollowsTheExpectedWorkflow()
        {
            // Expected Workflow
            // 1) Get/check notification of work.
            // 2) If a notification exists then continue.
            // 3) If the notice is flagged for audit, then perform an audit (e.g. check for duplicates).
            // 4) Get the experiment instance itself noted in the notification.
            // 5) Get the next experiment steps slated for execution (note: the default behavior here is
            //    the state of the steps at the very beginning of the experiment execution where all steps
            //    are in a 'Pending' state.
            // 6) Execute each step and update the step (e.g. status) after execution.
            // 7) Update the experiment instance (e.g. status) after all steps are executed.
            // 8a) If experiment is not completed, then set the notice visible on the work queue.
            // 8b) If the experiment is completed, then delete the notice from the work queue.

            this.SetupDefaultMockBehaviors();

            this.executionManager.ExecuteAsync(CancellationToken.None)
                .GetAwaiter().GetResult();

            // Get notice
            this.mockNoticeManager.Verify(mgr => mgr.GetWorkNoticeAsync(It.IsAny<CancellationToken>()), Times.Once);

            // Get experiment
            this.mockRestClient.Verify(
                client => client.GetAsync(
                     It.Is<Uri>(uri => uri.AbsolutePath.Equals($"/api/experiments/{this.mockExperiment.Id}")),
                     It.IsAny<CancellationToken>(),
                     It.IsAny<HttpCompletionOption>()),
                Times.Once);

            // update experiment (for status change)
            this.mockRestClient.Verify(
                client => client.PutAsync(
                    It.Is<Uri>(uri => uri.AbsolutePath.Equals($"/api/experiments/{this.mockExperiment.Id}")),
                    It.IsAny<HttpContent>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            // Get steps
            this.mockRestClient.Verify(
                client => client.GetAsync(
                     It.Is<Uri>(uri => uri.AbsolutePath.Contains($"/api/experiments/{this.mockExperiment.Id}/steps")),
                     It.IsAny<CancellationToken>(),
                     It.IsAny<HttpCompletionOption>()),
                Times.Once);

            // Update steps
            // The default returns steps that are all Pending. The manager will execute only one of those so we expect
            // only one POST.
            this.mockRestClient.Verify(
                client => client.PutAsync(
                    It.Is<Uri>(uri => uri.AbsolutePath.Contains($"/api/experiments/{this.mockExperiment.Id}/steps")),
                    It.IsAny<HttpContent>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            // Update experiment notice to be visible again
            this.mockNoticeManager.Verify(
                mgr => mgr.SetWorkNoticeVisibilityAsync(It.IsAny<ExperimentMetadataInstance>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
                Times.Once);

            this.mockRestClient.VerifyNoOtherCalls();
        }

        [Test]
        [TestCase(ExecutionStatus.Pending)]
        [TestCase(ExecutionStatus.InProgress)]
        [TestCase(ExecutionStatus.InProgressContinue)]
        public void ExecutionManagerExecutesParallelAgentStepsConcurrently(ExecutionStatus status)
        {
            // Parallel Steps should execute concurrently
            this.mockExperimentSteps = this.CreateSteps(
                status,
                status);

            // set each sequence to the same value for parallel execution
            this.mockExperimentSteps.ForEach(step => step.Sequence = 100);

            // Execute the Agent Steps specified above
            ConcurrentBag<ExperimentStepInstance> stepsExecuted = this.ExecutionManagerExecutesProvidedSteps();

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
        [TestCase(ExecutionStatus.Cancelled)]
        [TestCase(ExecutionStatus.Failed)]
        [TestCase(ExecutionStatus.Succeeded)]
        public void ExecutionManagerWillNotExecuteTerminalAgentSteps(ExecutionStatus terminalStatus)
        {
            // Terminal statuses (Cancelled, Failed, Succeeded) are not queued for execution
            this.mockExperimentSteps = this.CreateSteps(
                terminalStatus,
                terminalStatus);

            // Execute the steps specified above
            ConcurrentBag<ExperimentStepInstance> stepsExecuted = this.ExecutionManagerExecutesProvidedSteps();

            // Validate that the steps with terminal statuses were not executed
            Assert.IsEmpty(stepsExecuted);
        }

        [Test]
        public void ExecutionManagerExecutesExpectedStepsToStartAnExperiment()
        {
            // When all steps are in status 'Pending', we expect to execute the very first
            // one (by sequence).
            this.mockExperimentSteps = this.CreateSteps(
                ExecutionStatus.Pending,
                ExecutionStatus.Pending);

            // Execute the steps specified above
            ConcurrentBag<ExperimentStepInstance> stepsExecuted = this.ExecutionManagerExecutesProvidedSteps();

            // The Execution Manager will only execute the first pending step in sequence
            Assert.AreEqual(this.mockExperimentSteps.ElementAt(0).Id, stepsExecuted.ElementAt(0).Id);
            Assert.IsTrue(stepsExecuted.All(step => step.Status == ExecutionStatus.Succeeded));
            Assert.AreNotEqual(this.mockExperimentSteps.Count, stepsExecuted.Count);
        }

        [Test]
        public void ExecutionManagerExecutesInProgressContinueStepsAlongWithPending()
        {
            // InProgressContinue and Pending steps can be executed together
            this.mockExperimentSteps = this.CreateSteps(
                ExecutionStatus.InProgressContinue,
                ExecutionStatus.Pending);

            // Execute the steps specified above
            ConcurrentBag<ExperimentStepInstance> stepsExecuted = this.ExecutionManagerExecutesProvidedSteps();

            // Validate that the steps executed successfully
            Assert.IsTrue(stepsExecuted.All(step => step.Status == ExecutionStatus.Succeeded));
            Assert.AreEqual(this.mockExperimentSteps.Count, stepsExecuted.Count);
            CollectionAssert.AreEquivalent(
                this.mockExperimentSteps.Select(step => step.Id),
                stepsExecuted.Select(step => step.Id));
        }

        [Test]
        [TestCase(ExecutionStatus.InProgress, ExecutionStatus.InProgress)]
        [TestCase(ExecutionStatus.InProgressContinue, ExecutionStatus.InProgressContinue)]
        [TestCase(ExecutionStatus.InProgress, ExecutionStatus.InProgressContinue)]
        [TestCase(ExecutionStatus.InProgressContinue, ExecutionStatus.InProgress)]
        public void ExecutionManagerExecutesReentrantStepsEveryTime(ExecutionStatus reentrantStep1, ExecutionStatus reentrantStep2)
        {
            // Reentrant steps are those that are in a status of InProgress or InProgressContinue
            // The steps are created with statuses that match the order provided below.
            this.mockExperimentSteps = this.CreateSteps(
                reentrantStep1,
                reentrantStep2);

            // Execute the steps specified above
            ConcurrentBag<ExperimentStepInstance> stepsExecuted = this.ExecutionManagerExecutesProvidedSteps();

            // Validate that all reentrant steps execute successfully
            Assert.IsTrue(stepsExecuted.All(step => step.Status == ExecutionStatus.Succeeded));
            Assert.AreEqual(this.mockExperimentSteps.Count, stepsExecuted.Count);
            CollectionAssert.AreEquivalent(
                this.mockExperimentSteps.Select(step => step.Id),
                stepsExecuted.Select(step => step.Id));
        }

        [Test]
        [TestCase(ExecutionStatus.InProgress, ExecutionStatus.InProgressContinue)]
        [TestCase(ExecutionStatus.InProgressContinue, ExecutionStatus.InProgress)]
        public void ExecutionManagerDoesNotExecuteAnyPendingStepsWhenAStepIsInProgress(ExecutionStatus reentrantStepA, ExecutionStatus reentrantStepB)
        {
            // Execution Manager will not execute Pending steps if any step has an InProgress status
            this.mockExperimentSteps = this.CreateSteps(
                reentrantStepA,
                reentrantStepB,
                ExecutionStatus.Pending);

            // Execute the steps specified above
            ConcurrentBag<ExperimentStepInstance> stepsExecuted = this.ExecutionManagerExecutesProvidedSteps();

            // Verify that the Pending step did not execute with the InProgress step
            Assert.AreNotEqual(this.mockExperimentSteps.Count, stepsExecuted.Count);
            CollectionAssert.AreEquivalent(
                this.mockExperimentSteps
                    .Where(step => step.Status != ExecutionStatus.Pending)
                    .Select(step => step.Id),
                stepsExecuted.Select(step => step.Id));
        }

        [Test]
        [TestCase(ExecutionStatus.InProgress)]
        [TestCase(ExecutionStatus.InProgressContinue)]
        [TestCase(ExecutionStatus.Pending)]
        public void ExecutionManagerCancelsExpectedStepsWhenTheExperimentIsFailedAndThereAreNonCompletedStepsRemaining(ExecutionStatus nonCompletedStatus)
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Failed, typeof(ExamplePayloadProvider)),
              new KeyValuePair<ExecutionStatus, Type>(nonCompletedStatus, typeof(ExampleWorkloadProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleDiagnosticsProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCertificationProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Succeeded, typeof(ExampleCleanupProvider))
            };

            this.mockExperimentSteps = this.CreateSteps(steps);

            IEnumerable<ExperimentStepInstance> stepsUpdated = this.ExecutionManagerExecutesProvidedStepsAndUpdatesExperiment();

            Assert.IsTrue(stepsUpdated.Count(step => step.Status == ExecutionStatus.SystemCancelled) == 1);
            Assert.AreEqual(this.mockExperimentSteps.ElementAt(2).Id, stepsUpdated.First().Id);
            Assert.IsTrue(stepsUpdated.All(step => step.Status == ExecutionStatus.SystemCancelled));
        }

        [Test]
        public void ExecutionManagerAppliesTheExpectedDefaultVisibilityTimeoutForNoticesOnTheQueue()
        {
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.InProgress, typeof(TestProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.InProgress, typeof(TestProvider))
            };

            this.mockExperimentSteps = this.CreateSteps(steps);
            this.ExecutionManagerExecutesProvidedSteps();

            this.mockNoticeManager.Verify(mgr => mgr.SetWorkNoticeVisibilityAsync(
                It.IsAny<ExperimentMetadataInstance>(),
                TimeSpan.FromSeconds(1),
                It.IsAny<CancellationToken>()));
        }

        [Test]
        public void ExecutionManagerAppliesTheExpectedProviderRequestedVisibilityTimeoutForNoticesOnTheQueue()
        {
            TestProvider.VisibilityTimeout = TimeSpan.FromSeconds(45);
            var steps = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.InProgress, typeof(TestProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.InProgress, typeof(TestProvider))
            };

            this.mockExperimentSteps = this.CreateSteps(steps);
            this.ExecutionManagerExecutesProvidedSteps();

            this.mockNoticeManager.Verify(mgr => mgr.SetWorkNoticeVisibilityAsync(
                It.IsAny<ExperimentMetadataInstance>(),
                TimeSpan.FromSeconds(45),
                It.IsAny<CancellationToken>()));
        }

        private static HttpResponseMessage CreateResponseMessage(HttpStatusCode expectedStatusCode, object expectedContent)
        {
            HttpResponseMessage mockResponse = new HttpResponseMessage(expectedStatusCode)
            {
                Content = new StringContent(expectedContent.ToJson())
            };

            return mockResponse;
        }

        private List<ExperimentStepInstance> CreateSteps(params ExecutionStatus[] orderedStatuses)
        {
            int sequence = 0;
            List<ExperimentStepInstance> steps = new List<ExperimentStepInstance>();
            foreach (ExecutionStatus status in orderedStatuses)
            {
                ExperimentStepInstance step = this.mockFixture.Create<ExperimentStepInstance>();
                step.Status = status;
                step.Sequence = sequence += 100;
                steps.Add(step);
            }

            return steps;
        }

        private List<ExperimentStepInstance> CreateSteps(List<KeyValuePair<ExecutionStatus, Type>> stepsInfo)
        {
            int sequence = 0;
            List<ExperimentStepInstance> steps = new List<ExperimentStepInstance>();
            foreach (var item in stepsInfo)
            {
                var component = new ExperimentComponent(item.Value.FullName, "Any Name", "Any Description", "Any Group");

                ExperimentStepInstance step = this.mockFixture.CreateExperimentStep(component);
                step.Status = item.Key;
                step.Sequence = sequence += 100;
                steps.Add(step);
            }

            return steps;
        }

        private ConcurrentBag<ExperimentStepInstance> ExecutionManagerExecutesProvidedStepsAndUpdatesExperiment()
        {
            this.SetupDefaultMockBehaviors();

            // We can verify the steps that are executed by the fact that they are updated after
            // execution.
            ConcurrentBag<ExperimentStepInstance> stepsExecuted = new ConcurrentBag<ExperimentStepInstance>();
            this.mockRestClient.Setup(client => client.PutAsync(
                    It.Is<Uri>(uri => uri.AbsoluteUri.Contains($"/api/experiments/{this.mockExperiment.Id}/steps")),
                    It.IsAny<HttpContent>(),
                    It.IsAny<CancellationToken>()))
                .Callback<Uri, HttpContent, CancellationToken>((uri, content, token) =>
                {
                    ExperimentStepInstance stepExecuted = content.ReadAsStringAsync()
                        .GetAwaiter().GetResult().FromJson<ExperimentStepInstance>();

                    stepsExecuted.Add(stepExecuted);
                })
                .Returns(Task.Run(() =>
                {
                    // We want to imitate the behavior where the step is updated.
                    if (!stepsExecuted?.Any() ?? false)
                    {
                        return ExecutionManagerTests.CreateResponseMessage(HttpStatusCode.OK, null);
                    }

                    return ExecutionManagerTests.CreateResponseMessage(HttpStatusCode.OK, stepsExecuted.Last());
                }));

            this.executionManager.ExecuteAsync(CancellationToken.None)
                .GetAwaiter().GetResult();

            // returns the list of steps that were executed
            return stepsExecuted;
        }

        private ConcurrentBag<ExperimentStepInstance> ExecutionManagerExecutesProvidedSteps()
        {
            this.SetupDefaultMockBehaviors();

            // We can verify the steps that are executed by the fact that they are updated after
            // execution.
            ConcurrentBag<ExperimentStepInstance> stepsExecuted = new ConcurrentBag<ExperimentStepInstance>();

            this.mockRestClient.Setup(client => client.PutAsync(
                    It.Is<Uri>(uri => uri.AbsoluteUri.Contains($"/api/experiments/{this.mockExperiment.Id}/steps")),
                    It.IsAny<HttpContent>(),
                    It.IsAny<CancellationToken>()))
                .Callback<Uri, HttpContent, CancellationToken>((uri, content, token) =>
                {
                    ExperimentStepInstance stepExecuted = content.ReadAsStringAsync()
                        .GetAwaiter().GetResult().FromJson<ExperimentStepInstance>();

                    stepsExecuted.Add(stepExecuted);
                });

            this.executionManager.ExecuteAsync(CancellationToken.None)
                .GetAwaiter().GetResult();

            // returns the list of steps that were executed
            return stepsExecuted;
        }

        private void SetupDefaultMockBehaviors()
        {
            HttpResponseMessage getExperimentResponse = this.mockFixture.CreateHttpResponse(HttpStatusCode.OK, this.mockExperiment);
            HttpResponseMessage getStepsResponse = this.mockFixture.CreateHttpResponse(HttpStatusCode.OK, this.mockExperimentSteps);

            // 1) Get/Peek notice
            this.mockNoticeManager.Setup(mgr => mgr.GetWorkNoticeAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(this.mockNotice);

            // 2) Get experiment
            this.mockRestClient.Setup(client => client.GetAsync(
                     It.Is<Uri>(uri => uri.AbsolutePath.Equals($"/api/experiments/{this.mockExperiment.Id}")),
                     It.IsAny<CancellationToken>(),
                     It.IsAny<HttpCompletionOption>()))
                .ReturnsAsync(getExperimentResponse);

            // 3) Get steps
            this.mockRestClient.Setup(client => client.GetAsync(
                     It.Is<Uri>(uri => uri.AbsolutePath.Contains($"/api/experiments/{this.mockExperiment.Id}/steps")),
                     It.IsAny<CancellationToken>(),
                     It.IsAny<HttpCompletionOption>()))
                .ReturnsAsync(getStepsResponse);

            // 4) Execute and Update steps
            // The default returns steps that are all Pending. The manager will execute only one of those so we expect
            // only one POST.
            int currentStepNumber = 0;
            this.mockRestClient.Setup(client => client.PutAsync(
                    It.Is<Uri>(uri => uri.AbsoluteUri.Contains($"/api/experiments/{this.mockExperiment.Id}/steps")),
                    It.IsAny<HttpContent>(),
                    It.IsAny<CancellationToken>()))
                .Callback<Uri, HttpContent, CancellationToken>((uri, content, token) => currentStepNumber++) // Step number must be incremented in the callback
                .Returns(Task.Run(() =>
                {
                    // We want to imitate the behavior where the step is updated.
                    return ExecutionManagerTests.CreateResponseMessage(HttpStatusCode.OK, this.mockExperimentSteps[currentStepNumber]);
                }));

            // 5) Update Experiment (status must be set to 'Pending' in the default workflow).
            ExperimentInstance updatedExperiment = new ExperimentInstance(
                       this.mockExperiment.Id,
                       this.mockExperiment.Definition);

            updatedExperiment.Status = ExperimentStatus.Pending;
            HttpResponseMessage updateExperimentResponse = ExecutionManagerTests.CreateResponseMessage(HttpStatusCode.OK, updatedExperiment);

            this.mockRestClient.Setup(client => client.PutAsync(
                    It.Is<Uri>(uri => uri.AbsolutePath.Equals($"/api/experiments/{this.mockExperiment.Id}")),
                    It.IsAny<HttpContent>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(updateExperimentResponse);

            // 6) change visibility if work is not done.
            this.mockNoticeManager.Setup(mgr => mgr.SetWorkNoticeVisibilityAsync(It.IsAny<ExperimentMetadataInstance>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // 7) Delete notice if work is done.
            this.mockNoticeManager.Setup(mgr => mgr.DeleteWorkNoticeAsync(It.IsAny<ExperimentMetadataInstance>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        /// <summary>
        /// Test class enables protected methods to be exposed to ease the task of unit testing
        /// individual parts of the execution workflow.
        /// </summary>
        private class TestExecutionManager : ExecutionManager
        {
            public TestExecutionManager(IServiceCollection services, IConfiguration configuration, IAsyncPolicy retryPolicy)
               : base(services, configuration, "anyWorkQueue", retryPolicy)
            {
            }
        }

        [ExecutionConstraints(SupportedStepType.EnvironmentSetup, SupportedStepTarget.ExecuteRemotely)]
        private class TestProvider : ExperimentProvider
        {
            public TestProvider(IServiceCollection services)
                : base(services)
            {
            }

            public static TimeSpan? VisibilityTimeout { get; set; }

            protected override Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
            {
                return Task.FromResult(new ExecutionResult(ExecutionStatus.Succeeded)
                {
                    Extension = TestProvider.VisibilityTimeout
                });
            }
        }
    }
}