namespace Juno.Experiments.Api
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.CRC;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;
    using Polly;

    [TestFixture]
    [Category("Unit")]
    public class ExperimentSummaryControllerTests
    {
        private ExperimentSummaryController controller;

        private FixtureDependencies mockDependencies;

        private ExecutionClient mockExecutionClient;

        [SetUp]
        public void SetupTest()
        {
            this.mockDependencies = new FixtureDependencies(MockBehavior.Strict);
            this.mockExecutionClient = new ExecutionClient(this.mockDependencies.RestClient.Object, new Uri("https://any/uri"), Policy.NoOpAsync());
            this.controller = new ExperimentSummaryController(this.mockExecutionClient, this.mockDependencies.Configuration, NullLogger.Instance);
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
        public async Task ControllerMakesTheExpectedCallsToGetExperimentsSummaries()
        {
            this.mockDependencies.RestClient.SetupGetExperimentsSummaries()
                .Returns(Task.FromResult(new List<ExperimentSummary>().ToHttpResponse()))
                .Verifiable();

            await this.controller.GetExperimentsSummariesAsync(CancellationToken.None);

            this.mockDependencies.RestClient.VerifyAll();
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResponseWhenGettingExperimentsSummaries()
        {
            var experimentSummaries = new List<ExperimentSummary>()
            {
                new ExperimentSummary("x", "x", 50, new DateTime(2020, 07, 20), new List<BusinessSignalKPI>() { new BusinessSignalKPI("x", "x", "x", 10, 20, "Red") })
            };

            this.mockDependencies.RestClient.SetupGetExperimentsSummaries()
                .Returns(Task.FromResult(experimentSummaries.ToHttpResponse()))
                .Verifiable();

            OkObjectResult result = await this.controller.GetExperimentsSummariesAsync(CancellationToken.None)
                as OkObjectResult;

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Value);
            Assert.IsTrue(result.StatusCode == StatusCodes.Status200OK);
            Assert.AreEqual(experimentSummaries.Count, (result.Value as IEnumerable<ExperimentSummary>).Count());
            Assert.AreEqual(experimentSummaries.FirstOrDefault().ExperimentName, (result.Value as IEnumerable<ExperimentSummary>).FirstOrDefault().ExperimentName);
            Assert.AreEqual(experimentSummaries.FirstOrDefault().Revision, (result.Value as IEnumerable<ExperimentSummary>).FirstOrDefault().Revision);
        }

        [Test]
        public async Task ControllerErrorsAsExpectedWhileDeterminingExperimentsSummaries()
        {
            ProblemDetails errorDetails = new ProblemDetails
            {
                Title = "Error while determining Experiments Summaries."
            };

            // Mock Setup:
            // An error occurs when trying to create the experiment context
            this.mockDependencies.RestClient.SetupGetExperimentsSummaries()
                .Returns(Task.FromResult(errorDetails.ToHttpResponse(HttpStatusCode.Conflict)));

            ObjectResult result = await this.controller.GetExperimentsSummariesAsync(CancellationToken.None)
                as ObjectResult;

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Value);
            Assert.AreEqual(StatusCodes.Status409Conflict, result.StatusCode);
            Assert.IsInstanceOf<ProblemDetails>(result.Value);
            Assert.AreEqual(errorDetails.Title, (result.Value as ProblemDetails).Title);
        }
    }
}