namespace Juno.Extensions.AspNetCore
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Repository;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using NUnit.Framework;

    using CosmosTable = Microsoft.Azure.Cosmos.Table;
    using Storage = Microsoft.Azure.Storage;

    [TestFixture]
    [Category("Unit")]
    public class ApiControllerExtensionsTests
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

        private TestController controller;
        private string anyEvent;
        private EventContext anyEventContext;
        private ILogger anyLogger;

        [SetUp]
        public void Setup()
        {
            this.controller = new TestController();
            this.anyEvent = "AnyEvent";
            this.anyEventContext = new EventContext(Guid.NewGuid());
            this.anyLogger = NullLogger.Instance;
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase("    ")]
        public void ExecuteApiOperationAsyncExtensionValidatesRequiredParameters(string invalidValue)
        {
            Func<Task<IActionResult>> validAction = () => Task.FromResult(new StatusCodeResult(200) as IActionResult);

            Assert.Throws<ArgumentException>(() => this.controller.ExecuteApiOperationAsync(invalidValue, this.anyEventContext, this.anyLogger, validAction)
                .GetAwaiter().GetResult());

            Assert.Throws<ArgumentException>(() => this.controller.ExecuteApiOperationAsync(this.anyEvent, null, this.anyLogger, validAction)
                .GetAwaiter().GetResult());

            Assert.Throws<ArgumentException>(() => this.controller.ExecuteApiOperationAsync(this.anyEvent, this.anyEventContext, null, validAction)
                .GetAwaiter().GetResult());

            Assert.Throws<ArgumentException>(() => this.controller.ExecuteApiOperationAsync(this.anyEvent, this.anyEventContext, this.anyLogger, null)
               .GetAwaiter().GetResult());
        }

        [Test]
        public void ExecuteApiOperationAsyncExtensionExecutesTheActionProvided()
        {
            bool actionInvoked = false;
            Func<Task<IActionResult>> expectedAction = () => Task.Run(() =>
            {
                actionInvoked = true;
                return new StatusCodeResult(200) as IActionResult;
            });

            this.controller.ExecuteApiOperationAsync(this.anyEvent, this.anyEventContext, this.anyLogger, expectedAction)
                .GetAwaiter().GetResult();

            Assert.IsTrue(actionInvoked);
        }

        [Test]
        public void ExecuteApiOperationAsyncExtensionReturnsTheExpectedResultOnSuccess()
        {
            IActionResult expectedResult = new StatusCodeResult(200);
            Func<Task<IActionResult>> action = () => Task.Run(() =>
            {
                return expectedResult as IActionResult;
            });

            IActionResult result = this.controller.ExecuteApiOperationAsync(this.anyEvent, this.anyEventContext, this.anyLogger, action)
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsInstanceOf<StatusCodeResult>(result);
            Assert.IsTrue(object.ReferenceEquals(expectedResult, result));
        }

        [Test]
        public void ExecuteApiOperationAsyncExtensionReturnsTheExpectedResultWhenErrorsOccur()
        {
            foreach (var entry in ApiControllerExtensionsTests.potentialErrors)
            {
                // Key = Exception
                // Value = The expected HTTP status code in the result
                Func<Task<IActionResult>> action = () => throw entry.Key;

                ObjectResult result = this.controller.ExecuteApiOperationAsync(this.anyEvent, this.anyEventContext, this.anyLogger, action)
                    .GetAwaiter().GetResult() as ObjectResult;

                Assert.IsNotNull(result);
                Assert.AreEqual(entry.Value, result.StatusCode);
            }
        }

        // Used ONLY to expose the extension methods.
        private class TestController : ControllerBase
        {
        }
    }
}