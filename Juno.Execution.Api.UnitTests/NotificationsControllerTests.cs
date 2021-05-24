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
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Repository;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;

    using Storage = Microsoft.Azure.Storage;

    [TestFixture]
    [Category("Unit")]
    public class NotificationsControllerTests
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
            [new Storage.StorageException(new Storage.RequestResult { HttpStatusCode = StatusCodes.Status403Forbidden }, "Any error", null)] = StatusCodes.Status403Forbidden,
            [new Exception("All other errors")] = StatusCodes.Status500InternalServerError
        };

        private NotificationsController controller;
        private Fixture mockFixture;
        private FixtureDependencies mockDependencies;
        private ExperimentMetadata mockNotice;
        private ExperimentMetadataInstance mockNoticeInstance;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockDependencies = new FixtureDependencies();
            this.mockFixture.SetupExperimentMocks();
            this.mockNotice = this.mockFixture.Create<ExperimentMetadata>();
            this.mockNoticeInstance = new ExperimentMetadataInstance(Guid.NewGuid().ToString(), this.mockNotice);
            this.controller = new NotificationsController(this.mockDependencies.NotificationManager.Object, NullLogger.Instance);
        }

        [Test]
        public void ControllerConstructorSetsParametersToExpectedValues()
        {
            NotificationsController controller = new NotificationsController(this.mockDependencies.NotificationManager.Object);
            EqualityAssert.PropertySet(controller, "NotificationManager", this.mockDependencies.NotificationManager.Object);
            EqualityAssert.PropertySet(controller, "Logger", NullLogger.Instance);

            controller = new NotificationsController(this.mockDependencies.NotificationManager.Object, this.mockDependencies.Logger.Object);
            EqualityAssert.PropertySet(controller, "NotificationManager", this.mockDependencies.NotificationManager.Object);
            EqualityAssert.PropertySet(controller, "Logger", this.mockDependencies.Logger.Object);
        }

        [Test]
        public async Task ControllerCreatesTheExpectedNotice()
        {
            this.mockDependencies.NotificationManager
                .Setup(mgr => mgr.CreateNoticeAsync(
                    It.IsAny<string>(),
                    this.mockNotice,
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .Returns(Task.FromResult(this.mockNoticeInstance))
                .Verifiable();

            // Setup data manaager to return a valid experiment instance upon the creation
            // of an experiment.
            await this.controller.CreateNoticeAsync("any queue", this.mockNotice, CancellationToken.None);

            this.mockDependencies.NotificationManager.VerifyAll();
        }

        [Test]
        public async Task ControllerPutsTheNoticeOnTheExpectedQueueLocation()
        {
            // Mock Setup:
            // Verify the controller places the notice on the expected queue.
            string expectedQueueName = "any queue";

            this.mockDependencies.NotificationManager
                .Setup(mgr => mgr.CreateNoticeAsync(
                    expectedQueueName,
                    It.IsAny<ExperimentMetadata>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .Returns(Task.FromResult(this.mockNoticeInstance))
                .Verifiable();

            await this.controller.CreateNoticeAsync(expectedQueueName, this.mockNotice, CancellationToken.None);

            this.mockDependencies.NotificationManager.VerifyAll();
        }

        [Test]
        public void ControllerDefaultVisibilityDelayMatchesExpectedWhenCreatingNotices()
        {
            // Mock Setup:
            // Verify the timespan provided for visibility delay is used.
            TimeSpan expectedDelay = TimeSpan.FromSeconds(1);

            this.mockDependencies.NotificationManager
                .Setup(mgr => mgr.CreateNoticeAsync(
                    It.IsAny<string>(),
                    It.IsAny<ExperimentMetadata>(),
                    It.IsAny<CancellationToken>(),
                    expectedDelay))
                .Returns(Task.FromResult(this.mockNoticeInstance))
                .Verifiable();

            this.controller.CreateNoticeAsync("any queue", this.mockNotice, CancellationToken.None, (int)expectedDelay.TotalSeconds)
                .GetAwaiter().GetResult();

            this.mockDependencies.NotificationManager.VerifyAll();
        }

        [Test]
        public async Task ControllerUsesTheExpectedVisibilityDelayWhenDefined()
        {
            TimeSpan expectedDelay = TimeSpan.FromSeconds(20);

            this.mockDependencies.NotificationManager
                 .Setup(mgr => mgr.CreateNoticeAsync(
                    It.IsAny<string>(),
                    It.IsAny<ExperimentMetadata>(),
                    It.IsAny<CancellationToken>(),
                    expectedDelay))
                .Returns(Task.FromResult(this.mockNoticeInstance))
                .Verifiable();

            await this.controller.CreateNoticeAsync("any queue", this.mockNotice, CancellationToken.None, (int)expectedDelay.TotalSeconds);

            this.mockDependencies.NotificationManager.VerifyAll();
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResultWhenSuccessfullyCreatingANotice()
        {
            this.mockDependencies.NotificationManager
                .Setup(mgr => mgr.CreateNoticeAsync(
                    It.IsAny<string>(),
                    It.IsAny<ExperimentMetadata>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .Returns(Task.FromResult(this.mockNoticeInstance))
                .Verifiable();

            CreatedAtActionResult result = await this.controller.CreateNoticeAsync("any queue", this.mockNotice, CancellationToken.None)
                as CreatedAtActionResult;

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Value);
            Assert.AreEqual(StatusCodes.Status201Created, result.StatusCode);
            Assert.AreEqual(this.mockNoticeInstance, result.Value);
        }

        [Test]
        public async Task ControllerHandlesErrorsAsExpectedDuringAttemptsToCreateANotice()
        {
            foreach (var entry in NotificationsControllerTests.potentialErrors)
            {
                // Key = Exception
                // Value = The expected HTTP status code in the result
                this.mockDependencies.NotificationManager
                    .Setup(mgr => mgr.CreateNoticeAsync(
                        It.IsAny<string>(),
                        It.IsAny<ExperimentMetadata>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<TimeSpan?>()))
                    .Throws(entry.Key);

                ObjectResult result = await this.controller.CreateNoticeAsync("any queue", this.mockNotice, CancellationToken.None)
                    as ObjectResult;

                Assert.IsNotNull(result);
                Assert.AreEqual(entry.Value, result.StatusCode);
                Assert.IsInstanceOf<ProblemDetails>(result.Value);
            }
        }

        [Test]
        public async Task ControllerReadsNoticesFromTheExpectedQueueLocation()
        {
            // Mock Setup:
            // Verify the controller reads notices from the expected queue.
            string expectedQueueName = "any queue";

            this.mockDependencies.NotificationManager
                .Setup(mgr => mgr.PeekNoticeAsync(expectedQueueName, It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()))
                .Returns(Task.FromResult(null as ExperimentMetadataInstance))
                .Verifiable();

            await this.controller.GetNoticeAsync(expectedQueueName, CancellationToken.None);

            this.mockDependencies.NotificationManager.VerifyAll();
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResultWhenNoticesExist()
        {
            // Mock Setup:
            // Verify the controller places the notice on the expected queue.

            this.mockDependencies.NotificationManager
                .Setup(mgr => mgr.PeekNoticeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()))
                .Returns(Task.FromResult(this.mockNoticeInstance));

            OkObjectResult result = await this.controller.GetNoticeAsync("any queue", CancellationToken.None)
                as OkObjectResult;

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Value);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.AreEqual(this.mockNoticeInstance, result.Value);
        }

        [Test]
        public async Task ControllerHandlesErrorsAsExpectedDuringAttemptsToGetANotice()
        {
            foreach (var entry in NotificationsControllerTests.potentialErrors)
            {
                // Key = Exception
                // Value = The expected HTTP status code in the result
                this.mockDependencies.NotificationManager
                    .Setup(mgr => mgr.PeekNoticeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()))
                    .Throws(entry.Key);

                ObjectResult result = await this.controller.GetNoticeAsync("any queue", CancellationToken.None)
                    as ObjectResult;

                Assert.IsNotNull(result);
                Assert.AreEqual(entry.Value, result.StatusCode);
                Assert.IsInstanceOf<ProblemDetails>(result.Value);
            }
        }
    }
}
