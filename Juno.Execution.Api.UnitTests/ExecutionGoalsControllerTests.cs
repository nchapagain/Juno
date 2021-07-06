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
    public class ExecutionGoalsControllerTests
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
        private TargetGoalTrigger mockTrigger;
        private TargetGoalTableEntity mockTableEntity;
        private ExperimentInstanceStatus experimentInstanceStatusTemplate;
        private Mock<IScheduleTimerDataManager> mockTargetGoalDataManager;
        private Mock<IScheduleDataManager> mockExecutionGoalDataManager;
        private Mock<IExperimentKustoTelemetryDataManager> mockExecutionGoalTelemetryDataManager;
        private ExecutionGoalsController controller;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetUpGoalBasedScheduleMocks();
            this.mockFixture.SetupExperimentMocks();

            GoalBasedSchedule template = this.mockFixture.Create<GoalBasedSchedule>();
            this.experimentInstanceStatusTemplate = this.mockFixture.Create<ExperimentInstanceStatus>();

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

            this.mockTableEntity = new TargetGoalTableEntity()
            {
                Id = this.mockTargetGoal.Name,
                PartitionKey = this.mockExecutionGoal.Version,
                RowKey = this.mockTargetGoal.Name,
                CronExpression = cronExpression,
                Enabled = this.mockTargetGoal.Enabled,
                TeamName = this.mockExecutionGoal.TeamName,
                ExecutionGoal = this.mockExecutionGoalItem.Id
            };

            this.mockTrigger = new TargetGoalTrigger(
                id: this.mockTableEntity.Id,
                executionGoal: this.mockTableEntity.ExecutionGoal,
                name: this.mockTableEntity.RowKey,
                cronExpression: this.mockTableEntity.CronExpression,
                enabled: this.mockTableEntity.Enabled,
                teamName: this.mockTableEntity.TeamName,
                version: this.mockTableEntity.PartitionKey,
                created: DateTime.UtcNow,
                lastModified: DateTime.UtcNow);

            this.mockExecutionGoalDataManager = new Mock<IScheduleDataManager>();
            this.mockTargetGoalDataManager = new Mock<IScheduleTimerDataManager>();
            this.mockExecutionGoalTelemetryDataManager = new Mock<IExperimentKustoTelemetryDataManager>();
            this.controller = new ExecutionGoalsController(this.mockExecutionGoalDataManager.Object, this.mockTargetGoalDataManager.Object, this.mockExecutionGoalTelemetryDataManager.Object, NullLogger.Instance);
        }

        [Test]
        public void ControllerConstructorValidatesParameters()
        {
            Assert.Throws<ArgumentException>(() => new ExecutionGoalsController(null, this.mockTargetGoalDataManager.Object, this.mockExecutionGoalTelemetryDataManager.Object));
            Assert.Throws<ArgumentException>(() => new ExecutionGoalsController(this.mockExecutionGoalDataManager.Object, null, this.mockExecutionGoalTelemetryDataManager.Object));
            Assert.Throws<ArgumentException>(() => new ExecutionGoalsController(this.mockExecutionGoalDataManager.Object, this.mockTargetGoalDataManager.Object, null));
        }

        [Test]
        public void ControllerConstructorSetsParametersToExpectedValues()
        {
            ExecutionGoalsController controller = new ExecutionGoalsController(this.mockExecutionGoalDataManager.Object, this.mockTargetGoalDataManager.Object, this.mockExecutionGoalTelemetryDataManager.Object);
            EqualityAssert.PropertySet(controller, "TargetGoalDataManager", this.mockTargetGoalDataManager.Object);
            EqualityAssert.PropertySet(controller, "ExecutionGoalDataManager", this.mockExecutionGoalDataManager.Object);
            EqualityAssert.PropertySet(controller, "Logger", NullLogger.Instance);
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
        public async Task ContollerCreatesTheExpectedExecutionGoalItem()
        {
            await this.controller.CreateExecutionGoalAsync(this.mockExecutionGoalItem, CancellationToken.None);

            this.mockExecutionGoalDataManager.Verify(mgr => mgr.CreateExecutionGoalAsync(
                It.Is<Item<GoalBasedSchedule>>(executionGoal => executionGoal.Equals(this.mockExecutionGoalItem)), It.IsAny<CancellationToken>()));
        }

        [Test]
        public async Task ControllerCreatesTheExpectedTargetGoalEntries()
        {
            await this.controller.CreateExecutionGoalAsync(this.mockExecutionGoalItem, CancellationToken.None);

            this.mockTargetGoalDataManager.Verify(mgr => mgr.CreateTargetGoalsAsync(
                It.Is<Item<GoalBasedSchedule>>(executionGoal => executionGoal.Equals(this.mockExecutionGoalItem)), It.IsAny<CancellationToken>()));
        }

        [Test]
        public async Task ControllerReturnsTheExpecedResultWhenAnExecutionGoalItemIsCreatedSuccessfully()
        {
            this.mockExecutionGoalDataManager.Setup(mgr => mgr.CreateExecutionGoalAsync(It.IsAny<Item<GoalBasedSchedule>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(this.mockExecutionGoalItem));

            CreatedAtActionResult result = await this.controller.CreateExecutionGoalAsync(this.mockExecutionGoalItem, CancellationToken.None)
                as CreatedAtActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status201Created, result.StatusCode);
            Assert.IsTrue(object.ReferenceEquals(this.mockExecutionGoalItem, result.Value));
        }

        [Test]
        public async Task ControllerHandlesErrorsAsExpectedDuringExecutionGoalCreation()
        {
            foreach (var entry in ExecutionGoalsControllerTests.potentialErrors)
            {
                // Key = Exception
                // Value = The expected HTTP status code in the result
                this.mockExecutionGoalDataManager.Setup(mgr => mgr.CreateExecutionGoalAsync(It.IsAny<Item<GoalBasedSchedule>>(), It.IsAny<CancellationToken>()))
                    .Throws(entry.Key);

                IActionResult result = await this.controller.CreateExecutionGoalAsync(this.mockExecutionGoalItem, CancellationToken.None);

                Assert.IsNotNull(result);
                Assert.AreEqual(entry.Value, (result as IStatusCodeActionResult).StatusCode);
                Assert.IsInstanceOf<ProblemDetails>((result as ObjectResult).Value);
            }
        }

        [Test]
        public void ControllerValidatesNonStringParametersWhenCreatingExecutionGoalFromTemplate()
        {
            Assert.ThrowsAsync<ArgumentException>(() => this.controller.CreateExecutionGoalFromTemplateAsync("template", "teamName", null, CancellationToken.None));
        }

        [Test]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase(null)]
        public void ControllerValidatesStringParametersWhenCreatingExecutionGoalFromTemplate(string invalidParameter)
        {
            string validString = "ThisIsAString";
            var targetGoalParameter = new List<TargetGoalParameter>()
            {
            new TargetGoalParameter(validString, false, new Dictionary<string, IConvertible>())
            };

            ExecutionGoalParameter executionGoalParameter = new ExecutionGoalParameter(targetGoalParameter);

            Assert.ThrowsAsync<ArgumentException>(() => this.controller.CreateExecutionGoalFromTemplateAsync("string", invalidParameter, executionGoalParameter, CancellationToken.None));
            Assert.ThrowsAsync<ArgumentException>(() => this.controller.CreateExecutionGoalFromTemplateAsync(invalidParameter, "string", executionGoalParameter, CancellationToken.None));
            Assert.ThrowsAsync<ArgumentException>(() => this.controller.CreateExecutionGoalFromTemplateAsync("string", "string", null, CancellationToken.None));
        }

        [Test]
        public async Task ControllerCreatesExpectedExecutionGoalFromTemplate()
        {
            var executionGoalToCopy = FixtureExtensions.CreateExecutionGoalTemplate();
            List<TargetGoal> targetGoals = new List<TargetGoal>()
            {
                ExecutionGoalsControllerTests.CreateValidExecutionGoalTemplateTargetGoal(1),
                ExecutionGoalsControllerTests.CreateValidExecutionGoalTemplateTargetGoal(2),
                ExecutionGoalsControllerTests.CreateValidExecutionGoalTemplateTargetGoal(3),
            };

            var metadata = new Dictionary<string, IConvertible>()
            {
                ["datecreated"] = "08/9/2020",
                ["intent"] = "To test the scope of this parameter",
                ["experimentCategory"] = "A-Z",
                ["teamName"] = "CRC AIR",
                ["imtestingNewCategory"] = "$.parameters.imtestingNewCategory",
                ["owner"] = "experimentOwner@microsoft.com",
                ["experiment.name"] = "my experiment"
            };

            GoalBasedSchedule template = new GoalBasedSchedule(
                experimentName: "MCU2020 Template",
                description: "an execution goal",
                experiment: executionGoalToCopy.Experiment,
                targetGoals: targetGoals,
                controlGoals: executionGoalToCopy.ControlGoals,
                metadata: metadata);

            Item<GoalBasedSchedule> templateItem = new Item<GoalBasedSchedule>(Guid.NewGuid().ToString(), template);

            List<TargetGoalParameter> targetGoalParamater = new List<TargetGoalParameter>()
            {
                new TargetGoalParameter("name1", true, new Dictionary<string, IConvertible>()
                {
                    { "targetInstances", 19 },
                    { "nodeCpuId", Guid.NewGuid().ToString() }
                }),
                new TargetGoalParameter("name2", false, new Dictionary<string, IConvertible>()
                {
                    { "targetInstances", 12 },
                    { "nodeCpuId", Guid.NewGuid().ToString() }
                }),
                new TargetGoalParameter("name3", false, new Dictionary<string, IConvertible>()
                {
                    { "targetInstances", "6" },
                    { "nodeCpuId", Guid.NewGuid().ToString() }
                })
            };

            ExecutionGoalParameter parameters = new ExecutionGoalParameter(targetGoalParamater, metadata, template.Parameters);

            Item<GoalBasedSchedule> expectedExecutionGoal = new Item<GoalBasedSchedule>(Guid.NewGuid().ToString(), template.Inlined(parameters));

            this.mockExecutionGoalDataManager.Setup(mgr => mgr.GetExecutionGoalTemplateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(templateItem));

            this.mockExecutionGoalDataManager.Setup(mgr => mgr.CreateExecutionGoalAsync(It.Is<Item<GoalBasedSchedule>>(entity => !entity.Definition.IsInlined()), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(expectedExecutionGoal));

            CreatedAtActionResult result = await this.controller.CreateExecutionGoalFromTemplateAsync("template", template.TeamName, parameters, CancellationToken.None)
                as CreatedAtActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status201Created, result.StatusCode);
            Assert.AreEqual(result.Value as Item<GoalBasedSchedule>, expectedExecutionGoal);

            this.mockExecutionGoalDataManager.Verify(
                mgr => mgr.CreateExecutionGoalAsync(
                It.IsAny<Item<GoalBasedSchedule>>(),
                It.IsAny<CancellationToken>()),
                Times.Once());
        }

        [Test]
        public async Task ControllerCreatesExpectedExecutionGoalFromTemplateOnlyForTargetGoalParametersProvided()
        {
            var executionGoalToCopy = FixtureExtensions.CreateExecutionGoalTemplate();

            List<TargetGoal> targetGoals = new List<TargetGoal>()
            {
                ExecutionGoalsControllerTests.CreateValidExecutionGoalTemplateTargetGoal(1),
                ExecutionGoalsControllerTests.CreateValidExecutionGoalTemplateTargetGoal(2),
                ExecutionGoalsControllerTests.CreateValidExecutionGoalTemplateTargetGoal(3),
            };

            var metadata = new Dictionary<string, IConvertible>()
            {
                ["datecreated"] = "08/9/2020",
                ["intent"] = "To test the scope of this parameter",
                ["experimentCategory"] = "A-Z",
                ["teamName"] = "CRC AIR",
                ["experiment.name"] = "my experiment"
            };

            GoalBasedSchedule template = new GoalBasedSchedule(
                experimentName: "MCU2020 Template",
                description: "New Execution Goal template to schedule the MCU2020.",
                experiment: executionGoalToCopy.Experiment,
                targetGoals: targetGoals,
                controlGoals: executionGoalToCopy.ControlGoals,
                parameters: executionGoalToCopy.Parameters,
                metadata: metadata);

            Item<GoalBasedSchedule> templateItem = new Item<GoalBasedSchedule>(Guid.NewGuid().ToString(), template);

            List<TargetGoalParameter> targetGoalParamater = new List<TargetGoalParameter>()
            {
                new TargetGoalParameter("name1", true, new Dictionary<string, IConvertible>()
                {
                    { "targetInstances", 19 },
                    { "nodeCpuId", Guid.NewGuid().ToString() }
                })
            };

            ExecutionGoalParameter parameters = new ExecutionGoalParameter(targetGoalParamater, metadata);

            Item<GoalBasedSchedule> expectedExecutionGoal = new Item<GoalBasedSchedule>(Guid.NewGuid().ToString(), template.Inlined(parameters));

            this.mockExecutionGoalDataManager.Setup(mgr => mgr.GetExecutionGoalTemplateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(templateItem));

            this.mockExecutionGoalDataManager.Setup(mgr => mgr.CreateExecutionGoalAsync(It.Is<Item<GoalBasedSchedule>>(entity => !entity.Definition.IsInlined()), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(expectedExecutionGoal));

            CreatedAtActionResult result = await this.controller.CreateExecutionGoalFromTemplateAsync("template", template.TeamName, parameters, CancellationToken.None)
                as CreatedAtActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status201Created, result.StatusCode);
            Assert.AreEqual(result.Value as Item<GoalBasedSchedule>, expectedExecutionGoal);

            this.mockExecutionGoalDataManager.Verify(mgr => mgr.CreateExecutionGoalAsync(
                It.IsAny<Item<GoalBasedSchedule>>(),
                It.IsAny<CancellationToken>()));
        }

        [Test]
        public async Task ControllerHandlesErrorsAsExpectedDuringExecutionGoalCreationFromTemplate()
        {
            GoalBasedSchedule template = FixtureExtensions.CreateExecutionGoalTemplate();
            ExecutionGoalSummary executionGoalMetadata = FixtureExtensions.CreateExecutionGoalMetadata();
            ExecutionGoalParameter parameters = new ExecutionGoalParameter(executionGoalMetadata.ParameterNames.TargetGoals, sharedParameters: template.Parameters);
            foreach (var entry in ExecutionGoalsControllerTests.potentialErrors)
            {
                // Key = Exception
                // Value = The expected HTTP status code in the result
                this.mockExecutionGoalDataManager.Setup(mgr => mgr.GetExecutionGoalTemplateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Throws(entry.Key);

                IActionResult result = await this.controller.CreateExecutionGoalFromTemplateAsync("template", template.TeamName, parameters, CancellationToken.None);

                Assert.IsNotNull(result);
                Assert.AreEqual(entry.Value, (result as IStatusCodeActionResult).StatusCode);
                Assert.IsInstanceOf<ProblemDetails>((result as ObjectResult).Value);
            }
        }

        [Test]
        public async Task ControllerGetsTheExpectedExecutionGoal()
        {
            await this.controller.GetExecutionGoalsAsync(CancellationToken.None, this.mockExecutionGoal.TeamName, this.mockExecutionGoalItem.Id);

            this.mockExecutionGoalDataManager.Verify(mgr => mgr.GetExecutionGoalAsync(
                this.mockExecutionGoalItem.Id,
                this.mockExecutionGoal.TeamName,
                It.IsAny<CancellationToken>()));
        }

        [Test]
        public async Task ControllerGetsTheExpectedExecutionGoalStatusWhenExecutionGoalViewIsFull()
        {
            await this.controller.GetExecutionGoalsAsync(CancellationToken.None, this.mockExecutionGoal.TeamName, this.mockExecutionGoalItem.Id, ExecutionGoalView.Full);

            this.mockExecutionGoalDataManager.Verify(mgr => mgr.GetExecutionGoalAsync(
                this.mockExecutionGoalItem.Id,
                this.mockExecutionGoal.TeamName,
                It.IsAny<CancellationToken>()));
        }

        [Test]
        public async Task ControllerGetsTheExpectedExecutionGoalStatusWhenExecutionGoalViewIsStatus()
        {
            await this.controller.GetExecutionGoalsAsync(CancellationToken.None, this.mockExecutionGoal.TeamName, this.mockExecutionGoalItem.Id, ExecutionGoalView.Status);

            this.mockExecutionGoalTelemetryDataManager.Verify(mgr => mgr.GetExecutionGoalStatusAsync(
                this.mockExecutionGoalItem.Id,
                It.IsAny<CancellationToken>(),
                this.mockExecutionGoal.TeamName));
        }

        [Test]
        public async Task ControllerGetsTheExpectedExecutionGoalStatusWhenExecutionGoalViewIsSummary()
        {
            await this.controller.GetExecutionGoalsAsync(CancellationToken.None, this.mockExecutionGoal.TeamName, this.mockExecutionGoalItem.Id, ExecutionGoalView.Summary);

            this.mockExecutionGoalDataManager.Verify(mgr => mgr.GetExecutionGoalsInfoAsync(
                It.IsAny<CancellationToken>(),
                this.mockExecutionGoal.TeamName,
                this.mockExecutionGoalItem.Id));
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResultWhenAnExecutionGoalIsretrievedSuccessfully()
        {
            this.mockExecutionGoalDataManager.Setup(mgr => mgr.GetExecutionGoalAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(this.mockExecutionGoalItem));

            ObjectResult result = await this.controller.GetExecutionGoalsAsync(CancellationToken.None, "Team name", "Experiment ID") as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.IsTrue(object.ReferenceEquals(this.mockExecutionGoalItem, result.Value));
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResultWhenAnExperimentInstanceStatusIsretrievedSuccessfully()
        {
            IList<ExperimentInstanceStatus> resultTemplate = new List<ExperimentInstanceStatus>() { this.experimentInstanceStatusTemplate };
            this.mockExecutionGoalTelemetryDataManager.Setup(mgr => mgr.GetExecutionGoalStatusAsync(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .Returns(Task.FromResult(resultTemplate));

            ObjectResult result = await this.controller.GetExecutionGoalsAsync(CancellationToken.None, this.mockExecutionGoal.TeamName, this.mockExecutionGoalItem.Id, ExecutionGoalView.Status) as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.IsTrue(object.ReferenceEquals(resultTemplate, result.Value));
        }

        [Test]
        public async Task ControllerHandlesErrorsAsexpectedWhenGettingAnExecutionGoal()
        {
            foreach (var entry in ExecutionGoalsControllerTests.potentialErrors)
            {
                // Key = Exception
                // Value = The expected HTTP status code in the result
                this.mockExecutionGoalDataManager.Setup(mgr => mgr.GetExecutionGoalAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Throws(entry.Key);

                IActionResult result = await this.controller.GetExecutionGoalsAsync(CancellationToken.None, "string2", "string");

                Assert.IsNotNull(result);
                Assert.AreEqual(entry.Value, (result as IStatusCodeActionResult).StatusCode);
                Assert.IsInstanceOf<ProblemDetails>((result as ObjectResult).Value);
            }
        }

        [Test]
        public async Task ControllerGetsTheExpectedExecutionGoals()
        {
            this.mockTargetGoalDataManager.Setup(mgr => mgr.GetTargetGoalTriggersAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new List<TargetGoalTrigger>() { this.mockTrigger } as IEnumerable<TargetGoalTrigger>));

            await this.controller.GetExecutionGoalsAsync(CancellationToken.None, this.mockExecutionGoal.TeamName);

            this.mockExecutionGoalDataManager.Verify(
                mgr => mgr.GetExecutionGoalsAsync(
                It.IsAny<CancellationToken>(),
                It.IsAny<string>()),
                Times.Once());
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResultWhenExecutionGoalsAreretrievedSuccessfully()
        {
            this.mockExecutionGoalDataManager.Setup(mgr => mgr.GetExecutionGoalsAsync(It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .Returns(Task.FromResult(new List<Item<GoalBasedSchedule>>() { this.mockExecutionGoalItem } as IEnumerable<Item<GoalBasedSchedule>>));
            this.mockTargetGoalDataManager.Setup(mgr => mgr.GetTargetGoalTriggersAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new List<TargetGoalTrigger> { this.mockTrigger } as IEnumerable<TargetGoalTrigger>));

            ObjectResult result = await this.controller.GetExecutionGoalsAsync(CancellationToken.None, this.mockTrigger.TeamName) as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.AreEqual(new List<Item<GoalBasedSchedule>>() { this.mockExecutionGoalItem } as IEnumerable<Item<GoalBasedSchedule>>, result.Value);
        }

        [Test]
        public async Task ControllerHandlesErrorsAsexpectedWhenGettingExecutionGoals()
        {
            foreach (var entry in ExecutionGoalsControllerTests.potentialErrors)
            {
                this.mockExecutionGoalDataManager.Setup(mgr => mgr.GetExecutionGoalsAsync(It.IsAny<CancellationToken>(), It.IsAny<string>()))
                    .Throws(entry.Key);

                IActionResult result = await this.controller.GetExecutionGoalsAsync(CancellationToken.None, "string2");

                Assert.IsNotNull(result);
                Assert.AreEqual(entry.Value, (result as IStatusCodeActionResult).StatusCode);
                Assert.IsInstanceOf<ProblemDetails>((result as ObjectResult).Value);
            }
        }

        [Test]
        public async Task ControllerValidatesTheTargetGoalsOfAnExecutionGoalBeforeUpdate()
        {
            Mock<IValidationRule<GoalBasedSchedule>> mockValidation = new Mock<IValidationRule<GoalBasedSchedule>>();
            mockValidation.Setup(validation => validation.Validate(It.IsAny<GoalBasedSchedule>()))
                .Returns(new ValidationResult(false));

            ExecutionGoalValidation.Instance.Add(mockValidation.Object);
            ObjectResult result = await this.controller.UpdateExecutionGoalAsync(this.mockExecutionGoalItem, CancellationToken.None) as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status400BadRequest, result.StatusCode);
        }

        [Test]
        public async Task ControllerUpdatesTheExpectedExecutionGoalItem()
        {
            await this.controller.UpdateExecutionGoalAsync(this.mockExecutionGoalItem, CancellationToken.None);

            this.mockExecutionGoalDataManager.Verify(mgr => mgr.UpdateExecutionGoalAsync(this.mockExecutionGoalItem, It.IsAny<CancellationToken>()));
        }

        [Test]
        public async Task ControllerUpdatesTheExpectedTargetGoals()
        {
            await this.controller.UpdateExecutionGoalAsync(this.mockExecutionGoalItem, CancellationToken.None);

            this.mockTargetGoalDataManager.Verify(mgr => mgr.UpdateTargetGoalTriggersAsync(this.mockExecutionGoalItem, It.IsAny<CancellationToken>()));
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResultWhenAnExecutionGoalItemIsUpdatedSuccessfully()
        {
            this.mockExecutionGoalDataManager.Setup(mgr => mgr.UpdateExecutionGoalAsync(It.IsAny<Item<GoalBasedSchedule>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(this.mockExecutionGoalItem));

            ObjectResult result = await this.controller.UpdateExecutionGoalAsync(this.mockExecutionGoalItem, CancellationToken.None)
                as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.IsTrue(object.ReferenceEquals(this.mockExecutionGoalItem, result.Value));
        }

        [Test]
        public async Task ControllerHandlesErrorsAsExpectedDuringExecutionGoalUpdate()
        {
            foreach (var entry in ExecutionGoalsControllerTests.potentialErrors)
            {
                // Key = Exception
                // Value = The expected HTTP status code in the result
                this.mockExecutionGoalDataManager.Setup(mgr => mgr.UpdateExecutionGoalAsync(It.IsAny<Item<GoalBasedSchedule>>(), It.IsAny<CancellationToken>()))
                    .Throws(entry.Key);

                IActionResult result = await this.controller.UpdateExecutionGoalAsync(this.mockExecutionGoalItem, CancellationToken.None);

                Assert.IsNotNull(result);
                Assert.AreEqual(entry.Value, (result as IStatusCodeActionResult).StatusCode);
                Assert.IsInstanceOf<ProblemDetails>((result as ObjectResult).Value);
            }
        }

        [Test]
        public async Task ControllerDeletesExpectedExecutionGoal()
        {
            await this.controller.DeleteExecutionGoalAsync(this.mockExecutionGoalItem.Id, this.mockExecutionGoal.TeamName, CancellationToken.None);

            this.mockExecutionGoalDataManager.Verify(mgr => mgr.DeleteExecutionGoalAsync(this.mockExecutionGoalItem.Id, this.mockExecutionGoal.TeamName, It.IsAny<CancellationToken>()));
        }

        [Test]
        public async Task ControllerDeletesExpectedTargetGoals()
        {
            await this.controller.DeleteExecutionGoalAsync(this.mockExecutionGoalItem.Id, this.mockExecutionGoal.TeamName, CancellationToken.None);

            this.mockTargetGoalDataManager.Verify(mgr => mgr.DeleteTargetGoalTriggersAsync(this.mockExecutionGoalItem.Id, It.IsAny<CancellationToken>()));
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResultWhenAnExecutionGoalIsDeletedSuccessfully()
        {
            this.mockExecutionGoalDataManager.Setup(mgr => mgr.DeleteExecutionGoalAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            this.mockTargetGoalDataManager.Setup(mgr => mgr.DeleteTargetGoalTriggersAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            StatusCodeResult result = await this.controller.DeleteExecutionGoalAsync("some string", "some other string", CancellationToken.None) as StatusCodeResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status204NoContent, result.StatusCode);
        }

        [Test]
        public async Task ControllerHandlesErrorsAsExpectedDuringExecutionGoalDeletion()
        {
            foreach (var entry in ExecutionGoalsControllerTests.potentialErrors)
            {
                // Key = Exception
                // Value = The expected HTTP status code in the result
                this.mockExecutionGoalDataManager.Setup(mgr => mgr.DeleteExecutionGoalAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Throws(entry.Key);

                IActionResult result = await this.controller.DeleteExecutionGoalAsync("string", "string2", CancellationToken.None);

                Assert.IsNotNull(result);
                Assert.AreEqual(entry.Value, (result as IStatusCodeActionResult).StatusCode);
                Assert.IsInstanceOf<ProblemDetails>((result as ObjectResult).Value);
            }
        }

        private static TargetGoal CreateValidExecutionGoalTemplateTargetGoal(int value)
        {
            return new (
                name: $"name{value}",
                true,
                preconditions: new List<Precondition>()
                {
                    new Precondition(
                        type: ContractExtension.TimerTriggerType,
                        parameters: new Dictionary<string, IConvertible>()
                        {
                            [ContractExtension.CronExpression] = "* * * * *"
                        }),
                    new Precondition(
                        type: ContractExtension.SuccessfulExperimentsProvider,
                        new Dictionary<string, IConvertible>()
                        {
                            ["targetExperimentInstances"] = "$.parameters.targetInstances"
                        })
                },
                actions: new List<ScheduleAction>()
                {
                    new ScheduleAction(
                        type: ContractExtension.SelectEnvironmentAndCreateExperimentProvider,
                        new Dictionary<string, IConvertible>()
                        {
                            ["metadata.nodeCpuId"] = "$.parameters.nodeCpuId",
                            ["metadata.workload"] = $"Workload{value}",
                            ["metadata.payloadPFVersion"] = $"PayloadPFVersion{value}",
                            ["guestAgentVersion"] = $"GuestAgentVersion{value}",
                            ["guestAgentPlatform"] = $"gaPlatform{value}"
                        })
                },
                id: $"targetGoalId{value}");
        }
    }
}