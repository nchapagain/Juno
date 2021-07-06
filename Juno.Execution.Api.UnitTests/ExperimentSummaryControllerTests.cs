namespace Juno.Execution.Api
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.CRC;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ExperimentSummaryControllerTests
    {
        private ExperimentSummaryController controller;

        private FixtureDependencies mockDependencies;

        [SetUp]
        public void SetupTest()
        {
            this.mockDependencies = new FixtureDependencies();
            this.controller = new ExperimentSummaryController(this.mockDependencies.AnalysisCacheManager.Object, NullLogger.Instance);

            this.mockDependencies.AnalysisCacheManager.Setup(mgr => mgr.GetBusinessSignalsAsync(It.IsAny<string>(), CancellationToken.None))
                .Returns(Task.FromResult(It.IsAny<IEnumerable<BusinessSignal>>()));

            this.mockDependencies.AnalysisCacheManager.Setup(mgr => mgr.GetExperimentsProgressAsync(It.IsAny<string>(), CancellationToken.None))
                .Returns(Task.FromResult(It.IsAny<IEnumerable<ExperimentProgress>>()));
        }

        // Constructor Unit Tests
        [Test]
        public void ControllerContructorsValidateParameters()
        {
            Assert.Throws<ArgumentException>(() => new ExperimentSummaryController(null));
        }

        [Test]
        public void ControllerConstructorSetsParametersToExpectedValues()
        {
            ExperimentSummaryController controller = new ExperimentSummaryController(this.mockDependencies.AnalysisCacheManager.Object);
            EqualityAssert.PropertySet(controller, "AnalysisCacheManager", this.mockDependencies.AnalysisCacheManager.Object);
            EqualityAssert.PropertySet(controller, "Logger", NullLogger.Instance);

            controller = new ExperimentSummaryController(this.mockDependencies.AnalysisCacheManager.Object, this.mockDependencies.Logger.Object);

            EqualityAssert.PropertySet(controller, "AnalysisCacheManager", this.mockDependencies.AnalysisCacheManager.Object);
            EqualityAssert.PropertySet(controller, "Logger", this.mockDependencies.Logger.Object);
        }

        [Test]
        public async Task ControllerCallsRequiredMethodsToDetermineExperimentsSummaries()
        {
            await this.controller.GetExperimentSummaryAsync(CancellationToken.None);

            this.mockDependencies.AnalysisCacheManager.Verify(mgr => mgr.GetBusinessSignalsAsync(
                It.IsAny<string>(), CancellationToken.None));

            this.mockDependencies.AnalysisCacheManager.Verify(mgr => mgr.GetExperimentsProgressAsync(
                It.IsAny<string>(), CancellationToken.None));
        }

        [Test]
        public async Task ControllerErrorsAsExpectedDuringBusinessSignalsError()
        {
            this.mockDependencies.AnalysisCacheManager
                .Setup(mgr => mgr.GetBusinessSignalsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Throws(new Exception("Something went wrong"));

            var actionResult = await this.controller.GetExperimentSummaryAsync(CancellationToken.None);

            Assert.IsNotNull(actionResult);
            Assert.IsInstanceOf<ProblemDetails>((actionResult as ObjectResult).Value);
            Assert.AreEqual("Something went wrong", ((actionResult as ObjectResult).Value as ProblemDetails).Detail);
            Assert.AreEqual(500, ((actionResult as ObjectResult).Value as ProblemDetails).Status);
        }

        [Test]
        public async Task ControllerErrorsAsExpectedDuringProgressErrors()
        {
            this.mockDependencies.AnalysisCacheManager
                .Setup(mgr => mgr.GetExperimentsProgressAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Throws(new Exception("Something went wrong"));

            var actionResult = await this.controller.GetExperimentSummaryAsync(CancellationToken.None);

            Assert.IsNotNull(actionResult);
            Assert.IsInstanceOf<ProblemDetails>((actionResult as ObjectResult).Value);
            Assert.AreEqual("Something went wrong", ((actionResult as ObjectResult).Value as ProblemDetails).Detail);
            Assert.AreEqual(500, ((actionResult as ObjectResult).Value as ProblemDetails).Status);
        }
    }
}