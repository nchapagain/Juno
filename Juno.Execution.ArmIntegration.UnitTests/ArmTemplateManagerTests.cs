namespace Juno.Execution.ArmIntegration
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Security;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Repository.KeyVault;
    using Microsoft.Azure.CRC.Rest;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    [SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Disposed in [TearDown]")]
    public class ArmTemplateManagerTests
    {
        private Fixture mockFixture;
        private FixtureDependencies mockDependencies;
        private VmResourceGroupDefinition mockVmResourceGroup;
        private TestArmTemplateManager armTemplateManager;

        [SetUp]
        public void Setup()
        {
            this.mockFixture = new Fixture();
            this.mockDependencies = new FixtureDependencies();

            this.mockVmResourceGroup = this.mockFixture.Create<VmResourceGroupDefinition>();
            this.armTemplateManager = new TestArmTemplateManager(this.mockDependencies.Configuration, NullLogger.Instance);

            this.armTemplateManager.KeyVaultClient
                .Setup(k => k.SetSecretAsync(
                    It.IsAny<string>(),
                    It.IsAny<SecureString>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(Task.CompletedTask));
        }

        [TearDown]
        public void CleanupTest()
        {
            this.armTemplateManager.Dispose();
        }

        [Test]
        public void DeleteResourceGroupShouldReturnAcceptedStatusOnDeleteRequest()
        {
            var result = this.DeleteResourceGroup(CleanupState.NotStarted);
            Assert.IsNotNull(result);
            Assert.AreEqual(result.DeletionState, CleanupState.Accepted);
        }

        [Test]
        public void DeleteResourceGroupShouldReturnDeletingStatusOnInProgressDelete()
        {
            var result = this.DeleteResourceGroup(CleanupState.Deleting, HttpStatusCode.OK, HttpStatusCode.NoContent);
            Assert.IsNotNull(result);
            Assert.AreEqual(result.DeletionState, CleanupState.Deleting);
        }

        [Test]
        public void DeleteResourceGroupShouldReturnDeletedOnDeleteComplete()
        {
            var result = this.DeleteResourceGroup(CleanupState.Succeeded);
            Assert.IsNotNull(result);
            Assert.AreEqual(result.DeletionState, CleanupState.Succeeded);
        }

        [Test]
        public void DeleteResourceGroupShouldThrowOnDeleteRejected()
        {
            var ex = Assert.Throws<ArmException>(() => this.DeleteResourceGroup(CleanupState.NotStarted, HttpStatusCode.InternalServerError));
        }

        [Test]
        public void DeleteResourceGroupShouldThrowOnCheckDeleteStatusError()
        {
            var ex = Assert.Throws<ArmException>(() => this.DeleteResourceGroup(CleanupState.Accepted, HttpStatusCode.OK, HttpStatusCode.InternalServerError));
        }

        [Test]
        public void DeployResourceGroupShouldCreateNewDeployment()
        {
            ArmDeploymentResponse mockResponse = this.mockFixture.Create<ArmDeploymentResponse>();
            mockResponse.Properties.ProvisioningState = ProvisioningState.Accepted.ToString();
            var mockResourceGroup = this.mockFixture.Create<VmResourceGroupDefinition>();
            mockResourceGroup.State = ProvisioningState.Pending;
            mockResourceGroup.DeploymentId = null;

            using (var responseMessage = ArmTemplateManagerTests.CreateResponseMessage(HttpStatusCode.Created, mockResponse))
            {
                this.armTemplateManager.ArmRestClient
                    .Setup(it => it.PutAsync(It.IsAny<Uri>(), It.IsAny<HttpContent>(), CancellationToken.None))
                    .Returns(Task.FromResult(responseMessage));

                var result = this.armTemplateManager.DeployResourceGroupAndVirtualMachinesAsync(mockResourceGroup, CancellationToken.None).Result;

                Assert.IsNotNull(result);
                Assert.AreEqual(result.State, ProvisioningState.Accepted);
                Assert.IsNotNull(result.DeploymentId);
            }
        }

        [Test]
        public void DeployResourceGroupShouldUpdateStatusForExistingDeployment()
        {
            ArmDeploymentResponse mockResponse = this.mockFixture.Create<ArmDeploymentResponse>();
            mockResponse.Properties.ProvisioningState = ProvisioningState.Accepted.ToString();
            this.mockVmResourceGroup.State = ProvisioningState.Accepted;

            mockResponse.Properties.ProvisioningState = ProvisioningState.Running.ToString();
            mockResponse.Id = this.mockVmResourceGroup.DeploymentId;

            using (var responseMessage = ArmTemplateManagerTests.CreateResponseMessage(HttpStatusCode.Created, mockResponse))
            {
                this.armTemplateManager.ArmRestClient
                    .Setup(it => it.GetAsync(It.IsAny<Uri>(), CancellationToken.None, HttpCompletionOption.ResponseContentRead))
                    .Returns(Task.FromResult(responseMessage));

                var results = this.armTemplateManager.DeployResourceGroupAndVirtualMachinesAsync(this.mockVmResourceGroup, CancellationToken.None).Result;

                Assert.IsNotNull(results);
                Assert.AreEqual(ProvisioningState.Running, results.State);
                Assert.IsNotNull(results.DeploymentId);
                Assert.IsNotNull(this.mockVmResourceGroup.DeploymentId, results.DeploymentId);
            }
        }

        [Test]
        public void DeployResourceGroupShouldCreateNewVmsDeployments()
        {
            this.VerifyVmDeployment(true);
        }

        [Test]
        public void DeployResourceGroupShouldUpdateVmsDeploymentStatus()
        {
            this.VerifyVmDeployment(false);
        }

        private static HttpResponseMessage CreateResponseMessage(HttpStatusCode expectedStatusCode, object expectedContent = null)
        {
            HttpResponseMessage mockResponse = new HttpResponseMessage(expectedStatusCode);
            if (expectedContent != null)
            {
                mockResponse.Content = new StringContent(expectedContent.ToJson());
            }

            return mockResponse;
        }

        private void VerifyVmDeployment(bool newVmDeployment)
        {
            this.mockVmResourceGroup.State = ProvisioningState.Succeeded;
            this.mockVmResourceGroup.KeyVaultName = "kv";

            ProvisioningState expectedVmDeploymentStatus = newVmDeployment ? ProvisioningState.Accepted : ProvisioningState.Succeeded;
            ArmDeploymentResponse mockResponse = this.mockFixture.Create<ArmDeploymentResponse>();
            mockResponse.Properties.ProvisioningState = newVmDeployment ? ProvisioningState.Accepted.ToString() : ProvisioningState.Succeeded.ToString();
            mockResponse.Id = "d_id";

            foreach (var vm in this.mockVmResourceGroup.VirtualMachines)
            {
                vm.State = newVmDeployment ? ProvisioningState.Pending : ProvisioningState.Succeeded;
            }

            using (var responseMessage = ArmTemplateManagerTests.CreateResponseMessage(HttpStatusCode.Created, mockResponse))
            {
                this.armTemplateManager.ArmRestClient
                    .Setup(it => it.PutAsync(It.IsAny<Uri>(), It.IsAny<HttpContent>(), CancellationToken.None))
                    .Returns(Task.FromResult(responseMessage));

                this.armTemplateManager.ArmRestClient
                    .Setup(it => it.GetAsync(It.IsAny<Uri>(), CancellationToken.None, HttpCompletionOption.ResponseContentRead))
                    .Returns(Task.FromResult(responseMessage));

                var result = this.armTemplateManager.DeployResourceGroupAndVirtualMachinesAsync(this.mockVmResourceGroup, CancellationToken.None).Result;

                Assert.IsNotNull(result);

                foreach (var vm in this.mockVmResourceGroup.VirtualMachines)
                {
                    Assert.AreEqual(expectedVmDeploymentStatus, vm.State);
                    Assert.IsNotNull(vm.DeploymentId);
                    Assert.IsNotNull(vm.DeploymentId, mockResponse.Id);
                }
            }
        }

        private VmResourceGroupDefinition DeleteResourceGroup(
            CleanupState deletionState,
            HttpStatusCode deleteRequestStatusCode = HttpStatusCode.Accepted,
            HttpStatusCode headRequestStatusCode = HttpStatusCode.NotFound)
        {
            this.mockVmResourceGroup.DeletionState = deletionState;

            using (var responseMessage = ArmTemplateManagerTests.CreateResponseMessage(deleteRequestStatusCode, string.Empty))
            {
                this.armTemplateManager.ArmRestClient
                    .Setup(it => it.DeleteAsync(It.IsAny<Uri>(), CancellationToken.None))
                    .Returns(Task.FromResult(responseMessage));

                using (var headRequestMessage = ArmTemplateManagerTests.CreateResponseMessage(headRequestStatusCode, string.Empty))
                {
                    this.armTemplateManager.ArmRestClient
                        .Setup(it => it.HeadAsync(It.IsAny<Uri>(), CancellationToken.None))
                        .Returns(Task.FromResult(headRequestMessage));

                    this.armTemplateManager.DeleteResourceGroupAsync(this.mockVmResourceGroup, CancellationToken.None).GetAwaiter().GetResult();
                    return this.mockVmResourceGroup;
                }
            }
        }

        private class TestArmTemplateManager : ArmTemplateManager
        {
            public TestArmTemplateManager(IConfiguration configuration, ILogger logger = null)
                : base(configuration, logger)
            {
            }

            public Mock<IAzureKeyVault> KeyVaultClient { get; set; } = new Mock<IAzureKeyVault>();

            public Mock<IRestClient> ArmRestClient { get; set; } = new Mock<IRestClient>();

            protected override IAzureKeyVault CreateKeyVaultClient(Uri keyVaultUri)
            {
                return this.KeyVaultClient.Object;
            }

            protected override IRestClient CreateRestClient()
            {
                return this.ArmRestClient.Object;
            }
        }
    }
}