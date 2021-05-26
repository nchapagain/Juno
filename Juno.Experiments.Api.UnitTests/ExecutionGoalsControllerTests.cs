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
        private Goal mockTargetGoal;
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

            this.mockTargetGoal = new Goal(
                name: "TargetGoal",
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
                template.ExecutionGoalId,
                template.Name,
                template.TeamName,
                template.Description,
                template.ScheduleMetadata,
                template.Enabled,
                template.Version,
                template.Experiment,
                new List<Goal> { this.mockTargetGoal },
                template.ControlGoals);

            this.mockExecutionGoalItem = new Item<GoalBasedSchedule>(this.mockExecutionGoal.ExecutionGoalId, this.mockExecutionGoal);
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
            this.mockDependencies.RestClient.SetUpPostExecutionGoal()
                .Returns(Task.FromResult(new Item<GoalBasedSchedule>(this.mockExecutionGoal.ExecutionGoalId, this.mockExecutionGoal).ToHttpResponse()));

            CreatedAtActionResult result = await this.controller.CreateExecutionGoalAsync(this.mockExecutionGoalItem, CancellationToken.None)
                as CreatedAtActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status201Created, result.StatusCode);
            Assert.AreEqual(this.mockExecutionGoal.ExecutionGoalId, (result.Value as Item<GoalBasedSchedule>).Id);
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResponseWhenCreatingAnExecutionGoalFromTemplate()
        {
            const string templateId = "templateId";
            this.mockDependencies.RestClient.SetUpPostExecutionGoal(templateId)
                .Returns(Task.FromResult(this.mockExecutionGoalItem.ToHttpResponse()));

            ExecutionGoalParameter templateExecutionGoalMetadata = this.mockFixture.Create<ExecutionGoalSummary>().ParameterNames;

            ExecutionGoalParameter executionGoalMetadata = new ExecutionGoalParameter(
                this.mockExecutionGoal.ExecutionGoalId,
                this.mockExecutionGoal.ExperimentName,
                this.mockExecutionGoal.TeamName,
                templateExecutionGoalMetadata.Owner,
                this.mockExecutionGoal.Enabled,
                templateExecutionGoalMetadata.TargetGoals,
                this.mockExecutionGoal.Parameters);

            CreatedAtActionResult result = await this.controller.CreateExecutionGoalFromTemplateAsync(templateId, this.mockExecutionGoal.TeamName, executionGoalMetadata, CancellationToken.None)
                as CreatedAtActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status201Created, result.StatusCode);
            Assert.AreEqual(this.mockExecutionGoal.ExecutionGoalId, (result.Value as Item<GoalBasedSchedule>).Definition.ExecutionGoalId);
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResponseWhenUpdatingAnExecutionGoalFromTemplate()
        {
            ExecutionGoalParameter executionGoalParameter = this.mockExecutionGoal.GetParametersFromTemplate();

            this.mockDependencies.RestClient.SetupPutExecutionGoalFromTemplate(this.mockExecutionGoal.ExecutionGoalId, this.mockExecutionGoal.TeamName)
                .Returns(Task.FromResult(this.mockExecutionGoalItem.ToHttpResponse()));

            ObjectResult result = await this.controller.UpdateExecutionGoalFromTemplateAsync(executionGoalParameter.ExecutionGoalId, this.mockExecutionGoal.TeamName, executionGoalParameter, CancellationToken.None)
                as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.AreEqual(executionGoalParameter.ExecutionGoalId, (result.Value as Item<GoalBasedSchedule>).Definition.ExecutionGoalId);
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResponseWhenGettingAnExecutionGoal()
        {
            this.mockDependencies.RestClient.SetupGetExecutionGoal(this.mockExecutionGoal.ExecutionGoalId, this.mockExecutionGoal.TeamName)
                .Returns(Task.FromResult(this.mockExecutionGoalItem.ToHttpResponse()));

            ObjectResult result = await this.controller.GetExecutionGoalAsync(this.mockExecutionGoal.ExecutionGoalId, this.mockExecutionGoal.TeamName, CancellationToken.None)
                as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.AreEqual(this.mockExecutionGoal.ExecutionGoalId, (result.Value as Item<GoalBasedSchedule>).Definition.ExecutionGoalId);
        }

        [Test]
        public async Task ControllerReturnsTheExpectedReponseWhenGettingExecutionGoals()
        {
            this.mockDependencies.RestClient.SetupGetExecutionGoals(this.mockExecutionGoal.TeamName)
               .Returns(Task.FromResult((new List<Item<GoalBasedSchedule>>() { this.mockExecutionGoalItem }).ToHttpResponse()));

            ObjectResult result = await this.controller.GetExecutionGoalsAsync(CancellationToken.None, this.mockExecutionGoal.TeamName)
                as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
        }

        [Test]
        public async Task ControllerReturnsTheExpectedReponseWhenGettingExecutionGoalViewIsTypeEmpty()
        {
            this.mockDependencies.RestClient.SetupGetExecutionGoal(this.mockExecutionGoal.ExecutionGoalId, this.mockExecutionGoal.TeamName, ExecutionGoalView.Full)
                .Returns(Task.FromResult(this.mockExecutionGoalItem.ToHttpResponse()));

            ObjectResult result = await this.controller.GetExecutionGoalsAsync(CancellationToken.None, this.mockExecutionGoal.TeamName, this.mockExecutionGoal.ExecutionGoalId)
                as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
        }

        [Test]
        public async Task ControllerReturnsTheExpectedReponseWhenGettingExecutionGoalViewTypeFull()
        {
            this.mockDependencies.RestClient.SetupGetExecutionGoal(this.mockExecutionGoal.ExecutionGoalId, this.mockExecutionGoal.TeamName, ExecutionGoalView.Full)
                .Returns(Task.FromResult(this.mockExecutionGoalItem.ToHttpResponse()));

            ObjectResult result = await this.controller.GetExecutionGoalAsync(this.mockExecutionGoal.ExecutionGoalId, this.mockExecutionGoal.TeamName, CancellationToken.None, ExecutionGoalView.Full)
                as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
        }

        [Test]
        public async Task ControllerReturnsTheExpectedReponseWhenGettingExecutionGoalViewTypeStatus()
        {
            this.mockDependencies.RestClient.SetupGetExecutionGoal(this.mockExecutionGoal.ExecutionGoalId, this.mockExecutionGoal.TeamName, ExecutionGoalView.Status)
                .Returns(Task.FromResult((this.mockExecutionGoalStatus as IEnumerable<ExperimentInstanceStatus>).ToHttpResponse()));

            ObjectResult result = await this.controller.GetExecutionGoalAsync(this.mockExecutionGoal.ExecutionGoalId, this.mockExecutionGoal.TeamName, CancellationToken.None, ExecutionGoalView.Status)
                as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
        }

        [Test]
        public async Task ControllerGetsTheExpectedResponseForExecutionGoalSummaryView()
        {
            this.mockDependencies.RestClient.SetupGetExecutionGoal(this.mockExecutionGoal.ExecutionGoalId, this.mockExecutionGoal.TeamName, ExecutionGoalView.Summary)
                .Returns(Task.FromResult(this.mockExecutionGoalItem.ToHttpResponse()));

            ObjectResult result = await this.controller.GetExecutionGoalAsync(this.mockExecutionGoal.ExecutionGoalId, this.mockExecutionGoal.TeamName, CancellationToken.None, ExecutionGoalView.Summary)
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
            Assert.AreEqual(this.mockExecutionGoal.ExecutionGoalId, (result.Value as Item<GoalBasedSchedule>).Definition.ExecutionGoalId);
        }

        [Test]
        public async Task ControllerReturnsTheexpectedResponseWhenDeletingAnExecutionGoal()
        {
            this.mockDependencies.RestClient.Setup(client => client.DeleteAsync(
                It.Is<Uri>(uri => uri.PathAndQuery == $"/api/executionGoals/{this.mockExecutionGoal.ExecutionGoalId}?teamName={this.mockExecutionGoal.TeamName}"),
                It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)));

            StatusCodeResult result = await this.controller.DeleteExecutionGoalAsync(this.mockExecutionGoal.ExecutionGoalId, this.mockExecutionGoal.TeamName, CancellationToken.None)
                as StatusCodeResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status204NoContent, result.StatusCode);
        }
    }
}