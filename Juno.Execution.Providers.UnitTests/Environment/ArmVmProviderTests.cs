namespace Juno.Execution.Providers.Environment
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Security;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;
    using AutoFixture;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.Execution.ArmIntegration;
    using Juno.Execution.Providers.AutoTriage;
    using Juno.Execution.TipIntegration;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ArmVmProviderTests
    {
        private ProviderFixture mockFixture;
        private AzureVirtualMachine azureVirtualMachine;
        private VmResourceGroupDefinition mockVmResourceGroup;
        private IEnumerable<EnvironmentEntity> mockTipSessionEntities;
        private TestArmVmProvider provider;
        private List<DiagnosticsRequest> mockDiagnosticRequest;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new ProviderFixture(typeof(ArmVmProvider));
            this.mockFixture.DataClient.SetReturnsDefault(new HttpResponseMessage(HttpStatusCode.OK));
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

            this.mockVmResourceGroup = this.mockFixture.Create<VmResourceGroupDefinition>();
            this.mockFixture.Component.Parameters.AddRange(new Dictionary<string, IConvertible>
            {
                { "subscriptionId", Guid.NewGuid().ToString() },
                { "osDiskStorageAccountType", "Standard_LRS" },
                { "osPublisher", "MicrosoftWindowsServer" },
                { "osOffer", "WindowsServer" },
                { "osSku", "WindowsServer" },
                { "osVersion", "VersionABC" },
                { "vmSize", "Standard_F4s_v2" },
                { "vmCount", 2 }
            });

            this.mockFixture.Services.AddSingleton(NullLogger.Instance);
            this.mockFixture.Services.AddSingleton(this.mockFixture.KeyVault.Object);
            this.mockDiagnosticRequest = new List<DiagnosticsRequest>()
                {
                    new DiagnosticsRequest(
                        this.mockFixture.ExperimentId,
                        Guid.NewGuid().ToString(),
                        DiagnosticsIssueType.ArmVmCreationFailure,
                        DateTime.UtcNow.AddHours(-2),
                        DateTime.UtcNow,
                        new Dictionary<string, IConvertible>()
                        {
                            { DiagnosticsParameter.TipSessionId, this.mockVmResourceGroup.TipSessionId },
                            { DiagnosticsParameter.SubscriptionId, this.mockVmResourceGroup.SubscriptionId },
                            { DiagnosticsParameter.ResourceGroupName, this.mockVmResourceGroup.Name },
                            { DiagnosticsParameter.ExperimentId, this.mockFixture.ExperimentId },
                            { DiagnosticsParameter.ProviderName, nameof(ArmVmProvider) }
                        })
                };
            this.provider = new TestArmVmProvider(this.mockFixture.Services);
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
        [TestCase("Standard_D2_v2")]
        [TestCase("Standard_D2_v2;Standard_D2s_v3")]
        [TestCase("Standard_D2_v2;Standard_D2s_v3;Standard_F2s_v2")]
        public void ProviderSupportsTargetingMultipleVmSkusWhenTheyAreAvailableForAGivenTipSession(string availableVmSkus)
        {
            EnvironmentEntity tipSessionEntity = EnvironmentEntity.TipSession("AnyId", "*", new Dictionary<string, IConvertible>
            {
                ["SupportedVmSkus"] = availableVmSkus
            });

            this.mockFixture.Component.Parameters["vmSize"] = availableVmSkus;

            string targetVmSku = TestArmVmProvider.GetTargetVmSku(this.mockFixture.Component, tipSessionEntity, new EventContext(Guid.NewGuid()));
            Assert.IsTrue(availableVmSkus.Split(';').Contains(targetVmSku));
        }

        [Test]
        public void ProviderSupportsThrowsIfTheVmSkusDefinedForTheExperimentStepDoNotMatchWithAtLeastOneVmSkuAvailableOnTheTipSession()
        {
            EnvironmentEntity tipSessionEntity = EnvironmentEntity.TipSession("AnyId", "*", new Dictionary<string, IConvertible>
            {
                ["SupportedVmSkus"] = "Standard_D2_v3;Standard_D2s_v3"
            });

            // The VM SKUs defined for the experiment step do not match the SKUs available for the
            // TiP session/node.
            this.mockFixture.Component.Parameters["vmSize"] = "Standard_F2s_v2;Standard_E2s_v2";

            Assert.Throws<ProviderException>(() => TestArmVmProvider.GetTargetVmSku(this.mockFixture.Component, tipSessionEntity, new EventContext(Guid.NewGuid())));
        }

        [Test]
        [TestCase("Standard_D2_v2")]
        [TestCase("Standard_D2_v2;Standard_D2s_v3")]
        [TestCase("Standard_D2_v2;Standard_D2s_v3;Standard_F2s_v2")]
        public void ProviderSupportsTargetingMultipleVmSkusWhenATipSessionIsNotInUse(string explicitVmSkus)
        {
            this.mockFixture.Component.Parameters["vmSize"] = explicitVmSkus;
            this.mockFixture.Component.Parameters["useTipSession"] = false;

            string targetVmSku = TestArmVmProvider.GetTargetVmSku(this.mockFixture.Component, new EventContext(Guid.NewGuid()));
            Assert.IsTrue(explicitVmSkus.Split(';').Contains(targetVmSku));
        }

        [Test]
        [TestCase("Standard_D2s_v3")]
        [TestCase("Standard_D16s_v3")]
        [TestCase("Standard_D128s_v3")]
        [TestCase("Standard_E2s_v3")]
        [TestCase("Standard_E16s_v3")]
        [TestCase("Standard_E128s_v3")]
        [TestCase("Standard_F2s_v2")]
        [TestCase("Standard_F16s_v2")]
        [TestCase("Standard_F128s_v2")]
        [TestCase("Standard_G4s_v2")]
        [TestCase("Standard_G16s_v2")]
        [TestCase("Standard_G128s_v2")]
        [TestCase("Standard_M64s")]
        [TestCase("Standard_M256s")]
        [TestCase("Standard_M64s_v2")]
        [TestCase("Standard_M256s_v2")]
        [TestCase("Standard_E2as_v3")]
        [TestCase("Standard_E16as_v3")]
        [TestCase("Standard_E128as_v3")]
        public void ProviderUsesTheDiskSkuDefinedInTheExperimentStepWhenItIsValidForTheTargetVmSku(string vmSku)
        {
            string proposedDiskSku = "Premium_LRS"; // Available only with DS, ES, FS etc. family VM SKUs
            string targetDiskSku = TestArmVmProvider.GetTargetDiskSku(proposedDiskSku, vmSku);
            Assert.AreEqual(proposedDiskSku, targetDiskSku);

            proposedDiskSku = "Standard_LRS";
            targetDiskSku = TestArmVmProvider.GetTargetDiskSku(proposedDiskSku, vmSku);
            Assert.AreEqual(proposedDiskSku, targetDiskSku);
        }

        [Test]
        [TestCase("Standard_D2_v3")]
        [TestCase("Standard_D16_v3")]
        [TestCase("Standard_D128_v3")]
        [TestCase("Standard_E2_v3")]
        [TestCase("Standard_E16_v3")]
        [TestCase("Standard_E128_v3")]
        [TestCase("Standard_F2_v2")]
        [TestCase("Standard_F16_v2")]
        [TestCase("Standard_F128_v2")]
        [TestCase("Standard_G4_v2")]
        [TestCase("Standard_G16_v2")]
        [TestCase("Standard_G128_v2")]
        [TestCase("Standard_M64")]
        [TestCase("Standard_M256")]
        [TestCase("Standard_M64_v2")]
        [TestCase("Standard_M256_v2")]
        [TestCase("Standard_E2a_v3")]
        [TestCase("Standard_E16a_v3")]
        [TestCase("Standard_E128a_v3")]
        public void ProviderUsesASupportedOsDiskSkuWhenTheOneDefinedInTheExperimentStepIsNotValidForTheTargetVmSku(string vmSku)
        {
            string proposedDiskSku = "Premium_LRS"; // Available only with DS, ES, FS etc. family VM SKUs
            string targetDiskSku = TestArmVmProvider.GetTargetDiskSku(proposedDiskSku, vmSku);
            Assert.AreEqual(targetDiskSku, "Standard_LRS");
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
        public void ProviderRequestsAutoTriageDiagnosticsOnFailedExperimentsWithDiagnosticsEnabled()
        {
            // Enable Diagnostics flag is on
            this.mockFixture.Component.Parameters[StepParameters.EnableDiagnostics] = true;
            // Execution will fail with invalid resource group deployment
            this.ValidateResourceGroupDeployment(ProvisioningState.Failed, ExecutionStatus.Failed);

            // call to request autotriage diagnostics are made
            this.mockFixture.DataClient.Verify(method =>
                method.UpdateStateItemsAsync(
                    this.mockFixture.ExperimentId,
                    ContractExtension.DiagnosticsRequests,
                    this.mockDiagnosticRequest,
                    CancellationToken.None,
                    null),
                Times.AtMostOnce);
        }

        [Test]
        public void ProviderDoesNotRequestAutoTriageDiagnosticsOnFailedExperimentsWithoutDiagnosticsEnabled()
        {
            // Enable Diagnostics flag is not enabled
            this.mockFixture.Component.Parameters[StepParameters.EnableDiagnostics] = false;
            // Execution will fail with invalid resource group deployment
            this.ValidateResourceGroupDeployment(ProvisioningState.Failed, ExecutionStatus.Failed);

            // call to request autotriage diagnostics are not made
            this.mockFixture.DataClient.Verify(method =>
                method.UpdateStateItemsAsync(
                    this.mockFixture.ExperimentId,
                    ContractExtension.DiagnosticsRequests,
                    this.mockDiagnosticRequest,
                    CancellationToken.None,
                    null),
                Times.Never);
        }

        [Test]
        [TestCase(ProvisioningState.Accepted, ExecutionStatus.InProgress)]
        [TestCase(ProvisioningState.Running, ExecutionStatus.InProgress)]
        [TestCase(ProvisioningState.Succeeded, ExecutionStatus.Succeeded)]
        public void ProviderWithDiagnosticsEnabledDoesNotRequestDiagnosticsFromUnfailedExperiments(ProvisioningState state, ExecutionStatus status)
        {
            // Enable Diagnostics flag is enabled
            this.mockFixture.Component.Parameters[StepParameters.EnableDiagnostics] = true;
            // Testing other resource group deployment execution statuses
            this.ValidateResourceGroupDeployment(state, status);

            // call to request autotriage diagnostics are not made
            this.mockFixture.DataClient.Verify(method =>
                method.UpdateStateItemsAsync(
                    this.mockFixture.ExperimentId,
                    ContractExtension.DiagnosticsRequests,
                    this.mockDiagnosticRequest,
                    CancellationToken.None,
                    null),
                Times.Never);
        }

        [Test]
        public void ProviderUsesTheExpectedVmSpecificationsDefinedForTheExperimentStep()
        {
            int expectedVmCount = this.mockFixture.Component.Parameters.GetValue<int>("vmCount");

            VmResourceGroupDefinition definition = TestArmVmProvider.CreateResourceGroupDefinition(
                this.mockFixture.Context,
                this.mockFixture.Component,
                "AnyRegion",
                "Standard_D2_v3");

            Assert.IsNotNull(definition);
            Assert.AreEqual(expectedVmCount, definition.VirtualMachines.Count);
            definition.VirtualMachines.ToList().ForEach(defn =>
            {
                Assert.AreEqual(defn.OsDiskStorageAccountType, this.mockFixture.Component.Parameters["osDiskStorageAccountType"]);
                var imageReference = defn.ImageReference.ToObject<ImageReference>();
                Assert.AreEqual(imageReference.Offer, this.mockFixture.Component.Parameters["osOffer"]);
                Assert.AreEqual(imageReference.Publisher, this.mockFixture.Component.Parameters["osPublisher"]);
                Assert.AreEqual(imageReference.Sku, this.mockFixture.Component.Parameters["osSku"]);
                Assert.AreEqual(imageReference.Version, this.mockFixture.Component.Parameters["osVersion"]);
            });
        }

        [Test]
        public void ProviderUsesTheExpectedVmSpecificationsDefinedForTheExperimentStepWhenDataDisksAreDefined_Scenario1()
        {
            this.mockFixture.Component.Parameters["dataDiskCount"] = 3;
            this.mockFixture.Component.Parameters["dataDiskSizeInGB"] = 123;
            this.mockFixture.Component.Parameters["dataDiskSku"] = "Standard_LRS";
            this.mockFixture.Component.Parameters["dataDiskStorageAccountType"] = "Standard_LRS";

            VmResourceGroupDefinition definition = TestArmVmProvider.CreateResourceGroupDefinition(
                this.mockFixture.Context,
                this.mockFixture.Component,
                "AnyRegion",
                "AnyVmSku");

            Assert.IsNotNull(definition);
            definition.VirtualMachines.ToList().ForEach(defn =>
            {
                Assert.IsTrue(defn.VirtualDisks.Count == 3);
                defn.VirtualDisks.ToList().ForEach(disk =>
                {
                    Assert.AreEqual(disk.DiskSizeGB, this.mockFixture.Component.Parameters["dataDiskSizeInGB"]);
                    Assert.AreEqual(disk.Sku, this.mockFixture.Component.Parameters["dataDiskSku"]);
                    Assert.AreEqual(disk.StorageAccountType, this.mockFixture.Component.Parameters["dataDiskStorageAccountType"]);
                });
            });
        }

        [Test]
        public void ProviderUsesTheExpectedVmSpecificationsDefinedForTheExperimentStepWhenDataDisksAreDefined_Scenario2()
        {
            this.mockFixture.Component.Parameters["dataDiskCount"] = 3;
            this.mockFixture.Component.Parameters["dataDiskSizeInGB"] = 123;
            this.mockFixture.Component.Parameters["dataDiskSku"] = "Premium_LRS";
            this.mockFixture.Component.Parameters["dataDiskStorageAccountType"] = "Premium_LRS";

            VmResourceGroupDefinition definition = TestArmVmProvider.CreateResourceGroupDefinition(
                this.mockFixture.Context,
                this.mockFixture.Component,
                "AnyRegion",
                "Standard_D16s_v3");

            Assert.IsNotNull(definition);
            definition.VirtualMachines.ToList().ForEach(defn =>
            {
                Assert.IsTrue(defn.VirtualDisks.Count == 3);
                defn.VirtualDisks.ToList().ForEach(disk =>
                {
                    Assert.AreEqual(disk.DiskSizeGB, this.mockFixture.Component.Parameters["dataDiskSizeInGB"]);
                    Assert.AreEqual(disk.Sku, this.mockFixture.Component.Parameters["dataDiskSku"]);
                    Assert.AreEqual(disk.StorageAccountType, this.mockFixture.Component.Parameters["dataDiskStorageAccountType"]);
                });
            });
        }

        [Test]
        public void ProviderCreatesTheExpectedResourceGroupDefinitionToTargetVmCreationWithATipSession()
        {
            string expectedVmSku = "AnyVmSku01";
            string expectedRegion = "AnyRegion01";
            string expectedClusterName = "AnyCluster01";
            string expectedTipSessionId = Guid.NewGuid().ToString();
            string expectedNodeId = Guid.NewGuid().ToString();

            VmResourceGroupDefinition definition = TestArmVmProvider.CreateResourceGroupDefinition(
                this.mockFixture.Context,
                this.mockFixture.Component,
                expectedRegion,
                expectedVmSku,
                expectedClusterName,
                expectedTipSessionId,
                expectedNodeId);

            Assert.IsNotNull(definition);
            Assert.AreEqual(definition.ClusterId, expectedClusterName);
            Assert.AreEqual(definition.NodeId, expectedNodeId);
            Assert.AreEqual(definition.Region, expectedRegion);
            Assert.AreEqual(definition.TipSessionId, expectedTipSessionId);
            Assert.AreEqual(definition.Environment, EnvironmentSettings.Initialize(this.mockFixture.Configuration).Environment);
            Assert.AreEqual(definition.ExperimentId, this.mockFixture.Context.Experiment.Id);
            Assert.AreEqual(definition.StepId, this.mockFixture.Context.ExperimentStep.Id);

            // VM definitions should use the specifications from
        }

        [Test]
        public void ProviderReturnCorrectStatusonResourceDeployment()
        {
            this.ValidateResourceGroupDeployment(ProvisioningState.Accepted, ExecutionStatus.InProgress);
            this.ValidateResourceGroupDeployment(ProvisioningState.Running, ExecutionStatus.InProgress);
            this.ValidateResourceGroupDeployment(ProvisioningState.Failed, ExecutionStatus.Failed);
            this.ValidateResourceGroupDeployment(ProvisioningState.Succeeded, ExecutionStatus.Succeeded);
        }

        [Test]
        public void ProviderAddsExpectedTagsToResourceGroupsCreated()
        {
            string expectedVmSku = "AnyVmSku01";
            string expectedRegion = "AnyRegion01";
            string expectedClusterName = "AnyCluster01";
            string expectedTipSessionId = Guid.NewGuid().ToString();
            string expectedNodeId = Guid.NewGuid().ToString();

            VmResourceGroupDefinition definition = TestArmVmProvider.CreateResourceGroupDefinition(
                this.mockFixture.Context,
                this.mockFixture.Component,
                expectedRegion,
                expectedVmSku,
                expectedClusterName,
                expectedTipSessionId,
                expectedNodeId);

            Assert.IsNotNull(definition.Tags);
            Assert.IsTrue(definition.Tags.Count == 7 + this.mockFixture.Component.Tags.Count);
            Assert.AreEqual(definition.Tags["experimentId"], this.mockFixture.Context.Experiment.Id);
            Assert.AreEqual(definition.Tags["experimentGroup"], this.mockFixture.Component.Group);
            Assert.AreEqual(definition.Tags["experimentStepId"], this.mockFixture.Context.ExperimentStep.Id);
            Assert.AreEqual(definition.Tags["tipSessionId"], expectedTipSessionId);
            Assert.AreEqual(definition.Tags["nodeId"], expectedNodeId);
            Assert.IsTrue(definition.Tags.ContainsKey("createdDate"));
            Assert.IsTrue(definition.Tags.ContainsKey("expirationDate"));
        }

        private void ValidateResourceGroupDeployment(ProvisioningState resourceGroupState, ExecutionStatus expectedStatus)
        {
            this.mockFixture.DataClient.OnSaveState<VmResourceGroupDefinition>().Returns(Task.CompletedTask);
            // Reset the status and VM states/statuses
            if (expectedStatus == ExecutionStatus.Succeeded)
            {
                this.mockVmResourceGroup.State = ProvisioningState.Succeeded;
                this.mockVmResourceGroup.VirtualMachines.ToList().ForEach(deployment => deployment.State = ProvisioningState.Succeeded);

                this.mockFixture.DataClient
                    .Setup(c => c.GetOrCreateStateAsync<VmResourceGroupDefinition>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string>()))
                    .ReturnsAsync(this.mockVmResourceGroup);
            }

            this.mockFixture.DataClient.OnGetEntitiesProvisioned()
                .Returns(Task.FromResult(this.mockTipSessionEntities));

            this.provider.ArmResourceManager
                .Setup(mgr => mgr.DeployResourceGroupAndVirtualMachinesAsync(
                    It.IsAny<VmResourceGroupDefinition>(),
                    It.IsAny<CancellationToken>()))
                .Callback<VmResourceGroupDefinition, CancellationToken>((resourceGroup, token) =>
                {
                    resourceGroup.State = resourceGroupState;
                })
                .Returns(Task.FromResult(this.mockVmResourceGroup));

            var result = this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None).Result;

            Assert.IsNotNull(result);
            Assert.AreEqual(expectedStatus, result.Status);
        }

        private class TestArmVmProvider : ArmVmProvider
        {
            public TestArmVmProvider(IServiceCollection services)
            : base(services)
            {
            }

            public Mock<IArmResourceManager> ArmResourceManager { get; set; } = new Mock<IArmResourceManager>();

            public static new VmResourceGroupDefinition CreateResourceGroupDefinition(
                ExperimentContext context, ExperimentComponent component, string region, string vmSku, string clusterName = null, string tipSessionId = null, string nodeId = null)
            {
                return ArmVmProvider.CreateResourceGroupDefinition(context, component, region, vmSku, clusterName, tipSessionId, nodeId);
            }

            public static new string GetTargetDiskSku(string proposedDiskSku, string vmSku)
            {
                return ArmVmProvider.GetTargetDiskSku(proposedDiskSku, vmSku);
            }

            public static new string GetTargetVmSku(ExperimentComponent component, EventContext telemetryContext)
            {
                return ArmVmProvider.GetTargetVmSku(component, telemetryContext);
            }

            public static new string GetTargetVmSku(ExperimentComponent component, EnvironmentEntity tipSessionEntity, EventContext telemetryContext)
            {
                return ArmVmProvider.GetTargetVmSku(component, tipSessionEntity, telemetryContext);
            }

            protected override IArmResourceManager CreateArmResourceManager(ExperimentContext context)
            {
                return this.ArmResourceManager.Object;
            }
        }
    }
}