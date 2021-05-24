namespace Juno.Execution.Providers.Environment
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Contracts;
    using Juno.Execution.ArmIntegration;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ArmVmCleanupProviderTests
    {
        private string resourceGroupNamePramKey = "resourceGroupName";
        private string resourceGroupSubIdPramKey = "subscriptionId";

        private Fixture mockFixture;
        private FixtureDependencies mockDependencies;
        private Mock<IProviderDataClient> mockDataClient;
        private ExperimentComponent mockExperimentComponent;
        private ServiceCollection providerServices;
        private ExperimentContext mockExperimentContext;
        private VmResourceGroupDefinition mockVmResourceGroup;
        private TestArmVmCleanupProvider provider;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks(ExperimentType.AB);
            this.mockDataClient = new Mock<IProviderDataClient>();
            this.mockExperimentComponent = this.mockFixture.Create<ExperimentComponent>();
            this.mockDependencies = new FixtureDependencies();

            this.mockExperimentContext = new ExperimentContext(
                this.mockFixture.Create<ExperimentInstance>(),
                this.mockFixture.Create<ExperimentStepInstance>(),
                this.mockDependencies.Configuration);

            this.mockVmResourceGroup = this.mockFixture.Create<VmResourceGroupDefinition>();
            this.providerServices = new ServiceCollection();
            this.providerServices.AddSingleton(NullLogger.Instance);
            this.providerServices.AddSingleton<IProviderDataClient>(this.mockDataClient.Object);

            this.provider = new TestArmVmCleanupProvider(this.providerServices);
        }

        [Test]
        public void ProviderValidatesRequiredParameters()
        {
            var armProvider = new ArmVmCleanupProvider(this.providerServices);
            Assert.ThrowsAsync<ArgumentException>(() =>
                armProvider.ExecuteAsync(null, this.mockExperimentComponent, CancellationToken.None));
            Assert.ThrowsAsync<ArgumentException>(() =>
                armProvider.ExecuteAsync(this.mockExperimentContext, null, CancellationToken.None));
        }

        [Test]
        public void ProviderReturnCorrectStatusOnDeleteRequestAccepted()
        {
            this.ValidateDeploymentResults(CleanupState.NotStarted, ExecutionStatus.InProgress);
        }

        [Test]
        public void ProviderReturnCorrectStatusWhenTheArmServiceRejectsTheDeleteRequest()
        {
            this.provider.ArmResourceManager
                .Setup(mgr => mgr.DeleteResourceGroupAsync(It.IsAny<VmResourceGroupDefinition>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .Throws(new ArmException($"Resource group deletion rejected."));

            this.ValidateDeploymentResults(CleanupState.NotStarted, ExecutionStatus.Failed);
        }

        [Test]
        public void ProviderReturnCorrectStatusDeleteCompeleted()
        {
            this.ValidateDeploymentResults(CleanupState.Succeeded, ExecutionStatus.Succeeded);
        }

        [Test]
        public void ProviderReturnCorrectStatusDeleteInProgress()
        {
            this.ValidateDeploymentResults(CleanupState.Accepted, ExecutionStatus.InProgress);
        }

        [Test]
        public void ProviderReturnCorrectStatusDeleteStatusError()
        {
            this.ValidateDeploymentResults(CleanupState.Failed, ExecutionStatus.Failed);
        }

        [Test]
        public void ProviderReturnCorrectStatusDeleteRequestCancelled()
        {
            var cancellationToken = new CancellationTokenSource();
            cancellationToken.Cancel();
            this.ValidateDeploymentResults(CleanupState.Succeeded, ExecutionStatus.Cancelled, cancellationToken.Token);
            this.ValidateDeploymentResults(CleanupState.Failed, ExecutionStatus.Cancelled, cancellationToken.Token);
            this.ValidateDeploymentResults(CleanupState.Succeeded, ExecutionStatus.Cancelled, cancellationToken.Token);
        }

        [Test]
        public void ProviderDoesNotRetryBeforeLastAttemptTimeout()
        {
            this.mockVmResourceGroup.DeletionState = CleanupState.Deleting;

            this.mockDataClient
                .Setup(c => c.GetOrCreateStateAsync<VmResourceGroupDefinition>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .ReturnsAsync(this.mockVmResourceGroup);

            this.mockDataClient
                .Setup(c => c.GetOrCreateStateAsync<DateTime>(
                    It.IsAny<string>(),
                    $"deleteAttemptStart_{this.mockExperimentContext.Experiment.Id}",
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .ReturnsAsync(DateTime.UtcNow);

            // ensure delete isn't called again before last attempt time expires
            var result = this.provider.ExecuteAsync(this.mockExperimentContext, this.mockExperimentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();

            this.provider.ArmResourceManager.Verify(s => s.DeleteResourceGroupAsync(
                It.IsAny<VmResourceGroupDefinition>(),
                It.IsAny<CancellationToken>(),
                true),      // The forceDelete should NOT happen
                Times.Never,
                "Delete should not be retried if previous attempt is not done.");
        }

        [Test]
        public void ProviderRetriesOnLastAttemptTimeout()
        {
            this.mockVmResourceGroup.DeletionState = CleanupState.Deleting;

            this.mockDataClient
                .Setup(c => c.GetOrCreateStateAsync<VmResourceGroupDefinition>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .ReturnsAsync(this.mockVmResourceGroup);

            this.mockDataClient
                .Setup(c => c.GetOrCreateStateAsync<DateTime>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .ReturnsAsync(DateTime.UtcNow.Subtract(TimeSpan.FromHours(1)));

            // ensure delete isn't called again before last attempt time expires
            var result = this.provider.ExecuteAsync(this.mockExperimentContext, this.mockExperimentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();

            this.provider.ArmResourceManager.Verify(s => s.DeleteResourceGroupAsync(
                It.IsAny<VmResourceGroupDefinition>(),
                It.IsAny<CancellationToken>(),
                true),  // The forceDelete SHOULD happen
                Times.Once,
                "Provider should resubmit delete if previous attempt times out.");
        }

        [Test]
        public void ArmVmCleanupProviderCleansSpecificResourceGroupWhenSuppliedInTheParameter()
        {
            this.mockVmResourceGroup.DeletionState = CleanupState.NotStarted;

            this.mockExperimentComponent.Parameters.Add(this.resourceGroupNamePramKey, Guid.NewGuid().ToString());
            this.mockExperimentComponent.Parameters.Add(this.resourceGroupSubIdPramKey, Guid.NewGuid().ToString());

            this.mockDataClient
                .Setup(c => c.GetOrCreateStateAsync<DateTime>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .ReturnsAsync(DateTime.UtcNow.Subtract(TimeSpan.FromHours(1)));

            // ensure delete isn't called again before last attempt time expires
            var result = this.provider.ExecuteAsync(this.mockExperimentContext, this.mockExperimentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();

            this.provider.ArmResourceManager.Verify(s => s.DeleteResourceGroupAsync(
                It.IsAny<VmResourceGroupDefinition>(),
                It.IsAny<CancellationToken>(),
                true),  // The forceDelete SHOULD happen
                Times.Once,
                "Provider should resubmit delete if previous attempt times out.");
        }

        private static HttpResponseMessage CreateResponseMessage(
        HttpStatusCode expectedStatusCode,
        object expectedContent = null)
        {
            HttpResponseMessage mockResponse = new HttpResponseMessage(expectedStatusCode);
            if (expectedContent != null)
            {
                mockResponse.Content = new StringContent(expectedContent.ToJson());
            }

            return mockResponse;
        }

        private void ValidateDeploymentResults(CleanupState state, ExecutionStatus expectedStatus, CancellationToken? token = null)
        {
            this.mockVmResourceGroup.DeletionState = state;
            token = token ?? new CancellationToken(false);

            this.mockDataClient
                .Setup(c => c.GetOrCreateStateAsync<VmResourceGroupDefinition>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .ReturnsAsync(this.mockVmResourceGroup);

            this.providerServices.AddSingleton(this.mockDataClient.Object);

            var result = this.provider.ExecuteAsync(this.mockExperimentContext, this.mockExperimentComponent, token.Value)
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.AreEqual(expectedStatus, result.Status);
        }

        private class TestArmVmCleanupProvider : ArmVmCleanupProvider
        {
            public TestArmVmCleanupProvider(IServiceCollection services)
            : base(services)
            {
            }

            public Mock<IArmResourceManager> ArmResourceManager { get; set; } = new Mock<IArmResourceManager>();

            protected override IArmResourceManager CreateArmResourceManager(ExperimentContext context)
            {
                return this.ArmResourceManager.Object;
            }
        }
    }
}
