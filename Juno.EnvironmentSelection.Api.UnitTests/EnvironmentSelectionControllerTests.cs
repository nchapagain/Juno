namespace Juno.EnvironmentSelection.Api
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Contracts;
    using Juno.EnvironmentSelection.Service;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Infrastructure;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Repository;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;

    using CosmosTable = Microsoft.Azure.Cosmos.Table;
    using Storage = Microsoft.Azure.Storage;

    [TestFixture]
    [Category("Unit")]
    public class EnvironmentSelectionControllerTests
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

        private Mock<IEnvironmentSelectionService> mockSelectionService;
        private EnvironmentSelectionController controller;
        private Fixture mockFixture;

        [SetUp]
        public void SetupTests()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupEnvironmentSelectionMocks();

            this.mockSelectionService = new Mock<IEnvironmentSelectionService>();
            this.controller = new EnvironmentSelectionController(this.mockSelectionService.Object, NullLogger.Instance);
        }

        [Test]
        public void EnvironmentSelectionControllerConstructorValidatesParameters()
        {
            Assert.Throws<ArgumentException>(() => new EnvironmentSelectionController(null, NullLogger.Instance));
        }

        [Test]
        public void GetEnvironmentFromFilterAsyncValidatesParameters()
        {
            Assert.ThrowsAsync<ArgumentException>(() => this.controller.ReserveEnvironmentsAsync(null, CancellationToken.None));
        }

        [Test]
        public void GetEnviromentPassesCorrectValuesToEnvironmentSelectionInterface()
        {
            EnvironmentQuery expectedQuery = this.mockFixture.Create<EnvironmentQuery>();

            this.mockSelectionService.Setup(serv => serv.GetEnvironmentCandidatesAsync(It.IsAny<EnvironmentQuery>(), It.IsAny<CancellationToken>()))
                .Callback<EnvironmentQuery, CancellationToken>((filters, token) =>
                {
                    Assert.IsTrue(object.ReferenceEquals(expectedQuery, filters));
                })
                .Returns(Task.FromResult(new List<EnvironmentCandidate> { this.mockFixture.Create<EnvironmentCandidate>() } as IEnumerable<EnvironmentCandidate>));

            ObjectResult result = this.controller.ReserveEnvironmentsAsync(expectedQuery, CancellationToken.None).GetAwaiter().GetResult() as ObjectResult;
        }

        [Test]
        public void GetEnvironmentReturnsExpectedValueWhenGivenValidFilters()
        {
            EnvironmentQuery expectedQuery = this.mockFixture.Create<EnvironmentQuery>();
            IEnumerable<EnvironmentCandidate> expectedResult = new List<EnvironmentCandidate> { this.mockFixture.Create<EnvironmentCandidate>() };

            this.mockSelectionService.Setup(serv => serv.GetEnvironmentCandidatesAsync(It.IsAny<EnvironmentQuery>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(expectedResult));

            ObjectResult result = this.controller.ReserveEnvironmentsAsync(expectedQuery, CancellationToken.None).GetAwaiter().GetResult() as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(expectedResult, result.Value as IEnumerable<EnvironmentCandidate>);
        }

        [Test]
        public async Task ControllerHandlesExceptionsWhenGettingEnvironmentCandidates()
        {
            EnvironmentQuery expectedQuery = this.mockFixture.Create<EnvironmentQuery>();
            foreach (var entry in EnvironmentSelectionControllerTests.potentialErrors)
            {
                this.mockSelectionService.Setup(mgr => mgr.GetEnvironmentCandidatesAsync(It.IsAny<EnvironmentQuery>(), It.IsAny<CancellationToken>()))
                    .Throws(entry.Key);

                IActionResult result = await this.controller.ReserveEnvironmentsAsync(expectedQuery, CancellationToken.None).ConfigureAwait(false);

                Assert.IsNotNull(result);
                Assert.AreEqual(entry.Value, (result as IStatusCodeActionResult).StatusCode);
                Assert.IsInstanceOf<ProblemDetails>((result as ObjectResult).Value);
            }
        }
    }
}
