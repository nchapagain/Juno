namespace Juno.Scheduler.Actions
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using AutoFixture;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Juno.DataManagement;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Repository;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using Newtonsoft.Json;
    using NuGet.Protocol;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class CreateExperimentProviderTests
    {
        private static string executionGoalName = "executionGoalName";
        private static string experimentTemplateFile = "experimentTemplateFile";
        private static string workQueueName = "workQueue";

        private CancellationToken cancellationToken = CancellationToken.None;
        private Fixture mockFixtureGBS;
        private IServiceCollection services;

        private Mock<IScheduleTimerDataManager> timerDataManager;
        private Mock<IExperimentTemplateDataManager> experimentTemplateDataManager;
        private Mock<IExperimentClient> experimentClient;

        private TargetGoalTrigger gbScheduleTrigger;
        private ExperimentItem gbsExperimentItem;
        private ScheduleContext mockContext;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixtureGBS = new Fixture();

            this.mockFixtureGBS.SetUpGoalBasedScheduleMocks();
            this.mockFixtureGBS.SetupExperimentMocks();

            this.timerDataManager = new Mock<IScheduleTimerDataManager>();
            this.experimentTemplateDataManager = new Mock<IExperimentTemplateDataManager>();
            this.experimentClient = new Mock<IExperimentClient>();

            this.services = new ServiceCollection();
            this.services.AddSingleton<IScheduleTimerDataManager>(this.timerDataManager.Object);
            this.services.AddSingleton<IExperimentTemplateDataManager>(this.experimentTemplateDataManager.Object);
            this.services.AddSingleton<IExperimentClient>(this.experimentClient.Object);

            this.gbScheduleTrigger = this.mockFixtureGBS.Create<TargetGoalTrigger>();
            this.gbsExperimentItem = this.mockFixtureGBS.Create<ExperimentItem>();

            this.mockContext = new ScheduleContext(this.mockFixtureGBS.Create<GoalBasedSchedule>(), this.mockFixtureGBS.Create<TargetGoalTrigger>(), new Mock<IConfiguration>().Object);
        }

        [Test]
        public void ProviderReturnsTheExpectedResponseWhenAnExperimentIsSuccessfullyCreated()
        {
            string executionGoalName = "TargetGoal";
            string experimentTemplateFile = "Any_Experiment_Template.json";
            string workQueue = "mockQueue";
            ScheduleAction component = this.mockFixtureGBS.Create<ScheduleAction>();
            component.Parameters.Add(CreateExperimentProviderTests.executionGoalName, executionGoalName);
            component.Parameters.Add(CreateExperimentProviderTests.experimentTemplateFile, experimentTemplateFile);
            component.Parameters.Add(CreateExperimentProviderTests.workQueueName, workQueue);

            this.experimentClient.Setup(x => x.CreateExperimentFromTemplateAsync(It.IsAny<ExperimentTemplate>(), this.cancellationToken, It.IsAny<string>()))
                .ReturnsAsync(this.GetHttpResponse(HttpStatusCode.OK))
                .Callback<ExperimentTemplate, CancellationToken, string>((experimentTemplate, token, testWorkQueue) =>
                {
                    var overridePram = new TemplateOverride(component.Parameters).ToJson();
                    this.mockContext.ExecutionGoal.Experiment.Metadata.Add("targetGoal", "TargetGoal");
                    this.mockContext.ExecutionGoal.Experiment.Metadata.Add("executionGoal", "ExecutionGoal");
                    Assert.AreEqual(experimentTemplate.Experiment, this.mockContext.ExecutionGoal.Experiment);
                    Assert.AreEqual(overridePram, experimentTemplate.Override);
                });

            ScheduleActionProvider provider = new CreateExperimentProvider(this.services);
            provider.ConfigureServicesAsync(component, this.mockContext);
            var response = provider.ExecuteActionAsync(component, this.mockContext, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(ExecutionStatus.Succeeded, response.Status);
        }

        [Test]
        public void ProviderReturnsTheExpectedResponseWhenWorkQueueIsProvided()
        {
            string executionGoalName = "TargetGoal";
            string experimentTemplateFile = "Any_Experiment_Template.json";
            string workQueue = "mockQueue";
            ScheduleAction component = this.mockFixtureGBS.Create<ScheduleAction>();
            component.Parameters.Add(CreateExperimentProviderTests.executionGoalName, executionGoalName);
            component.Parameters.Add(CreateExperimentProviderTests.experimentTemplateFile, experimentTemplateFile);
            component.Parameters.Add(CreateExperimentProviderTests.workQueueName, workQueue);

            this.experimentTemplateDataManager.Setup(x => x.GetExperimentTemplateAsync(It.IsAny<string>(), It.IsAny<string>(), this.cancellationToken, It.IsAny<string>()))
                .ReturnsAsync(this.gbsExperimentItem);

            ExperimentTemplate template = new ExperimentTemplate()
            {
                Experiment = this.gbsExperimentItem.Definition,
                Override = new TemplateOverride(component.Parameters).ToJson()
            };

            this.experimentClient.Setup(x => x.CreateExperimentFromTemplateAsync(It.IsAny<ExperimentTemplate>(), this.cancellationToken, It.IsAny<string>()))
                .ReturnsAsync(this.GetHttpResponse(HttpStatusCode.OK))
                .Callback<ExperimentTemplate, CancellationToken, string>((experimentTemplate, token, workQueueString) =>
                {
                    Assert.AreEqual(workQueue, workQueueString);
                });

            ScheduleActionProvider provider = new CreateExperimentProvider(this.services);
            provider.ConfigureServicesAsync(component, this.mockContext);
            var response = provider.ExecuteActionAsync(component, this.mockContext, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(ExecutionStatus.Succeeded, response.Status);
        }

        [Test]
        public void ProviderReturnsTheExpectedResponseWhenAnExperimentCreationRequestFails()
        {
            string executionGoalName = "ScheduleName";
            ScheduleAction component = this.mockFixtureGBS.Create<ScheduleAction>();
            component.Parameters.Add(CreateExperimentProviderTests.executionGoalName, executionGoalName);

            // Creating cosmos table that gets ExecutionGoal Timer Trigger
            this.timerDataManager.Setup(x => x.GetTargetGoalTriggerAsync(It.IsAny<string>(), It.IsAny<string>(), this.cancellationToken)).ReturnsAsync(this.gbScheduleTrigger);

            this.experimentTemplateDataManager.Setup(x => x.GetExperimentTemplateAsync(It.IsAny<string>(), It.IsAny<string>(), this.cancellationToken, It.IsAny<string>()))
                .ReturnsAsync(this.gbsExperimentItem);

            this.experimentClient.Setup(x => x.CreateExperimentFromTemplateAsync(It.IsAny<ExperimentTemplate>(), this.cancellationToken, It.IsAny<string>()))
                .ReturnsAsync(this.GetHttpResponse(HttpStatusCode.BadRequest));

            ScheduleActionProvider provider = new CreateExperimentProvider(this.services);

            var response = provider.ExecuteActionAsync(component, this.mockContext, CancellationToken.None).GetAwaiter().GetResult();
            provider.ConfigureServicesAsync(component, this.mockContext);

            Assert.AreEqual(response.Status, ExecutionStatus.Failed);
        }

        [Test]
        public void ProviderReturnsExpectedResponseIfAnExceptionIsThrownWhenGettingAnExecutionGoalTimerTrigger()
        {
            string executionGoalName = "ScheduleName";
            ScheduleAction component = this.mockFixtureGBS.Create<ScheduleAction>();
            component.Parameters.Add(CreateExperimentProviderTests.executionGoalName, executionGoalName);

            this.timerDataManager.Setup(x => x.GetTargetGoalTriggerAsync(It.IsAny<string>(), It.IsAny<string>(), this.cancellationToken)).ThrowsAsync(new DataStoreException("Moq failure"));
            ScheduleActionProvider provider = new CreateExperimentProvider(this.services);

            var response = provider.ExecuteActionAsync(component, this.mockContext, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(response.Status, ExecutionStatus.Failed);
        }

        [Test]
        public void ProviderReturnsFailedExecutionResultStatusIfExceptionIsThrownAtGetExperimentTemplateDefinitionAsync()
        {
            string executionGoalName = "ScheduleName";
            ScheduleAction component = this.mockFixtureGBS.Create<ScheduleAction>();
            component.Parameters.Add(CreateExperimentProviderTests.executionGoalName, executionGoalName);

            this.timerDataManager.Setup(x => x.GetTargetGoalTriggerAsync(It.IsAny<string>(), It.IsAny<string>(), this.cancellationToken)).ReturnsAsync(this.gbScheduleTrigger);
            ScheduleActionProvider provider = new CreateExperimentProvider(this.services);

            this.experimentTemplateDataManager.Setup(x => x.GetExperimentTemplateAsync(It.IsAny<string>(), It.IsAny<string>(), this.cancellationToken, It.IsAny<string>())).
                ThrowsAsync(new ArgumentException("Moq failure"));

            var response = provider.ExecuteActionAsync(component, this.mockContext, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(response.Status, ExecutionStatus.Failed);
        }

        [Test]
        public void ProviderValidatesParameters()
        {
            ScheduleActionProvider provider = new CreateExperimentProvider(this.services);
            Assert.Throws<ArgumentException>(() => provider.ExecuteActionAsync(null, this.mockContext, CancellationToken.None).GetAwaiter().GetResult());
        }

        [Test]
        public void ProviderValidatesRequiredServiceDependenciesAreProvided()
        {
            IServiceCollection serviceToValidate = new ServiceCollection();
            ScheduleActionProvider provider = new CreateExperimentProvider(serviceToValidate);

            string executionGoalName = "ScheduleName";
            ScheduleAction component = this.mockFixtureGBS.Create<ScheduleAction>();
            component.Parameters.Add(CreateExperimentProviderTests.executionGoalName, executionGoalName);

            var response = provider.ExecuteActionAsync(component, this.mockContext, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(response.Status, ExecutionStatus.Failed);
        }

        public HttpResponseMessage GetHttpResponse(HttpStatusCode statusCode)
        {
            return new HttpResponseMessage { StatusCode = statusCode, Content = new StringContent(JsonConvert.SerializeObject(this.gbsExperimentItem)) };
        }
    }
}
