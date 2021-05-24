namespace Juno.Agent.Api
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.DataManagement;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Repository.Storage;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class AgentFileUploadControllerTests
    {
        private AgentFileUploadController controller;
        private Mock<IExperimentFileManager> mockFileManager;
        private Mock<ILogger> mockLogger;

        [SetUp]
        public void SetupTest()
        {
            this.mockFileManager = new Mock<IExperimentFileManager>();
            this.mockLogger = new Mock<ILogger>();
            this.controller = new AgentFileUploadController(this.mockFileManager.Object, NullLogger.Instance);

            // Setup the mock HTTP Request components. This allows the unit tests to set
            // properties on the ControllerBase.Request property that defines an individual
            // user call/request.
            this.controller.ControllerContext = new ControllerContext(new ActionContext(
                new DefaultHttpContext(),
                new Microsoft.AspNetCore.Routing.RouteData(),
                new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor()));
        }

        [Test]
        public void ControllerConstructorsValidateRequiredParameters()
        {
            Assert.Throws<ArgumentException>(() => new AgentFileUploadController(null));
        }

        [Test]
        public void ControllerSetsParametersToExpectedValues()
        {
            var controller = new AgentFileUploadController(this.mockFileManager.Object);

            EqualityAssert.PropertySet(controller, "FileManager", this.mockFileManager.Object);
            EqualityAssert.PropertySet(controller, "Logger", NullLogger.Instance);

            controller = new AgentFileUploadController(this.mockFileManager.Object, this.mockLogger.Object);

            EqualityAssert.PropertySet(controller, "FileManager", this.mockFileManager.Object);
            EqualityAssert.PropertySet(controller, "Logger", this.mockLogger.Object);
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase("    ")]
        public void ControllerValidatesRequiredParameters(string invalidParameter)
        {
            string validExperimentId = Guid.NewGuid().ToString();
            string validFileName = "AnyFileName";

            Assert.Throws<ArgumentException>(() => this.controller.UploadFileAsync(
                invalidParameter, validFileName, "AnyAgentType", "AnyAgentId", DateTime.UtcNow, CancellationToken.None)
            .GetAwaiter().GetResult());

            Assert.Throws<ArgumentException>(() => this.controller.UploadFileAsync(
                validExperimentId, invalidParameter, "AnyAgentType", "AnyAgentId", DateTime.UtcNow, CancellationToken.None)
            .GetAwaiter().GetResult());
        }

        [Test]
        public async Task ControllerUploadsTheExpectedFile()
        {
            string experimentId = Guid.NewGuid().ToString();
            string fileName = "AnyFileName";
            string agentType = "AnyType";
            string agentId = "Cluster01,Node01";
            DateTime timestamp = DateTime.UtcNow;

            using (Stream fileStream = new MemoryStream(Encoding.UTF8.GetBytes("Any file content")))
            {
                this.controller.Request.ContentType = "text/plain; charset=utf-8";
                this.controller.Request.Body = fileStream;

                this.mockFileManager
                    .Setup(mgr => mgr.CreateFileAsync(
                        experimentId,
                        fileName,
                        It.Is<BlobStream>(blob => object.ReferenceEquals(fileStream, blob.Content)),
                        timestamp,
                        It.IsAny<CancellationToken>(),
                        agentType,
                        agentId))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                OkResult result = await this.controller.UploadFileAsync(experimentId, fileName, agentType, agentId, timestamp, CancellationToken.None)
                    as OkResult;

                Assert.IsNotNull(result);
                Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);

                this.mockFileManager.VerifyAll();
            }
        }

        [Test]
        public async Task ControllerUploadsTheExpectedFileWhenAgentInformationIsProvided()
        {
            string experimentId = Guid.NewGuid().ToString();
            string fileName = "AnyFileName";
            string agentType = "AnyType";
            string agentId = "Cluster01,Node01";
            DateTime timestamp = DateTime.UtcNow;

            using (Stream fileStream = new MemoryStream(Encoding.UTF8.GetBytes("Any file content")))
            {
                this.controller.Request.ContentType = "text/plain; charset=utf-8";
                this.controller.Request.Body = fileStream;

                this.mockFileManager
                    .Setup(mgr => mgr.CreateFileAsync(
                        experimentId,
                        fileName,
                        It.Is<BlobStream>(blob => object.ReferenceEquals(fileStream, blob.Content)),
                        timestamp,
                        It.IsAny<CancellationToken>(),
                        agentType,
                        agentId))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                OkResult result = await this.controller.UploadFileAsync(experimentId, fileName, agentType, agentId, timestamp, CancellationToken.None)
                    as OkResult;

                Assert.IsNotNull(result);
                Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);

                this.mockFileManager.VerifyAll();
            }
        }

        [Test]
        public async Task ControllerRequiresTheContentTypeAndEncodingToBeDefinedWhenUploadingFiles()
        {
            string experimentId = Guid.NewGuid().ToString();
            string fileName = "AnyFileName";
            string agentType = "AnyType";
            string agentId = "Cluster01,Node01";
            DateTime timestamp = DateTime.UtcNow;

            using (Stream fileStream = new MemoryStream(Encoding.UTF8.GetBytes("Any file content")))
            {
                this.controller.Request.Body = fileStream;

                this.mockFileManager
                    .Setup(mgr => mgr.CreateFileAsync(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<BlobStream>(),
                        It.IsAny<DateTime>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<string>(),
                        It.IsAny<string>()))
                    .Returns(Task.CompletedTask);

                // ContentType not defined
                ObjectResult errorResult1 = await this.controller.UploadFileAsync(experimentId, fileName, agentType, agentId, timestamp, CancellationToken.None)
                    as ObjectResult;

                Assert.IsNotNull(errorResult1);
                Assert.AreEqual(StatusCodes.Status400BadRequest, errorResult1.StatusCode);

                // ContentType is missing the encoding definition
                this.controller.Request.ContentType = "text/plain";
                ObjectResult errorResult2 = await this.controller.UploadFileAsync(experimentId, fileName, agentType, agentId, timestamp, CancellationToken.None)
                   as ObjectResult;

                Assert.IsNotNull(errorResult2);
                Assert.AreEqual(StatusCodes.Status400BadRequest, errorResult2.StatusCode);

                // Content Type correctly defined
                this.controller.Request.ContentType = "text/plain; charset=utf-8";
                OkResult successResult = await this.controller.UploadFileAsync(experimentId, fileName, agentType, agentId, timestamp, CancellationToken.None)
                   as OkResult;

                Assert.IsNotNull(successResult);
                Assert.AreEqual(StatusCodes.Status200OK, successResult.StatusCode);
            }
        }
    }
}
