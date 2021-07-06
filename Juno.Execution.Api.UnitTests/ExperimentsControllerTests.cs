namespace Juno.Execution.Api
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Contracts;
    using Juno.Contracts.Validation;
    using Juno.Providers.Workloads;
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
    public class ExperimentsControllerTests
    {
        // This provides a mapping of all known potential errors mapped to the HTTP status
        // code expected in the result. Each controller method handles exceptions in consistent
        // ways, so this is applicable to all controller methods.
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

        private ExperimentsController controller;
        private Fixture mockFixture;
        private FixtureDependencies mockDependencies;
        private Experiment mockExperiment;
        private ExperimentInstance mockExperimentInstance;
        private ExperimentStepInstance mockExperimentStepInstance;
        private List<ExperimentStepInstance> mockExperimentStepInstances;
        private ExperimentMetadata mockExperimentContext;
        private ExperimentMetadataInstance mockExperimentContextInstance;
        private string mockAgentId;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks();
            this.mockDependencies = new FixtureDependencies();
            this.controller = new ExperimentsController(this.mockDependencies.DataManager.Object, NullLogger.Instance);

            this.mockExperiment = this.mockFixture.Create<Experiment>();
            this.mockExperimentInstance = this.mockFixture.Create<ExperimentInstance>();
            this.mockExperimentStepInstance = this.mockFixture.Create<ExperimentStepInstance>();
            this.mockExperimentContext = this.mockFixture.Create<ExperimentMetadata>();
            this.mockExperimentContextInstance = this.mockFixture.Create<ExperimentMetadataInstance>();
            this.mockExperimentStepInstances = new List<ExperimentStepInstance>
            {
                this.mockExperimentStepInstance
            };

            // Setup the default return values for the controller experiment data manager. Given there are
            // no custom setups in the test methods, the data manager will return the following values by
            // default.
            this.mockDependencies.DataManager.SetReturnsDefault(Task.FromResult(this.mockExperimentInstance));
            this.mockAgentId = "Cluster01,Node01,VM01,TiPSession01";
        }

        [Test]
        public void ControllerContructorsValidateParameters()
        {
            Assert.Throws<ArgumentException>(() => new ExperimentsController(null));
        }

        [Test]
        public void ControllerConstructorSetsParametersToExpectedValues()
        {
            ExperimentsController controller = new ExperimentsController(this.mockDependencies.DataManager.Object);
            EqualityAssert.PropertySet(controller, "DataManager", this.mockDependencies.DataManager.Object);
            EqualityAssert.PropertySet(controller, "Logger", NullLogger.Instance);

            controller = new ExperimentsController(this.mockDependencies.DataManager.Object, this.mockDependencies.Logger.Object);

            EqualityAssert.PropertySet(controller, "DataManager", this.mockDependencies.DataManager.Object);
            EqualityAssert.PropertySet(controller, "Logger", this.mockDependencies.Logger.Object);
        }

        [Test]
        public async Task ControllerValidatesTheSchemaOfAnExperimentBeforeCreation()
        {
            Mock<IValidationRule<Experiment>> mockValidation = new Mock<IValidationRule<Experiment>>();
            mockValidation.Setup(validation => validation.Validate(It.IsAny<Experiment>()))
                .Returns(new ValidationResult(false));

            ExperimentValidation.Instance.Add(mockValidation.Object);
            ObjectResult result = await this.controller.CreateExperimentAsync(this.mockExperiment, CancellationToken.None) as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status400BadRequest, result.StatusCode);
        }

        [Test]
        public async Task ControllerCreatesTheExpectedExperimentInstance()
        {
            Experiment expectedExperiment = this.mockExperiment.Inlined();

            // Notes:
            // We do not have a consensus on where the recommendation ID should be created or the format of it. We are
            // leaving this here for reference as we come back to this so that we do not lose track of the work we did
            // to integrate it and can simply refactor that to match the requisite semantics in the future.
            //
            // The controller adds a recommendation ID to the experiment metadata. So that we can
            // do an equality comparison at the end we have to ensure the metatdata will match.
            // expectedExperiment.AddRecommendationId();

            await this.controller.CreateExperimentAsync(this.mockExperiment, CancellationToken.None);

            this.mockDependencies.DataManager.Verify(
                mgr => mgr.CreateExperimentAsync(
                    It.Is<Experiment>(experiment => experiment.Equals(expectedExperiment)), It.IsAny<CancellationToken>()));
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResultWhenAnExperimentInstanceIsCreatedSuccessfully()
        {
            // Setup data manaager to return a valid experiment instance upon the creation
            // of an experiment.
            this.mockDependencies.DataManager
                .Setup(mgr => mgr.CreateExperimentAsync(It.IsAny<Experiment>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(this.mockExperimentInstance);

            CreatedAtActionResult result = await this.controller.CreateExperimentAsync(this.mockExperiment, CancellationToken.None)
                as CreatedAtActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status201Created, result.StatusCode);
            Assert.IsTrue(object.ReferenceEquals(this.mockExperimentInstance, result.Value));
        }

        [Test]
        public async Task ControllerHandlesErrorsAsExpectedDuringExperimentCreation()
        {
            foreach (var entry in ExperimentsControllerTests.potentialErrors)
            {
                // Key = Exception
                // Value = The expected HTTP status code in the result
                this.mockDependencies.DataManager
                    .Setup(mgr => mgr.CreateExperimentAsync(It.IsAny<Experiment>(), It.IsAny<CancellationToken>()))
                    .Throws(entry.Key);

                IActionResult result = await this.controller.CreateExperimentAsync(this.mockExperiment, CancellationToken.None);

                Assert.IsNotNull(result);
                Assert.AreEqual(entry.Value, (result as IStatusCodeActionResult).StatusCode);
                Assert.IsInstanceOf<ProblemDetails>((result as ObjectResult).Value);
            }
        }

        [Test]
        public void ControllerValidatesRequiredParametersWhenCreatingExperiment()
        {
            Assert.ThrowsAsync<ArgumentException>(async () => await this.controller.CreateExperimentAsync(null, It.IsAny<CancellationToken>()));
        }

        // Notes:
        // We do not have a consensus on where the recommendation ID should be created or the format of it. We are
        // leaving this here for reference as we come back to this so that we do not lose track of the work we did
        // to integrate it and can simply refactor that to match the requisite semantics in the future.
        ////[Test]
        ////public async Task ControllerAddsARecommendationIdToExperimentsDuringCreation()
        ////{
        ////    bool confirmed = false;
        ////    Assert.IsFalse(this.mockExperiment.Metadata.ContainsKey(MetadataProperty.RecommendationId));

        ////    this.mockDependencies.DataManager
        ////            .Setup(mgr => mgr.CreateExperimentAsync(It.IsAny<Experiment>(), It.IsAny<CancellationToken>()))
        ////            .Callback<Experiment, CancellationToken>((experiment, token) =>
        ////            {
        ////                confirmed = experiment.Metadata.ContainsKey(MetadataProperty.RecommendationId);
        ////            });

        ////    await this.controller.CreateExperimentAsync(this.mockExperiment, CancellationToken.None);

        ////    Assert.IsTrue(confirmed);
        ////}

        [Test]
        public async Task ControllerCreatesTheExpectedExperimentSharedContext()
        {
            await this.controller.CreateExperimentContextAsync(
                this.mockExperimentInstance.Id, this.mockExperimentContext, CancellationToken.None);

            this.mockDependencies.DataManager.Verify(mgr => mgr.CreateExperimentContextAsync(
                this.mockExperimentContext, It.IsAny<CancellationToken>(), null));
        }

        [Test]
        public async Task ControllerCreatesTheExpectedExperimentStepContext()
        {
            await this.controller.CreateExperimentContextAsync(
                this.mockExperimentInstance.Id, this.mockExperimentContext, CancellationToken.None, this.mockExperimentStepInstance.Id);

            this.mockDependencies.DataManager.Verify(mgr => mgr.CreateExperimentContextAsync(
                this.mockExperimentContext, It.IsAny<CancellationToken>(), this.mockExperimentStepInstance.Id));
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResultWhenAnExperimentContextInstanceIsCreatedSuccessfully()
        {
            // Setup data manaager to return a valid experiment context instance.
            this.mockDependencies.DataManager
                .Setup(mgr => mgr.CreateExperimentContextAsync(It.IsAny<ExperimentMetadata>(), It.IsAny<CancellationToken>(), null))
                .ReturnsAsync(this.mockExperimentContextInstance);

            CreatedAtActionResult result = await this.controller.CreateExperimentContextAsync(
                this.mockExperimentInstance.Id, this.mockExperimentContext, CancellationToken.None) as CreatedAtActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status201Created, result.StatusCode);
            Assert.IsTrue(object.ReferenceEquals(this.mockExperimentContextInstance, result.Value));
        }

        [Test]
        public async Task ControllerHandlesErrorsAsExpectedDuringExperimentContextCreation()
        {
            foreach (var entry in ExperimentsControllerTests.potentialErrors)
            {
                // Key = Exception
                // Value = The expected HTTP status code in the result
                this.mockDependencies.DataManager
                    .Setup(mgr => mgr.CreateExperimentContextAsync(It.IsAny<ExperimentMetadata>(), It.IsAny<CancellationToken>(), null))
                    .Throws(entry.Key);

                IActionResult result = await this.controller.CreateExperimentContextAsync(
                    this.mockExperimentInstance.Id, this.mockExperimentContext, CancellationToken.None);

                Assert.IsNotNull(result);
                Assert.AreEqual(entry.Value, (result as IStatusCodeActionResult).StatusCode);
                Assert.IsInstanceOf<ProblemDetails>((result as ObjectResult).Value);
            }
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void ControllerValidatesRequiredParametersWhenCreatingExperimentContext(string invalidParameter)
        {
            Assert.ThrowsAsync<ArgumentException>(async () => await this.controller.CreateExperimentContextAsync(invalidParameter, It.IsAny<ExperimentMetadata>(), It.IsAny<CancellationToken>(), It.IsAny<string>()));
            Assert.ThrowsAsync<ArgumentException>(async () => await this.controller.CreateExperimentContextAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>(), It.IsAny<string>()));
        }

        [Test]
        public async Task ControllerCreatesTheExpectedExperimentSteps()
        {
            await this.controller.CreateExperimentStepsAsync(this.mockExperimentInstance.Id, CancellationToken.None);

            this.mockDependencies.DataManager.Verify(mgr => mgr.CreateExperimentStepsAsync(
                this.mockExperimentInstance, It.IsAny<CancellationToken>()));
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResultWhenExperimentStepInstancesAreCreatedSuccessfully()
        {
            IEnumerable<ExperimentStepInstance> expectedSteps = new List<ExperimentStepInstance>
            {
                this.mockExperimentStepInstance,
                this.mockExperimentStepInstance
            };

            // Setup data manaager to return a valid experiment step instances.
            this.mockDependencies.DataManager
                .Setup(mgr => mgr.CreateExperimentStepsAsync(It.IsAny<ExperimentInstance>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedSteps);

            CreatedAtActionResult result = await this.controller.CreateExperimentStepsAsync(
                this.mockExperimentInstance.Id, CancellationToken.None) as CreatedAtActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status201Created, result.StatusCode);

            IEnumerable<ExperimentStepInstance> stepsCreated = result.Value as IEnumerable<ExperimentStepInstance>;
            Assert.IsNotNull(stepsCreated);
            Assert.IsTrue(stepsCreated.Count() == stepsCreated.Count());
            Assert.IsTrue(object.ReferenceEquals(expectedSteps.ElementAt(0), stepsCreated.ElementAt(0)));
            Assert.IsTrue(object.ReferenceEquals(expectedSteps.ElementAt(1), stepsCreated.ElementAt(1)));
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResultWhenExperimentStepInstancesAreCreatedSuccessfullyFromAComponentDefinition_Scenario1()
        {
            IEnumerable<ExperimentStepInstance> expectedSteps = new List<ExperimentStepInstance>
            {
                // 1:1 - a single experiment step created from the component definition
                this.mockExperimentStepInstance
            };

            // Setup data manager to return a valid experiment step instances.
            this.mockDependencies.DataManager
                .Setup(mgr => mgr.CreateExperimentStepsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<ExperimentComponent>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedSteps);

            CreatedAtActionResult result = await this.controller.CreateExperimentStepsAsync(
                this.mockExperimentInstance.Id, CancellationToken.None, this.mockExperimentStepInstance.Definition, 0) as CreatedAtActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status201Created, result.StatusCode);

            IEnumerable<ExperimentStepInstance> stepsCreated = result.Value as IEnumerable<ExperimentStepInstance>;
            Assert.IsNotNull(stepsCreated);
            Assert.IsTrue(stepsCreated.Count() == stepsCreated.Count());
            Assert.IsTrue(object.ReferenceEquals(expectedSteps.ElementAt(0), stepsCreated.ElementAt(0)));
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResultWhenExperimentStepInstancesAreCreatedSuccessfullyFromAComponentDefinition_Scenario2()
        {
            IEnumerable<ExperimentStepInstance> expectedSteps = new List<ExperimentStepInstance>
            {
                // 1:many - multiple experiment steps created from the component definition
                this.mockExperimentStepInstance,
                this.mockExperimentStepInstance
            };

            // Setup data manaager to return a valid experiment step instances.
            this.mockDependencies.DataManager
                .Setup(mgr => mgr.CreateExperimentStepsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<ExperimentComponent>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedSteps);

            CreatedAtActionResult result = await this.controller.CreateExperimentStepsAsync(
                this.mockExperimentInstance.Id, CancellationToken.None, this.mockExperimentStepInstance.Definition, 0) as CreatedAtActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status201Created, result.StatusCode);

            IEnumerable<ExperimentStepInstance> stepsCreated = result.Value as IEnumerable<ExperimentStepInstance>;
            Assert.IsNotNull(stepsCreated);
            Assert.IsTrue(stepsCreated.Count() == stepsCreated.Count());
            Assert.IsTrue(object.ReferenceEquals(expectedSteps.ElementAt(0), stepsCreated.ElementAt(0)));
            Assert.IsTrue(object.ReferenceEquals(expectedSteps.ElementAt(1), stepsCreated.ElementAt(1)));
        }

        [Test]
        public void ControllerValidatesRequiredParametersWhenCreatingExperimentSteps()
        {
            Assert.ThrowsAsync<ArgumentException>(async () => await this.controller.CreateExperimentStepsAsync(null, It.IsAny<CancellationToken>()));
        }

        [Test]
        public async Task ControllerHandlesErrorsAsExpectedDuringExperimentStepsCreation()
        {
            foreach (var entry in ExperimentsControllerTests.potentialErrors)
            {
                // Key = Exception
                // Value = The expected HTTP status code in the result
                this.mockDependencies.DataManager
                    .Setup(mgr => mgr.CreateExperimentStepsAsync(It.IsAny<ExperimentInstance>(), It.IsAny<CancellationToken>()))
                    .Throws(entry.Key);

                IActionResult result = await this.controller.CreateExperimentStepsAsync(
                    this.mockExperimentInstance.Id, CancellationToken.None);

                Assert.IsNotNull(result);
                Assert.AreEqual(entry.Value, (result as IStatusCodeActionResult).StatusCode);
                Assert.IsInstanceOf<ProblemDetails>((result as ObjectResult).Value);
            }
        }

        [Test]
        public async Task ControllerWillNotCreateExperimentStepsUnlessAMatchingExperimentExists()
        {
            // Setup the data manager to indicate an experiment does not exist.  In most implementations,
            // the data manager throws an exception.  The controller handles the case where this is not true.
            this.mockDependencies.DataManager
                .Setup(mgr => mgr.GetExperimentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(null as ExperimentInstance);

            IActionResult result = await this.controller.CreateExperimentStepsAsync(this.mockExperimentInstance.Id, CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status404NotFound, (result as IStatusCodeActionResult).StatusCode);
        }

        [Test]
        public async Task ControllerCreatesTheExpectedExperimentAgentStep()
        {
            this.mockDependencies.DataManager
                .Setup(mgr => mgr.GetExperimentStepAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(this.mockExperimentStepInstance);

            ExperimentComponent component = FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider), "Any Child Component");

            await this.controller.CreateExperimentAgentStepAsync(
                component,
                this.mockExperimentStepInstance.ExperimentId,
                this.mockAgentId,
                this.mockExperimentStepInstance.Id,
                CancellationToken.None);

            this.mockDependencies.DataManager.Verify(mgr => mgr.CreateAgentStepsAsync(
                this.mockExperimentStepInstance,
                component,
                this.mockAgentId,
                It.IsAny<CancellationToken>()));
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResultWhenAnAgentStepInstanceIsCreatedSuccessfully()
        {
            ExperimentComponent component = FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider), "Any Child Component");

            this.mockDependencies.DataManager
                .Setup(mgr => mgr.CreateAgentStepsAsync(
                    It.IsAny<ExperimentStepInstance>(),
                    It.IsAny<ExperimentComponent>(),
                    this.mockAgentId,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(this.mockExperimentStepInstances.AsEnumerable());

            CreatedAtActionResult result = await this.controller.CreateExperimentAgentStepAsync(
                component,
                this.mockExperimentStepInstance.ExperimentId,
                this.mockAgentId,
                this.mockExperimentStepInstance.Id,
                CancellationToken.None) as CreatedAtActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status201Created, result.StatusCode);
            Assert.IsInstanceOf<IEnumerable<ExperimentStepInstance>>(result.Value);
            Assert.IsTrue(object.ReferenceEquals(this.mockExperimentStepInstance, (result.Value as IEnumerable<ExperimentStepInstance>).First()));
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void ControllerValidatesRequiredParametersWhenCreatingExperimentAgentStep(string invalidValue)
        {
            // checks that the definition is not null
            Assert.ThrowsAsync<ArgumentException>(async ()
                => await this.controller.CreateExperimentAgentStepAsync(
                    null,
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()));

            // checks that the agentId parameter is not null or contains WhiteSpace
            Assert.ThrowsAsync<ArgumentException>(async ()
                => await this.controller.CreateExperimentAgentStepAsync(
                    It.IsAny<ExperimentComponent>(),
                    It.IsAny<string>(),
                    invalidValue,
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()));

            // checks that the parentId parameter is not null or contains WhiteSpace
            Assert.ThrowsAsync<ArgumentException>(async ()
                => await this.controller.CreateExperimentAgentStepAsync(
                    It.IsAny<ExperimentComponent>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    invalidValue,
                    It.IsAny<CancellationToken>()));
        }

        [Test]
        public async Task ControllerHandlesErrorsAsExpectedDuringExperimentAgentStepCreation()
        {
            ExperimentComponent component = FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider), "Any Child Component");

            foreach (var entry in ExperimentsControllerTests.potentialErrors)
            {
                // Key = Exception
                // Value = The expected HTTP status code in the result
                this.mockDependencies.DataManager
                    .Setup(mgr => mgr.CreateAgentStepsAsync(
                        It.IsAny<ExperimentStepInstance>(),
                        It.IsAny<ExperimentComponent>(),
                        this.mockAgentId,
                        It.IsAny<CancellationToken>()))
                    .Throws(entry.Key);

                IActionResult result = await this.controller.CreateExperimentAgentStepAsync(
                    component,
                    this.mockExperimentStepInstance.ExperimentId,
                    this.mockAgentId,
                    this.mockExperimentStepInstance.Id,
                    CancellationToken.None);

                Assert.IsNotNull(result);
                Assert.AreEqual(entry.Value, (result as IStatusCodeActionResult).StatusCode);
                Assert.IsInstanceOf<ProblemDetails>((result as ObjectResult).Value);
            }
        }

        [Test]
        public async Task ControllerDeletesTheExpectedExperiment()
        {
            await this.controller.DeleteExperimentAsync(this.mockExperimentInstance.Id, CancellationToken.None);

            this.mockDependencies.DataManager.Verify(mgr => mgr.DeleteExperimentAsync(
                this.mockExperimentInstance.Id, It.IsAny<CancellationToken>()));
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResultWhenAnExperimentInstanceIsDeletedSuccessfully()
        {
            this.mockDependencies.DataManager
                .Setup(mgr => mgr.DeleteExperimentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            StatusCodeResult result = await this.controller.DeleteExperimentAsync("Any Experiment ID", CancellationToken.None)
                as StatusCodeResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status204NoContent, result.StatusCode);
        }

        [Test]
        public async Task ControllerHandlesErrorsAsExpectedDuringExperimentDeletion()
        {
            foreach (var entry in ExperimentsControllerTests.potentialErrors)
            {
                // Key = Exception
                // Value = The expected HTTP status code in the result
                this.mockDependencies.DataManager
                    .Setup(mgr => mgr.DeleteExperimentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Throws(entry.Key);

                IActionResult result = await this.controller.DeleteExperimentAsync("Any Experiment ID", CancellationToken.None);

                Assert.IsNotNull(result);
                Assert.AreEqual(entry.Value, (result as IStatusCodeActionResult).StatusCode);
                Assert.IsInstanceOf<ProblemDetails>((result as ObjectResult).Value);
            }
        }

        [Test]
        public void ControllerValidatesRequiredParametersWhenDeletingExperiment()
        {
            Assert.ThrowsAsync<ArgumentException>(async () => await this.controller.DeleteExperimentAsync(null, CancellationToken.None));
        }

        [Test]
        public async Task ControllerDeletesTheExpectedExperimentSharedContext()
        {
            await this.controller.DeleteExperimentContextAsync(this.mockExperimentInstance.Id, CancellationToken.None);

            this.mockDependencies.DataManager.Verify(mgr => mgr.DeleteExperimentContextAsync(
                this.mockExperimentInstance.Id, It.IsAny<CancellationToken>(), null));
        }

        [Test]
        public async Task ControllerDeletesTheExpectedExperimentStepContext()
        {
            await this.controller.DeleteExperimentContextAsync(this.mockExperimentInstance.Id, CancellationToken.None, "SomeStep");

            this.mockDependencies.DataManager.Verify(mgr => mgr.DeleteExperimentContextAsync(
                this.mockExperimentInstance.Id, It.IsAny<CancellationToken>(), "SomeStep"));
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResultWhenAnExperimentContextInstanceIsDeletedSuccessfully()
        {
            this.mockDependencies.DataManager
                .Setup(mgr => mgr.DeleteExperimentContextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>(), null))
                .Returns(Task.CompletedTask);

            StatusCodeResult result = await this.controller.DeleteExperimentContextAsync("Any Experiment ID", CancellationToken.None)
                as StatusCodeResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status204NoContent, result.StatusCode);
        }

        [Test]
        public void ControllerValidatesRequiredParametersWhenDeletingExperimentContext()
        {
            Assert.ThrowsAsync<ArgumentException>(async () => await this.controller.DeleteExperimentContextAsync(null, CancellationToken.None));
        }

        [Test]
        public async Task ControllerHandlesErrorsAsExpectedDuringExperimentContextDeletion()
        {
            foreach (var entry in ExperimentsControllerTests.potentialErrors)
            {
                // Key = Exception
                // Value = The expected HTTP status code in the result
                this.mockDependencies.DataManager
                    .Setup(mgr => mgr.DeleteExperimentContextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>(), null))
                    .Throws(entry.Key);

                IActionResult result = await this.controller.DeleteExperimentContextAsync("Any Experiment ID", CancellationToken.None);

                Assert.IsNotNull(result);
                Assert.AreEqual(entry.Value, (result as IStatusCodeActionResult).StatusCode);
                Assert.IsInstanceOf<ProblemDetails>((result as ObjectResult).Value);
            }
        }

        [Test]
        public async Task ControllerDeletesTheExpectedExperimentSteps()
        {
            await this.controller.DeleteExperimentStepsAsync(this.mockExperimentInstance.Id, CancellationToken.None);

            this.mockDependencies.DataManager.Verify(mgr => mgr.DeleteExperimentStepsAsync(
                this.mockExperimentInstance.Id, It.IsAny<CancellationToken>()));
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResultWhenExperimentStepInstancesAreDeletedSuccessfully()
        {
            this.mockDependencies.DataManager
                .Setup(mgr => mgr.DeleteExperimentStepsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            StatusCodeResult result = await this.controller.DeleteExperimentStepsAsync("Any Experiment ID", CancellationToken.None)
                as StatusCodeResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status204NoContent, result.StatusCode);
        }

        [Test]
        public async Task ControllerHandlesErrorsAsExpectedDuringExperimentStepDeletion()
        {
            foreach (var entry in ExperimentsControllerTests.potentialErrors)
            {
                // Key = Exception
                // Value = The expected HTTP status code in the result
                this.mockDependencies.DataManager
                    .Setup(mgr => mgr.DeleteExperimentStepsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Throws(entry.Key);

                IActionResult result = await this.controller.DeleteExperimentStepsAsync("Any Experiment ID", CancellationToken.None);

                Assert.IsNotNull(result);
                Assert.AreEqual(entry.Value, (result as IStatusCodeActionResult).StatusCode);
                Assert.IsInstanceOf<ProblemDetails>((result as ObjectResult).Value);
            }
        }

        [Test]
        public void ControllerValidatesRequiredParametersWhenDeletingExperimentSteps()
        {
            Assert.ThrowsAsync<ArgumentException>(async () => await this.controller.DeleteExperimentStepsAsync(null, CancellationToken.None));
        }

        [Test]
        public async Task ControllerDeletesTheExpectedAgentStepsForAnExperiment()
        {
            await this.controller.DeleteExperimentAgentStepsAsync(this.mockExperimentInstance.Id, CancellationToken.None);

            this.mockDependencies.DataManager.Verify(mgr => mgr.DeleteAgentStepsAsync(
                this.mockExperimentInstance.Id, It.IsAny<CancellationToken>()));
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResultWhenAgentStepInstancesAreDeletedSuccessfully()
        {
            this.mockDependencies.DataManager
                .Setup(mgr => mgr.DeleteAgentStepsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            StatusCodeResult result = await this.controller.DeleteExperimentAgentStepsAsync("Cluster,Node,VM", CancellationToken.None)
                as StatusCodeResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status204NoContent, result.StatusCode);
        }

        [Test]
        public async Task ControllerHandlesErrorsAsExpectedDuringExperimentAgentStepDeletion()
        {
            foreach (var entry in ExperimentsControllerTests.potentialErrors)
            {
                // Key = Exception
                // Value = The expected HTTP status code in the result
                this.mockDependencies.DataManager
                    .Setup(mgr => mgr.DeleteAgentStepsAsync(this.mockExperimentInstance.Id, It.IsAny<CancellationToken>()))
                    .Throws(entry.Key);

                IActionResult result = await this.controller.DeleteExperimentAgentStepsAsync(this.mockExperimentInstance.Id, CancellationToken.None);

                Assert.IsNotNull(result);
                Assert.AreEqual(entry.Value, (result as IStatusCodeActionResult).StatusCode);
                Assert.IsInstanceOf<ProblemDetails>((result as ObjectResult).Value);
            }
        }

        [Test]
        public void ControllerValidatesRequiredParametersWhenDeletingExperimentAgentSteps()
        {
            Assert.ThrowsAsync<ArgumentException>(async () => await this.controller.DeleteExperimentAgentStepsAsync(null, CancellationToken.None));
        }

        [Test]
        public async Task ControllerGetsTheExpectedExperimentInstance()
        {
            await this.controller.GetExperimentAsync(this.mockExperimentInstance.Id, CancellationToken.None);

            this.mockDependencies.DataManager.Verify(
                mgr => mgr.GetExperimentAsync(this.mockExperimentInstance.Id, It.IsAny<CancellationToken>()));
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResultWhenAnExperimentInstanceIsRetrievedSuccessfully()
        {
            // Setup data manaager to return a valid experiment instance upon the creation
            // of an experiment.
            this.mockDependencies.DataManager
                .Setup(mgr => mgr.GetExperimentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(this.mockExperimentInstance);

            ObjectResult result = await this.controller.GetExperimentAsync("Any Experiment ID", CancellationToken.None)
                as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.IsTrue(object.ReferenceEquals(this.mockExperimentInstance, result.Value));
        }

        [Test]
        public async Task ControllerHandlesErrorsAsExpectedWhenGettingAnExperiment()
        {
            foreach (var entry in ExperimentsControllerTests.potentialErrors)
            {
                // Key = Exception
                // Value = The expected HTTP status code in the result
                this.mockDependencies.DataManager
                    .Setup(mgr => mgr.GetExperimentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Throws(entry.Key);

                IActionResult result = await this.controller.GetExperimentAsync("Any Experiment ID", CancellationToken.None);

                Assert.IsNotNull(result);
                Assert.AreEqual(entry.Value, (result as IStatusCodeActionResult).StatusCode);
                Assert.IsInstanceOf<ProblemDetails>((result as ObjectResult).Value);
            }
        }

        [Test]
        public void ControllerValidatesRequiredParametersWhenGettingAnExperiment()
        {
            Assert.ThrowsAsync<ArgumentException>(async () => await this.controller.GetExperimentAsync(null, CancellationToken.None));
        }

        [Test]
        public async Task ControllerGetsTheExpectedExperimentSharedContextInstance()
        {
            await this.controller.GetExperimentContextAsync(this.mockExperimentInstance.Id, CancellationToken.None);

            this.mockDependencies.DataManager.Verify(
                mgr => mgr.GetExperimentContextAsync(this.mockExperimentInstance.Id, It.IsAny<CancellationToken>(), null));
        }

        [Test]
        public void ControllerValidatesRequiredParametersWhenGettingAnExperimentContext()
        {
            Assert.ThrowsAsync<ArgumentException>(async () => await this.controller.GetExperimentContextAsync(null, CancellationToken.None));
        }

        [Test]
        public async Task ControllerGetsTheExpectedExperimentStepContextInstance()
        {
            await this.controller.GetExperimentContextAsync(this.mockExperimentInstance.Id, CancellationToken.None, "SomeStep");

            this.mockDependencies.DataManager.Verify(
                mgr => mgr.GetExperimentContextAsync(this.mockExperimentInstance.Id, It.IsAny<CancellationToken>(), "SomeStep"));
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResultWhenAnExperimentContextInstanceIsRetrievedSuccessfully()
        {
            // Setup data manaager to return a valid experiment instance upon the creation
            // of an experiment.
            this.mockDependencies.DataManager
                .Setup(mgr => mgr.GetExperimentContextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>(), null))
                .ReturnsAsync(this.mockExperimentContextInstance);

            ObjectResult result = await this.controller.GetExperimentContextAsync("Any Experiment ID", CancellationToken.None)
                as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.IsTrue(object.ReferenceEquals(this.mockExperimentContextInstance, result.Value));
        }

        [Test]
        public async Task ControllerHandlesErrorsAsExpectedWhenGettingAnExperimentContext()
        {
            foreach (var entry in ExperimentsControllerTests.potentialErrors)
            {
                // Key = Exception
                // Value = The expected HTTP status code in the result
                this.mockDependencies.DataManager
                    .Setup(mgr => mgr.GetExperimentContextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>(), null))
                    .Throws(entry.Key);

                IActionResult result = await this.controller.GetExperimentContextAsync("Any Experiment ID", CancellationToken.None);

                Assert.IsNotNull(result);
                Assert.AreEqual(entry.Value, (result as IStatusCodeActionResult).StatusCode);
                Assert.IsInstanceOf<ProblemDetails>((result as ObjectResult).Value);
            }
        }

        [Test]
        public async Task ControllerGetsTheExpectedExperimentStepInstances()
        {
            await this.controller.GetExperimentStepsAsync(this.mockExperimentInstance.Id, CancellationToken.None);

            this.mockDependencies.DataManager.Verify(
                mgr => mgr.GetExperimentStepsAsync(this.mockExperimentInstance.Id, It.IsAny<CancellationToken>(), It.IsAny<IQueryFilter>()));
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResultWhenExperimentStepInstancesAreRetrievedSuccessfully()
        {
            IEnumerable<ExperimentStepInstance> expectedSteps = new List<ExperimentStepInstance>
            {
                this.mockExperimentStepInstance,
                this.mockExperimentStepInstance
            };

            // Setup data manaager to return a valid experiment instance upon the creation
            // of an experiment.
            this.mockDependencies.DataManager
                .Setup(mgr => mgr.GetExperimentStepsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<IQueryFilter>()))
                .ReturnsAsync(expectedSteps);

            ObjectResult result = await this.controller.GetExperimentStepsAsync("Any Experiment ID", CancellationToken.None)
                as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.IsTrue(object.ReferenceEquals(expectedSteps, result.Value));
        }

        [Test]
        public void ControllerValidatesRequiredParametersWhenGettingAnExperimentSteps()
        {
            Assert.ThrowsAsync<ArgumentException>(async () => await this.controller.GetExperimentStepsAsync(null, CancellationToken.None));
        }

        [Test]
        public async Task ControllerHandlesErrorsAsExpectedWhenGettingExperimentSteps()
        {
            foreach (var entry in ExperimentsControllerTests.potentialErrors)
            {
                // Key = Exception
                // Value = The expected HTTP status code in the result
                this.mockDependencies.DataManager
                    .Setup(mgr => mgr.GetExperimentStepsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<IQueryFilter>()))
                    .Throws(entry.Key);

                IActionResult result = await this.controller.GetExperimentStepsAsync("Any Experiment ID", CancellationToken.None);

                Assert.IsNotNull(result);
                Assert.AreEqual(entry.Value, (result as IStatusCodeActionResult).StatusCode);
                Assert.IsInstanceOf<ProblemDetails>((result as ObjectResult).Value);
            }
        }

        [Test]
        public async Task ControllerGetsTheExpectedAgentStepInstancesForAnExperiment()
        {
            await this.controller.GetExperimentAgentStepsAsync(this.mockExperimentInstance.Id, CancellationToken.None);

            this.mockDependencies.DataManager.Verify(mgr => mgr.GetExperimentAgentStepsAsync(
                this.mockExperimentInstance.Id, It.IsAny<CancellationToken>(), It.IsAny<IQueryFilter>()));
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResultWhenAgentStepInstancesAreRetrievedSuccessfully()
        {
            IEnumerable<ExperimentStepInstance> expectedSteps = new List<ExperimentStepInstance>
            {
                this.mockExperimentStepInstance,
                this.mockExperimentStepInstance
            };

            // Setup data manaager to return a valid experiment instance upon the creation
            // of an experiment.
            this.mockDependencies.DataManager
                .Setup(mgr => mgr.GetExperimentAgentStepsAsync(
                    this.mockExperimentInstance.Id,
                    It.IsAny<CancellationToken>(),
                    It.IsAny<IQueryFilter>()))
                .ReturnsAsync(expectedSteps);

            ObjectResult result = await this.controller.GetExperimentAgentStepsAsync(this.mockExperimentInstance.Id, CancellationToken.None)
                as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.IsTrue(object.ReferenceEquals(expectedSteps, result.Value));
        }

        [Test]
        public async Task ControllerHandlesErrorsAsExpectedWhenGettingAgentSteps()
        {
            foreach (var entry in ExperimentsControllerTests.potentialErrors)
            {
                // Key = Exception
                // Value = The expected HTTP status code in the result
                this.mockDependencies.DataManager
                    .Setup(mgr => mgr.GetExperimentAgentStepsAsync(
                        this.mockExperimentInstance.Id,
                        It.IsAny<CancellationToken>(),
                        It.IsAny<IQueryFilter>()))
                    .Throws(entry.Key);

                IActionResult result = await this.controller.GetExperimentAgentStepsAsync(this.mockExperimentInstance.Id, CancellationToken.None);

                Assert.IsNotNull(result);
                Assert.AreEqual(entry.Value, (result as IStatusCodeActionResult).StatusCode);
                Assert.IsInstanceOf<ProblemDetails>((result as ObjectResult).Value);
            }
        }

        [Test]
        public async Task ControllerValidatesTheSchemaOfAnExperimentBeforeUpdate()
        {
            Mock<IValidationRule<Experiment>> mockValidation = new Mock<IValidationRule<Experiment>>();
            mockValidation.Setup(validation => validation.Validate(It.IsAny<Experiment>()))
                .Returns(new ValidationResult(false));

            ExperimentValidation.Instance.Add(mockValidation.Object);
            ExperimentInstance invalidInstance = new ExperimentInstance("Any Experiment ID", this.mockExperiment);

            ObjectResult result = await this.controller.UpdateExperimentAsync(
                invalidInstance.Id,
                invalidInstance,
                CancellationToken.None) as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status400BadRequest, result.StatusCode);
        }

        [Test]
        public async Task ControllerUpdatesTheExpectedExperimentInstance()
        {
            await this.controller.UpdateExperimentAsync(
                this.mockExperimentInstance.Id,
                this.mockExperimentInstance,
                CancellationToken.None);

            this.mockDependencies.DataManager.Verify(
                mgr => mgr.UpdateExperimentAsync(this.mockExperimentInstance, It.IsAny<CancellationToken>()));
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void ControllerValidatesRequiredParametersWhenUpdatingAnExperiment(string invalidParameter)
        {
            // checks for missing experiment parameter to update
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await this.controller.UpdateExperimentAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()));
            // checks for invalid experimentID
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await this.controller.UpdateExperimentAsync(invalidParameter, It.IsAny<ExperimentInstance>(), It.IsAny<CancellationToken>()));
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResultWhenAnExperimentInstanceIsUpdatedSuccessfully()
        {
            this.mockDependencies.DataManager
                .Setup(mgr => mgr.UpdateExperimentAsync(It.IsAny<ExperimentInstance>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(this.mockExperimentInstance);

            ObjectResult result = await this.controller.UpdateExperimentAsync(
                this.mockExperimentInstance.Id,
                this.mockExperimentInstance,
                CancellationToken.None)
                as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.IsTrue(object.ReferenceEquals(this.mockExperimentInstance, result.Value));
        }

        [Test]
        public async Task ControllerHandlesErrorsAsExpectedDuringExperimentUpdate()
        {
            foreach (var entry in ExperimentsControllerTests.potentialErrors)
            {
                // Key = Exception
                // Value = The expected HTTP status code in the result
                this.mockDependencies.DataManager
                    .Setup(mgr => mgr.UpdateExperimentAsync(It.IsAny<ExperimentInstance>(), It.IsAny<CancellationToken>()))
                    .Throws(entry.Key);

                IActionResult result = await this.controller.UpdateExperimentAsync(
                    this.mockExperimentInstance.Id,
                    this.mockExperimentInstance,
                    CancellationToken.None);

                Assert.IsNotNull(result);
                Assert.AreEqual(entry.Value, (result as IStatusCodeActionResult).StatusCode);
                Assert.IsInstanceOf<ProblemDetails>((result as ObjectResult).Value);
            }
        }

        [Test]
        public async Task ControllerUpdatesTheExpectedExperimentSharedContextInstance()
        {
            await this.controller.UpdateExperimentContextAsync(
                this.mockExperimentInstance.Id, this.mockExperimentContextInstance, CancellationToken.None);

            this.mockDependencies.DataManager.Verify(
                mgr => mgr.UpdateExperimentContextAsync(this.mockExperimentContextInstance, It.IsAny<CancellationToken>(), null));
        }

        [Test]
        public async Task ControllerUpdatesTheExpectedExperimentStepContextInstance()
        {
            await this.controller.UpdateExperimentContextAsync(
                this.mockExperimentInstance.Id, this.mockExperimentContextInstance, CancellationToken.None, "SomeStep");

            this.mockDependencies.DataManager.Verify(
                mgr => mgr.UpdateExperimentContextAsync(this.mockExperimentContextInstance, It.IsAny<CancellationToken>(), "SomeStep"));
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void ControllerValidatesRequiredParametersWhenUpdatingAnExperimentContext(string invalidParameter)
        {
            // checks for missing experiment context parameter to update
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await this.controller.UpdateExperimentContextAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()));
            // checks for invalid experimentID
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await this.controller.UpdateExperimentContextAsync(invalidParameter, It.IsAny<ExperimentMetadataInstance>(), It.IsAny<CancellationToken>()));
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResultWhenAnExperimentContextInstanceIsUpdatedSuccessfully()
        {
            this.mockDependencies.DataManager
                .Setup(mgr => mgr.UpdateExperimentContextAsync(It.IsAny<ExperimentMetadataInstance>(), It.IsAny<CancellationToken>(), null))
                .ReturnsAsync(this.mockExperimentContextInstance);

            ObjectResult result = await this.controller.UpdateExperimentContextAsync(
                this.mockExperimentInstance.Id, this.mockExperimentContextInstance, CancellationToken.None) as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.IsTrue(object.ReferenceEquals(this.mockExperimentContextInstance, result.Value));
        }

        [Test]
        public async Task ControllerHandlesErrorsAsExpectedDuringExperimentContextUpdate()
        {
            foreach (var entry in ExperimentsControllerTests.potentialErrors)
            {
                // Key = Exception
                // Value = The expected HTTP status code in the result
                this.mockDependencies.DataManager
                    .Setup(mgr => mgr.UpdateExperimentContextAsync(It.IsAny<ExperimentMetadataInstance>(), It.IsAny<CancellationToken>(), null))
                    .Throws(entry.Key);

                IActionResult result = await this.controller.UpdateExperimentContextAsync(
                    this.mockExperimentInstance.Id, this.mockExperimentContextInstance, CancellationToken.None);

                Assert.IsNotNull(result);
                Assert.AreEqual(entry.Value, (result as IStatusCodeActionResult).StatusCode);
                Assert.IsInstanceOf<ProblemDetails>((result as ObjectResult).Value);
            }
        }

        [Test]
        public async Task ControllerUpdatesTheExpectedExperimentStepInstance()
        {
            await this.controller.UpdateExperimentStepAsync(
                this.mockExperimentInstance.Id, this.mockExperimentStepInstance.Id, this.mockExperimentStepInstance, CancellationToken.None);

            this.mockDependencies.DataManager.Verify(
                mgr => mgr.UpdateExperimentStepAsync(this.mockExperimentStepInstance, It.IsAny<CancellationToken>()));
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void ControllerValidatesRequiredParametersWhenUpdatingAnExperimentStep(string invalidParameter)
        {
            // checks for missing experiment step parameter to update
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await this.controller.UpdateExperimentStepAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    null,
                    It.IsAny<CancellationToken>()));

            // checks for invalid experimentID
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await this.controller.UpdateExperimentStepAsync(
                    invalidParameter,
                    It.IsAny<string>(),
                    It.IsAny<ExperimentStepInstance>(),
                    It.IsAny<CancellationToken>()));

            // checks for invalid stepID
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await this.controller.UpdateExperimentStepAsync(
                    It.IsAny<string>(),
                    invalidParameter,
                    It.IsAny<ExperimentStepInstance>(),
                    It.IsAny<CancellationToken>()));
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResultWhenAnExperimentStepInstanceIsUpdatedSuccessfully()
        {
            this.mockDependencies.DataManager
                .Setup(mgr => mgr.UpdateExperimentStepAsync(It.IsAny<ExperimentStepInstance>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(this.mockExperimentStepInstance);

            ObjectResult result = await this.controller.UpdateExperimentStepAsync(
                this.mockExperimentInstance.Id,
                this.mockExperimentStepInstance.Id,
                this.mockExperimentStepInstance,
                CancellationToken.None) as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.IsTrue(object.ReferenceEquals(this.mockExperimentStepInstance, result.Value));
        }

        [Test]
        public async Task ControllerHandlesErrorsAsExpectedDuringExperimentStepUpdate()
        {
            foreach (var entry in ExperimentsControllerTests.potentialErrors)
            {
                // Key = Exception
                // Value = The expected HTTP status code in the result
                this.mockDependencies.DataManager
                    .Setup(mgr => mgr.UpdateExperimentStepAsync(It.IsAny<ExperimentStepInstance>(), It.IsAny<CancellationToken>()))
                    .Throws(entry.Key);

                IActionResult result = await this.controller.UpdateExperimentStepAsync(
                    this.mockExperimentInstance.Id,
                    this.mockExperimentStepInstance.Id,
                    this.mockExperimentStepInstance,
                    CancellationToken.None);

                Assert.IsNotNull(result);
                Assert.AreEqual(entry.Value, (result as IStatusCodeActionResult).StatusCode);
                Assert.IsInstanceOf<ProblemDetails>((result as ObjectResult).Value);
            }
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public async Task ControllerValidatesExperimentIdParameterWhenUpdatingAnExperimentAgentStep(string invalidParameter)
        {
            this.mockDependencies.DataManager
                .Setup(mgr => mgr.UpdateAgentStepAsync(It.IsAny<ExperimentStepInstance>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(this.mockExperimentStepInstance);

            ObjectResult result = await this.controller.UpdateExperimentAgentStepAsync(
                invalidParameter,
                this.mockExperimentStepInstance.Id,
                this.mockExperimentStepInstance,
                CancellationToken.None) as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(result.StatusCode, StatusCodes.Status400BadRequest);
            Assert.IsInstanceOf<ProblemDetails>((result as ObjectResult).Value);
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public async Task ControllerValidatesStepIdParameterWhenUpdatingAnExperimentAgentStep(string invalidParameter)
        {
            this.mockDependencies.DataManager
                .Setup(mgr => mgr.UpdateAgentStepAsync(It.IsAny<ExperimentStepInstance>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(this.mockExperimentStepInstance);

            ObjectResult result = await this.controller.UpdateExperimentAgentStepAsync(
                this.mockExperimentStepInstance.ExperimentId,
                invalidParameter,
                this.mockExperimentStepInstance,
                CancellationToken.None) as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(result.StatusCode, StatusCodes.Status400BadRequest);
            Assert.IsInstanceOf<ProblemDetails>((result as ObjectResult).Value);
        }

        [Test]
        public async Task ControllerValidatesStepInstanceParameterWhenUpdatingAnExperimentAgentStep()
        {
            this.mockDependencies.DataManager
                .Setup(mgr => mgr.UpdateAgentStepAsync(It.IsAny<ExperimentStepInstance>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(this.mockExperimentStepInstance);

            ObjectResult result = await this.controller.UpdateExperimentAgentStepAsync(
                this.mockExperimentStepInstance.ExperimentId,
                this.mockExperimentStepInstance.Id,
                null,
                CancellationToken.None) as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(result.StatusCode, StatusCodes.Status400BadRequest);
            Assert.IsInstanceOf<ProblemDetails>((result as ObjectResult).Value);
        }

        [Test]
        public async Task ControllerUpdatesTheExpectedExperimentAgentStepInstance()
        {
            await this.controller.UpdateExperimentAgentStepAsync(
                this.mockExperimentStepInstance.ExperimentId,
                this.mockExperimentStepInstance.Id,
                this.mockExperimentStepInstance,
                CancellationToken.None);

            this.mockDependencies.DataManager.Verify(
                mgr => mgr.UpdateAgentStepAsync(this.mockExperimentStepInstance, It.IsAny<CancellationToken>()));
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResultWhenAnExperimentAgentStepInstanceIsUpdatedSuccessfully()
        {
            this.mockDependencies.DataManager
                .Setup(mgr => mgr.UpdateAgentStepAsync(It.IsAny<ExperimentStepInstance>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(this.mockExperimentStepInstance);

            ObjectResult result = await this.controller.UpdateExperimentAgentStepAsync(
                this.mockExperimentStepInstance.ExperimentId,
                this.mockExperimentStepInstance.Id,
                this.mockExperimentStepInstance,
                CancellationToken.None) as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.IsTrue(object.ReferenceEquals(this.mockExperimentStepInstance, result.Value));
        }

        [Test]
        public async Task ControllerHandlesErrorsAsExpectedDuringExperimentAgentStepUpdate()
        {
            foreach (var entry in ExperimentsControllerTests.potentialErrors)
            {
                // Key = Exception
                // Value = The expected HTTP status code in the result
                this.mockDependencies.DataManager
                    .Setup(mgr => mgr.UpdateAgentStepAsync(It.IsAny<ExperimentStepInstance>(), It.IsAny<CancellationToken>()))
                    .Throws(entry.Key);

                IActionResult result = await this.controller.UpdateExperimentAgentStepAsync(
                    this.mockExperimentStepInstance.ExperimentId,
                    this.mockExperimentStepInstance.Id,
                    this.mockExperimentStepInstance,
                    CancellationToken.None);

                Assert.IsNotNull(result);
                Assert.AreEqual(entry.Value, (result as IStatusCodeActionResult).StatusCode);
                Assert.IsInstanceOf<ProblemDetails>((result as ObjectResult).Value);
            }
        }
    }
}