namespace Juno.Experiments.Api
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
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
    public class ExecutionGoalTemplateControllerTests
    {
        private ExecutionGoalTemplatesController controller;
        private Fixture mockFixture;
        private FixtureDependencies mockDependencies;
        private ExecutionClient mockExecutionClient;
        private GoalBasedSchedule mockExecutionGoalTemplate;
        private Item<GoalBasedSchedule> mockExecutionGoalTemplateItem;
        private Goal mockTargetGoal;

        [SetUp]
        public void SetupTests()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetUpGoalBasedScheduleMocks();
            this.mockFixture.SetupExperimentMocks();

            this.mockDependencies = new FixtureDependencies(MockBehavior.Strict);
            this.mockExecutionClient = new ExecutionClient(this.mockDependencies.RestClient.Object, new Uri("https://any/uri"), Policy.NoOpAsync());
            this.controller = new ExecutionGoalTemplatesController(this.mockExecutionClient, this.mockDependencies.Configuration, NullLogger.Instance);
            GoalBasedSchedule template = this.mockFixture.Create<GoalBasedSchedule>();

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

            this.mockExecutionGoalTemplate = new GoalBasedSchedule(
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

            this.mockExecutionGoalTemplateItem = new Item<GoalBasedSchedule>(Guid.NewGuid().ToString(), this.mockExecutionGoalTemplate);
        }

        [Test]
        public void ControllerConstructorsValidateParameters()
        {
            Assert.Throws<ArgumentException>(() => new ExecutionGoalTemplatesController(null, this.mockDependencies.Configuration));
            Assert.Throws<ArgumentException>(() => new ExecutionGoalTemplatesController(this.mockExecutionClient, null));
        }

        [Test]
        public void ExecutionGoalTemplateItemNeedsIdParamater()
        {
            Assert.Throws<ArgumentException>(() => new Item<GoalBasedSchedule>(string.Empty, this.mockExecutionGoalTemplate));
        }

        [Test]
        public void ControllerConstructorSetsParametersToExpectedValues()
        {
            ExecutionGoalTemplatesController controller = new ExecutionGoalTemplatesController(this.mockExecutionClient, this.mockDependencies.Configuration);
            EqualityAssert.PropertySet(controller, "Client", this.mockExecutionClient);
            EqualityAssert.PropertySet(controller, "Configuration", this.mockDependencies.Configuration);
            EqualityAssert.PropertySet(controller, "Logger", NullLogger.Instance);

            controller = new ExecutionGoalTemplatesController(this.mockExecutionClient, this.mockDependencies.Configuration, this.mockDependencies.Logger.Object);

            EqualityAssert.PropertySet(controller, "Client", this.mockExecutionClient);
            EqualityAssert.PropertySet(controller, "Configuration", this.mockDependencies.Configuration);
            EqualityAssert.PropertySet(controller, "Logger", this.mockDependencies.Logger.Object);
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResponseWhenCreatingAnExecutionGoalTemplate()
        {
            Mock<IValidationRule<GoalBasedSchedule>> mockValidation = new Mock<IValidationRule<GoalBasedSchedule>>();
            mockValidation.Setup(validation => validation.Validate(It.IsAny<GoalBasedSchedule>()))
                .Returns(new ValidationResult(true));

            ExecutionGoalValidation.Instance.Clear();
            ExecutionGoalValidation.Instance.Add(mockValidation.Object);

            this.mockDependencies.RestClient.SetUpPostExecutionGoalTemplate()
                .Returns(Task.FromResult(this.mockExecutionGoalTemplateItem.ToHttpResponse()));

            ObjectResult result = await this.controller.CreateExecutionGoalTemplateAsync(this.mockExecutionGoalTemplateItem, CancellationToken.None)
                as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status201Created, result.StatusCode);
            Assert.AreEqual(this.mockExecutionGoalTemplate.ExecutionGoalId, (result.Value as Item<GoalBasedSchedule>).Definition.ExecutionGoalId);
        }

        [Test]
        public async Task ControllerValidatesTheTargetGoalsOfAnExecutionGoalTemplateBeforeCreation()
        {
            Mock<IValidationRule<GoalBasedSchedule>> mockValidation = new Mock<IValidationRule<GoalBasedSchedule>>();
            mockValidation.Setup(validation => validation.Validate(It.IsAny<GoalBasedSchedule>()))
                .Returns(new ValidationResult(false));

            this.mockDependencies.RestClient.SetUpPostExecutionGoalTemplate()
                .Returns(Task.FromResult(this.mockExecutionGoalTemplateItem.ToHttpResponse()));

            ExecutionGoalTemplateValidation.Instance.Add(mockValidation.Object);
            ObjectResult result = await this.controller.CreateExecutionGoalTemplateAsync(this.mockExecutionGoalTemplateItem, CancellationToken.None) as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status400BadRequest, result.StatusCode);
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResponseWhenUpdatingAnExecutionGoalTemplate()
        {
            Mock<IValidationRule<GoalBasedSchedule>> mockValidation = new Mock<IValidationRule<GoalBasedSchedule>>();
            mockValidation.Setup(validation => validation.Validate(It.IsAny<GoalBasedSchedule>()))
                .Returns(new ValidationResult(true));

            ExecutionGoalValidation.Instance.Clear();
            ExecutionGoalValidation.Instance.Add(mockValidation.Object);

            this.mockDependencies.RestClient.SetUpPutExecutionGoalTemplate()
                .Returns(Task.FromResult(this.mockExecutionGoalTemplateItem.ToHttpResponse()));

            ObjectResult result = await this.controller.UpdateExecutionGoalTemplateAsync(this.mockExecutionGoalTemplateItem, CancellationToken.None)
                as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.AreEqual(this.mockExecutionGoalTemplate.ExecutionGoalId, (result.Value as Item<GoalBasedSchedule>).Definition.ExecutionGoalId);
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResponseWhenGettingAllExecutionGoalTemplateMetadata()
        {
            IEnumerable<ExecutionGoalSummary> expectedResult = new List<ExecutionGoalSummary>() { this.mockFixture.Create<ExecutionGoalSummary>() };
            this.mockDependencies.RestClient.SetupGetExecutionGoalTemplate(view: View.Summary, null, null)
                .Returns(Task.FromResult(expectedResult.ToHttpResponse()));

            ObjectResult result = await this.controller.GetExecutionGoalTemplatesAsync(CancellationToken.None, null, null, View.Summary) as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.AreEqual(expectedResult, result.Value as IEnumerable<ExecutionGoalSummary>);
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResponseWhenGettingExecutionGoalTemplateMetadataForTeam()
        {
            IEnumerable<ExecutionGoalSummary> expectedResult = new List<ExecutionGoalSummary>() { this.mockFixture.Create<ExecutionGoalSummary>() };
            const string teamName = "teamName";
            this.mockDependencies.RestClient.SetupGetExecutionGoalTemplate(view: View.Summary, teamName, null)
                .Returns(Task.FromResult(expectedResult.ToHttpResponse()));

            ObjectResult result = await this.controller.GetExecutionGoalTemplatesAsync(CancellationToken.None, teamName, null, View.Summary) as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.AreEqual(expectedResult, result.Value as IEnumerable<ExecutionGoalSummary>);
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResponseWhenGettingExecutionGoalTemplateMetadataForOneTemplate()
        {
            ExecutionGoalSummary expectedResult = this.mockFixture.Create<ExecutionGoalSummary>();
            string teamName = "teamName";
            string templateId = Guid.NewGuid().ToString();
            this.mockDependencies.RestClient.SetupGetExecutionGoalTemplate(view: View.Summary, teamName, templateId)
                .Returns(Task.FromResult(expectedResult.ToHttpResponse()));

            ObjectResult result = await this.controller.GetExecutionGoalTemplatesAsync(CancellationToken.None, teamName, templateId, View.Summary) as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.AreEqual(expectedResult, result.Value as ExecutionGoalSummary);
        }

        [Test]
        public async Task ControllerReturnsTheExpectedReponseWhenGettingExecutionGoalTemplateMetadataWithId()
        {
            ExecutionGoalSummary expectedResult = this.mockFixture.Create<ExecutionGoalSummary>();
            const string teamName = "teamName";
            string id = Guid.NewGuid().ToString();

            this.mockDependencies.RestClient.SetupGetExecutionGoalTemplate(View.Summary, teamName, id)
                .Returns(Task.FromResult(expectedResult.ToHttpResponse()));

            ObjectResult result = await this.controller.GetExecutionGoalTemplatesAsync(CancellationToken.None, teamName, id, View.Summary) as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.AreEqual(expectedResult, result.Value as ExecutionGoalSummary);
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResponseWhenGettingExecutionGoalTemplateWithId()
        {
            const string teamName = "teamName";
            string id = Guid.NewGuid().ToString();
            Item<GoalBasedSchedule> expectedResult = new Item<GoalBasedSchedule>(id, this.mockFixture.Create<GoalBasedSchedule>());

            this.mockDependencies.RestClient.SetupGetExecutionGoalTemplate(View.Full, teamName, id)
                .Returns(Task.FromResult(expectedResult.ToHttpResponse()));

            ObjectResult result = await this.controller.GetExecutionGoalTemplatesAsync(CancellationToken.None, teamName, id, View.Full) as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.AreEqual(expectedResult, result.Value as Item<GoalBasedSchedule>);
        }

        [Test]
        public async Task ControllerReturnsExpectedResponseWhenGetExecutionGoalTemplatesIsAskedByTeamName()
        {
            const string teamName = "teamName";
            string id = Guid.NewGuid().ToString();
            IEnumerable<Item<GoalBasedSchedule>> expectedResult = new List<Item<GoalBasedSchedule>>() { new Item<GoalBasedSchedule>(id, this.mockFixture.Create<GoalBasedSchedule>()) };

            this.mockDependencies.RestClient.SetupGetExecutionGoalTemplate(View.Full, teamName, null)
                .Returns(Task.FromResult(expectedResult.ToHttpResponse()));

            ObjectResult result = await this.controller.GetExecutionGoalTemplatesAsync(CancellationToken.None, teamName, null, View.Full) as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.AreEqual(expectedResult, result.Value as IEnumerable<Item<GoalBasedSchedule>>);
        }

        [Test]
        public async Task ControllerReturnsExpectedResponseWhenGettingAllExecutionGoalTemplates()
        {
            string id = Guid.NewGuid().ToString();
            IEnumerable<Item<GoalBasedSchedule>> expectedResult = new List<Item<GoalBasedSchedule>>() { new Item<GoalBasedSchedule>(id, this.mockFixture.Create<GoalBasedSchedule>()) };

            this.mockDependencies.RestClient.SetupGetExecutionGoalTemplate(View.Full, null, null)
                .Returns(Task.FromResult(expectedResult.ToHttpResponse()));

            ObjectResult result = await this.controller.GetExecutionGoalTemplatesAsync(CancellationToken.None, null, null, View.Full) as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.AreEqual(expectedResult, result.Value as IEnumerable<Item<GoalBasedSchedule>>);
        }

        [Test]
        public async Task ControllerReturnsTheexpectedResponseWhenDeletingAnExecutionGoalTemplate()
        {
            this.mockDependencies.RestClient.Setup(client => client.DeleteAsync(
                It.Is<Uri>(uri => uri.PathAndQuery == $"/api/executionGoalTemplates/{this.mockExecutionGoalTemplate.ExecutionGoalId}/?teamName={this.mockExecutionGoalTemplate.TeamName}"),
                It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)));

            StatusCodeResult result = await this.controller.DeleteExecutionGoalTemplateAsync(this.mockExecutionGoalTemplate.ExecutionGoalId, this.mockExecutionGoalTemplate.TeamName, CancellationToken.None)
                as StatusCodeResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status204NoContent, result.StatusCode);
        }
    }
}
