namespace Juno.Agent.Api
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno;
    using Juno.Contracts;
    using Juno.Contracts.Validation;
    using Juno.DataManagement;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Infrastructure;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Repository;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class AgentExperimentsControllerTests
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
            [new Exception("All other errors")] = StatusCodes.Status500InternalServerError
        };

        private AgentExperimentsController controller;
        private Fixture mockFixture;
        private FixtureDependencies mockDependencies;
        private ExperimentInstance mockExperimentInstance;
        private ExperimentMetadata mockExperimentContext;
        private ExperimentMetadataInstance mockExperimentContextInstance;
        private ExperimentStepInstance mockExperimentStepInstance;
        private List<ExperimentStepInstance> mockExperimentStepInstances;

        [SetUp]
        public void Setup()
        {
            this.mockFixture = new Fixture();
            this.mockFixture
                .SetupAgentMocks()
                .SetupExperimentMocks();
            this.mockDependencies = new FixtureDependencies();
            this.controller = new AgentExperimentsController(this.mockDependencies.DataManager.Object, NullLogger.Instance);
            this.mockExperimentContext = this.mockFixture.Create<ExperimentMetadata>();
            this.mockExperimentInstance = this.mockFixture.Create<ExperimentInstance>();
            this.mockExperimentContextInstance = this.mockFixture.Create<ExperimentMetadataInstance>();
            this.mockExperimentStepInstance = this.mockFixture.CreateExperimentStep(
                agentId: "Cluster01,VM01,Node01,TiPSession01",
                parentStepId: Guid.NewGuid().ToString());

            this.mockExperimentStepInstances = new List<ExperimentStepInstance> { this.mockExperimentStepInstance };
        }

        // Constructor Validation Unit Test Cases
        [Test]
        public void ControllerConstructorValidatesRequiredDataManagerParameter()
        {
            Assert.Throws<ArgumentException>(() => new AgentExperimentsController(null));
        }

        [Test]
        public void ControllerConstructorSetsParametersToExpectedValues()
        {
            AgentExperimentsController controller = new AgentExperimentsController(this.mockDependencies.DataManager.Object);
            EqualityAssert.PropertySet(controller, "DataManager", this.mockDependencies.DataManager.Object);
            EqualityAssert.PropertySet(controller, "Logger", NullLogger.Instance);

            controller = new AgentExperimentsController(this.mockDependencies.DataManager.Object, this.mockDependencies.Logger.Object);

            EqualityAssert.PropertySet(controller, "DataManager", this.mockDependencies.DataManager.Object);
            EqualityAssert.PropertySet(controller, "Logger", this.mockDependencies.Logger.Object);
        }

        // Experiment Unit Test Cases: Get Requests
        [Test]
        public async Task ControllerReturnsExpectedExperiment()
        {
            this.mockDependencies.DataManager
                .Setup(mgr => mgr.GetExperimentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(this.mockExperimentInstance);

            ObjectResult result = await this.controller.GetExperimentAsync(this.mockExperimentInstance.Id, CancellationToken.None)
                as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.IsTrue(object.ReferenceEquals(this.mockExperimentInstance, result.Value));
        }

        [Test]
        public async Task ControllerGetsTheExpectedExperimentInstance()
        {
            await this.controller.GetExperimentAsync(this.mockExperimentInstance.Id, CancellationToken.None);

            this.mockDependencies.DataManager
                .Verify(mgr => mgr.GetExperimentAsync(this.mockExperimentInstance.Id, It.IsAny<CancellationToken>()));
        }

        [Test]
        public void ControllerValidatesRequiredParametersWhenGettingExperiment()
        {
            Assert.ThrowsAsync<ArgumentException>(async () => await this.controller.GetExperimentAsync(null, It.IsAny<CancellationToken>()));
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResponseWhenErrorsOccurInGettingExperiment()
        {
            // Key = Exception, Value = The expected HTTP status code in the result
            foreach (var entry in AgentExperimentsControllerTests.potentialErrors)
            {
                this.mockDependencies.DataManager
                    .Setup(mgr => mgr.GetExperimentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Throws(entry.Key);

                ObjectResult result = await this.controller.GetExperimentAsync(this.mockExperimentInstance.Id, CancellationToken.None)
                    as ObjectResult;

                Assert.IsNotNull(result);
                Assert.AreEqual(entry.Value, result.StatusCode);
                Assert.IsInstanceOf<ProblemDetails>(result.Value);
            }
        }

        // Agent Experiment Unit Tests -  Get Request
        [Test]
        public async Task ControllerReturnsExpectedAgentExperiment()
        {
            this.mockDependencies.DataManager
                .Setup(mgr => mgr.GetAgentExperimentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(this.mockExperimentInstance);

            ObjectResult result = await this.controller.GetAgentExperimentAsync(this.mockExperimentInstance.Id, CancellationToken.None)
                as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.IsTrue(object.ReferenceEquals(this.mockExperimentInstance, result.Value));
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void ControllerValidatesRequiredParametersWhenGettingAnAgentExperiment(string invalidParameter)
        {
            Assert.ThrowsAsync<ArgumentException>(async () => await this.controller.GetAgentExperimentAsync(invalidParameter, It.IsAny<CancellationToken>()));
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResponseWhenErrorsOccurInGettingAgentExperiment()
        {
            // Key = Exception, Value = The expected HTTP status code in the result
            foreach (var entry in AgentExperimentsControllerTests.potentialErrors)
            {
                this.mockDependencies.DataManager
                    .Setup(mgr => mgr.GetAgentExperimentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Throws(entry.Key);

                ObjectResult result = await this.controller
                    .GetAgentExperimentAsync(this.mockExperimentInstance.Id, CancellationToken.None) as ObjectResult;

                Assert.IsNotNull(result);
                Assert.AreEqual(entry.Value, result.StatusCode);
                Assert.IsInstanceOf<ProblemDetails>(result.Value);
            }
        }

        // Experiment Context Unit Tests: Create Requests
        [Test]
        public async Task ControllerWillFailToCreateExperimentContextUnlessAMatchingExperimentExists()
        {
            // Setup the data manager to indicate an experiment does not exist.  In most implementations,
            // the data manager throws an exception.  The controller handles the case where this is not true.
            this.mockDependencies.DataManager
                .Setup(mgr => mgr.GetExperimentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(null as ExperimentInstance);

            IActionResult result = await this.controller
                .CreateExperimentContextAsync(this.mockExperimentInstance.Id, this.mockExperimentContext, CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status404NotFound, (result as IStatusCodeActionResult).StatusCode);
        }

        [Test]
        public async Task ControllerReturnsSuccessfulResponseWhenCreatingAnExperimentContext()
        {
            // Setup data manager to return a valid experiment instance
            this.mockDependencies.DataManager
                .Setup(mgr => mgr.GetExperimentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(this.mockExperimentInstance as ExperimentInstance);

            // Setup data manager to return a valid experiment context instance for the experiment
            this.mockDependencies.DataManager
                .Setup(mgr => mgr.GetExperimentContextAsync(this.mockExperimentInstance.Id, It.IsAny<CancellationToken>(), null))
                .ReturnsAsync(this.mockExperimentContextInstance);

            CreatedAtActionResult result = await this.controller.CreateExperimentContextAsync(
                this.mockExperimentInstance.Id, this.mockExperimentContext, CancellationToken.None) as CreatedAtActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status201Created, result.StatusCode);
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResponseWhenErrorsOccurInCreatingExperimentContext()
        {
            // Setup data manager to return a valid experiment instance
            this.mockDependencies.DataManager
                .Setup(mgr => mgr.GetExperimentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(this.mockExperimentInstance as ExperimentInstance);

            // Key = Exception, Value = The expected HTTP status code in the result
            foreach (var entry in AgentExperimentsControllerTests.potentialErrors)
            {
                this.mockDependencies.DataManager
                    .Setup(mgr => mgr.CreateExperimentContextAsync(It.IsAny<ExperimentMetadata>(), It.IsAny<CancellationToken>(), It.IsAny<string>()))
                    .Throws(entry.Key);

                ObjectResult result = await this.controller.CreateExperimentContextAsync(this.mockExperimentInstance.Id, this.mockExperimentContext, CancellationToken.None, null)
                    as ObjectResult;

                Assert.IsNotNull(result);
                Assert.AreEqual(entry.Value, result.StatusCode);
                Assert.IsInstanceOf<ProblemDetails>(result.Value);
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

        // Experiment Context Unit Tests: Get Requests
        [Test]
        public async Task ControllerReturnsExpectedExperimentContext()
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
        public void ControllerValidatesRequiredParametersWhenGettingExperimentContext()
        {
            Assert.ThrowsAsync<ArgumentException>(async () => await this.controller.GetExperimentContextAsync(null, It.IsAny<CancellationToken>()));
        }

        [Test]
        public async Task ControllerReturnsTheExpectedExperimentContextInstance()
        {
            await this.controller.GetExperimentContextAsync(this.mockExperimentInstance.Id, CancellationToken.None);

            this.mockDependencies.DataManager.Verify(
                mgr => mgr.GetExperimentContextAsync(this.mockExperimentInstance.Id, It.IsAny<CancellationToken>(), null));
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResponseWhenErrorsOccurInGettingExperimentContext()
        {
            // Key = Exception, Value = The expected HTTP status code in the result
            foreach (var entry in AgentExperimentsControllerTests.potentialErrors)
            {
                this.mockDependencies.DataManager
                    .Setup(mgr => mgr.GetExperimentContextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>(), null))
                    .Throws(entry.Key);

                ObjectResult result = await this.controller.GetExperimentContextAsync(
                    this.mockExperimentContextInstance.Id, CancellationToken.None) as ObjectResult;

                Assert.IsNotNull(result);
                Assert.AreEqual(entry.Value, result.StatusCode);
                Assert.IsInstanceOf<ProblemDetails>(result.Value);
            }
        }
        
        [Test]
        public async Task ControllerReturnsTheExpectedExperimentStepContextInstance()
        {
            await this.controller.GetExperimentContextAsync(this.mockExperimentInstance.Id, CancellationToken.None, "SomeStep");

            this.mockDependencies.DataManager
                .Verify(mgr => mgr.GetExperimentContextAsync(this.mockExperimentInstance.Id, It.IsAny<CancellationToken>(), "SomeStep"));
        }

        // Experiment Context Unit Tests: Update Requests
        [Test]
        public async Task ControllerReturnsUpdatedExperimentContext()
        {
            // Setup data manaager to return a valid experiment context instance.
            this.mockDependencies.DataManager
                .Setup(mgr => mgr.UpdateExperimentContextAsync(It.IsAny<ExperimentMetadataInstance>(), It.IsAny<CancellationToken>(), null))
                .ReturnsAsync(this.mockExperimentContextInstance);

            ObjectResult result = await this.controller.UpdateExperimentContextAsync(
                this.mockExperimentContextInstance.Id, this.mockExperimentContextInstance, CancellationToken.None) as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.IsTrue(object.ReferenceEquals(this.mockExperimentContextInstance, result.Value));
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
                this.mockExperimentInstance.Id, 
                this.mockExperimentContextInstance, 
                CancellationToken.None, 
                "SomeStep");

            this.mockDependencies.DataManager.Verify(
                mgr => mgr.UpdateExperimentContextAsync(
                    this.mockExperimentContextInstance, 
                    It.IsAny<CancellationToken>(), 
                    "SomeStep"));
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResponseWhenErrorsOccurInUpdatingAgentExperimentContext()
        {
            // Key = Exception, Value = The expected HTTP status code in the result
            foreach (var entry in AgentExperimentsControllerTests.potentialErrors)
            {
                this.mockDependencies.DataManager
                    .Setup(mgr => mgr.UpdateExperimentContextAsync(It.IsAny<ExperimentMetadataInstance>(), It.IsAny<CancellationToken>(), null))
                    .Throws(entry.Key);

                ObjectResult result = await this.controller.UpdateExperimentContextAsync(
                    this.mockExperimentContextInstance.Id, this.mockExperimentContextInstance, CancellationToken.None) as ObjectResult;

                Assert.IsNotNull(result);
                Assert.AreEqual(entry.Value, result.StatusCode);
                Assert.IsInstanceOf<ProblemDetails>(result.Value);
            }
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void ControllerValidatesRequiredParametersWhenUpdatingAgentExperiment(string invalidValue)
        {
            Assert.ThrowsAsync<ArgumentException>(async () => await this.controller.UpdateExperimentContextAsync(invalidValue, It.IsAny<ExperimentMetadataInstance>(), It.IsAny<CancellationToken>()));
            Assert.ThrowsAsync<ArgumentException>(async () => await this.controller.UpdateExperimentContextAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()));
        }

        // Agent Steps Unit Tests: Get Requests
        [Test]
        public async Task ControllerReturnsTheExpectedAgentStep()
        {
            this.mockDependencies.DataManager
                .Setup(mgr => mgr.GetAgentStepsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>(), null))
                .ReturnsAsync(this.mockExperimentStepInstances as IEnumerable<ExperimentStepInstance>);

            ObjectResult result = await this.controller.GetAgentStepsAsync(this.mockExperimentStepInstance.AgentId, CancellationToken.None)
                as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.IsTrue(object.ReferenceEquals(this.mockExperimentStepInstance, (result.Value as List<ExperimentStepInstance>)[0]));
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void ControllerValidatesRequiredParametersWhenGettingAgentSteps(string invalidParameter)
        {
            Assert.ThrowsAsync<ArgumentException>(async () => await this.controller.GetAgentStepsAsync(invalidParameter, It.IsAny<CancellationToken>()));
        }

        [Test]
        public async Task ControllerReturnsTheExpectedExperimentStepInstances()
        {
            await this.controller.GetAgentStepsAsync(this.mockExperimentInstance.Id, CancellationToken.None);

            this.mockDependencies.DataManager
                .Verify(mgr => mgr.GetAgentStepsAsync(this.mockExperimentInstance.Id, It.IsAny<CancellationToken>(), It.IsAny<IQueryFilter>()));
        }

        [Test]
        public async Task ControllerGetsTheExpectedResultWhenAgentStepInstancesAreRetrievedSuccessfully()
        {
            IEnumerable<ExperimentStepInstance> expectedSteps = new List<ExperimentStepInstance>
            {
                this.mockExperimentStepInstance,
                this.mockExperimentStepInstance
            };

            // Setup data manager to return a valid experiment instance upon the creation of an experiment.
            this.mockDependencies.DataManager
                .Setup(mgr => mgr.GetAgentStepsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<IQueryFilter>()))
                .ReturnsAsync(expectedSteps);

            ObjectResult result = await this.controller.GetAgentStepsAsync("Any Experiment ID", CancellationToken.None)
                as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.IsTrue(object.ReferenceEquals(expectedSteps, result.Value));
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResponseWhenErrorsOccurInGettingAgentStep()
        {
            // Key = Exception, Value = The expected HTTP status code in the result
            foreach (var entry in AgentExperimentsControllerTests.potentialErrors)
            {
                this.mockDependencies.DataManager
                    .Setup(mgr => mgr.GetAgentStepsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<IQueryFilter>()))
                    .Throws(entry.Key);

                ObjectResult result = await this.controller.GetAgentStepsAsync(this.mockExperimentStepInstance.AgentId, CancellationToken.None)
                    as ObjectResult;

                Assert.IsNotNull(result);
                Assert.AreEqual(entry.Value, result.StatusCode);
                Assert.IsInstanceOf<ProblemDetails>(result.Value);
            }
        }

        [Test]
        public async Task ControllerGetsTheExpectedAgentStepInstancesForAnExperiment()
        {
            await this.controller.GetAgentStepsAsync(this.mockExperimentInstance.Id, CancellationToken.None);

            this.mockDependencies.DataManager
                .Verify(mgr => mgr.GetAgentStepsAsync(this.mockExperimentInstance.Id, It.IsAny<CancellationToken>(), It.IsAny<IQueryFilter>()));
        }

        // Agent Steps Unit Tests: Update Requests
        [Test]
        public async Task ControllerUpdatesTheExpectedAgentExperimentStep()
        {
            this.mockDependencies.DataManager
                .Setup(mgr => mgr.UpdateAgentStepAsync(It.IsAny<ExperimentStepInstance>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(this.mockExperimentStepInstance);

            ObjectResult result = await this.controller.UpdateAgentStepAsync(
                this.mockExperimentStepInstance.Id, this.mockExperimentStepInstance, CancellationToken.None) as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.IsTrue(object.ReferenceEquals(this.mockExperimentStepInstance, result.Value));
        }
        
        [Test]
        public async Task ControllerReturnsTheExpectedResponseWhenErrorsOccurInUpdatingAgentStep()
        {
            // Key = Exception, Value = The expected HTTP status code in the result
            foreach (var entry in AgentExperimentsControllerTests.potentialErrors)
            {
                this.mockDependencies.DataManager
                    .Setup(mgr => mgr.UpdateAgentStepAsync(It.IsAny<ExperimentStepInstance>(), It.IsAny<CancellationToken>()))
                    .Throws(entry.Key);

                ObjectResult result = await this.controller.UpdateAgentStepAsync(
                    this.mockExperimentStepInstance.Id, this.mockExperimentStepInstance, CancellationToken.None) as ObjectResult;

                Assert.IsNotNull(result);
                Assert.AreEqual(entry.Value, result.StatusCode);
                Assert.IsInstanceOf<ProblemDetails>(result.Value);
            }
        }

        [Test]
        public void ControllerValidatesStepIdWhenUpdatingAgentStep()
        {
            IEnumerable<ExperimentStepInstance> expectedSteps = new List<ExperimentStepInstance>
            {
                this.mockExperimentStepInstance,
                this.mockExperimentStepInstance
            };

            // Setup data manager to return a valid experiment instance upon the creation of an experiment.
            this.mockDependencies.DataManager
                .Setup(mgr => mgr.GetAgentStepsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<IQueryFilter>()))
                .ReturnsAsync(expectedSteps);

            // Attempt to update the agent steps with different step instances and Ids
            Assert.ThrowsAsync<ArgumentException>(async () => 
                await this.controller.UpdateAgentStepAsync(expectedSteps.ElementAt(0).AgentId, expectedSteps.ElementAt(1), It.IsAny<CancellationToken>()));
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void ControllerValidatesRequiredParametersWhenUpdatingAgentStep(string invalidValue)
        {
            Assert.ThrowsAsync<ArgumentException>(async () => await this.controller.UpdateAgentStepAsync(invalidValue, It.IsAny<ExperimentStepInstance>(), It.IsAny<CancellationToken>()));
            Assert.ThrowsAsync<ArgumentException>(async () => await this.controller.UpdateAgentStepAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()));
        }
    }
}