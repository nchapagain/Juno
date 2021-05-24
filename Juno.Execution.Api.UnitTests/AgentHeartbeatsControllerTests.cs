namespace Juno.Execution.Api
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno;
    using Juno.Contracts;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Repository;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class AgentHeartbeatsControllerTests
    {
        private static Dictionary<Exception, int> potentialErrors = new Dictionary<Exception, int>
        {
            [new SchemaException()] = StatusCodes.Status400BadRequest,
            [new DataStoreException(DataErrorReason.DataAlreadyExists)] = StatusCodes.Status409Conflict,
            [new DataStoreException(DataErrorReason.DataNotFound)] = StatusCodes.Status404NotFound,
            [new DataStoreException(DataErrorReason.ETagMismatch)] = StatusCodes.Status412PreconditionFailed,
            [new DataStoreException(DataErrorReason.PartitionKeyMismatch)] = StatusCodes.Status412PreconditionFailed,
            [new DataStoreException(DataErrorReason.Undefined)] = StatusCodes.Status500InternalServerError,
            [new Exception("All other errors")] = StatusCodes.Status500InternalServerError
        };

        private Fixture mockFixture;
        private FixtureDependencies mockDependencies;
        private AgentHeartbeatsController controller;
        private AgentHeartbeat mockHeartbeat;
        private AgentIdentification mockAgentIdentification;
        private AgentHeartbeatInstance mockHeartbeatInstance;

        [SetUp]
        public void Setup()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupAgentMocks();

            this.mockDependencies = new FixtureDependencies();
            this.controller = new AgentHeartbeatsController(this.mockDependencies.HeartbeatManager.Object, NullLogger.Instance);
            this.mockHeartbeat = this.mockFixture.Create<AgentHeartbeat>();
            this.mockHeartbeatInstance = this.mockFixture.Create<AgentHeartbeatInstance>();
            this.mockAgentIdentification = this.mockFixture.Create<AgentIdentification>();
        }

        // Constructor Validation Test
        [Test]
        public void ControllerConstructorsValidatesRequiredHeartbeatManagerParameter()
        {
            Assert.Throws<ArgumentException>(() => new AgentHeartbeatsController(null));
        }

        // Get Requests for Agent Heartbeats
        [Test]
        public async Task ControllerReturnsExpectedHeartbeat()
        {
            this.mockDependencies.HeartbeatManager
                .Setup(mgr => mgr.GetHeartbeatAsync(It.IsAny<AgentIdentification>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(this.mockHeartbeatInstance));

            ObjectResult result = await this.controller.GetHeartbeatAsync(this.mockAgentIdentification.ToString(), CancellationToken.None)
                as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.IsTrue(object.ReferenceEquals(this.mockHeartbeatInstance, result.Value));
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResponseWhenErrorsOccurInGettingHeartbeats()
        {
            // Key = Exception, Value = The expected HTTP status code in the result
            foreach (var entry in AgentHeartbeatsControllerTests.potentialErrors)
            {
                this.mockDependencies.HeartbeatManager
                    .Setup(mgr => mgr.GetHeartbeatAsync(It.IsAny<AgentIdentification>(), It.IsAny<CancellationToken>()))
                    .Throws(entry.Key);

                ObjectResult result = await this.controller.GetHeartbeatAsync(this.mockAgentIdentification.ToString(), CancellationToken.None)
                    as ObjectResult;

                Assert.IsNotNull(result);
                Assert.AreEqual(entry.Value, result.StatusCode);
                Assert.IsInstanceOf<ProblemDetails>(result.Value);
            }
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void ControllerValidatedRequiredParametersWhenGettingHeartbeats(string invalidParameter)
        {
            Assert.ThrowsAsync<ArgumentException>(async () => await this.controller.GetHeartbeatAsync(invalidParameter, It.IsAny<CancellationToken>()));
        }
    }
}