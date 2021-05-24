namespace Juno.Agent.Api
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

        // Agent Heartbeat Creation Test Cases
        [Test]
        public async Task ControllerReturnsSuccessfulResponseWhenCreatingAgentHeartbeat()
        {
            this.mockDependencies.HeartbeatManager
                .Setup(mgr => mgr.CreateHeartbeatAsync(It.IsAny<AgentHeartbeat>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(this.mockHeartbeatInstance);

            CreatedAtActionResult result = await this.controller.CreateHeartbeatAsync(this.mockHeartbeat,  CancellationToken.None)
                as CreatedAtActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status201Created, result.StatusCode);
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResponseWhenErrorsOccurInCreatingHeartbeat()
        {
            foreach (var entry in AgentHeartbeatsControllerTests.potentialErrors)
            {
                // Key = Exception, Value = The expected HTTP status code in the result
                this.mockDependencies.HeartbeatManager
                    .Setup(mgr => mgr.CreateHeartbeatAsync(this.mockHeartbeat, CancellationToken.None))
                    .Throws(entry.Key);

                ObjectResult result = await this.controller.CreateHeartbeatAsync(this.mockHeartbeat, CancellationToken.None) as ObjectResult;

                Assert.IsNotNull(result);
                Assert.AreEqual(entry.Value, result.StatusCode);
                Assert.IsInstanceOf<ProblemDetails>(result.Value);
            }
        }

        [Test]
        public void ControllerValidatesRequiredParametersWhenCreatingAgentHeartbeat()
        {
            Assert.ThrowsAsync<ArgumentException>(async () => await this.controller.CreateHeartbeatAsync(null, CancellationToken.None));
        }

        // Agent Heartbeat Get Test Cases
        [Test]
        public async Task ControllerReturnsExpectedHeartbeat()
        {
            this.mockDependencies.HeartbeatManager
                .Setup(mgr => mgr.GetHeartbeatAsync(It.IsAny<AgentIdentification>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(this.mockHeartbeatInstance);

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
        public void ControllerValidatesRequiredParametersWhenUpdatingAgentStepData(string invalidValue)
        {
            Assert.ThrowsAsync<ArgumentException>(async () => await this.controller.GetHeartbeatAsync(invalidValue, It.IsAny<CancellationToken>()));
        }
    }
}