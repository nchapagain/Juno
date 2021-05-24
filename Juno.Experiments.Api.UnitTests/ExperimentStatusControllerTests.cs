namespace Juno.Experiments.Api
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Api.Client;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.CRC;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;
    using Polly;

    [TestFixture]
    [Category("Unit")]
    public class ExperimentStatusControllerTests
    {
        private ExperimentStatusController controller;
        private FixtureDependencies mockDependencies;
        private ExecutionClient mockExecutionClient;

        [SetUp]
        public void SetupTest()
        {
            this.mockDependencies = new FixtureDependencies(MockBehavior.Strict);
            this.mockDependencies.SetupAgentMocks();
            this.mockExecutionClient = new ExecutionClient(this.mockDependencies.RestClient.Object, new Uri("https://any/uri"), Policy.NoOpAsync());
            this.controller = new ExperimentStatusController(this.mockExecutionClient, this.mockDependencies.Configuration, NullLogger.Instance);
        }

        [Test]
        public void ControllerContructorsValidateParameters()
        {
            Assert.Throws<ArgumentException>(() => new ExperimentStatusController(null, this.mockDependencies.Configuration));
            Assert.Throws<ArgumentException>(() => new ExperimentStatusController(this.mockExecutionClient, null));
        }

        [Test]
        public void ControllerConstructorSetsParametersToExpectedValues()
        {
            ExperimentStatusController controller = new ExperimentStatusController(this.mockExecutionClient, this.mockDependencies.Configuration);
            EqualityAssert.PropertySet(controller, "Client", this.mockExecutionClient);
            EqualityAssert.PropertySet(controller, "Configuration", this.mockDependencies.Configuration);
            EqualityAssert.PropertySet(controller, "Logger", NullLogger.Instance);

            controller = new ExperimentStatusController(this.mockExecutionClient, this.mockDependencies.Configuration, this.mockDependencies.Logger.Object);

            EqualityAssert.PropertySet(controller, "Client", this.mockExecutionClient);
            EqualityAssert.PropertySet(controller, "Configuration", this.mockDependencies.Configuration);
            EqualityAssert.PropertySet(controller, "Logger", this.mockDependencies.Logger.Object);
        }

        [Test]
        public async Task ControllerMakesTheExpectedCallsToGetExperimentInstanceStatuses()
        {
            this.mockDependencies.RestClient
                .Setup(client => client.GetAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<HttpCompletionOption>()))
                .Verifiable();

            await this.controller.GetExperimentInstanceStatusesAsync("AnyExperiment", CancellationToken.None);

            this.mockDependencies.RestClient.VerifyAll();
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResponseWhenGettingExperimentInstanceStatuses()
        {
            List<JObject> expectedSummaries = new List<JObject>
            {
                JObject.FromObject(new
                {
                    id = Guid.NewGuid(),
                    name = "AnyExperiment"
                }),
                JObject.FromObject(new
                {
                    id = Guid.NewGuid(),
                    name = "AnyExperiment"
                })
            };

            this.mockDependencies.RestClient
              .Setup(client => client.GetAsync(
                  It.IsAny<Uri>(),
                  It.IsAny<CancellationToken>(),
                  It.IsAny<HttpCompletionOption>()))
              .ReturnsAsync(expectedSummaries.ToHttpResponse(HttpStatusCode.OK));

            OkObjectResult result = await this.controller.GetExperimentInstanceStatusesAsync("AnyExperiment", CancellationToken.None)
                as OkObjectResult;

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Value);
            Assert.IsTrue(result.StatusCode == StatusCodes.Status200OK);
        }

        [Test]
        public async Task ControllerHandlesErrorsAsExpectedDuringAnAttemptToGetExperimentInstanceStatuses()
        {
            ProblemDetails errorDetails = new ProblemDetails
            {
                Title = "Experiment retrieval error"
            };

            this.mockDependencies.RestClient
              .Setup(client => client.GetAsync(
                  It.IsAny<Uri>(),
                  It.IsAny<CancellationToken>(),
                  It.IsAny<HttpCompletionOption>()))
              .ReturnsAsync(errorDetails.ToHttpResponse(HttpStatusCode.NotFound));

            ObjectResult result = await this.controller.GetExperimentInstanceStatusesAsync("AnyExperiment", CancellationToken.None)
                as ObjectResult;

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Value);
            Assert.AreEqual(StatusCodes.Status404NotFound, result.StatusCode);
            Assert.IsInstanceOf<ProblemDetails>(result.Value);
            Assert.AreEqual(errorDetails.Title, (result.Value as ProblemDetails).Title);
        }
    }
}
