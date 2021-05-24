namespace Juno.Experiments.Api
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;
    using Polly;

    [TestFixture]
    [Category("Unit")]
    public class EnvironmentSelectionControllerTests
    {
        private Fixture mockFixture;
        private EnvironmentSelectionController controller;
        private Mock<IEnvironmentClient> mockExecutionClient;

        [SetUp]
        public void SetupTests()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupEnvironmentSelectionMocks();
            this.mockExecutionClient = new Mock<IEnvironmentClient>();
            this.controller = new EnvironmentSelectionController(this.mockExecutionClient.Object, NullLogger.Instance);
        }

        [Test]
        public void EnvironmentSelectionControllerValidatesParameters()
        {
            Assert.Throws<ArgumentException>(() => new EnvironmentSelectionController(null, NullLogger.Instance));
        }

        [Test]
        public void GetEnvironmentFromFilterAsyncValidatesParameters()
        {
            Assert.ThrowsAsync<ArgumentException>(() => this.controller.ReserveEnvironmentsAsync(null, CancellationToken.None));
        }

        [Test]
        public void GetEnvironmentFromFilterPostsCorrectFilters()
        {
            EnvironmentQuery query = this.mockFixture.Create<EnvironmentQuery>();
            IEnumerable<EnvironmentCandidate> expectedResult = new List<EnvironmentCandidate> { this.mockFixture.Create<EnvironmentCandidate>() };

            this.mockExecutionClient.Setup(client => client.ReserveEnvironmentsAsync(It.IsAny<EnvironmentQuery>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(expectedResult.ToHttpResponse()));
            ObjectResult result = this.controller.ReserveEnvironmentsAsync(query, CancellationToken.None).GetAwaiter().GetResult() as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.AreEqual(expectedResult, result.Value as IEnumerable<EnvironmentCandidate>);
        }
    }
}
