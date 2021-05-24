namespace Juno.Execution.Api
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Contracts;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Infrastructure;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Repository;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;
    using CosmosTable = Microsoft.Azure.Cosmos.Table;
    using Storage = Microsoft.Azure.Storage;

    [TestFixture]
    [Category("Unit")]
    public class ExperimentStatusControllerTests
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

        private ExperimentStatusController controller;
        private FixtureDependencies mockDependencies;
        private List<ExperimentInstance> mockExperimentInstances;

        [SetUp]
        public void SetupTest()
        {
            this.mockDependencies = new FixtureDependencies();
            this.mockDependencies.SetupExperimentMocks();
            this.controller = new ExperimentStatusController(this.mockDependencies.DataManager.Object, NullLogger.Instance);
            this.mockExperimentInstances = new List<ExperimentInstance>
            {
                this.mockDependencies.Create<ExperimentInstance>(),
                this.mockDependencies.Create<ExperimentInstance>(),
                this.mockDependencies.Create<ExperimentInstance>(),
                this.mockDependencies.Create<ExperimentInstance>()
            };
        }

        [Test]
        public void ControllerContructorsValidateParameters()
        {
            Assert.Throws<ArgumentException>(() => new ExperimentStatusController(null));
        }

        [Test]
        public void ControllerConstructorSetsParametersToExpectedValues()
        {
            ExperimentStatusController controller = new ExperimentStatusController(this.mockDependencies.DataManager.Object);
            EqualityAssert.PropertySet(controller, "DataManager", this.mockDependencies.DataManager.Object);
            EqualityAssert.PropertySet(controller, "Logger", NullLogger.Instance);

            controller = new ExperimentStatusController(this.mockDependencies.DataManager.Object, this.mockDependencies.Logger.Object);

            EqualityAssert.PropertySet(controller, "DataManager", this.mockDependencies.DataManager.Object);
            EqualityAssert.PropertySet(controller, "Logger", this.mockDependencies.Logger.Object);
        }

        [Test]
        public void ControllerValidatesRequiredParametersBeforeGettingExperimentInstanceStatuses()
        {
            Assert.ThrowsAsync<ArgumentException>(async () => await this.controller.GetExperimentInstanceStatusesAsync(null, CancellationToken.None));
        }

        [Test]
        public async Task ControllerGetsTheExpectedExperimentInstanceStatusData()
        {
            await this.controller.GetExperimentInstanceStatusesAsync("Any_Experiment", CancellationToken.None);

            this.mockDependencies.DataManager.Verify(
                mgr => mgr.QueryExperimentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()));
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResultWhenExperimentInstancesExists()
        {
            string expectedExperiment = "Any_Experiment_Name";

            List<JObject> expectedResults = new List<JObject>();
            this.mockExperimentInstances.ForEach(experiment => expectedResults.Add(JObject.FromObject(new
            {
                id = experiment.Id,
                created = experiment.Created,
                name = experiment.Definition.Name,
                status = experiment.Status,
                revision = "12345_10.0.123.456",
                teamName = experiment.Definition.Metadata["teamName"]
            })));

            // Setup data manaager to return a valid experiment instance upon the creation
            // of an experiment.
            this.mockDependencies.DataManager
                .Setup(mgr => mgr.QueryExperimentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResults as IEnumerable<JObject>);

            ObjectResult result = await this.controller.GetExperimentInstanceStatusesAsync(expectedExperiment, CancellationToken.None)
                as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.IsTrue(object.ReferenceEquals(expectedResults, result.Value));
        }

        [Test]
        public async Task ControllerHandlesErrorsAsExpectedWhenGettingExperimentInstanceStatuses()
        {
            foreach (var entry in ExperimentStatusControllerTests.potentialErrors)
            {
                // Key = Exception
                // Value = The expected HTTP status code in the result
                this.mockDependencies.DataManager
                    .Setup(mgr => mgr.QueryExperimentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Throws(entry.Key);

                IActionResult result = await this.controller.GetExperimentInstanceStatusesAsync("Any Experiment ID", CancellationToken.None);

                Assert.IsNotNull(result);
                Assert.AreEqual(entry.Value, (result as IStatusCodeActionResult).StatusCode);
                Assert.IsInstanceOf<ProblemDetails>((result as ObjectResult).Value);
            }
        }
    }
}
