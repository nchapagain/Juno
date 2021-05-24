using System;
using System.Collections.Generic;
using System.Text;

namespace Juno.Execution.Providers.Environment
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Execution.TipIntegration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;
    using TipGateway.Entities;

    [TestFixture]
    [Category("Unit")]
    public class CrossRackTipCreationProviderTests
    {
        private ProviderFixture mockFixture;
        private Mock<ITipClient> mockTipClient;
        private List<TipRack> mockRacks;
        private List<TipSession> mockTipSessions;
        private CrossRackTipCreationProvider.State mockState;
        private CrossRackTipCreationProvider provider;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new ProviderFixture(typeof(CrossRackTipCreationProvider));
            this.mockFixture.SetupExperimentMocks(ExperimentType.AB);
            this.mockTipClient = new Mock<ITipClient>();

            this.mockFixture.Services.AddSingleton(NullLogger.Instance);
            this.mockFixture.Services.AddSingleton(this.mockTipClient.Object);

            this.mockState = new CrossRackTipCreationProvider.State
            {
                StepTimeout = DateTime.UtcNow.AddMinutes(10),
                Timeout = TimeSpan.FromMinutes(10),
            };

            this.mockRacks = new List<TipRack>
            {
                new TipRack()
                {
                    RackLocation = "rack1",
                    ClusterName = "cluster1",
                    CpuId = "cpu1",
                    Region = "region1",
                    RemainingTipSessions = 4,
                    PreferredVmSku = "sku-a",
                    SupportedVmSkus = new List<string>() { "sku-a" },
                    NodeList = new List<string>() { "node-a1", "node-a2", "node-a3", "node-a4" },
                    MachinePoolName = "Cluster01MP1"
                },
                new TipRack()
                {
                    RackLocation = "rack2",
                    ClusterName = "cluster2",
                    CpuId = "cpu1",
                    Region = "region2",
                    RemainingTipSessions = 4,
                    PreferredVmSku = "sku-a",
                    SupportedVmSkus = new List<string>() { "sku-a", "sku-b", "sku-c" },
                    NodeList = new List<string>() { "node-b1", "node-b2", "node-b3", "node-b4" },
                    MachinePoolName = "Cluster01MP1"
                }
            };

            this.mockTipSessions = new List<TipSession>
            {
                new TipSession()
                {
                    TipSessionId = "session-node-a1",
                    ClusterName = "cluster1",
                    Region = "region1",
                    GroupName = "Group A",
                    NodeId = "node-a1",
                    ChangeIdList = new List<string>() { "Change-node-a1" },
                    Status = TipSessionStatus.Creating,
                    CreatedTimeUtc = DateTime.UtcNow.Subtract(TimeSpan.FromDays(1)),
                    ExpirationTimeUtc = DateTime.UtcNow.Add(TimeSpan.FromDays(1)),
                    DeletedTimeUtc = DateTime.MaxValue,
                    SupportedVmSkus = new List<string>() { "sku-a" },
                    PreferredVmSku = "sku-a"
                },
                new TipSession()
                {
                    TipSessionId = "session-node-a2",
                    ClusterName = "cluster1",
                    Region = "region1",
                    GroupName = "Group B",
                    NodeId = "node-a2",
                    ChangeIdList = new List<string>() { "Change-node-a2" },
                    Status = TipSessionStatus.Creating,
                    CreatedTimeUtc = DateTime.UtcNow.Subtract(TimeSpan.FromDays(1)),
                    ExpirationTimeUtc = DateTime.UtcNow.Add(TimeSpan.FromDays(1)),
                    DeletedTimeUtc = DateTime.MaxValue,
                    SupportedVmSkus = new List<string>() { "sku-a", "sku-b", "sku-c" },
                    PreferredVmSku = "sku-a"
                }
            };

            this.provider = new CrossRackTipCreationProvider(this.mockFixture.Services);
            this.SetupMockDefaults();
        }

        [Test]
        public void ProviderThrowsExcpetionIfThereIsNotTheExpectedRacks()
        {
            string treatmentCluster = Guid.NewGuid().ToString();
            this.mockFixture.Component.Parameters.Add("treatmentGroup", "Group A");
            this.mockFixture.Component.Parameters.Add("treatmentCluster", treatmentCluster);

            this.mockFixture.DataClient.OnGetEntityPool().ReturnsAsync(TipRack.ToEnvironmentEntities(this.mockRacks.ToList()));

            ExecutionResult result = this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(ExecutionStatus.Failed, result.Status);
        }

        [Test]
        public void ProviderPostsExpectedTipSessionParametersToTipClient()
        {
            string treatmentCluster = Guid.NewGuid().ToString();
            this.mockFixture.Component.Parameters.Add("treatmentGroup", "Group A");
            this.mockFixture.Component.Parameters.Add("treatmentCluster", treatmentCluster);
            this.mockRacks.First().ClusterName = treatmentCluster;
            this.mockFixture.DataClient.OnGetEntityPool().ReturnsAsync(TipRack.ToEnvironmentEntities(this.mockRacks.ToList()));

            string otherCluster = this.mockRacks.Where(rack => !rack.ClusterName.Equals(treatmentCluster, StringComparison.OrdinalIgnoreCase)).First().ClusterName;

            this.mockTipClient.Setup(c => c.CreateTipSessionAsync(It.IsAny<TipParameters>(), It.IsAny<CancellationToken>()))
                .Callback<TipParameters, CancellationToken>((parameters, token) =>
                {
                    Assert.IsTrue(parameters.ClusterName == treatmentCluster || parameters.ClusterName == otherCluster);
                })
                .ReturnsAsync((TipParameters parameters, CancellationToken token) => CrossRackTipCreationProviderTests.CreateTipNodeSessionChangeDetail());

            ExecutionResult result = this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsNotNull(result);
            Assert.AreEqual(ExecutionStatus.InProgress, result.Status);
            this.mockTipClient.Verify(c => c.CreateTipSessionAsync(It.IsAny<TipParameters>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        private static TipNodeSessionChange CreateTipNodeSessionChange(Guid? tipSessionId = null, Guid? tipSessionChangeId = null)
        {
            return new TipNodeSessionChange()
            {
                TipNodeSessionId = tipSessionId?.ToString() ?? Guid.NewGuid().ToString(),
                TipNodeSessionChangeId = tipSessionChangeId?.ToString() ?? Guid.NewGuid().ToString(),
                Status = TipNodeSessionChangeStatus.Queued
            };
        }

        private static TipNodeSessionChangeDetails CreateTipNodeSessionChangeDetail(Guid? tipSessionId = null, Guid? tipSessionChangeId = null)
        {
            return new TipNodeSessionChangeDetails()
            {
                TipNodeSessionId = tipSessionId?.ToString() ?? Guid.NewGuid().ToString(),
                TipNodeSessionChangeId = tipSessionChangeId?.ToString() ?? Guid.NewGuid().ToString(),
                Status = TipNodeSessionChangeStatus.Queued
            };
        }

        private void SetupMockDefaults()
        {
            this.mockFixture.DataClient.OnGetState<CrossRackTipCreationProvider.State>().ReturnsAsync(this.mockState);
            this.mockFixture.DataClient.OnGetEntityPool().ReturnsAsync(TipRack.ToEnvironmentEntities(this.mockRacks.ToList()));
            this.mockFixture.DataClient.OnGetEntitiesProvisioned().ReturnsAsync(new List<EnvironmentEntity>());
            this.mockFixture.DataClient.OnRemoveEntitiesProvisioned().Returns(Task.CompletedTask);
            this.mockFixture.DataClient.OnUpdateEntitiesProvisioned().Returns(Task.CompletedTask);

            // TiP session creation request default
            this.mockTipClient.Setup(c => c.CreateTipSessionAsync(It.IsAny<TipParameters>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((TipParameters parameters, CancellationToken token) => CrossRackTipCreationProviderTests.CreateTipNodeSessionChangeDetail());

            this.mockTipClient.Setup(c => c.DeleteTipSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CrossRackTipCreationProviderTests.CreateTipNodeSessionChange());

            // TiP session creation requests are confirmed succeeded by default.
            this.mockTipClient.Setup(c => c.IsTipSessionChangeFailedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
        }
    }
}
