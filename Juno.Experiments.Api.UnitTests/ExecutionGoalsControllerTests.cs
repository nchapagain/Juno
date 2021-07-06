namespace Juno.Experiments.Api
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Juno.Contracts.Validation;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Newtonsoft.Json;
    using NUnit.Framework;
    using Polly;

    [TestFixture]
    [Category("Unit")]
    public class ExecutionGoalsControllerTests
    {
        private ExecutionGoalsController controller;
        private Fixture mockFixture;
        private FixtureDependencies mockDependencies;
        private ExecutionClient mockExecutionClient;
        private GoalBasedSchedule mockExecutionGoal;
        private TargetGoal mockTargetGoal;
        private Item<GoalBasedSchedule> mockExecutionGoalItem;
        private IList<ExperimentInstanceStatus> mockExecutionGoalStatus;

        [SetUp]
        public void SetupTests()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetUpGoalBasedScheduleMocks();
            this.mockFixture.SetupExperimentMocks();

            this.mockDependencies = new FixtureDependencies(MockBehavior.Strict);
            this.mockExecutionClient = new ExecutionClient(this.mockDependencies.RestClient.Object, new Uri("https://any/uri"), Policy.NoOpAsync());
            this.controller = new ExecutionGoalsController(this.mockExecutionClient, this.mockDependencies.Configuration, NullLogger.Instance);
            GoalBasedSchedule template = this.mockFixture.Create<GoalBasedSchedule>();
            this.mockExecutionGoalStatus = new List<ExperimentInstanceStatus>() { this.mockFixture.Create<ExperimentInstanceStatus>() };

            string cronExpression = "* * * * *";

            this.mockTargetGoal = new TargetGoal(
                name: "TargetGoal",
                true,
                preconditions: new List<Precondition>()
                {
                    new Precondition(
                        type: ContractExtension.TimerTriggerType,
                        parameters: new Dictionary<string, IConvertible>()
                        {
                            [ContractExtension.CronExpression] = cronExpression
                        }),
                    new Precondition(
                        type: ContractExtension.SuccessfulExperimentsProvider,
                        new Dictionary<string, IConvertible>()
                        {
                            ["targetExperimentInstances"] = 5
                        })
                },
                actions: new List<ScheduleAction>()
                {
                    new ScheduleAction(
                        type: ContractExtension.SelectEnvironmentAndCreateExperimentProvider,
                        new Dictionary<string, IConvertible>()
                        {
                            ["metadata.workload"] = "WorkloadABC"
                        })
                });

            this.mockExecutionGoal = new GoalBasedSchedule(
                template.ExperimentName,
                template.Description,
                template.Experiment,
                new List<TargetGoal> { this.mockTargetGoal },
                template.ControlGoals,
                template.Metadata);

            this.mockExecutionGoalItem = new Item<GoalBasedSchedule>(Guid.NewGuid().ToString(), this.mockExecutionGoal);
        }

        [Test]
        public void ControllerConstructorsValidateParameters()
        {
            Assert.Throws<ArgumentException>(() => new ExecutionGoalsController(null, this.mockDependencies.Configuration));
            Assert.Throws<ArgumentException>(() => new ExecutionGoalsController(this.mockExecutionClient, null));
        }

        [Test]
        public void ControllerConstructorSetsParametersToExpectedValues()
        {
            ExecutionGoalsController controller = new ExecutionGoalsController(this.mockExecutionClient, this.mockDependencies.Configuration);
            EqualityAssert.PropertySet(controller, "Client", this.mockExecutionClient);
            EqualityAssert.PropertySet(controller, "Configuration", this.mockDependencies.Configuration);
            EqualityAssert.PropertySet(controller, "Logger", NullLogger.Instance);

            controller = new ExecutionGoalsController(this.mockExecutionClient, this.mockDependencies.Configuration, this.mockDependencies.Logger.Object);

            EqualityAssert.PropertySet(controller, "Client", this.mockExecutionClient);
            EqualityAssert.PropertySet(controller, "Configuration", this.mockDependencies.Configuration);
            EqualityAssert.PropertySet(controller, "Logger", this.mockDependencies.Logger.Object);
        }

        [Test]
        public async Task ControllerValidatesTheTargetGoalsOfAnExecutionGoalBeforeCreation()
        {
            Mock<IValidationRule<GoalBasedSchedule>> mockValidation = new Mock<IValidationRule<GoalBasedSchedule>>();
            mockValidation.Setup(validation => validation.Validate(It.IsAny<GoalBasedSchedule>()))
                .Returns(new ValidationResult(false));

            ExecutionGoalValidation.Instance.Add(mockValidation.Object);
            ObjectResult result = await this.controller.CreateExecutionGoalAsync(this.mockExecutionGoalItem, CancellationToken.None) as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status400BadRequest, result.StatusCode);
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResponseWhenCreatingAnExecutionGoal()
        {
            string id = Guid.NewGuid().ToString();
            this.mockDependencies.RestClient.SetUpPostExecutionGoal()
                .Returns(Task.FromResult(new Item<GoalBasedSchedule>(id, this.mockExecutionGoal).ToHttpResponse()));

            CreatedAtActionResult result = await this.controller.CreateExecutionGoalAsync(this.mockExecutionGoalItem, CancellationToken.None)
                as CreatedAtActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status201Created, result.StatusCode);
            Assert.AreEqual(id, (result.Value as Item<GoalBasedSchedule>).Id);
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResponseWhenCreatingAnExecutionGoalFromTemplate()
        {
            const string templateId = "templateId";
            this.mockDependencies.RestClient.SetUpPostExecutionGoal(templateId)
                .Returns(Task.FromResult(this.mockExecutionGoalItem.ToHttpResponse()));

            ExecutionGoalParameter templateExecutionGoalMetadata = this.mockFixture.Create<ExecutionGoalSummary>().ParameterNames;

            ExecutionGoalParameter executionGoalMetadata = new ExecutionGoalParameter(
                templateExecutionGoalMetadata.TargetGoals,
                this.mockExecutionGoal.Parameters);

            CreatedAtActionResult result = await this.controller.CreateExecutionGoalFromTemplateAsync(templateId, this.mockExecutionGoal.TeamName, executionGoalMetadata, CancellationToken.None)
                as CreatedAtActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status201Created, result.StatusCode);
            Assert.AreEqual(this.mockExecutionGoalItem.Id, (result.Value as Item<GoalBasedSchedule>).Id);
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResponseWhenUpdatingAnExecutionGoalFromTemplate()
        {
            ExecutionGoalParameter executionGoalParameter = this.mockExecutionGoal.GetParametersFromTemplate();

            string executionGoalId = Guid.NewGuid().ToString();
            string templateId = Guid.NewGuid().ToString();
            this.mockDependencies.RestClient.SetupPutExecutionGoalFromTemplate(templateId, executionGoalId, Uri.EscapeUriString(this.mockExecutionGoal.TeamName))
                .Returns(Task.FromResult(this.mockExecutionGoalItem.ToHttpResponse()));

            ObjectResult result = await this.controller.UpdateExecutionGoalFromTemplateAsync(templateId, executionGoalId, this.mockExecutionGoal.TeamName, executionGoalParameter, CancellationToken.None)
                as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResponseWhenGettingAnExecutionGoal()
        {
            Item<GoalBasedSchedule> expectedResult = this.mockExecutionGoalItem;
            this.mockDependencies.RestClient.SetupGetExecutionGoal(expectedResult.Id, Uri.EscapeUriString(this.mockExecutionGoal.TeamName))
               .Returns(Task.FromResult(expectedResult.ToHttpResponse()));

            ObjectResult result = await this.controller.GetExecutionGoalsAsync(CancellationToken.None, this.mockExecutionGoal.TeamName, this.mockExecutionGoalItem.Id)
                as ObjectResult;

            Item<GoalBasedSchedule> actualResult = result.Value as Item<GoalBasedSchedule>;
            Assert.IsNotNull(actualResult, "Could not read content as expected type");
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.AreEqual(expectedResult, actualResult);
        }

        [Test]
        public async Task ControllerReturnsTheExpectedReponseWhenGettingExecutionGoals()
        {
            List<Item<GoalBasedSchedule>> expectedResult = new List<Item<GoalBasedSchedule>>() { this.mockExecutionGoalItem };
            this.mockDependencies.RestClient.SetupGetExecutionGoals(Uri.EscapeUriString(this.mockExecutionGoal.TeamName))
               .Returns(Task.FromResult(expectedResult.ToHttpResponse()));

            ObjectResult result = await this.controller.GetExecutionGoalsAsync(CancellationToken.None, this.mockExecutionGoal.TeamName)
                as ObjectResult;

            List<Item<GoalBasedSchedule>> actualResult = result.Value as List<Item<GoalBasedSchedule>>;
            Assert.IsNotNull(actualResult, "Could not read content as expected type");
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.AreEqual(expectedResult, actualResult);
        }

        [Test]
        public async Task ControllerReturnsTheExpectedReponseWhenGettingExecutionGoalViewIsTypeEmpty()
        {
            this.mockDependencies.RestClient.SetupGetExecutionGoal(this.mockExecutionGoalItem.Id, this.mockExecutionGoal.TeamName, ExecutionGoalView.Full)
                .Returns(Task.FromResult(this.mockExecutionGoalItem.ToHttpResponse()));

            ObjectResult result = await this.controller.GetExecutionGoalsAsync(CancellationToken.None, this.mockExecutionGoal.TeamName, this.mockExecutionGoalItem.Id)
                as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
        }

        [Test]
        public async Task ControllerReturnsTheExpectedReponseWhenGettingExecutionGoalViewTypeFull()
        {
            this.mockDependencies.RestClient.SetupGetExecutionGoal(this.mockExecutionGoalItem.Id, this.mockExecutionGoal.TeamName, ExecutionGoalView.Full)
                .Returns(Task.FromResult(this.mockExecutionGoalItem.ToHttpResponse()));

            ObjectResult result = await this.controller.GetExecutionGoalAsync(this.mockExecutionGoalItem.Id, this.mockExecutionGoal.TeamName, CancellationToken.None, ExecutionGoalView.Full)
                as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
        }

        [Test]
        public async Task ControllerReturnsTheExpectedReponseWhenGettingExecutionGoalViewTypeStatus()
        {
            this.mockDependencies.RestClient.SetupGetExecutionGoal(this.mockExecutionGoalItem.Id, this.mockExecutionGoal.TeamName, ExecutionGoalView.Status)
                .Returns(Task.FromResult((this.mockExecutionGoalStatus as IEnumerable<ExperimentInstanceStatus>).ToHttpResponse()));

            ObjectResult result = await this.controller.GetExecutionGoalAsync(this.mockExecutionGoalItem.Id, this.mockExecutionGoal.TeamName, CancellationToken.None, ExecutionGoalView.Status)
                as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
        }

        [Test]
        public async Task ControllerGetsTheExpectedResponseForExecutionGoalSummaryView()
        {
            this.mockDependencies.RestClient.SetupGetExecutionGoal(this.mockExecutionGoalItem.Id, this.mockExecutionGoal.TeamName, ExecutionGoalView.Summary)
                .Returns(Task.FromResult(this.mockExecutionGoalItem.ToHttpResponse()));

            ObjectResult result = await this.controller.GetExecutionGoalAsync(this.mockExecutionGoalItem.Id, this.mockExecutionGoal.TeamName, CancellationToken.None, ExecutionGoalView.Summary)
                as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResponseWhenUpdatingExecutionGoal()
        {
            this.mockDependencies.RestClient.SetupPutExecutionGoal()
                .Returns(Task.FromResult(this.mockExecutionGoalItem.ToHttpResponse()));

            ObjectResult result = await this.controller.UpdateExecutionGoalAsync(this.mockExecutionGoalItem, CancellationToken.None)
                as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.AreEqual(this.mockExecutionGoalItem.Id, (result.Value as Item<GoalBasedSchedule>).Id);
        }

        [Test]
        public async Task ControllerReturnsTheexpectedResponseWhenDeletingAnExecutionGoal()
        {
            this.mockDependencies.RestClient.Setup(client => client.DeleteAsync(
                It.Is<Uri>(uri => uri.PathAndQuery == $"/api/executionGoals/{this.mockExecutionGoalItem.Id}?teamName={this.mockExecutionGoal.TeamName}"),
                It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)));

            StatusCodeResult result = await this.controller.DeleteExecutionGoalAsync(this.mockExecutionGoalItem.Id, this.mockExecutionGoal.TeamName, CancellationToken.None)
                as StatusCodeResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status204NoContent, result.StatusCode);
        }
    }
}