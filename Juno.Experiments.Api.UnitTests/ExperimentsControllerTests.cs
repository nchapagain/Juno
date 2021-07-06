namespace Juno.Experiments.Api
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.Contracts.Validation;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.CRC;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;
    using Polly;

    [TestFixture]
    [Category("Unit")]
    public class ExperimentsControllerTests
    {
        private ExperimentsController controller;
        private Fixture mockFixture;
        private FixtureDependencies mockDependencies;
        private Experiment mockExperiment;
        private ExperimentInstance mockExperimentInstance;
        private ExperimentStepInstance mockExperimentStepInstance;
        private ExperimentMetadata mockExperimentContext;
        private ExperimentMetadataInstance mockExperimentContextInstance;
        private ExperimentMetadataInstance mockNotificationInstance;
        private ExecutionClient mockExecutionClient;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks();
            this.mockDependencies = new FixtureDependencies(MockBehavior.Strict);
            this.mockExecutionClient = new ExecutionClient(this.mockDependencies.RestClient.Object, new Uri("https://any/uri"), Policy.NoOpAsync());
            this.controller = new ExperimentsController(this.mockExecutionClient, this.mockDependencies.Configuration, NullLogger.Instance);

            this.mockExperiment = this.mockFixture.Create<Experiment>();
            this.mockExperimentInstance = this.mockFixture.Create<ExperimentInstance>();
            this.mockExperimentStepInstance = this.mockFixture.Create<ExperimentStepInstance>();
            this.mockExperimentContext = this.mockFixture.Create<ExperimentMetadata>();
            this.mockExperimentContextInstance = this.mockFixture.Create<ExperimentMetadataInstance>();
            this.mockNotificationInstance = this.mockFixture.Create<ExperimentMetadataInstance>();

            ExperimentValidation.Instance.Clear();
        }

        [Test]
        public void ControllerContructorsValidateParameters()
        {
            Assert.Throws<ArgumentException>(() => new ExperimentsController(null, this.mockDependencies.Configuration));
            Assert.Throws<ArgumentException>(() => new ExperimentsController(this.mockExecutionClient, null));
        }

        [Test]
        public void ControllerConstructorSetsParametersToExpectedValues()
        {
            ExperimentsController controller = new ExperimentsController(this.mockExecutionClient, this.mockDependencies.Configuration);
            EqualityAssert.PropertySet(controller, "Client", this.mockExecutionClient);
            EqualityAssert.PropertySet(controller, "Configuration", this.mockDependencies.Configuration);
            EqualityAssert.PropertySet(controller, "Logger", NullLogger.Instance);

            controller = new ExperimentsController(this.mockExecutionClient, this.mockDependencies.Configuration, this.mockDependencies.Logger.Object);

            EqualityAssert.PropertySet(controller, "Client", this.mockExecutionClient);
            EqualityAssert.PropertySet(controller, "Configuration", this.mockDependencies.Configuration);
            EqualityAssert.PropertySet(controller, "Logger", this.mockDependencies.Logger.Object);
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResponseWhenAnExperimentIsCancelled()
        {
            // To cancel an experiment, a CancellationProvider step is added to the existing steps. So, the client
            // will return a response indicating that step was successfully created to indicate the experiment is
            // in the process of being cancelled.
            this.mockDependencies.RestClient
                .Setup(client => client.PostAsync(It.IsAny<Uri>(), It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.Created));

            string experimentId = Guid.NewGuid().ToString();
            NoContentResult result = await this.controller.CancelExperimentAsync(experimentId, true, CancellationToken.None) as NoContentResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status204NoContent, result.StatusCode);
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResponseWhenAnExperimentCancellationRequestFails()
        {
            // To cancel an experiment, a CancellationProvider step is added to the existing steps. So, the client
            // will return a response indicating that step was successfully created to indicate the experiment is
            // in the process of being cancelled.
            this.mockDependencies.RestClient
                .Setup(client => client.PostAsync(It.IsAny<Uri>(), It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound));

            string experimentId = Guid.NewGuid().ToString();
            ObjectResult result = await this.controller.CancelExperimentAsync(experimentId, true, CancellationToken.None) as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status404NotFound, result.StatusCode);
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
        public async Task ControllerValidatesTheSchemaOfAnExperimentOnlyWithoutCreatingAnInstanceInTheSystemWhenTheValidateFlagIsUsed()
        {
            bool experimentCreated = false;
            this.mockDependencies.RestClient
                .Setup(client => client.PostAsync(It.IsAny<Uri>(), It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()))
                .Callback<Uri, HttpContent, CancellationToken>((uri, content, token) => experimentCreated = true)
                .Returns(Task.FromResult(this.mockExperimentInstance.ToHttpResponse()));

            ObjectResult result = await this.controller.CreateExperimentAsync(this.mockExperiment, CancellationToken.None, validate: true) as ObjectResult;

            Assert.IsFalse(experimentCreated);
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResponseWhenTheValidateFlagIsUsedAndTheExperimentProvidedIsValid()
        {
            Mock<IValidationRule<Experiment>> mockValidation = new Mock<IValidationRule<Experiment>>();
            mockValidation.Setup(validation => validation.Validate(It.IsAny<Experiment>()))
                .Returns(new ValidationResult(true));

            ExperimentValidation.Instance.Add(mockValidation.Object);
            OkResult result = await this.controller.CreateExperimentAsync(this.mockExperiment, CancellationToken.None, validate: true) as OkResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResponseWhenTheValidateFlagIsUsedAndTheExperimentProvidedIsNotValid()
        {
            Mock<IValidationRule<Experiment>> mockValidation = new Mock<IValidationRule<Experiment>>();
            mockValidation.Setup(validation => validation.Validate(It.IsAny<Experiment>()))
                .Returns(new ValidationResult(false));

            ExperimentValidation.Instance.Add(mockValidation.Object);
            ObjectResult result = await this.controller.CreateExperimentAsync(this.mockExperiment, CancellationToken.None, validate: true) as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status400BadRequest, result.StatusCode);
        }

        [Test]
        public async Task ControllerMakesTheExpectedCallsToSetupTheExperiment()
        {
            // Expected Flow:
            // 1) Create the experiment instance/document.
            // 2) Create the experiment context instance document.
            // 3) Create the experiment steps
            // 4) Queue up the experiment notice-of-work.

            // Mock Setup:
            // Create the experiment instance document.
            this.mockDependencies.RestClient.SetupPostExperiment()
                .Returns(Task.FromResult(this.mockExperimentInstance.ToHttpResponse()))
                .Verifiable();

            // Mock Setup:
            // Create the experiment context document.
            this.mockDependencies.RestClient.SetupPostExperimentContext(this.mockExperimentInstance.Id)
                .Returns(Task.FromResult(this.mockExperimentContextInstance.ToHttpResponse()))
                .Verifiable();

            // Mock Setup:
            // Create the experiment steps.
            this.mockDependencies.RestClient.SetupPostExperimentSteps(this.mockExperimentInstance.Id)
                .Returns(Task.FromResult(new ExperimentStepInstance[] { this.mockExperimentStepInstance }.ToHttpResponse()))
                .Verifiable();

            // Mock Setup:
            // Queue notice-of-work for experiment
            this.mockDependencies.RestClient.SetupPostNotification()
               .Returns(Task.FromResult(this.mockNotificationInstance.ToHttpResponse()))
               .Verifiable();

            await this.controller.CreateExperimentAsync(this.mockExperiment, CancellationToken.None);

            this.mockDependencies.RestClient.VerifyAll();
        }

        [Test]
        public Task ControllerPublishesANotificationOfWorkOnTheExpectedWorkQueue()
        {
            // Expected Flow:
            // 1) Create the experiment instance/document.
            // 2) Create the experiment context instance document.
            // 3) Create the experiment steps
            // 4) Queue up the experiment notice-of-work.

            // Mock Setup:
            // Create the experiment instance document.
            this.mockDependencies.RestClient.SetupPostExperiment()
                .Returns(Task.FromResult(this.mockExperimentInstance.ToHttpResponse()));

            // Mock Setup:
            // Create the experiment context document.
            this.mockDependencies.RestClient.SetupPostExperimentContext(this.mockExperimentInstance.Id)
                .Returns(Task.FromResult(this.mockExperimentContextInstance.ToHttpResponse()));

            // Mock Setup:
            // Create the experiment steps.
            this.mockDependencies.RestClient.SetupPostExperimentSteps(this.mockExperimentInstance.Id)
                .Returns(Task.FromResult(new ExperimentStepInstance[] { this.mockExperimentStepInstance }.ToHttpResponse()));

            // Mock Setup:
            // Queue notice-of-work for experiment
            this.mockDependencies.RestClient.SetupPostNotification()
               .Returns(Task.FromResult(this.mockNotificationInstance.ToHttpResponse()))
               .Callback<Uri, HttpContent, CancellationToken>((uri, content, token) =>
               {
                   EnvironmentSettings settings = EnvironmentSettings.Initialize(this.mockDependencies.Configuration);
                   Assert.IsTrue(uri.PathAndQuery == $"/api/notifications?workQueue={settings.ExecutionSettings.WorkQueueName}");
               })
               .Verifiable();

            return this.controller.CreateExperimentAsync(this.mockExperiment, CancellationToken.None);
        }

        [Test]
        public Task ControllerPublishesANotificationOfWorkOnTheExpectedWorkQueueWhenTheQueueNameIsProvided()
        {
            string expectedWorkQueue = "anyQueueYouLike";

            // Expected Flow:
            // 1) Create the experiment instance/document.
            // 2) Create the experiment context instance document.
            // 3) Create the experiment steps
            // 4) Queue up the experiment notice-of-work.

            // Mock Setup:
            // Create the experiment instance document.
            this.mockDependencies.RestClient.SetupPostExperiment()
                .Returns(Task.FromResult(this.mockExperimentInstance.ToHttpResponse()));

            // Mock Setup:
            // Create the experiment context document.
            this.mockDependencies.RestClient.SetupPostExperimentContext(this.mockExperimentInstance.Id)
                .Returns(Task.FromResult(this.mockExperimentContextInstance.ToHttpResponse()));

            // Mock Setup:
            // Create the experiment steps.
            this.mockDependencies.RestClient.SetupPostExperimentSteps(this.mockExperimentInstance.Id)
                .Returns(Task.FromResult(new ExperimentStepInstance[] { this.mockExperimentStepInstance }.ToHttpResponse()));

            // Mock Setup:
            // Queue notice-of-work for experiment
            this.mockDependencies.RestClient.SetupPostNotification()
               .Returns(Task.FromResult(this.mockNotificationInstance.ToHttpResponse()))
               .Callback<Uri, HttpContent, CancellationToken>((uri, content, token) =>
               {
                   EnvironmentSettings settings = EnvironmentSettings.Initialize(this.mockDependencies.Configuration);
                   Assert.IsTrue(uri.PathAndQuery == $"/api/notifications?workQueue={expectedWorkQueue}");
               })
               .Verifiable();

            return this.controller.CreateExperimentAsync(this.mockExperiment, CancellationToken.None, workQueue: expectedWorkQueue);
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResultWhenAnExperimentIsCreatedSuccessfully()
        {
            // Mock Setup:
            // Create the experiment instance document.
            this.mockDependencies.RestClient.SetupPostExperiment()
                .Returns(Task.FromResult(this.mockExperimentInstance.ToHttpResponse()))
                .Verifiable();

            // Mock Setup:
            // Create the experiment context document.
            this.mockDependencies.RestClient.SetupPostExperimentContext(this.mockExperimentInstance.Id)
                .Returns(Task.FromResult(this.mockExperimentContextInstance.ToHttpResponse()))
                .Verifiable();

            // Mock Setup:
            // Create the experiment steps.
            this.mockDependencies.RestClient.SetupPostExperimentSteps(this.mockExperimentInstance.Id)
                .Returns(Task.FromResult(new ExperimentStepInstance[] { this.mockExperimentStepInstance }.ToHttpResponse()))
                .Verifiable();

            // Mock Setup:
            // Queue notice-of-work for experiment
            this.mockDependencies.RestClient.SetupPostNotification()
               .Returns(Task.FromResult(this.mockNotificationInstance.ToHttpResponse()))
               .Verifiable();

            CreatedAtActionResult result = await this.controller.CreateExperimentAsync(this.mockExperiment, CancellationToken.None)
                as CreatedAtActionResult;

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Value);
            Assert.IsTrue(result.StatusCode == StatusCodes.Status201Created);
            Assert.IsTrue(result.Value.Equals(this.mockExperimentInstance));
        }

        [Test]
        public async Task ControllerHandlesErrorsAsExpectedDuringAnAttemptToCreateTheExperiment()
        {
            ProblemDetails errorDetails = new ProblemDetails
            {
                Title = "Experiment creation error"
            };

            // Mock Setup:
            // An error occurs when trying to create the experiment.
            this.mockDependencies.RestClient.SetupPostExperiment()
                .Returns(Task.FromResult(errorDetails.ToHttpResponse(HttpStatusCode.Conflict)));

            ObjectResult result = await this.controller.CreateExperimentAsync(this.mockExperiment, CancellationToken.None)
                as ObjectResult;

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Value);
            Assert.AreEqual(StatusCodes.Status409Conflict, result.StatusCode);
            Assert.IsInstanceOf<ProblemDetails>(result.Value);
            Assert.AreEqual(errorDetails.Title, (result.Value as ProblemDetails).Title);
        }

        [Test]
        public async Task ControllerHandlesErrorsAsExpectedDuringAnAttemptToCreateTheExperimentContext()
        {
            ProblemDetails errorDetails = new ProblemDetails
            {
                Title = "Experiment context creation error"
            };

            // Mock Setup:
            // Create the experiment instance document.
            this.mockDependencies.RestClient.SetupPostExperiment()
              .Returns(Task.FromResult(this.mockExperimentInstance.ToHttpResponse()));

            // Mock Setup:
            // An error occurs when trying to create the experiment context
            this.mockDependencies.RestClient.SetupPostExperimentContext(this.mockExperimentInstance.Id)
                .Returns(Task.FromResult(errorDetails.ToHttpResponse(HttpStatusCode.Conflict)));

            ObjectResult result = await this.controller.CreateExperimentAsync(this.mockExperiment, CancellationToken.None)
                as ObjectResult;

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Value);
            Assert.AreEqual(StatusCodes.Status409Conflict, result.StatusCode);
            Assert.IsInstanceOf<ProblemDetails>(result.Value);
            Assert.AreEqual(errorDetails.Title, (result.Value as ProblemDetails).Title);
        }

        [Test]
        public async Task ControllerHandlesErrorsAsExpectedDuringAnAttemptToCreateTheExperimentSteps()
        {
            ProblemDetails errorDetails = new ProblemDetails
            {
                Title = "Experiment step creation error"
            };

            // Mock Setup:
            // Create the experiment instance document.
            this.mockDependencies.RestClient.SetupPostExperiment()
              .Returns(Task.FromResult(this.mockExperimentInstance.ToHttpResponse()));

            // Mock Setup:
            // Create the experiment context document.
            this.mockDependencies.RestClient.SetupPostExperimentContext(this.mockExperimentInstance.Id)
                .Returns(Task.FromResult(this.mockExperimentContextInstance.ToHttpResponse()));

            // Mock Setup:
            // An error occurs while trying to create the experiment steps.
            this.mockDependencies.RestClient.SetupPostExperimentSteps(this.mockExperimentInstance.Id)
                .Returns(Task.FromResult(errorDetails.ToHttpResponse(HttpStatusCode.Conflict)))
                .Verifiable();

            ObjectResult result = await this.controller.CreateExperimentAsync(this.mockExperiment, CancellationToken.None)
                as ObjectResult;

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Value);
            Assert.AreEqual(StatusCodes.Status409Conflict, result.StatusCode);
            Assert.IsInstanceOf<ProblemDetails>(result.Value);
            Assert.AreEqual(errorDetails.Title, (result.Value as ProblemDetails).Title);
        }

        [Test]
        public async Task ControllerHandlesErrorsAsExpectedDuringAnAttemptToQueueANoticeOfWork()
        {
            ProblemDetails errorDetails = new ProblemDetails
            {
                Title = "Notification send error"
            };

            // Mock Setup:
            // Create the experiment instance document.
            this.mockDependencies.RestClient.SetupPostExperiment()
              .Returns(Task.FromResult(this.mockExperimentInstance.ToHttpResponse()));

            // Mock Setup:
            // Create the experiment context document.
            this.mockDependencies.RestClient.SetupPostExperimentContext(this.mockExperimentInstance.Id)
                .Returns(Task.FromResult(this.mockExperimentContextInstance.ToHttpResponse()));

            // Mock Setup:
            // Create the experiment steps.
            this.mockDependencies.RestClient.SetupPostExperimentSteps(this.mockExperimentInstance.Id)
                .Returns(Task.FromResult(new ExperimentStepInstance[] { this.mockExperimentStepInstance }.ToHttpResponse()))
                .Verifiable();

            // Mock Setup:
            // An error occurs while trying to send the notification
            this.mockDependencies.RestClient.SetupPostNotification()
              .Returns(Task.FromResult(errorDetails.ToHttpResponse(HttpStatusCode.Conflict)))
              .Verifiable();

            ObjectResult result = await this.controller.CreateExperimentAsync(this.mockExperiment, CancellationToken.None)
                as ObjectResult;

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Value);
            Assert.AreEqual(StatusCodes.Status409Conflict, result.StatusCode);
            Assert.IsInstanceOf<ProblemDetails>(result.Value);
            Assert.AreEqual(errorDetails.Title, (result.Value as ProblemDetails).Title);
        }

        [Test]
        public async Task ControllerMakesTheExpectedCallsToGetAnExperiment()
        {
            // Mock Setup:
            // Get the experiment instance/document
            this.mockDependencies.RestClient.SetupGetExperiment(this.mockExperimentInstance.Id)
                .Returns(Task.FromResult(this.mockExperimentInstance.ToHttpResponse()))
                .Verifiable();

            await this.controller.GetExperimentAsync(this.mockExperimentInstance.Id, CancellationToken.None);

            this.mockDependencies.RestClient.VerifyAll();
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResponseWhenGettingAnExperiment()
        {
            // Mock Setup:
            // Get the experiment instance/document
            this.mockDependencies.RestClient.SetupGetExperiment(this.mockExperimentInstance.Id)
                .Returns(Task.FromResult(this.mockExperimentInstance.ToHttpResponse()))
                .Verifiable();

            OkObjectResult result = await this.controller.GetExperimentAsync(this.mockExperimentInstance.Id, CancellationToken.None)
                as OkObjectResult;

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Value);
            Assert.IsTrue(result.StatusCode == StatusCodes.Status200OK);
            Assert.IsTrue(result.Value.Equals(this.mockExperimentInstance));
        }

        [Test]
        public async Task ControllerHandlesErrorsAsExpectedDuringAnAttemptToGetAnExperiment()
        {
            ProblemDetails errorDetails = new ProblemDetails
            {
                Title = "Experiment retrieval error"
            };

            // Mock Setup:
            // An error occurs while attempting to get the experiment instance
            this.mockDependencies.RestClient.SetupGetExperiment(this.mockExperimentInstance.Id)
                .Returns(Task.FromResult(errorDetails.ToHttpResponse(HttpStatusCode.NotFound)))
                .Verifiable();

            ObjectResult result = await this.controller.GetExperimentAsync(this.mockExperimentInstance.Id, CancellationToken.None)
                as ObjectResult;

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Value);
            Assert.AreEqual(StatusCodes.Status404NotFound, result.StatusCode);
            Assert.IsInstanceOf<ProblemDetails>(result.Value);
            Assert.AreEqual(errorDetails.Title, (result.Value as ProblemDetails).Title);
        }

        [Test]
        public async Task ControllerMakesTheExpectedCallsToGetExperimentSteps()
        {
            // Mock Setup:
            // Get the experiment steps
            this.mockDependencies.RestClient.SetupGetExperimentSteps(this.mockExperimentInstance.Id)
                .Returns(Task.FromResult(new ExperimentStepInstance[] { this.mockExperimentStepInstance }.ToHttpResponse()))
                .Verifiable();

            await this.controller.GetExperimentStepsAsync(this.mockExperimentInstance.Id, CancellationToken.None);

            this.mockDependencies.RestClient.VerifyAll();
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResponseWhenGettingExperimentSteps()
        {
            // Mock Setup:
            // Get the experiment steps
            this.mockDependencies.RestClient.SetupGetExperimentSteps(this.mockExperimentInstance.Id)
                .Returns(Task.FromResult(new ExperimentStepInstance[] { this.mockExperimentStepInstance }.ToHttpResponse()))
                .Verifiable();

            OkObjectResult result = await this.controller.GetExperimentStepsAsync(this.mockExperimentInstance.Id, CancellationToken.None, View.Full)
                as OkObjectResult;

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Value);
            Assert.IsTrue(result.StatusCode == StatusCodes.Status200OK);
            Assert.IsInstanceOf<List<ExperimentStepInstance>>(result.Value);
            Assert.IsTrue((result.Value as List<ExperimentStepInstance>).First().Equals(this.mockExperimentStepInstance));
        }

        [Test]
        public async Task ControllerHandlesErrorsAsExpectedDuringAnAttemptToGetExperimentSteps()
        {
            ProblemDetails errorDetails = new ProblemDetails
            {
                Title = "Experiment step retrieval error"
            };

            // Mock Setup:
            // An error occurs while attempting to get the experiment instance
            this.mockDependencies.RestClient.SetupGetExperimentSteps(this.mockExperimentInstance.Id)
                .Returns(Task.FromResult(errorDetails.ToHttpResponse(HttpStatusCode.NotFound)))
                .Verifiable();

            ObjectResult result = await this.controller.GetExperimentStepsAsync(this.mockExperimentInstance.Id, CancellationToken.None)
                as ObjectResult;

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Value);
            Assert.AreEqual(StatusCodes.Status404NotFound, result.StatusCode);
            Assert.IsInstanceOf<ProblemDetails>(result.Value);
            Assert.AreEqual(errorDetails.Title, (result.Value as ProblemDetails).Title);
        }
    }
}