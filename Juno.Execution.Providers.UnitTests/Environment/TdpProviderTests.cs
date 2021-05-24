namespace Juno.Execution.Providers.Environment
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using System.Security;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.Execution.ArmIntegration;
    using Juno.Execution.Providers.Workloads;
    using Juno.Execution.TipIntegration;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;
    using static Juno.Execution.Providers.Workloads.TdpProvider;

    [TestFixture]
    [Category("Unit")]
    public class TdpProviderTests
    {
        private ProviderFixture mockFixture;
        private AzureVirtualMachine azureVirtualMachine;
        private VmResourceGroupDefinition mockVmResourceGroup;
        private IEnumerable<EnvironmentEntity> mockTipSessionEntities;
        private ExperimentContext mockExperimentContext;
        private IConfiguration mockConfiguration;
        private TestTenantDeploymentPerformanceProvider provider;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new ProviderFixture(typeof(TdpProvider));
            
            this.mockFixture.KeyVault.Setup(k => k.SetSecretAsync(It.IsAny<string>(), It.IsAny<SecureString>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(Task.CompletedTask));
            this.azureVirtualMachine = this.mockFixture.Create<AzureVirtualMachine>();
            var machineList = new List<AzureVirtualMachine>() { this.azureVirtualMachine };

            Guid mockTipSession = Guid.NewGuid();
            this.mockTipSessionEntities = new List<EnvironmentEntity>
            {
                TipSession.ToEnvironmentEntity(new TipSession
                {
                    TipSessionId = Guid.NewGuid().ToString(),
                    ClusterName = "AnyCluster01",
                    Region = "AnyRegion01",
                    GroupName = this.mockFixture.Component.Group,
                    NodeId = Guid.NewGuid().ToString(),
                    ChangeIdList = new List<string> { Guid.NewGuid().ToString() },
                    Status = TipSessionStatus.Created,
                    CreatedTimeUtc = DateTime.UtcNow,
                    ExpirationTimeUtc = DateTime.UtcNow.AddDays(1),
                    DeletedTimeUtc = DateTime.UtcNow.AddDays(1)
                }, this.mockFixture.Component.Group)
            };

            this.mockFixture.DataClient.OnGetEntitiesProvisioned()
                .Returns(Task.FromResult(this.mockTipSessionEntities));
            this.mockFixture.DataClient.SetReturnsDefault(new HttpResponseMessage(HttpStatusCode.OK));

            this.mockVmResourceGroup = this.mockFixture.Create<VmResourceGroupDefinition>();
            this.mockFixture.Component.Parameters.AddRange(new Dictionary<string, IConvertible>
            {
                { "subscriptionId", Guid.NewGuid().ToString() },
                { "osDiskStorageAccountType", "Premium_LRS" },
                { "osPublisher", "MicrosoftWindowsServer" },
                { "osOffer", "WindowsServer" },
                { "osSku", "WindowsServer" },
                { "osVersion", "VersionABC" },
                { "vmSize", "Standard_F4s_v2" },
                { "iterations", 50 }
            });

            this.mockFixture.Services.AddSingleton(NullLogger.Instance);
            this.mockFixture.Services.AddSingleton(this.mockFixture.KeyVault.Object);

            this.provider = new TestTenantDeploymentPerformanceProvider(this.mockFixture.Services);

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
        public void ProviderValidatesRequiredParameters()
        {
            Assert.ThrowsAsync<ArgumentException>(() => this.provider.ExecuteAsync(null, this.mockFixture.Component, CancellationToken.None));
            Assert.ThrowsAsync<ArgumentException>(() => this.provider.ExecuteAsync(this.mockFixture.Context, null, CancellationToken.None));
        }

        [Test]
        public async Task ProviderValidatesRequiredComponentParameters()
        {
            this.mockFixture.Component.Parameters.Clear();
            ExecutionResult result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            Assert.IsTrue(result.Status == ExecutionStatus.Failed);
            Assert.IsNotNull(result.Error);
            Assert.IsInstanceOf<SchemaException>(result.Error);
        }

        [Test]
        public async Task ProviderValidatesExpectedComponentParametersAreDefinedWhenNotUsingATipSessionToTargetVMCreation()
        {
            this.mockFixture.Component.Parameters["Regions"] = "AnyRegion01";
            this.mockFixture.Component.Parameters["UseTipSession"] = true;

            ExecutionResult result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);
            Assert.IsTrue(result.Status == ExecutionStatus.Failed);
            Assert.IsNotNull(result.Error);
            Assert.IsInstanceOf<SchemaException>(result.Error);

            this.mockFixture.Component.Parameters.Remove("UseTipSession");

            result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);
            Assert.IsTrue(result.Status == ExecutionStatus.Failed);
            Assert.IsNotNull(result.Error);
            Assert.IsInstanceOf<SchemaException>(result.Error);

            this.mockFixture.Component.Parameters["UseTipSession"] = false;

            result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);
            Assert.IsTrue(result.Status != ExecutionStatus.Failed);
            Assert.IsNull(result.Error);
        }

        [Test]
        public async Task ProviderFailsIfTheExpectedTipSessionCannotBeFound()
        {
            this.mockFixture.DataClient.OnGetEntitiesProvisioned()
                .Returns(Task.FromResult(null as IEnumerable<EnvironmentEntity>));

            ExecutionResult result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);
            Assert.IsTrue(result.Status == ExecutionStatus.Failed);
            Assert.IsNotNull(result.Error);

            ProviderException error = result.Error as ProviderException;
            Assert.IsNotNull(error);
            Assert.AreEqual(ErrorReason.ExpectedEnvironmentEntitiesNotFound, error.Reason);
        }

        [Test]
        public async Task TdpProviderWillDeployResourceGroupWhenInitiated()
        {
            TdpState providerState = null;

            this.mockFixture.DataClient.Setup(c => c.GetOrCreateStateAsync<TdpState>(
                    this.mockExperimentContext.Experiment.Id,
                    $"state-{this.mockExperimentContext.ExperimentStep.Id}",
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .ReturnsAsync(providerState)
                .Verifiable();

            this.provider.ArmResourceManager.Setup(m => m.DeployResourceGroupAndVirtualMachinesAsync(It.IsAny<VmResourceGroupDefinition>(), It.IsAny<CancellationToken>()))
                .Callback<VmResourceGroupDefinition, CancellationToken>((resourceGroup, token) =>
                {
                    Assert.AreEqual(1, resourceGroup.VirtualMachines.Count);
                })
                .ReturnsAsync((VmResourceGroupDefinition resourceGroup, CancellationToken token) =>
                {
                    resourceGroup.State = ProvisioningState.Creating;
                    return resourceGroup;
                })
                .Verifiable();

            this.mockFixture.DataClient.Setup(c => c.SaveStateAsync<TdpState>(
                this.mockExperimentContext.Experiment.Id,
                $"state-{this.mockExperimentContext.ExperimentStep.Id}",
                It.IsAny<TdpState>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string>()))
                .Callback<string, string, TdpState, CancellationToken, string>((experimentId, stepId, state, token, stateId) =>
                {
                    Assert.IsTrue(state.ConsecutiveFailures == 0);
                    Assert.IsTrue(state.SuccessfulVirtualMachines.Count == 0);
                    Assert.IsTrue(state.ActiveIterationState == TdpState.TdpStatus.Creating);
                })
                .Returns(Task.CompletedTask)
                .Verifiable();

            ExecutionResult result = await this.provider.ExecuteAsync(this.mockExperimentContext, this.mockFixture.Component, CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.AreEqual(ExecutionStatus.InProgress, result.Status);

            this.mockFixture.DataClient.Verify();
            this.provider.ArmResourceManager.Verify();
        }

        [Test]
        public async Task TdpProviderWillDeployVMWhenResourceGroupIsDeployed()
        {
            VmResourceGroupDefinition definition = TestResourceGroupDefinition.ResourceGroupReadyVmNotCreated();
            TdpState providerState = new TdpState();
            providerState.ActiveIterationResourceGroup = definition;
            providerState.ActiveIterationState = TdpState.TdpStatus.Creating;

            this.mockFixture.DataClient.Setup(c => c.GetOrCreateStateAsync<TdpState>(
                    this.mockExperimentContext.Experiment.Id,
                    $"state-{this.mockExperimentContext.ExperimentStep.Id}",
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .ReturnsAsync(providerState)
                .Verifiable();

            this.provider.ArmResourceManager.Setup(m => m.DeployResourceGroupAndVirtualMachinesAsync(It.IsAny<VmResourceGroupDefinition>(), It.IsAny<CancellationToken>()))
                .Callback<VmResourceGroupDefinition, CancellationToken>((resourceGroup, token) =>
                {
                    Assert.AreEqual(1, resourceGroup.VirtualMachines.Count);
                })
                .ReturnsAsync((VmResourceGroupDefinition resourceGroup, CancellationToken token) =>
                {
                    foreach (VmDefinition vm in resourceGroup.VirtualMachines)
                    {
                        vm.State = ProvisioningState.Creating;
                    }

                    return resourceGroup;
                })
                .Verifiable();

            this.mockFixture.DataClient.Setup(c => c.SaveStateAsync<TdpState>(
                this.mockExperimentContext.Experiment.Id,
                $"state-{this.mockExperimentContext.ExperimentStep.Id}",
                It.IsAny<TdpState>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string>()))
                .Callback<string, string, TdpState, CancellationToken, string>((experimentId, stepId, state, token, stateId) =>
                {
                    Assert.IsTrue(state.ConsecutiveFailures == 0);
                    Assert.IsTrue(state.SuccessfulVirtualMachines.Count == 0);
                    Assert.IsTrue(state.ActiveIterationState == TdpState.TdpStatus.Creating);
                })
                .Returns(Task.CompletedTask)
                .Verifiable();

            ExecutionResult result = await this.provider.ExecuteAsync(this.mockExperimentContext, this.mockFixture.Component, CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.AreEqual(ExecutionStatus.InProgress, result.Status);

            this.mockFixture.DataClient.Verify();
            this.provider.ArmResourceManager.Verify();
        }

        [Test]
        public async Task TdpProviderWillDeleteVmWhenPreviousAttemptSucceeded()
        {
            VmResourceGroupDefinition definition = TestResourceGroupDefinition.ResourceGroupReadyVmCreated();
            TdpState providerState = new TdpState();
            providerState.ActiveIterationResourceGroup = definition;
            providerState.ActiveIterationState = TdpState.TdpStatus.Creating;

            this.mockFixture.DataClient.Setup(c => c.GetOrCreateStateAsync<TdpState>(
                    this.mockExperimentContext.Experiment.Id,
                    $"state-{this.mockExperimentContext.ExperimentStep.Id}",
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .ReturnsAsync(providerState)
                .Verifiable();
            
            this.provider.ArmResourceManager.Setup(m => m.DeployResourceGroupAndVirtualMachinesAsync(It.IsAny<VmResourceGroupDefinition>(), It.IsAny<CancellationToken>()))
                .Callback<VmResourceGroupDefinition, CancellationToken>((resourceGroup, token) =>
                {
                    Assert.AreEqual(1, resourceGroup.VirtualMachines.Count);
                })
                .ReturnsAsync((VmResourceGroupDefinition resourceGroup, CancellationToken token) =>
                {
                    foreach (VmDefinition vm in resourceGroup.VirtualMachines)
                    {
                        vm.State = ProvisioningState.Succeeded;
                    }

                    return resourceGroup;
                })
                .Verifiable();

            this.provider.ArmResourceManager.Setup(m => m.DeleteResourceGroupAsync(It.IsAny<VmResourceGroupDefinition>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .Callback<VmResourceGroupDefinition, CancellationToken, bool>((resourceGroup, token, forceDelete) =>
                {
                    Assert.AreEqual(1, resourceGroup.VirtualMachines.Count);
                    resourceGroup.DeletionState = CleanupState.Deleting;
                })
                .Returns(Task.CompletedTask)
                .Verifiable();

            this.mockFixture.DataClient.Setup(c => c.SaveStateAsync<TdpState>(
                this.mockExperimentContext.Experiment.Id,
                $"state-{this.mockExperimentContext.ExperimentStep.Id}",
                It.IsAny<TdpState>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string>()))
                .Callback<string, string, TdpState, CancellationToken, string>((experimentId, stepId, state, token, stateId) =>
                {
                    Assert.IsTrue(state.ConsecutiveFailures == 0);
                    Assert.IsTrue(state.SuccessfulVirtualMachines.Count == 1);
                    Assert.IsTrue(state.ActiveIterationState == TdpState.TdpStatus.Deleting);
                })
                .Returns(Task.CompletedTask)
                .Verifiable();

            ExecutionResult result = await this.provider.ExecuteAsync(this.mockExperimentContext, this.mockFixture.Component, CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.AreEqual(ExecutionStatus.InProgress, result.Status);

            this.mockFixture.DataClient.Verify();
            this.provider.ArmResourceManager.Verify();
        }

        [Test]
        public async Task TdpProviderWillContinueDeployVmWhenPreviousVMDeleted()
        {
            TdpState providerState = new TdpState()
            {
                ConsecutiveFailures = 0
            };

            for (int i = 0; i < 10; i++)
            {
                providerState.SuccessfulVirtualMachines.Add($"VM-{i}");
            }

            this.mockFixture.DataClient.Setup(c => c.GetOrCreateStateAsync<TdpState>(
                    this.mockExperimentContext.Experiment.Id,
                    $"state-{this.mockExperimentContext.ExperimentStep.Id}",
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .ReturnsAsync(providerState)
                .Verifiable();

            VmResourceGroupDefinition definition = TestResourceGroupDefinition.ResourceGroupDeleted();
            providerState.ActiveIterationResourceGroup = definition;
            providerState.ActiveIterationState = TdpState.TdpStatus.Deleting;

            this.provider.ArmResourceManager.Setup(m => m.DeleteResourceGroupAsync(It.IsAny<VmResourceGroupDefinition>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .Callback<VmResourceGroupDefinition, CancellationToken, bool>((resourceGroup, token, forceDelete) =>
                {
                    Assert.AreEqual(1, resourceGroup.VirtualMachines.Count);
                    resourceGroup.DeletionState = CleanupState.Succeeded;
                })
                .Returns(Task.CompletedTask)
                .Verifiable();

            this.provider.ArmResourceManager.Setup(m => m.DeployResourceGroupAndVirtualMachinesAsync(It.IsAny<VmResourceGroupDefinition>(), It.IsAny<CancellationToken>()))
                .Callback<VmResourceGroupDefinition, CancellationToken>((resourceGroup, token) =>
                {
                    Assert.AreEqual(1, resourceGroup.VirtualMachines.Where(vm => vm.State == ProvisioningState.Pending).ToList().Count);
                })
                .ReturnsAsync((VmResourceGroupDefinition resourceGroup, CancellationToken token) =>
                {
                    resourceGroup.VirtualMachines.LastOrDefault().State = ProvisioningState.Creating;
                    return resourceGroup;
                })
                .Verifiable();

            this.mockFixture.DataClient.Setup(c => c.SaveStateAsync<TdpState>(
                this.mockExperimentContext.Experiment.Id,
                $"state-{this.mockExperimentContext.ExperimentStep.Id}",
                It.IsAny<TdpState>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string>()))
                .Callback<string, string, TdpState, CancellationToken, string>((experimentId, stepId, state, token, stateId) =>
                {
                    Assert.IsTrue(state.ConsecutiveFailures == 0);
                    Assert.IsTrue(state.SuccessfulVirtualMachines.Count == 10);
                })
                .Returns(Task.CompletedTask)
                .Verifiable();

            ExecutionResult result = await this.provider.ExecuteAsync(this.mockExperimentContext, this.mockFixture.Component, CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.AreEqual(ExecutionStatus.InProgress, result.Status);

            this.mockFixture.DataClient.Verify();
            this.provider.ArmResourceManager.Verify();
        }

        [Test]
        public async Task TdpProviderWillFailIfConsecutiveFailuresReachMaximum()
        {
            TdpState providerState = new TdpState()
            {
                ConsecutiveFailures = 5,
                MaximumConsecutiveFailure = 5
            };

            for (int i = 0; i < 10; i++)
            {
                providerState.SuccessfulVirtualMachines.Add($"VM-{i}");
            }

            this.mockFixture.DataClient.Setup(c => c.GetOrCreateStateAsync<TdpState>(
                    this.mockExperimentContext.Experiment.Id,
                    $"state-{this.mockExperimentContext.ExperimentStep.Id}",
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .ReturnsAsync(providerState)
                .Verifiable();

            VmResourceGroupDefinition definition = TestResourceGroupDefinition.ResourceGroupReadyVmFailed();
            providerState.ActiveIterationResourceGroup = definition;
            providerState.ActiveIterationState = TdpState.TdpStatus.Creating;

            this.provider.ArmResourceManager.Setup(m => m.DeployResourceGroupAndVirtualMachinesAsync(It.IsAny<VmResourceGroupDefinition>(), It.IsAny<CancellationToken>()))
                .Callback<VmResourceGroupDefinition, CancellationToken>((resourceGroup, token) =>
                {
                    Assert.AreEqual(ProvisioningState.Failed, resourceGroup.VirtualMachines.First().State);
                })
                .ReturnsAsync((VmResourceGroupDefinition resourceGroup, CancellationToken token) =>
                {
                    return resourceGroup;
                })
                .Verifiable();

            ExecutionResult result = await this.provider.ExecuteAsync(this.mockExperimentContext, this.mockFixture.Component, CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.AreEqual(ExecutionStatus.Failed, result.Status);
            Assert.AreEqual($"Reached maximum consecutive failures for deploying resource group '{definition.Name}' at '5' times.", result.Error.Message);

            this.mockFixture.DataClient.Verify();
            this.provider.ArmResourceManager.Verify();
        }

        [Test]
        public async Task TdpProviderReturnsInprogressWhenRequiredCountAreReachedWaitingForLastDeletion()
        {
            TdpState providerState = new TdpState()
            {
                ConsecutiveFailures = 0
            };

            for (int i = 0; i < 49; i++)
            {
                providerState.SuccessfulVirtualMachines.Add($"VM-{i}");
            }

            this.mockFixture.DataClient.Setup(c => c.GetOrCreateStateAsync<TdpState>(
                    this.mockExperimentContext.Experiment.Id,
                    $"state-{this.mockExperimentContext.ExperimentStep.Id}",
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .ReturnsAsync(providerState)
                .Verifiable();

            VmResourceGroupDefinition definition = TestResourceGroupDefinition.ResourceGroupReadyVmCreated();
            providerState.ActiveIterationResourceGroup = definition;

            this.provider.ArmResourceManager.Setup(m => m.DeployResourceGroupAndVirtualMachinesAsync(It.IsAny<VmResourceGroupDefinition>(), It.IsAny<CancellationToken>()))
                .Callback<VmResourceGroupDefinition, CancellationToken>((resourceGroup, token) =>
                {
                    Assert.AreEqual(ProvisioningState.Succeeded, resourceGroup.VirtualMachines.First().State);
                })
                .ReturnsAsync((VmResourceGroupDefinition resourceGroup, CancellationToken token) =>
                {
                    resourceGroup.VirtualMachines.First().State = ProvisioningState.Succeeded;
                    return resourceGroup;
                })
                .Verifiable();

            this.provider.ArmResourceManager.Setup(m => m.DeleteResourceGroupAsync(It.IsAny<VmResourceGroupDefinition>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .Callback<VmResourceGroupDefinition, CancellationToken, bool>((resourceGroup, token, forceDelete) =>
                {
                    resourceGroup.VirtualMachines.First().State = ProvisioningState.Deleting;
                    resourceGroup.DeletionState = CleanupState.Deleting;
                })
                .Returns(Task.CompletedTask)
                .Verifiable();

            this.mockFixture.DataClient.Setup(c => c.SaveStateAsync<TdpState>(
                this.mockExperimentContext.Experiment.Id,
                $"state-{this.mockExperimentContext.ExperimentStep.Id}",
                It.IsAny<TdpState>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string>()))
                .Callback<string, string, TdpState, CancellationToken, string>((experimentId, stepId, state, token, stateId) =>
                {
                    Assert.IsTrue(state.ConsecutiveFailures == 0);
                    Assert.IsTrue(state.SuccessfulVirtualMachines.Count == 50);
                })
                .Returns(Task.CompletedTask)
                .Verifiable();

            ExecutionResult result = await this.provider.ExecuteAsync(this.mockExperimentContext, this.mockFixture.Component, CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.AreEqual(ExecutionStatus.InProgress, result.Status);

            this.mockFixture.DataClient.Verify();
            this.provider.ArmResourceManager.Verify();
        }

        [Test]
        public async Task TdpProviderReturnsSuccessWhenRequiredCountAreReachedAndAllDeleted()
        {
            TdpState providerState = new TdpState()
            {
                ConsecutiveFailures = 0
            };

            for (int i = 0; i < 50; i++)
            {
                providerState.SuccessfulVirtualMachines.Add($"VM-{i}");
            }

            this.mockFixture.DataClient.Setup(c => c.GetOrCreateStateAsync<TdpState>(
                    this.mockExperimentContext.Experiment.Id,
                    $"state-{this.mockExperimentContext.ExperimentStep.Id}",
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .ReturnsAsync(providerState)
                .Verifiable();

            VmResourceGroupDefinition definition = TestResourceGroupDefinition.ResourceGroupDeleted();
            providerState.ActiveIterationResourceGroup = definition;
            providerState.ActiveIterationState = TdpState.TdpStatus.Deleting;

            this.provider.ArmResourceManager.Setup(m => m.DeleteResourceGroupAsync(It.IsAny<VmResourceGroupDefinition>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .Callback<VmResourceGroupDefinition, CancellationToken, bool>((resourceGroup, token, refreshAll) =>
                {
                    Assert.AreEqual(CleanupState.Succeeded, resourceGroup.DeletionState);
                })
                .Returns(Task.CompletedTask)
                .Verifiable();

            this.mockFixture.DataClient.Setup(c => c.SaveStateAsync<TdpState>(
                this.mockExperimentContext.Experiment.Id,
                $"state-{this.mockExperimentContext.ExperimentStep.Id}",
                It.IsAny<TdpState>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string>()))
                .Callback<string, string, TdpState, CancellationToken, string>((experimentId, stepId, state, token, stateId) =>
                {
                    Assert.IsTrue(state.ConsecutiveFailures == 0);
                    Assert.IsTrue(state.SuccessfulVirtualMachines.Count == 50);
                })
                .Returns(Task.CompletedTask)
                .Verifiable();

            ExecutionResult result = await this.provider.ExecuteAsync(this.mockExperimentContext, this.mockFixture.Component, CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.AreEqual(ExecutionStatus.Succeeded, result.Status);

            this.mockFixture.DataClient.Verify();
            this.provider.ArmResourceManager.Verify();
        }

        private static class TestResourceGroupDefinition
        {
            public static VmResourceGroupDefinition ResourceGroupReadyVmNotCreated()
            {
                VmResourceGroupDefinition result = TestResourceGroupDefinition.GetBaseDefinition();
                result.State = ProvisioningState.Succeeded;
                return result;
            }

            public static VmResourceGroupDefinition ResourceGroupReadyVmCreated()
            {
                VmResourceGroupDefinition result = TestResourceGroupDefinition.GetBaseDefinition();
                result.State = ProvisioningState.Succeeded;
                result.VirtualMachines.First().State = ProvisioningState.Succeeded;
                return result;
            }

            public static VmResourceGroupDefinition ResourceGroupReadyVmFailed()
            {
                VmResourceGroupDefinition result = TestResourceGroupDefinition.GetBaseDefinition();
                result.State = ProvisioningState.Succeeded;
                result.VirtualMachines.First().State = ProvisioningState.Failed;
                return result;
            }

            public static VmResourceGroupDefinition ResourceGroupDeleted()
            {
                VmResourceGroupDefinition result = TestResourceGroupDefinition.GetBaseDefinition();
                result.State = ProvisioningState.Deleted;
                result.DeletionState = CleanupState.Succeeded;
                return result;
            }

            private static VmResourceGroupDefinition GetBaseDefinition()
            {
                List<AzureVmSpecification> specList = new List<AzureVmSpecification>();
                specList.Add(TestResourceGroupDefinition.GetBasevmSpec());
                VmResourceGroupDefinition baseDefinition = new VmResourceGroupDefinition(
                    "TestEnvironment",
                    "TestSub",
                    "TestExpId",
                    Guid.NewGuid().ToString(),
                    specList,
                    "testRegion",
                    "testCluster",
                    "TestTipId",
                    "TestNode");
                return baseDefinition;
            }

            private static AzureVmSpecification GetBasevmSpec()
            {
                AzureVmSpecification spec = new AzureVmSpecification(
                    "testStorage",
                    "testSize",
                    "testPublisher",
                    "testOffer",
                    "testSku",
                    "testVersion",
                    2,
                    "testDiskSku",
                    1024,
                    "testStorage");

                return spec;
            }
        }

        private class TestTenantDeploymentPerformanceProvider : TdpProvider
        {
            public TestTenantDeploymentPerformanceProvider(IServiceCollection services)
            : base(services)
            {
            }

            public Mock<IArmResourceManager> ArmResourceManager { get; set; } = new Mock<IArmResourceManager>();

            public static new string GetTargetDiskSku(string proposedDiskSku, string vmSku)
            {
                return TdpProvider.GetTargetDiskSku(proposedDiskSku, vmSku);
            }

            public static new string GetTargetVmSku(ExperimentComponent component, EventContext telemetryContext)
            {
                return TdpProvider.GetTargetVmSku(component, telemetryContext);
            }

            public static new string GetTargetVmSku(ExperimentComponent component, EnvironmentEntity tipSessionEntity, EventContext telemetryContext)
            {
                return TdpProvider.GetTargetVmSku(component, tipSessionEntity, telemetryContext);
            }

            protected override IArmResourceManager CreateArmResourceManager(ExperimentContext context)
            {
                return this.ArmResourceManager.Object;
            }
        }
    }
}