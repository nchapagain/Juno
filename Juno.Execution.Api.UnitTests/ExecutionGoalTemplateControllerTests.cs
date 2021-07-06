namespace Juno.Execution.Api
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Contracts;
    using Juno.Contracts.Validation;
    using Juno.DataManagement;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Infrastructure;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Repository;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;

    using CosmosTable = Microsoft.Azure.Cosmos.Table;
    using Storage = Microsoft.Azure.Storage;

    [TestFixture]
    [Category("Unit")]
    public class ExecutionGoalTemplateControllerTests
    {
        // This provides a mapping of all known potential errors mapped to the HTTP status
        // code expected in the result. Each controller method handles exceptions in consistent
        // ways, so this is applicable to all controller methods. (Copied from ExperimentControllerTests.cs)
        private static Dictionary<Exception, int> potentialErrors = new Dictionary<Exception, int>
        {
            [new SchemaException()] = StatusCodes.Status400BadRequest,
            [new DataStoreException(DataErrorReason.DataAlreadyExists)] = StatusCodes.Status409Conflict,
            [new DataStoreException(DataErrorReason.DataNotFound)] = StatusCodes.Status404NotFound,
            [new DataStoreException(DataErrorReason.ETagMismatch)] = StatusCodes.Status412PreconditionFailed,
            [new DataStoreException(DataErrorReason.PartitionKeyMismatch)] = StatusCodes.Status412PreconditionFailed,
            [new DataStoreException(DataErrorReason.Undefined)] = StatusCodes.Status500InternalServerError,
            [new CosmosException("Any error", System.Net.HttpStatusCode.Forbidden, 0, "Any ID", 1.0)] = StatusCodes.Status403Forbidden,
            [new CosmosTable.StorageException(new CosmosTable.RequestResult { HttpStatusCode = StatusCodes.Status403Forbidden }, "Any error", null)] = StatusCodes.Status403Forbidden,
            [new Storage.StorageException(new Storage.RequestResult { HttpStatusCode = StatusCodes.Status403Forbidden }, "Any error", null)] = StatusCodes.Status403Forbidden,
            [new Exception("All other errors")] = StatusCodes.Status500InternalServerError
        };

        private Fixture mockFixture;
        private GoalBasedSchedule mockExecutionGoal;
        private Item<GoalBasedSchedule> mockExecutionGoalItem;
        private TargetGoal mockTargetGoal;
        private Mock<IScheduleDataManager> mockExecutionGoalDataManager;
        private ExecutionGoalTemplateController controller;
        private ExecutionGoalSummary mockExecutionMetadata;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetUpGoalBasedScheduleMocks();
            this.mockFixture.SetupExperimentMocks();

            GoalBasedSchedule template = this.mockFixture.Create<GoalBasedSchedule>();

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

            this.mockExecutionGoalDataManager = new Mock<IScheduleDataManager>();
            this.controller = new ExecutionGoalTemplateController(this.mockExecutionGoalDataManager.Object, NullLogger.Instance);

            this.mockExecutionMetadata = this.mockFixture.Create<ExecutionGoalSummary>();
        }

        [Test]
        public void ControllerConstructorValidatesParameters()
        {
            Assert.Throws<ArgumentException>(() => new ExecutionGoalTemplateController(null));
            Assert.Throws<ArgumentException>(() => new ExecutionGoalTemplateController(null, null));
            Assert.DoesNotThrow(() => new ExecutionGoalTemplateController(this.mockExecutionGoalDataManager.Object, null));
        }

        [Test]
        public void ControllerConstructorSetsParametersToExpectedValues()
        {
            ExecutionGoalTemplateController controller = new ExecutionGoalTemplateController(this.mockExecutionGoalDataManager.Object);
            EqualityAssert.PropertySet(controller, "ExecutionGoalDataManager", this.mockExecutionGoalDataManager.Object);
            EqualityAssert.PropertySet(controller, "Logger", NullLogger.Instance);
        }

        [Test]
        public async Task ControllerValidatesTheTargetGoalsOfAnExecutionGoalTemplateBeforeCreation()
        {
            Mock<IValidationRule<GoalBasedSchedule>> mockValidation = new Mock<IValidationRule<GoalBasedSchedule>>();
            mockValidation.Setup(validation => validation.Validate(It.IsAny<GoalBasedSchedule>()))
                .Returns(new ValidationResult(false));

            ExecutionGoalTemplateValidation.Instance.Clear();
            ExecutionGoalTemplateValidation.Instance.Add(mockValidation.Object);

            ObjectResult result = await this.controller.CreateExecutionGoalTemplateAsync(this.mockExecutionGoalItem, CancellationToken.None) as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status400BadRequest, result.StatusCode);
        }

        [Test]
        public async Task ContollerCreatesTheExpectedExecutionGoalTemplateItem()
        {
            Mock<IValidationRule<GoalBasedSchedule>> mockValidation = new Mock<IValidationRule<GoalBasedSchedule>>();
            mockValidation.Setup(validation => validation.Validate(It.IsAny<GoalBasedSchedule>()))
                .Returns(new ValidationResult(true));
            ExecutionGoalValidation.Instance.Clear();
            ExecutionGoalValidation.Instance.Add(mockValidation.Object);

            await this.controller.CreateExecutionGoalTemplateAsync(this.mockExecutionGoalItem, CancellationToken.None);

            this.mockExecutionGoalDataManager.Verify(mgr => mgr.CreateExecutionGoalTemplateAsync(
                It.Is<Item<GoalBasedSchedule>>(executionGoal => executionGoal.Equals(this.mockExecutionGoalItem)), false, It.IsAny<CancellationToken>()));
        }

        [Test]
        public async Task ControllerReturnsTheExpecedResultWhenAnExecutionGoalTemplateItemIsCreatedSuccessfully()
        {
            Mock<IValidationRule<GoalBasedSchedule>> mockValidation = new Mock<IValidationRule<GoalBasedSchedule>>();
            mockValidation.Setup(validation => validation.Validate(It.IsAny<GoalBasedSchedule>()))
                .Returns(new ValidationResult(true));
            ExecutionGoalValidation.Instance.Clear();
            ExecutionGoalValidation.Instance.Add(mockValidation.Object);

            this.mockExecutionGoalDataManager.Setup(mgr => mgr.CreateExecutionGoalTemplateAsync(It.IsAny<Item<GoalBasedSchedule>>(), false, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(this.mockExecutionGoalItem));

            CreatedAtActionResult result = await this.controller.CreateExecutionGoalTemplateAsync(this.mockExecutionGoalItem, CancellationToken.None)
                as CreatedAtActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status201Created, result.StatusCode);
            Assert.IsTrue(object.ReferenceEquals(this.mockExecutionGoalItem, result.Value));
        }

        [Test]
        public async Task ControllerHandlesErrorsAsExpectedDuringExecutionGoalTemplateCreation()
        {
            foreach (var entry in ExecutionGoalTemplateControllerTests.potentialErrors)
            {
                // Key = Exception
                // Value = The expected HTTP status code in the result
                this.mockExecutionGoalDataManager.Setup(mgr => mgr.CreateExecutionGoalTemplateAsync(It.IsAny<Item<GoalBasedSchedule>>(), false, It.IsAny<CancellationToken>()))
                    .Throws(entry.Key);

                IActionResult result = await this.controller.CreateExecutionGoalTemplateAsync(this.mockExecutionGoalItem, CancellationToken.None);

                Assert.IsNotNull(result);
                Assert.AreEqual(entry.Value, (result as IStatusCodeActionResult).StatusCode);
                Assert.IsInstanceOf<ProblemDetails>((result as ObjectResult).Value);
            }
        }

        [Test]
        public async Task ControllerGetsTheExpectedResponseForAllExecutionGoalTemplate()
        {
            await this.controller.GetExecutionGoalTemplatesAsync(CancellationToken.None, null, null, View.Full);

            this.mockExecutionGoalDataManager.Verify(mgr => mgr.GetExecutionGoalTemplatesAsync(
                It.IsAny<CancellationToken>(), null));
        }

        [Test]
        public async Task ControllerGetsTheExpectedResponseForAllExecutionGoalTemplateForGivenTeamName()
        {
            await this.controller.GetExecutionGoalTemplatesAsync(CancellationToken.None, this.mockExecutionGoal.TeamName, null, View.Full);

            this.mockExecutionGoalDataManager.Verify(mgr => mgr.GetExecutionGoalTemplatesAsync(
                It.IsAny<CancellationToken>(),
                this.mockExecutionGoal.TeamName));
        }

        [Test]
        public async Task ControllerGetsTheExpectedResponseForOneExecutionGoalTemplate()
        {
            await this.controller.GetExecutionGoalTemplatesAsync(CancellationToken.None, this.mockExecutionGoal.TeamName, this.mockExecutionGoalItem.Id, View.Full);

            this.mockExecutionGoalDataManager.Verify(mgr => mgr.GetExecutionGoalTemplateAsync(
                this.mockExecutionGoalItem.Id,
                this.mockExecutionGoal.TeamName,
                It.IsAny<CancellationToken>()));
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResultWhenAnExecutionGoalTemplateIsretrievedSuccessfully()
        {
            this.mockExecutionGoalDataManager.Setup(mgr => mgr.GetExecutionGoalTemplateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(this.mockExecutionGoalItem));

            ObjectResult result = await this.controller.GetExecutionGoalTemplatesAsync(CancellationToken.None, "Team name", "Experiment ID") as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.IsTrue(object.ReferenceEquals(this.mockExecutionGoalItem, result.Value));
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResultWhenAnExecutionGoalTemplateMetadataIsretrievedSuccessfully()
        {
            this.mockExecutionGoalDataManager.Setup(mgr => mgr.GetExecutionGoalTemplateInfoAsync(It.IsAny<CancellationToken>(), this.mockExecutionMetadata.TeamName, this.mockExecutionMetadata.Id))
                .ReturnsAsync(new List<ExecutionGoalSummary>() { this.mockExecutionMetadata });

            ObjectResult result = await this.controller.GetExecutionGoalTemplatesAsync(CancellationToken.None, this.mockExecutionMetadata.TeamName, this.mockExecutionMetadata.Id, View.Summary) as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.IsTrue(object.ReferenceEquals(this.mockExecutionMetadata, result.Value));
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResultWhenAllExecutionGoaTemplatelsIsretrievedSuccessfullyForGivenTeam()
        {
            var returnObject = new List<Item<GoalBasedSchedule>>() { this.mockExecutionGoalItem };

            this.mockExecutionGoalDataManager.Setup(mgr => mgr.GetExecutionGoalTemplatesAsync(It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .ReturnsAsync(returnObject);

            ObjectResult result = await this.controller.GetExecutionGoalTemplatesAsync(CancellationToken.None, "Team name") as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.IsTrue(object.ReferenceEquals(returnObject, result.Value));
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResultWhenAllExecutionGoalTemplateMetadataIsretrievedSuccessfullyForGivenTeam()
        {
            var metadataList = new List<ExecutionGoalSummary>() { this.mockExecutionMetadata };
            this.mockExecutionGoalDataManager.Setup(mgr => mgr.GetExecutionGoalTemplateInfoAsync(It.IsAny<CancellationToken>(), this.mockExecutionMetadata.TeamName, null))
                .ReturnsAsync(metadataList);

            ObjectResult result = await this.controller.GetExecutionGoalTemplatesAsync(CancellationToken.None, this.mockExecutionMetadata.TeamName, null, View.Summary) as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.IsTrue(object.ReferenceEquals(metadataList, result.Value));
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResultWhenAllExecutionGoaTemplatelsIsretrievedSuccessfully()
        {
            var returnObject = new List<Item<GoalBasedSchedule>>() { this.mockExecutionGoalItem };

            this.mockExecutionGoalDataManager.Setup(mgr => mgr.GetExecutionGoalTemplatesAsync(It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .ReturnsAsync(returnObject);

            ObjectResult result = await this.controller.GetExecutionGoalTemplatesAsync(CancellationToken.None) as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.IsTrue(object.ReferenceEquals(returnObject, result.Value));
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResultWhenAllExecutionGoalTemplateMetadataIsretrievedSuccessfully()
        {
            IEnumerable<ExecutionGoalSummary> metadataList = new List<ExecutionGoalSummary>() { this.mockExecutionMetadata };
            this.mockExecutionGoalDataManager.Setup(mgr => mgr.GetExecutionGoalTemplateInfoAsync(It.IsAny<CancellationToken>(), null, null))
                .ReturnsAsync(metadataList);

            ObjectResult result = await this.controller.GetExecutionGoalTemplatesAsync(CancellationToken.None, null, null, View.Summary) as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.AreEqual(metadataList, result.Value);
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResultWhenExecutionGoalsTemplatesAreretrievedSuccessfully()
        {
            this.mockExecutionGoalDataManager.Setup(mgr => mgr.GetExecutionGoalTemplatesAsync(It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .Returns(Task.FromResult(new List<Item<GoalBasedSchedule>>() { this.mockExecutionGoalItem } as IEnumerable<Item<GoalBasedSchedule>>));

            ObjectResult result = await this.controller.GetExecutionGoalTemplatesAsync(CancellationToken.None, this.mockExecutionGoal.TeamName) as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.AreEqual(new List<Item<GoalBasedSchedule>>() { this.mockExecutionGoalItem } as IEnumerable<Item<GoalBasedSchedule>>, result.Value);
        }

        [Test]
        public async Task ControllerHandlesErrorsAsexpectedWhenGettingExecutionGoalTemplates()
        {
            foreach (var entry in ExecutionGoalTemplateControllerTests.potentialErrors)
            {
                this.mockExecutionGoalDataManager.Setup(mgr => mgr.GetExecutionGoalTemplatesAsync(It.IsAny<CancellationToken>(), It.IsAny<string>()))
                    .Throws(entry.Key);

                IActionResult result = await this.controller.GetExecutionGoalTemplatesAsync(CancellationToken.None, null, null, View.Full);

                Assert.IsNotNull(result);
                Assert.AreEqual(entry.Value, (result as IStatusCodeActionResult).StatusCode);
                Assert.IsInstanceOf<ProblemDetails>((result as ObjectResult).Value);
            }
        }

        [Test]
        public async Task ControllerValidatesTheTargetGoalsOfAnExecutionGoalTemplateBeforeUpdate()
        {
            Mock<IValidationRule<GoalBasedSchedule>> mockValidation = new Mock<IValidationRule<GoalBasedSchedule>>();
            mockValidation.Setup(validation => validation.Validate(It.IsAny<GoalBasedSchedule>()))
                .Returns(new ValidationResult(false));

            ExecutionGoalTemplateValidation.Instance.Add(mockValidation.Object);
            ObjectResult result = await this.controller.UpdateExecutionGoalTemplateAsync(this.mockExecutionGoalItem, CancellationToken.None) as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status400BadRequest, result.StatusCode);
        }

        [Test]
        public async Task ControllerUpdatesTheExpectedExecutionGoalTemplateItem()
        {
            Mock<IValidationRule<GoalBasedSchedule>> mockValidation = new Mock<IValidationRule<GoalBasedSchedule>>();
            mockValidation.Setup(validation => validation.Validate(It.IsAny<GoalBasedSchedule>()))
                .Returns(new ValidationResult(true));

            ExecutionGoalValidation.Instance.Clear();
            ExecutionGoalValidation.Instance.Add(mockValidation.Object);

            await this.controller.UpdateExecutionGoalTemplateAsync(this.mockExecutionGoalItem, CancellationToken.None);

            this.mockExecutionGoalDataManager.Verify(mgr => mgr.CreateExecutionGoalTemplateAsync(this.mockExecutionGoalItem, true, It.IsAny<CancellationToken>()));
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResultWhenAnExecutionGoalTemplateItemIsUpdatedSuccessfully()
        {
            Mock<IValidationRule<GoalBasedSchedule>> mockValidation = new Mock<IValidationRule<GoalBasedSchedule>>();
            mockValidation.Setup(validation => validation.Validate(It.IsAny<GoalBasedSchedule>()))
                .Returns(new ValidationResult(true));

            ExecutionGoalValidation.Instance.Clear();
            ExecutionGoalValidation.Instance.Add(mockValidation.Object);

            this.mockExecutionGoalDataManager.Setup(mgr => mgr.CreateExecutionGoalTemplateAsync(It.IsAny<Item<GoalBasedSchedule>>(), true, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(this.mockExecutionGoalItem));

            ObjectResult result = await this.controller.UpdateExecutionGoalTemplateAsync(this.mockExecutionGoalItem, CancellationToken.None)
                as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status201Created, result.StatusCode);
            Assert.IsTrue(object.ReferenceEquals(this.mockExecutionGoalItem, result.Value));
        }

        [Test]
        public async Task ControllerHandlesErrorsAsExpectedDuringExecutionGoalTemplateUpdate()
        {
            foreach (var entry in ExecutionGoalTemplateControllerTests.potentialErrors)
            {
                // Key = Exception
                // Value = The expected HTTP status code in the result
                this.mockExecutionGoalDataManager.Setup(mgr => mgr.CreateExecutionGoalTemplateAsync(It.IsAny<Item<GoalBasedSchedule>>(), true, It.IsAny<CancellationToken>()))
                    .Throws(entry.Key);

                IActionResult result = await this.controller.UpdateExecutionGoalTemplateAsync(this.mockExecutionGoalItem, CancellationToken.None);

                Assert.IsNotNull(result);
                Assert.AreEqual(entry.Value, (result as IStatusCodeActionResult).StatusCode);
                Assert.IsInstanceOf<ProblemDetails>((result as ObjectResult).Value);
            }
        }

        [Test]
        public async Task ControllerDeletesExpectedExecutionGoalTemplate()
        {
            await this.controller.DeleteExecutionGoalTemplateAsync(this.mockExecutionGoalItem.Id, this.mockExecutionGoal.TeamName, CancellationToken.None);

            this.mockExecutionGoalDataManager.Verify(mgr => mgr.DeleteExecutionGoalTemplateAsync(this.mockExecutionGoalItem.Id, this.mockExecutionGoal.TeamName, It.IsAny<CancellationToken>()));
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResultWhenAnExecutionGoalTemplateIsDeletedSuccessfully()
        {
            this.mockExecutionGoalDataManager.Setup(mgr => mgr.DeleteExecutionGoalTemplateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            StatusCodeResult result = await this.controller.DeleteExecutionGoalTemplateAsync("some string", "some other string", CancellationToken.None) as StatusCodeResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status204NoContent, result.StatusCode);
        }

        [Test]
        public async Task ControllerHandlesErrorsAsExpectedDuringExecutionGoalTemplateDeletion()
        {
            foreach (var entry in ExecutionGoalTemplateControllerTests.potentialErrors)
            {
                // Key = Exception
                // Value = The expected HTTP status code in the result
                this.mockExecutionGoalDataManager.Setup(mgr => mgr.DeleteExecutionGoalTemplateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Throws(entry.Key);

                IActionResult result = await this.controller.DeleteExecutionGoalTemplateAsync("string", "string2", CancellationToken.None);

                Assert.IsNotNull(result);
                Assert.AreEqual(entry.Value, (result as IStatusCodeActionResult).StatusCode);
                Assert.IsInstanceOf<ProblemDetails>((result as ObjectResult).Value);
            }
        }
    }
}