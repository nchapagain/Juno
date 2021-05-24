namespace Juno.Execution.Providers.Payloads
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Azure.Core;
    using Juno.Contracts;
    using Juno.Execution.ArmIntegration;
    using Juno.Execution.TipIntegration;
    using Juno.Providers;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using NUnit.Framework;
    using TipGateway.Entities;
    using TipGateway.FabricApi.Requests;
    using static Juno.Execution.Providers.Payloads.TipNodePowerCycleProvider;

    [TestFixture]
    [Category("Unit")]
    public class TipNodePowerCycleProviderTests
    {
        private Mock<IProviderDataClient> mockDataClient;
        private Mock<IPayloadActivator> mockPayloadActivator;
        private ExperimentContext testExperimentContext;
        private ExperimentComponent testExperimentComponent;
        private Fixture mockFixture;
        private FixtureDependencies mockDependencies;
        private List<EnvironmentEntity> testEntitiesProvisioned;
        private Mock<ITipClient> mockTipClient;
        private IServiceCollection providerServices;
        private TipNodePowerCycleProvider powerCycleProvider;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks();
            this.mockDependencies = new FixtureDependencies();

            // A valid experiment component definition for the provider.
            this.testExperimentComponent = new ExperimentComponent(
                typeof(TipNodePowerCycleProvider).FullName,
                "power cycle",
                "powercycle node",
                "Group B",
                parameters: new Dictionary<string, IConvertible>
                {
                });

            this.testExperimentContext = new ExperimentContext(
                this.mockFixture.Create<ExperimentInstance>(),
                this.mockFixture.CreateExperimentStep(this.testExperimentComponent),
                this.mockDependencies.Configuration);

            this.mockPayloadActivator = new Mock<IPayloadActivator>();
            this.mockDataClient = new Mock<IProviderDataClient>();

            this.testEntitiesProvisioned = new List<EnvironmentEntity>
            {
                // Set the 'entities provisioned' to contain TiP sessions. The provider expects TiP sessions to have
                // been established in order to know which physical nodes on which the microcode should be deployed.
                EnvironmentEntity.TipSession(Guid.NewGuid().ToString(), "Group B", new Dictionary<string, IConvertible>
                {
                    [nameof(TipSession.NodeId)] = "Node01",
                    [nameof(TipSession.ClusterName)] = "Cluster01",
                    [nameof(TipSession.TipSessionId)] = "TiPSession01",
                    [nameof(TipSession.ChangeIdList)] = string.Join(",", new List<string> { "AnyOtherChangeId" })
                })
            };

            this.mockDataClient
               .Setup(client => client.GetOrCreateStateAsync<IEnumerable<EnvironmentEntity>>(
                   It.IsAny<string>(),
                   ContractExtension.EntitiesProvisioned,
                   It.IsAny<CancellationToken>(),
                   It.IsAny<string>()))
               .Returns(Task.FromResult(this.testEntitiesProvisioned as IEnumerable<EnvironmentEntity>));
            var changeResult = new TipNodeSessionChange();

            this.mockTipClient = new Mock<ITipClient>();

            this.providerServices = new ServiceCollection()
               .AddSingleton<IPayloadActivator>(this.mockPayloadActivator.Object)
               .AddSingleton<IProviderDataClient>(this.mockDataClient.Object)
               .AddSingleton<ITipClient>(this.mockTipClient.Object);

            this.powerCycleProvider = new TipNodePowerCycleProvider(this.providerServices);
        }

        [Test]
        public void ProviderInitiatesResetHealthWhenResetHealthIsNotCalled()
        {
            var mockState = new TipNodePowerCycleProviderState();
            mockState.StepTimeout = DateTime.UtcNow.Add(TimeSpan.FromMinutes(30));
            mockState.PowerCycleState = new TipChangeState();
            mockState.ResetHealthState = new TipChangeState();
            var changeResult = new TipNodeSessionChange();

            this.mockTipClient
                .Setup(s => s.ResetNodeHealthAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(changeResult));

            this.mockDataClient.OnGetState<TipNodePowerCycleProviderState>()
                .Returns(Task.FromResult(mockState));

            ExecutionResult result = this.powerCycleProvider.ExecuteAsync(this.testExperimentContext, this.testExperimentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.InProgress);
            this.mockTipClient.Verify(s => s.ResetNodeHealthAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            this.mockTipClient.Verify(s => s.SetNodePowerStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PowerAction>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public void ProviderWaitsForResetNodeHealthIfResetIsNotRegisteredDone()
        {
            this.mockDataClient
               .Setup(client => client.GetOrCreateStateAsync<IEnumerable<EnvironmentEntity>>(
                   It.IsAny<string>(),
                   ContractExtension.EntitiesProvisioned,
                   It.IsAny<CancellationToken>(),
                   It.IsAny<string>()))
               .Returns(Task.FromResult(this.testEntitiesProvisioned as IEnumerable<EnvironmentEntity>));
            var changeResult = new TipNodeSessionChangeDetails();

            this.mockTipClient
                .Setup(s => s.GetTipSessionChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(changeResult));

            var powerCycleProvider = new TipNodePowerCycleProvider(this.providerServices);

            var mockState = new TipNodePowerCycleProviderState();
            mockState.StepTimeout = DateTime.UtcNow.Add(TimeSpan.FromMinutes(30));
            mockState.PowerCycleState = new TipChangeState();
            mockState.ResetHealthState = new TipChangeState();
            mockState.ResetHealthState.RequestInitiated = true;
            mockState.ResetHealthState.TipNodeSessionId = Guid.NewGuid().ToString();
            mockState.ResetHealthState.TipNodeId = Guid.NewGuid().ToString();
            mockState.ResetHealthState.TipNodeSessionChangeId = Guid.NewGuid().ToString();

            this.mockDataClient.OnGetState<TipNodePowerCycleProviderState>()
                .Returns(Task.FromResult(mockState));

            ExecutionResult result = powerCycleProvider.ExecuteAsync(this.testExperimentContext, this.testExperimentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.InProgress);
            this.mockTipClient.Verify(s => s.GetTipSessionChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public void ProviderShouldNotCallPowerCycleUntilResetHealthIsDone()
        {
            this.mockDataClient
               .Setup(client => client.GetOrCreateStateAsync<IEnumerable<EnvironmentEntity>>(
                   It.IsAny<string>(),
                   ContractExtension.EntitiesProvisioned,
                   It.IsAny<CancellationToken>(),
                   It.IsAny<string>()))
               .Returns(Task.FromResult(this.testEntitiesProvisioned as IEnumerable<EnvironmentEntity>));
            var changeResult = new TipNodeSessionChangeDetails();

            this.mockTipClient
                .Setup(s => s.GetTipSessionChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(changeResult));

            var mockState = new TipNodePowerCycleProviderState();
            mockState.StepTimeout = DateTime.UtcNow.Add(TimeSpan.FromMinutes(30));
            mockState.PowerCycleState = new TipChangeState();
            mockState.ResetHealthState = new TipChangeState();
            mockState.ResetHealthState.RequestInitiated = true;
            mockState.ResetHealthState.TipNodeSessionId = Guid.NewGuid().ToString();
            mockState.ResetHealthState.TipNodeId = Guid.NewGuid().ToString();
            mockState.ResetHealthState.TipNodeSessionChangeId = Guid.NewGuid().ToString();

            this.mockDataClient.OnGetState<TipNodePowerCycleProviderState>()
                .Returns(Task.FromResult(mockState));

            ExecutionResult result = this.powerCycleProvider.ExecuteAsync(this.testExperimentContext, this.testExperimentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.InProgress);
            this.mockTipClient.Verify(s => s.SetNodePowerStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PowerAction>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public void ProviderShouldCallPowerCycleIfResetIsComplete()
        {
            this.mockDataClient
               .Setup(client => client.GetOrCreateStateAsync<IEnumerable<EnvironmentEntity>>(
                   It.IsAny<string>(),
                   ContractExtension.EntitiesProvisioned,
                   It.IsAny<CancellationToken>(),
                   It.IsAny<string>()))
               .Returns(Task.FromResult(this.testEntitiesProvisioned as IEnumerable<EnvironmentEntity>));
            var changeResult = new TipNodeSessionChange();

            this.mockTipClient
                .Setup(s => s.SetNodePowerStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PowerAction>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(changeResult));

            var mockState = new TipNodePowerCycleProviderState();
            mockState.StepTimeout = DateTime.UtcNow.Add(TimeSpan.FromMinutes(30));
            mockState.PowerCycleState = new TipChangeState();
            mockState.ResetHealthState = new TipChangeState();
            mockState.ResetHealthState.RequestInitiated = true;
            mockState.ResetHealthState.RequestCompleted = true;
            this.mockDataClient.OnGetState<TipNodePowerCycleProviderState>()
                .Returns(Task.FromResult(mockState));

            ExecutionResult result = this.powerCycleProvider.ExecuteAsync(this.testExperimentContext, this.testExperimentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.InProgress);
            this.mockTipClient.Verify(s => s.SetNodePowerStateAsync(It.IsAny<string>(), It.IsAny<string>(), PowerAction.PowerCycle, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public void ProviderWaitsForPowerCycleIfPowerCycleRequestNotRegisteredDone()
        {
            this.mockDataClient
               .Setup(client => client.GetOrCreateStateAsync<IEnumerable<EnvironmentEntity>>(
                   It.IsAny<string>(),
                   ContractExtension.EntitiesProvisioned,
                   It.IsAny<CancellationToken>(),
                   It.IsAny<string>()))
               .Returns(Task.FromResult(this.testEntitiesProvisioned as IEnumerable<EnvironmentEntity>));
            var changeResult = new TipNodeSessionChangeDetails();

            this.mockTipClient
                .Setup(s => s.GetTipSessionChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(changeResult));

            var mockState = new TipNodePowerCycleProviderState();
            mockState.StepTimeout = DateTime.UtcNow.Add(TimeSpan.FromMinutes(30));
            mockState.PowerCycleState = new TipChangeState();
            mockState.ResetHealthState = new TipChangeState();
            mockState.ResetHealthState.RequestInitiated = true;
            mockState.ResetHealthState.RequestCompleted = true;
            mockState.PowerCycleState.RequestInitiated = true;
            mockState.PowerCycleState.TipNodeSessionId = Guid.NewGuid().ToString();
            mockState.PowerCycleState.TipNodeId = Guid.NewGuid().ToString();
            mockState.PowerCycleState.TipNodeSessionChangeId = Guid.NewGuid().ToString();

            this.mockDataClient.OnGetState<TipNodePowerCycleProviderState>()
                .Returns(Task.FromResult(mockState));

            ExecutionResult result = this.powerCycleProvider.ExecuteAsync(this.testExperimentContext, this.testExperimentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.InProgress);
            this.mockTipClient.Verify(s => s.GetTipSessionChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public void ProviderWaitsOnNodeStatusIfPowerCycleRequestComplete()
        {
            this.mockDataClient
               .Setup(client => client.GetOrCreateStateAsync<IEnumerable<EnvironmentEntity>>(
                   It.IsAny<string>(),
                   ContractExtension.EntitiesProvisioned,
                   It.IsAny<CancellationToken>(),
                   It.IsAny<string>()))
               .Returns(Task.FromResult(this.testEntitiesProvisioned as IEnumerable<EnvironmentEntity>));
            var requestResult = new TipNodeSessionChange();

            this.mockTipClient
                .Setup(s => s.GetNodeStatusAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(requestResult));

            var changeResult = new TipNodeSessionChangeDetails();
            changeResult.Note = "New invoke fabric request for IManagement:GetNodeStatus function on cluster AMS22PrdApp01 by junosvc@microsoft.com. // Start executing invoking fabric function for TiP session Id 3bdef531-0740-4110-94f0-d6fe3e3e94ff // Invoked fabric API successfully. FabricResponse : <NodeStatus xmlns=\"RD.Fabric.Controller.Data\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><AvailabilityStateString>Available</AvailabilityStateString><ContainerCount>0</ContainerCount><InGoalState>false</InGoalState><InQuarantine>false</InQuarantine><IsDedicatedBareMetal>false</IsDedicatedBareMetal><IsDedicatedHost>false</IsDedicatedHost><IsIsolated>false</IsIsolated><IsOffline>false</IsOffline><NodeAvailabilityState>Available</NodeAvailabilityState><NodeCapabilityType>None</NodeCapabilityType><State>PoweringOn</State><TipNodeSessionId>3bdef531-0740-4110-94f0-d6fe3e3e94ff</TipNodeSessionId></NodeStatus>";
            this.mockTipClient
                .Setup(s => s.GetTipSessionChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(changeResult));

            var mockState = new TipNodePowerCycleProviderState();
            mockState.StepTimeout = DateTime.UtcNow.Add(TimeSpan.FromMinutes(30));
            mockState.PowerCycleState = new TipChangeState();
            mockState.ResetHealthState = new TipChangeState();
            mockState.ResetHealthState.RequestInitiated = true;
            mockState.ResetHealthState.RequestCompleted = true;
            mockState.PowerCycleState.RequestInitiated = true;
            mockState.PowerCycleState.RequestCompleted = true;

            this.mockDataClient.OnGetState<TipNodePowerCycleProviderState>()
                .Returns(Task.FromResult(mockState));

            ExecutionResult result = this.powerCycleProvider.ExecuteAsync(this.testExperimentContext, this.testExperimentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.InProgress);
            this.mockTipClient.Verify(s => s.GetNodeStatusAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            this.mockTipClient.Verify(s => s.SetNodePowerStateAsync(It.IsAny<string>(), It.IsAny<string>(), PowerAction.PowerCycle, It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public void ProviderReturnsSuccessWhenNodeInGoalState()
        {
            this.mockDataClient
               .Setup(client => client.GetOrCreateStateAsync<IEnumerable<EnvironmentEntity>>(
                   It.IsAny<string>(),
                   ContractExtension.EntitiesProvisioned,
                   It.IsAny<CancellationToken>(),
                   It.IsAny<string>()))
               .Returns(Task.FromResult(this.testEntitiesProvisioned as IEnumerable<EnvironmentEntity>));
            var requestResult = new TipNodeSessionChange();

            this.mockTipClient
                .Setup(s => s.GetNodeStatusAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(requestResult));

            var changeResult = new TipNodeSessionChangeDetails();
            changeResult.Note = "New invoke fabric request for IManagement:GetNodeStatus function on " +
                "cluster STG03PrdApp02 by riach@microsoft.com. " +
                "// Start executing invoking fabric function for TiP session Id 39e22b30-6463-4aca-a5bd-354184acc166 " +
                "// Invoked fabric API successfully. FabricResponse :" +
                " <NodeStatus xmlns=\"RD.Fabric.Controller.Data\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\">" +
                "<AvailabilityStateString>Available</AvailabilityStateString><ContainerCount>0</ContainerCount>" +
                "<InGoalState>true</InGoalState><InQuarantine>false</InQuarantine>" +
                "<IsDedicatedBareMetal>false</IsDedicatedBareMetal><IsDedicatedHost>false</IsDedicatedHost>" +
                "<IsIsolated>false</IsIsolated><IsOffline>false</IsOffline>" +
                "<NodeAvailabilityState>Available</NodeAvailabilityState>" +
                "<NodeCapabilityType>None</NodeCapabilityType><State>Ready</State>" +
                "<TipNodeSessionId>39e22b30-6463-4aca-a5bd-354184acc166</TipNodeSessionId></NodeStatus>";
            this.mockTipClient
                .Setup(s => s.GetTipSessionChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(changeResult));

            var mockState = new TipNodePowerCycleProviderState();
            mockState.StepTimeout = DateTime.UtcNow.Add(TimeSpan.FromMinutes(30));
            mockState.PowerCycleState = new TipChangeState();
            mockState.ResetHealthState = new TipChangeState();
            mockState.ResetHealthState.RequestInitiated = true;
            mockState.ResetHealthState.RequestCompleted = true;
            mockState.PowerCycleState.RequestInitiated = true;
            mockState.PowerCycleState.RequestCompleted = true;

            this.mockDataClient.OnGetState<TipNodePowerCycleProviderState>()
                .Returns(Task.FromResult(mockState));

            ExecutionResult result = this.powerCycleProvider.ExecuteAsync(this.testExperimentContext, this.testExperimentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.Succeeded);
            this.mockTipClient.Verify(s => s.GetNodeStatusAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            this.mockTipClient.Verify(s => s.SetNodePowerStateAsync(It.IsAny<string>(), It.IsAny<string>(), PowerAction.PowerCycle, It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public void ProviderReturnsInProgressWhenNodeStatusNotAvailable()
        {
            this.mockDataClient
               .Setup(client => client.GetOrCreateStateAsync<IEnumerable<EnvironmentEntity>>(
                   It.IsAny<string>(),
                   ContractExtension.EntitiesProvisioned,
                   It.IsAny<CancellationToken>(),
                   It.IsAny<string>()))
               .Returns(Task.FromResult(this.testEntitiesProvisioned as IEnumerable<EnvironmentEntity>));
            var requestResult = new TipNodeSessionChange();

            this.mockTipClient
                .Setup(s => s.GetNodeStatusAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(requestResult));

            var changeResult = new TipNodeSessionChangeDetails();
            changeResult.Note = "New invoke fabric request for IManagement:GetNodeStatus function on " +
                "cluster STG03PrdApp02 by riach@microsoft.com. " +
                "// Start executing invoking fabric function for TiP session Id 39e22b30-6463-4aca-a5bd-354184acc166 " +
                "// Invoked fabric API successfully. FabricResponse :";
            this.mockTipClient
                .Setup(s => s.GetTipSessionChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(changeResult));

            var mockState = new TipNodePowerCycleProviderState();
            mockState.StepTimeout = DateTime.UtcNow.Add(TimeSpan.FromMinutes(30));
            mockState.PowerCycleState = new TipChangeState();
            mockState.ResetHealthState = new TipChangeState();
            mockState.ResetHealthState.RequestInitiated = true;
            mockState.ResetHealthState.RequestCompleted = true;
            mockState.PowerCycleState.RequestInitiated = true;
            mockState.PowerCycleState.RequestCompleted = true;

            this.mockDataClient.OnGetState<TipNodePowerCycleProviderState>()
                .Returns(Task.FromResult(mockState));

            ExecutionResult result = this.powerCycleProvider.ExecuteAsync(this.testExperimentContext, this.testExperimentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.InProgress);
            this.mockTipClient.Verify(s => s.GetNodeStatusAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            this.mockTipClient.Verify(s => s.SetNodePowerStateAsync(It.IsAny<string>(), It.IsAny<string>(), PowerAction.PowerCycle, It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public void ProviderShouldNotReturnsSuccessWhenNodeInGoalStateButNotPowercycled()
        {
            this.mockDataClient
               .Setup(client => client.GetOrCreateStateAsync<IEnumerable<EnvironmentEntity>>(
                   It.IsAny<string>(),
                   ContractExtension.EntitiesProvisioned,
                   It.IsAny<CancellationToken>(),
                   It.IsAny<string>()))
               .Returns(Task.FromResult(this.testEntitiesProvisioned as IEnumerable<EnvironmentEntity>));
            var requestResult = new TipNodeSessionChange();

            this.mockTipClient
                .Setup(s => s.GetNodeStatusAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(requestResult));

            var changeResult = new TipNodeSessionChangeDetails();
            changeResult.Note = "New invoke fabric request for IManagement:GetNodeStatus function on " +
                "cluster STG03PrdApp02 by riach@microsoft.com. " +
                "// Start executing invoking fabric function for TiP session Id 39e22b30-6463-4aca-a5bd-354184acc166 " +
                "// Invoked fabric API successfully. FabricResponse :" +
                " <NodeStatus xmlns=\"RD.Fabric.Controller.Data\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\">" +
                "<AvailabilityStateString>Available</AvailabilityStateString><ContainerCount>0</ContainerCount>" +
                "<InGoalState>true</InGoalState><InQuarantine>false</InQuarantine>" +
                "<IsDedicatedBareMetal>false</IsDedicatedBareMetal><IsDedicatedHost>false</IsDedicatedHost>" +
                "<IsIsolated>false</IsIsolated><IsOffline>false</IsOffline>" +
                "<NodeAvailabilityState>Available</NodeAvailabilityState>" +
                "<NodeCapabilityType>None</NodeCapabilityType><State>Ready</State>" +
                "<TipNodeSessionId>39e22b30-6463-4aca-a5bd-354184acc166</TipNodeSessionId></NodeStatus>";

            this.mockTipClient
                .Setup(s => s.GetTipSessionChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(changeResult));

            var mockState = new TipNodePowerCycleProviderState();
            mockState.StepTimeout = DateTime.UtcNow.Add(TimeSpan.FromMinutes(30));
            mockState.PowerCycleState = new TipChangeState();
            mockState.ResetHealthState = new TipChangeState();
            mockState.ResetHealthState.RequestInitiated = true;
            mockState.ResetHealthState.RequestCompleted = true;
            mockState.PowerCycleState.RequestInitiated = true;
            mockState.PowerCycleState.RequestCompleted = false;

            this.mockDataClient.OnGetState<TipNodePowerCycleProviderState>()
                .Returns(Task.FromResult(mockState));

            ExecutionResult result = this.powerCycleProvider.ExecuteAsync(this.testExperimentContext, this.testExperimentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.InProgress);
            this.mockTipClient.Verify(s => s.GetNodeStatusAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            this.mockTipClient.Verify(s => s.SetNodePowerStateAsync(It.IsAny<string>(), It.IsAny<string>(), PowerAction.PowerCycle, It.IsAny<CancellationToken>()), Times.Never);
            this.mockTipClient.Verify(s => s.GetTipSessionChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
