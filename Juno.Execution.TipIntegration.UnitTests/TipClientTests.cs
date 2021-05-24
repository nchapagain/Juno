namespace Juno.Execution.TipIntegration
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Moq;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;
    using Polly;
    using TipGateway.Entities;
    using TipGateway.FabricApi;
    using TipGateway.FabricApi.Requests;
    using TipGateway.Interfaces;

    [TestFixture]
    [Category("Unit")]
    public class TipClientTests
    {
        private ITipClient tipClient;
        private Mock<ITipGateway> mockTipGateway;

        [SetUp]
        public void Setup()
        {
            this.mockTipGateway = new Mock<ITipGateway>(MockBehavior.Loose);

            var mockConfiguration = new ConfigurationBuilder()
                .SetBasePath(Path.Combine(
                 Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                 @"Configuration"))
                .AddJsonFile($"juno-local.testsettings.json")
                .Build();

            this.tipClient = new TipClient(mockConfiguration, this.mockTipGateway.Object, Policy.NoOpAsync());
        }

        [Test]
        public async Task TipClientCreatesTipSessionsWithRightParameters()
        {
            TipParameters parameters = new TipParameters()
            {
                DurationInMinutes = 123,
                CandidateNodesId = new List<string>() { "node1", "node2" },
                NodeCount = 2,
                ClusterName = "mockCluster",
                Region = "mockRegion",
                AutopilotServices = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("PFKey", "PFValue") }
            };

            this.mockTipGateway.Setup(g => g.CreateAsync(It.IsAny<NewTipNodeSession>()))
                .Callback<NewTipNodeSession>((nodeSession) =>
                {
                    Assert.AreEqual(123, nodeSession.DurationInMinutes);
                    Assert.AreEqual(2, nodeSession.NodeCount);
                    Assert.AreEqual("node2", nodeSession.CandidateNodes[1]);
                    Assert.AreEqual("mockRegion", nodeSession.Region);
                    Assert.AreEqual("mockCluster", nodeSession.ClusterName);
                    Assert.AreEqual("PFValue", nodeSession.AutopilotServices[0].Value);
                })
                .ReturnsAsync(new TipNodeSessionChange())
                .Verifiable();

            await this.tipClient.CreateTipSessionAsync(parameters, CancellationToken.None).ConfigureAwait(false);
            this.mockTipGateway.Verify();
        }

        [Test]
        public async Task TipClientDeletesSessionWithRightId()
        {
            this.mockTipGateway.Setup(g => g.DeleteAsync(It.IsAny<DeleteTipNodeSession>()))
                .Callback<DeleteTipNodeSession>((nodeSession) =>
                {
                    Assert.AreEqual("DeleteId", nodeSession.TipNodeSessionId);
                })
                .ReturnsAsync("DeletedChangeId")
                .Verifiable();

            await this.tipClient.DeleteTipSessionAsync("DeleteId", CancellationToken.None).ConfigureAwait(false);
            this.mockTipGateway.Verify();
        }

        [Test]
        public async Task TipClientGetsSessionWithRightId()
        {
            this.mockTipGateway.Setup(g => g.GetAsync(It.IsAny<string>()))
                .Callback<string>((nodeSession) =>
                {
                    Assert.AreEqual("SomeSessionId", nodeSession);
                })
                .ReturnsAsync(new TipNodeSessionDetails())
                .Verifiable();

            await this.tipClient.GetTipSessionAsync("SomeSessionId", CancellationToken.None).ConfigureAwait(false);
            this.mockTipGateway.Verify();
        }

        [Test]
        public async Task TipClientGetsSessionChangeWithRightIds()
        {
            this.mockTipGateway.Setup(g => g.GetChangeDetailsAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((nodeSession, nodeChange) =>
                {
                    Assert.AreEqual("SomeSessionId", nodeSession);
                    Assert.AreEqual("SomeChangeIdId", nodeChange);
                })
                .ReturnsAsync(new TipNodeSessionChangeDetails())
                .Verifiable();

            await this.tipClient.GetTipSessionChangeAsync("SomeSessionId", "SomeChangeIdId", CancellationToken.None).ConfigureAwait(false);
            this.mockTipGateway.Verify();
        }

        [Test]
        public async Task TipClientCanExtendTipSession()
        {
            this.mockTipGateway.Setup(g => g.UpdateAsync(It.IsAny<UpdateTipNodeSession>()))
                .Callback<UpdateTipNodeSession>((nodeSession) =>
                {
                    Assert.AreEqual("SomeSessionId", nodeSession.TipNodeSessionId);
                    Assert.AreEqual(321, nodeSession.ExtendExpirationTimeInMinutes);
                })
                .ReturnsAsync(new TipNodeSessionChangeDetails())
                .Verifiable();

            await this.tipClient.ExtendTipSessionAsync("SomeSessionId", 321, CancellationToken.None).ConfigureAwait(false);
            this.mockTipGateway.Verify();
        }

        [Test]
        public async Task TipClientCanApplyPilotfishServices()
        {
            List<KeyValuePair<string, string>> pilotfishServices = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>("IPU1", "Location1"),
                new KeyValuePair<string, string>("BIOS2", "Location2"),
            };

            this.mockTipGateway.Setup(g => g.UpdateAsync(It.IsAny<UpdateTipNodeSession>()))
                .Callback<UpdateTipNodeSession>((nodeSession) =>
                {
                    Assert.AreEqual("SomeSessionId", nodeSession.TipNodeSessionId);
                    Assert.AreEqual("Location1", nodeSession.AutopilotServices[0].Value);
                    Assert.AreEqual("BIOS2", nodeSession.AutopilotServices[1].Key);
                })
                .ReturnsAsync(new TipNodeSessionChangeDetails())
                .Verifiable();

            await this.tipClient.ApplyPilotFishServicesAsync("SomeSessionId", pilotfishServices, CancellationToken.None).ConfigureAwait(false);
            this.mockTipGateway.Verify();
        }

        [Test]
        public async Task TipClientCanApplyOverlakePilotfishServices()
        {
            List<KeyValuePair<string, string>> pilotfishServices = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>("IPU1", "Location1"),
                new KeyValuePair<string, string>("BIOS2", "Location2"),
            };

            this.mockTipGateway.Setup(g => g.UpdateAsync(It.IsAny<UpdateTipNodeSession>()))
                .Callback<UpdateTipNodeSession>((nodeSession) =>
                {
                    Assert.AreEqual("SomeSessionId", nodeSession.TipNodeSessionId);
                    Assert.AreEqual("Location1", nodeSession.OverlakeAutopilotServices[0].Value);
                    Assert.AreEqual("BIOS2", nodeSession.OverlakeAutopilotServices[1].Key);
                })
                .ReturnsAsync(new TipNodeSessionChangeDetails())
                .Verifiable();

            await this.tipClient.ApplyPilotFishServicesOnSocAsync("SomeSessionId", pilotfishServices, CancellationToken.None).ConfigureAwait(false);
            this.mockTipGateway.Verify();
        }

        [Test]
        [TestCase(NodeState.Excluded)]
        [TestCase(NodeState.Raw)]
        public Task TipClientSetNodeStateChangeRequestMakesTheExpectedNodeStateChange(NodeState nodeState)
        {
            TipNodeSessionDetails expectedSession = new TipNodeSessionDetails
            {
                Id = Guid.NewGuid().ToString(),
                Cluster = "Cluster du jour",
                CreatedBy = "SomebodyAnybody",
                Region = "AnyRegion",
                Status = TipNodeSessionStatus.Created,
                Nodes = new List<NodeSnapshot>
                {
                    new NodeSnapshot
                    {
                        NodeId = Guid.NewGuid().ToString(),
                        State = nodeState.ToString()
                    }
                }
            };

            // The logic will get the details for the active TiP session first.
            this.mockTipGateway.Setup(g => g.GetDetailsAsync(It.IsAny<string>()))
                .Callback<string>((tipSessionId) =>
                {
                    Assert.AreEqual(expectedSession.Id, tipSessionId);
                })
                .ReturnsAsync(expectedSession);

            // If the TiP sesssion is found, it will make the request to set the node state.
            this.mockTipGateway.Setup(g => g.InvokeFabricApiAsync(It.IsAny<FCApi>(), It.IsAny<string>(), It.IsAny<JObject>()))
                .Callback<FCApi, string, JObject>((state, tipSessionId, parameters) =>
                {
                    Assert.AreEqual(FCApi.ForceNodeState, state);
                    Assert.AreEqual(expectedSession.Id, tipSessionId);
                    Assert.IsNotNull(parameters);

                    ForceNodeStateRequest actualRequest = parameters.ToObject<ForceNodeStateRequest>();
                    Assert.IsNotNull(actualRequest);
                    Assert.AreEqual(expectedSession.Nodes.First().NodeId, actualRequest.HostNodeId);
                    Assert.AreEqual(nodeState, actualRequest.NodeState);
                })
                .ReturnsAsync(new TipNodeSessionChangeDetails());

            return this.tipClient.SetNodeStateAsync(expectedSession.Id, expectedSession.Nodes.First().NodeId, nodeState, CancellationToken.None);
        }
    }
}