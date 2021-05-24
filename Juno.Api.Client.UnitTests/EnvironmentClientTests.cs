namespace Juno.Api.Client
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using AutoFixture;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Rest;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class EnvironmentClientTests
    {
        private IEnvironmentClient environmentClient;
        private Mock<IRestClient> mockRestClient;
        private Fixture mockFixture;

        [SetUp]
        public void SetupTests()
        {
            this.mockRestClient = new Mock<IRestClient>();
            this.environmentClient = new EnvironmentClient(this.mockRestClient.Object, new Uri("https://anyuri.com"));
            this.mockFixture = new Fixture();
            this.mockFixture.SetupEnvironmentSelectionMocks();
        }

        [Test]
        public void GetEnvironmentsAsyncCallsExpectedApi()
        {
            EnvironmentQuery query = this.mockFixture.Create<EnvironmentQuery>();
            this.mockRestClient.Setup(client => client.PostAsync(
                It.IsAny<Uri>(),
                It.IsAny<HttpContent>(),
                It.IsAny<CancellationToken>()))
                .Callback<Uri, HttpContent, CancellationToken>((uri, token, options) =>
                {
                    Assert.IsTrue(uri.PathAndQuery.Equals($"/api/environments"));
                });

            _ = this.environmentClient.ReserveEnvironmentsAsync(query, CancellationToken.None).GetAwaiter().GetResult();
        }

        [Test]
        public void CreateReservedNodesAsyncCallsExpectedApi()
        {
            var reservedNodes = new ReservedNodes(new List<EnvironmentCandidate>() { new EnvironmentCandidate(node: "fake_nodeId", cluster: "fake_cluster") });
            this.mockRestClient.Setup(client => client.PostAsync(
                It.IsAny<Uri>(),
                It.IsAny<HttpContent>(),
                It.IsAny<CancellationToken>()))
                .Callback<Uri, HttpContent, CancellationToken>((uri, token, options) =>
                {
                    Assert.IsTrue(uri.PathAndQuery.Equals($"/api/environments/reservedNodes"));
                });

            _ = this.environmentClient.CreateReservedNodesAsync(reservedNodes, CancellationToken.None).GetAwaiter().GetResult();
        }

        [Test]
        public void DeleteReservedNodesAsyncCallsExpectedApi()
        {
            var reservedNodes = new ReservedNodes(new List<EnvironmentCandidate>() { new EnvironmentCandidate(node: "fake_nodeId", cluster: "fake_cluster") });
            this.mockRestClient.Setup(client => client.PostAsync(
                It.IsAny<Uri>(),
                It.IsAny<HttpContent>(),
                It.IsAny<CancellationToken>()))
                .Callback<Uri, HttpContent, CancellationToken>((uri, token, options) =>
                {
                    Assert.IsTrue(uri.PathAndQuery.Equals($"/api/environments/reservedNodes"));
                });

            _ = this.environmentClient.DeleteReservedNodesAsync(reservedNodes, CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}
