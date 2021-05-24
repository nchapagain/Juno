namespace Juno.EnvironmentSelection.Api
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.EnvironmentSelection.Service;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class NodeReservationControllerTests
    {
        private NodeReservationController controller;
        private Mock<IEnvironmentSelectionService> mockEnvironmentSelectionService;
        private ReservedNodes reservedNodes;

        [SetUp]
        public void SetupTests()
        {
            this.mockEnvironmentSelectionService = new Mock<IEnvironmentSelectionService>();
            this.controller = new NodeReservationController(this.mockEnvironmentSelectionService.Object, NullLogger.Instance);
            this.reservedNodes = new ReservedNodes(new List<EnvironmentCandidate>() { new EnvironmentCandidate(node: "fake_nodeId", cluster: "fake_cluster") });
        }

        [Test]
        public void EnvironmentReservedNodesControllerConstructorValidatesParameters()
        {
            Assert.Throws<ArgumentException>(() => new NodeReservationController(null, NullLogger.Instance));
        }

        [Test]
        public void CreateReservedNodesAsyncValidatesParameters()
        {
            Assert.ThrowsAsync<ArgumentException>(() => this.controller.CreateReservedNodesAsync(null, CancellationToken.None));
        }

        [Test]
        public void DeleteReservedNodesAsyncValidatesParameters()
        {
            Assert.ThrowsAsync<ArgumentException>(() => this.controller.DeleteReservedNodesAsync(null, CancellationToken.None));
        }

        [Test]
        public void CreateReservedNodesAsyncReturnsExpectedValueWhenGivenValidNodes()
        {
            this.mockEnvironmentSelectionService.Setup(ess => ess.ReserveEnvironmentCandidatesAsync(It.IsAny<IEnumerable<EnvironmentCandidate>>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(this.reservedNodes.Nodes));

            var result = this.controller.CreateReservedNodesAsync(this.reservedNodes, CancellationToken.None).GetAwaiter().GetResult() as ObjectResult;

            Assert.IsNotNull(result);
            IEnumerable<EnvironmentCandidate> actualResult = result.Value as IEnumerable<EnvironmentCandidate>;
            Assert.AreEqual(200, result.StatusCode);
            Assert.AreEqual(this.reservedNodes.Nodes, actualResult);
        }

        [Test]
        public void CreateReservedNodesAsyncReturnsExpectedValueWhenGivenInvalidNodes()
        {
            ReservedNodes invalidComponent = new ReservedNodes(new List<EnvironmentCandidate>() { new EnvironmentCandidate() });
            var result = this.controller.CreateReservedNodesAsync(invalidComponent, CancellationToken.None).GetAwaiter().GetResult() as ObjectResult;

            Assert.IsNotNull(result);
            string actualResult = (string)result.Value;
            Assert.AreEqual(400, result.StatusCode);
            Assert.AreEqual("NodeId and ClusterId must be non-default values to reserve successfully.", actualResult);
        }

        [Test]
        public void DeleteReservedNodesAsyncReturnsExpectedValueWhenGivenValidNodes()
        {
            this.mockEnvironmentSelectionService.Setup(ess => ess.DeleteReservationsAsync(It.IsAny<IEnumerable<EnvironmentCandidate>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(this.reservedNodes.Nodes));

            var result = this.controller.DeleteReservedNodesAsync(this.reservedNodes, CancellationToken.None).GetAwaiter().GetResult() as AcceptedResult;

            Assert.IsNotNull(result);
            IEnumerable<EnvironmentCandidate> actualResult = result.Value as IEnumerable<EnvironmentCandidate>;
            Assert.AreEqual(202, result.StatusCode);
            Assert.AreEqual(this.reservedNodes.Nodes, actualResult);
        }

        [Test]
        public void DeleteReservedNodesAsyncReturnsNodeFoundWhenGivenInvalidNodes()
        {
            ReservedNodes invalidComponent = new ReservedNodes(new List<EnvironmentCandidate>() { new EnvironmentCandidate() });
            var result = this.controller.DeleteReservedNodesAsync(invalidComponent, CancellationToken.None).GetAwaiter().GetResult() as ObjectResult;

            Assert.IsNotNull(result);
            string actualResult = (string)result.Value;
            Assert.AreEqual(400, result.StatusCode);
            Assert.AreEqual("NodeId and ClusterId must be non-default values to delete reservation successfully.", actualResult);
        }
    }
}
