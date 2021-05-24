namespace Juno.Execution.Providers.Environment
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Juno.Execution.TipIntegration;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;
    using TipGateway.Entities;
    using static Juno.Execution.Providers.Environment.TipCleanupProvider;

    [TestFixture]
    [Category("Unit")]
    public class TipCleanupProviderTests
    {
        private string tipSessionIdPramKey = "tipSessionId";

        private Fixture mockFixture;
        private Mock<ITipClient> mockTipClient;
        private Mock<IProviderDataClient> mockDataClient;
        private Mock<IEnvironmentClient> mockEnvironmentClient;
        private IConfiguration mockConfiguration;
        private ExperimentContext mockExperimentContext;
        private ExperimentComponent mockExperimentComponent;
        private ServiceCollection providerServices;

        [SetUp]
        public void SetupTest()
        {
            // This unit test mimics submitting tip delete request.:

            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks();
            this.mockTipClient = new Mock<ITipClient>();
            this.mockDataClient = new Mock<IProviderDataClient>();
            this.mockDataClient.SetReturnsDefault(new HttpResponseMessage(HttpStatusCode.OK));
            this.mockEnvironmentClient = new Mock<IEnvironmentClient>();
            this.mockEnvironmentClient.SetReturnsDefault(new HttpResponseMessage(HttpStatusCode.NoContent));

            this.mockExperimentComponent = new ExperimentComponent(
                typeof(TipCleanupProvider).FullName,
                "Delete Tip session",
                "Any Description",
                "Group A");

            this.providerServices = new ServiceCollection();
            this.providerServices.AddSingleton(NullLogger.Instance);
            this.providerServices.AddSingleton(this.mockTipClient.Object);
            this.providerServices.AddSingleton(this.mockDataClient.Object);
            this.providerServices.AddSingleton(this.mockEnvironmentClient.Object);

            this.mockConfiguration = new ConfigurationBuilder()
                  .SetBasePath(Path.Combine(
                              Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                              @"Configuration"))
                          .AddJsonFile($"juno-dev01.environmentsettings.json")
                          .Build();

            this.mockExperimentContext = new ExperimentContext(
                this.mockFixture.Create<ExperimentInstance>(),
                this.mockFixture.Create<ExperimentStepInstance>(),
                this.mockConfiguration);
        }

        [Test]
        public void TipCleanupProviderValidatesRequiredParameters()
        {
            var tipCleanupProvider = new TipCleanupProvider(this.providerServices);
            Assert.ThrowsAsync<ArgumentException>(() => tipCleanupProvider.ExecuteAsync(null, this.mockExperimentComponent, CancellationToken.None));
            Assert.ThrowsAsync<ArgumentException>(() => tipCleanupProvider.ExecuteAsync(this.mockExperimentContext, null, CancellationToken.None));
        }

        [Test]
        public void TipCleanupProviderMakesTheExpectedCallToTheTipServiceToSubmitTipDeleteRequest()
        {
            var tipCleanupProvider = new TestTipCleanupProvider(this.providerServices);
            DeleteTipSessionState providerState = null;

            // First return null state for the provider.
            this.mockDataClient.Setup(c => c.GetOrCreateStateAsync<DeleteTipSessionState>(
                    this.mockExperimentContext.Experiment.Id,
                    $"state-{this.mockExperimentContext.ExperimentStep.Id}",
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .ReturnsAsync(providerState)
                .Verifiable();

            // Return provisioned tip sessions
            this.mockDataClient.Setup(c => c.GetOrCreateStateAsync<IEnumerable<EnvironmentEntity>>(
                    this.mockExperimentContext.Experiment.Id,
                    ContractExtension.EntitiesProvisioned,
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .ReturnsAsync(this.SingleTipSessionPerGroup)
                .Verifiable();

            // Tip client should submit Tip delete request.
            this.mockTipClient.Setup(c => c.DeleteTipSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string tipSessionId, CancellationToken token) => new TipNodeSessionChange()
                {
                    TipNodeSessionId = tipSessionId,
                    TipNodeSessionChangeId = $"Change-tipSessionId",
                    Status = TipNodeSessionChangeStatus.Unknown
                })
                .Verifiable();

            // provider should save Tip with updated change id.
            this.mockDataClient.Setup(c => c.UpdateStateItemsAsync<EnvironmentEntity>(
                    this.mockExperimentContext.Experiment.Id,
                    ContractExtension.EntitiesProvisioned,
                    It.IsAny<IEnumerable<EnvironmentEntity>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            // Provider should save state
            this.mockDataClient.Setup(c => c.SaveStateAsync<DeleteTipSessionState>(
                    this.mockExperimentContext.Experiment.Id,
                    $"state-{this.mockExperimentContext.ExperimentStep.Id}",
                    It.IsAny<DeleteTipSessionState>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .Callback<string, string, DeleteTipSessionState, CancellationToken, string>((experimentId, stepId, state, token, stateId) =>
                {
                    Assert.IsTrue(state.TipDeletionStarted);
                })
                .Returns(Task.CompletedTask)
                .Verifiable();
            tipCleanupProvider.ConfigureServicesAsync(this.mockExperimentContext, this.mockExperimentComponent);

            ExecutionResult result = tipCleanupProvider.ExecuteAsync(this.mockExperimentContext, this.mockExperimentComponent, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            // Provider should return inprogress when starting new.
            Assert.AreEqual(ExecutionStatus.InProgressContinue, result.Status);
            this.mockTipClient.Verify(c => c.DeleteTipSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(1));
            this.mockTipClient.Verify();
            this.mockDataClient.Verify();
        }

        [Test]
        public void TipCleanupProviderReturnsSuccessAfterTipChangeResultValueReturnsSucceeded()
        {
            var tipCleanupProvider = new TestTipCleanupProvider(this.providerServices);
            var tipChangeIdSet = new Dictionary<string, string>();
            tipChangeIdSet.Add($"session-node-a", $"Change-tipSessionId");
            DeleteTipSessionState providerState = new DeleteTipSessionState()
            {
                TipDeletionStarted = true,
                TipChangeIdSet = tipChangeIdSet
            };

            // Return state showing finished for the provider.
            this.mockDataClient.Setup(c => c.GetOrCreateStateAsync<DeleteTipSessionState>(
                    this.mockExperimentContext.Experiment.Id,
                    $"state-{this.mockExperimentContext.ExperimentStep.Id}",
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .ReturnsAsync(providerState);

            // Return provisioned tip sessions
            this.mockDataClient.Setup(c => c.GetOrCreateStateAsync<IEnumerable<EnvironmentEntity>>(
                    this.mockExperimentContext.Experiment.Id,
                    ContractExtension.EntitiesProvisioned,
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .ReturnsAsync(this.SingleTipSessionPerGroup());

            // Tip client should submit GetTipChange if delete request is already triggered.
            this.mockTipClient.Setup(c => c.GetTipSessionChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string tipSessionId, string tipSessionChangeId, CancellationToken token) => new TipNodeSessionChangeDetails()
                {
                    Result = TipNodeSessionChangeResult.Succeeded
                })
                .Verifiable();
            tipCleanupProvider.ConfigureServicesAsync(this.mockExperimentContext, this.mockExperimentComponent);

            ExecutionResult result = tipCleanupProvider.ExecuteAsync(this.mockExperimentContext, this.mockExperimentComponent, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            // Provider should return Succeeded.
            Assert.AreEqual(ExecutionStatus.Succeeded, result.Status);
            this.mockTipClient.Verify(c => c.GetTipSessionChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(1));
            this.mockTipClient.Verify();
            this.mockDataClient.Verify();
        }

        [Test]
        public void TipCleanupProviderReturnsFailedAfterTipChangeResultValueReturnsFailed()
        {
            var tipCleanupProvider = new TestTipCleanupProvider(this.providerServices);
            var tipChangeIdSet = new Dictionary<string, string>();
            tipChangeIdSet.Add($"session-node-a", $"Change-tipSessionId");
            DeleteTipSessionState providerState = new DeleteTipSessionState()
            {
                TipDeletionStarted = true,
                TipChangeIdSet = tipChangeIdSet
            };

            // Return state showing finished for the provider.
            this.mockDataClient.Setup(c => c.GetOrCreateStateAsync<DeleteTipSessionState>(
                    this.mockExperimentContext.Experiment.Id,
                    $"state-{this.mockExperimentContext.ExperimentStep.Id}",
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .ReturnsAsync(providerState);

            // Return provisioned tip sessions
            this.mockDataClient.Setup(c => c.GetOrCreateStateAsync<IEnumerable<EnvironmentEntity>>(
                    this.mockExperimentContext.Experiment.Id,
                    ContractExtension.EntitiesProvisioned,
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .ReturnsAsync(this.SingleTipSessionPerGroup());

            // Tip client should submit GetTipChange if delete request is already triggered.
            this.mockTipClient.Setup(c => c.GetTipSessionChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string tipSessionId, string tipSessionChangeId, CancellationToken token) => new TipNodeSessionChangeDetails()
                {
                    Result = TipNodeSessionChangeResult.Failed
                })
                .Verifiable();
            tipCleanupProvider.ConfigureServicesAsync(this.mockExperimentContext, this.mockExperimentComponent);

            ExecutionResult result = tipCleanupProvider.ExecuteAsync(this.mockExperimentContext, this.mockExperimentComponent, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            // Provider should return Failed.
            Assert.AreEqual(ExecutionStatus.Failed, result.Status);
            this.mockTipClient.Verify(c => c.GetTipSessionChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(1));
            this.mockTipClient.Verify();
            this.mockDataClient.Verify();
        }

        [Test]
        public void TipCleanupProviderReturnsSuccessIfNoTipSessionToDelte()
        {
            var tipCleanupProvider = new TestTipCleanupProvider(this.providerServices);
            DeleteTipSessionState providerState = null;
            List<EnvironmentEntity> sessions = new List<EnvironmentEntity>();

            // Return state showing finished for the provider.
            this.mockDataClient.Setup(c => c.GetOrCreateStateAsync<DeleteTipSessionState>(
                    this.mockExperimentContext.Experiment.Id,
                    $"state-{this.mockExperimentContext.ExperimentStep.Id}",
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .ReturnsAsync(providerState);

            // Return provisioned tip sessions
            this.mockDataClient.Setup(c => c.GetOrCreateStateAsync<IEnumerable<EnvironmentEntity>>(
                    this.mockExperimentContext.Experiment.Id,
                    ContractExtension.EntitiesProvisioned,
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .ReturnsAsync(sessions);
            tipCleanupProvider.ConfigureServicesAsync(this.mockExperimentContext, this.mockExperimentComponent);

            ExecutionResult result = tipCleanupProvider.ExecuteAsync(this.mockExperimentContext, this.mockExperimentComponent, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            // Provider should return Succeeded.
            Assert.AreEqual(ExecutionStatus.Succeeded, result.Status);
            this.mockTipClient.Verify();
            this.mockDataClient.Verify();
        }

        [Test]
        public void TipCleanupProviderMakesTheExpectedCallToTheTipServiceToSubmitTipDeleteRequestForMultipleTipSessionPerGroup()
        {
            var tipCleanupProvider = new TestTipCleanupProvider(this.providerServices);
            DeleteTipSessionState providerState = null;

            // First return null state for the provider.
            this.mockDataClient.Setup(c => c.GetOrCreateStateAsync<DeleteTipSessionState>(
                    this.mockExperimentContext.Experiment.Id,
                    $"state-{this.mockExperimentContext.ExperimentStep.Id}",
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .ReturnsAsync(providerState)
                .Verifiable();

            // Return provisioned tip sessions
            this.mockDataClient.Setup(c => c.GetOrCreateStateAsync<IEnumerable<EnvironmentEntity>>(
                    this.mockExperimentContext.Experiment.Id,
                    ContractExtension.EntitiesProvisioned,
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .ReturnsAsync(this.MultipleTipSessionsPerGroup)
                .Verifiable();

            // Tip client should submit Tip delete request.
            this.mockTipClient.Setup(c => c.DeleteTipSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string tipSessionId, CancellationToken token) => new TipNodeSessionChange()
                {
                    TipNodeSessionId = tipSessionId,
                    TipNodeSessionChangeId = $"Change-tipSessionId",
                    Status = TipNodeSessionChangeStatus.Unknown
                })
                .Verifiable();

            // provider should save Tip with updated change id.
            this.mockDataClient.Setup(c => c.UpdateStateItemsAsync<EnvironmentEntity>(
                    this.mockExperimentContext.Experiment.Id,
                    ContractExtension.EntitiesProvisioned,
                    It.IsAny<IEnumerable<EnvironmentEntity>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            // Provider should save state
            this.mockDataClient.Setup(c => c.SaveStateAsync<DeleteTipSessionState>(
                this.mockExperimentContext.Experiment.Id,
                $"state-{this.mockExperimentContext.ExperimentStep.Id}",
                It.IsAny<DeleteTipSessionState>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string>()))
                .Callback<string, string, DeleteTipSessionState, CancellationToken, string>((experimentId, stepId, state, token, stateId) =>
                {
                    Assert.IsTrue(state.TipDeletionStarted);
                })
                .Returns(Task.CompletedTask)
                .Verifiable();
            tipCleanupProvider.ConfigureServicesAsync(this.mockExperimentContext, this.mockExperimentComponent);

            ExecutionResult result = tipCleanupProvider.ExecuteAsync(this.mockExperimentContext, this.mockExperimentComponent, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            // Provider should return inprogress when starting new.
            Assert.AreEqual(ExecutionStatus.InProgressContinue, result.Status);
            this.mockTipClient.Verify(c => c.DeleteTipSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            this.mockTipClient.Verify();
            this.mockDataClient.Verify();
        }

        [Test]
        public void TipCleanupProviderReturnsSuccessAfterTipChangeResultValueReturnsSucceededForMultipleTipSessionPerGroup()
        {
            var tipCleanupProvider = new TestTipCleanupProvider(this.providerServices);
            var tipChangeIdSet = new Dictionary<string, string>();
            tipChangeIdSet.Add($"session-node-a-1", $"Change-tipSessionId");
            tipChangeIdSet.Add($"session-node-a-2", $"Change-tipSessionId");
            DeleteTipSessionState providerState = new DeleteTipSessionState()
            {
                TipDeletionStarted = true,
                TipChangeIdSet = tipChangeIdSet
            };

            // Return state showing finished for the provider.
            this.mockDataClient.Setup(c => c.GetOrCreateStateAsync<DeleteTipSessionState>(
                    this.mockExperimentContext.Experiment.Id,
                    $"state-{this.mockExperimentContext.ExperimentStep.Id}",
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .ReturnsAsync(providerState);

            // Return provisioned tip sessions
            this.mockDataClient.Setup(c => c.GetOrCreateStateAsync<IEnumerable<EnvironmentEntity>>(
                    this.mockExperimentContext.Experiment.Id,
                    ContractExtension.EntitiesProvisioned,
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .ReturnsAsync(this.SingleTipSessionPerGroup());

            // Tip client should submit GetTipChange if delete request is already triggered.
            this.mockTipClient.Setup(c => c.GetTipSessionChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string tipSessionId, string tipSessionChangeId, CancellationToken token) => new TipNodeSessionChangeDetails()
                {
                    Result = TipNodeSessionChangeResult.Succeeded
                })
                .Verifiable();
            tipCleanupProvider.ConfigureServicesAsync(this.mockExperimentContext, this.mockExperimentComponent);

            ExecutionResult result = tipCleanupProvider.ExecuteAsync(this.mockExperimentContext, this.mockExperimentComponent, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            // Provider should return Succeeded.
            Assert.AreEqual(ExecutionStatus.Succeeded, result.Status);
            this.mockTipClient.Verify(c => c.GetTipSessionChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            this.mockTipClient.Verify();
            this.mockDataClient.Verify();
        }

        [Test]
        public void TipCleanupProviderCleansSpecificTipSessionWhenSuppliedInTheParameter()
        {
            var parameters = new Dictionary<string, IConvertible>();
            parameters.Add(this.tipSessionIdPramKey, Guid.NewGuid().ToString());

            ExperimentComponent mockExperimentComponentWithPrams = new ExperimentComponent(
                typeof(TipCleanupProvider).FullName,
                "Delete Tip session",
                "Any Description",
                "Group A",
                parameters);

            var tipCleanupProvider = new TestTipCleanupProvider(this.providerServices);
            var tipChangeIdSet = new Dictionary<string, string>();
            tipChangeIdSet.Add($"session-node-a", $"Change-tipSessionId");
            DeleteTipSessionState providerState = new DeleteTipSessionState()
            {
                TipDeletionStarted = true,
                TipChangeIdSet = tipChangeIdSet
            };

            // Return state showing finished for the provider.
            this.mockDataClient.Setup(c => c.GetOrCreateStateAsync<DeleteTipSessionState>(
                    this.mockExperimentContext.Experiment.Id,
                    $"state-{this.mockExperimentContext.ExperimentStep.Id}",
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .ReturnsAsync(providerState);
                         
            // Tip client should submit GetTipChange if delete request is already triggered.
            this.mockTipClient.Setup(c => c.GetTipSessionChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string tipSessionId, string tipSessionChangeId, CancellationToken token) => new TipNodeSessionChangeDetails()
                {
                    Result = TipNodeSessionChangeResult.Succeeded
                })
                .Verifiable();
            tipCleanupProvider.ConfigureServicesAsync(this.mockExperimentContext, mockExperimentComponentWithPrams);

            ExecutionResult result = tipCleanupProvider.ExecuteAsync(this.mockExperimentContext, mockExperimentComponentWithPrams, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            // Provider should return Succeeded.
            Assert.AreEqual(ExecutionStatus.Succeeded, result.Status);
            this.mockTipClient.Verify(c => c.GetTipSessionChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(1));
            this.mockTipClient.Verify();
            this.mockDataClient.Verify();
        }

        private IEnumerable<EnvironmentEntity> SingleTipSessionPerGroup()
        {
            List<EnvironmentEntity> sessions = new List<EnvironmentEntity>();
            sessions.Add(TipSession.ToEnvironmentEntity(new TipSession()
            {
                TipSessionId = "session-node-a",
                ClusterName = "cluster-a",
                Region = "region1",
                GroupName = "Group A",
                NodeId = "node1",
                ChangeIdList = new List<string>() { "Change-node-a" },
                Status = TipSessionStatus.Created,
                CreatedTimeUtc = DateTime.UtcNow.Subtract(TimeSpan.FromDays(1)),
                ExpirationTimeUtc = DateTime.UtcNow.Add(TimeSpan.FromDays(1)),
                DeletedTimeUtc = DateTime.MaxValue
            }, "Group A"));
            return sessions;
        }

        private IEnumerable<EnvironmentEntity> MultipleTipSessionsPerGroup()
        {
            List<EnvironmentEntity> sessions = new List<EnvironmentEntity>();
            sessions.Add(TipSession.ToEnvironmentEntity(new TipSession()
            {
                TipSessionId = "session-node-a-1",
                ClusterName = "cluster-a",
                Region = "region1",
                GroupName = "Group A",
                NodeId = "node1",
                ChangeIdList = new List<string>() { "Change-node-a-1" },
                Status = TipSessionStatus.Created,
                CreatedTimeUtc = DateTime.UtcNow.Subtract(TimeSpan.FromDays(1)),
                ExpirationTimeUtc = DateTime.UtcNow.Add(TimeSpan.FromDays(1)),
                DeletedTimeUtc = DateTime.MaxValue
            }, "Group A"));

            sessions.Add(TipSession.ToEnvironmentEntity(new TipSession()
            {
                TipSessionId = "session-node-a-2",
                ClusterName = "cluster-a",
                Region = "region1",
                GroupName = "Group A",
                NodeId = "node2",
                ChangeIdList = new List<string>() { "Change-node-a-2" },
                Status = TipSessionStatus.Created,
                CreatedTimeUtc = DateTime.UtcNow.Subtract(TimeSpan.FromDays(1)),
                ExpirationTimeUtc = DateTime.UtcNow.Add(TimeSpan.FromDays(1)),
                DeletedTimeUtc = DateTime.MaxValue
            }, "Group A"));

            return sessions;
        }

        private class TestTipCleanupProvider : TipCleanupProvider
        {
            public TestTipCleanupProvider(IServiceCollection services)
            : base(services)
            {
            }
        }
    }
}
