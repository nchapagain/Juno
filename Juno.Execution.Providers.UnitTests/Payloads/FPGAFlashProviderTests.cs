namespace Juno.Execution.Providers.Payloads
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Contracts;
    using Juno.Execution.AgentRuntime.Windows;
    using Juno.Providers;
    using Microsoft.Azure.CRC;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class FPGAFlashProviderTests
    {
        private Mock<IProviderDataClient> mockDataClient;
        private ExperimentContext testExperimentContext;
        private ExperimentComponent testExperimentComponent;
        private Fixture mockFixture;
        private FixtureDependencies mockDependencies;      
        private IServiceCollection providerServices;
        private Mock<IFPGAManager> fpgaManager;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks();
            this.mockDependencies = new FixtureDependencies();

            // A valid experiment component definition for the provider.
            this.testExperimentComponent = new ExperimentComponent(
                typeof(FPGAFlashProvider).FullName,
                "Flash the newer FPGA",
                "Intel Gen6 CPU",
                "Group B",
                parameters: new Dictionary<string, IConvertible>
                {
                    // The following parameters are required by the Microcodeactivationprovider.
                    ["imageFileName"] = "BSLImages.cab"
                });

            this.fpgaManager = new Mock<IFPGAManager>();
            this.mockDataClient = new Mock<IProviderDataClient>();
            this.providerServices = new ServiceCollection()
                .AddSingleton<IFPGAManager>(this.fpgaManager.Object)
                .AddSingleton<IProviderDataClient>(this.mockDataClient.Object);

            this.testExperimentContext = new ExperimentContext(
              this.mockFixture.Create<ExperimentInstance>(),
              this.mockFixture.CreateExperimentStep(this.testExperimentComponent),
              this.mockDependencies.Configuration);
        }

        [Test]
        public void ProviderFailsFlashWhenFPGAManagerFails()
        {
            var imageName = "BSLImages.cab";
            var flashProvider = new FPGAFlashProvider(this.providerServices);
            this.fpgaManager.Setup(s => s.FlashFPGA(It.IsAny<IProcessExecution>(), imageName)).Returns(new FPGAManagerResult()
            {
                ExecutionResult = "failed",
                Succeeded = false
            });
            flashProvider.ConfigureServicesAsync(this.testExperimentContext, this.testExperimentComponent)
                .GetAwaiter().GetResult();

            ExecutionResult result = flashProvider.ExecuteAsync(this.testExperimentContext, this.testExperimentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Error);
            Assert.IsTrue(result.Status == ExecutionStatus.Failed);
            this.fpgaManager.Verify(s => s.FlashFPGA(It.IsAny<IProcessExecution>(), imageName), Times.Once);
        }

        [Test]
        public void ProviderSucceedsFlashWhenFPGAManagerFails()
        {
            var imageName = "BSLImages.cab";
            var flashProvider = new FPGAFlashProvider(this.providerServices);
            this.fpgaManager.Setup(s => s.FlashFPGA(It.IsAny<IProcessExecution>(), imageName)).Returns(new FPGAManagerResult()
            {
                ExecutionResult = "done",
                Succeeded = true
            });
            flashProvider.ConfigureServicesAsync(this.testExperimentContext, this.testExperimentComponent)
                .GetAwaiter().GetResult();

            ExecutionResult result = flashProvider.ExecuteAsync(this.testExperimentContext, this.testExperimentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.Succeeded);
            this.fpgaManager.Verify(s => s.FlashFPGA(It.IsAny<IProcessExecution>(), imageName), Times.Once);
        }

        [Test]
        public void ProviderFailsWhenImageParameterMissing()
        {
            this.testExperimentComponent.Parameters.Clear();
            var imageName = "BSLImages.cab";
            var flashProvider = new FPGAFlashProvider(this.providerServices);
            this.fpgaManager.Setup(s => s.FlashFPGA(It.IsAny<IProcessExecution>(), imageName)).Returns(new FPGAManagerResult()
            {
                ExecutionResult = "done",
                Succeeded = true
            });
            flashProvider.ConfigureServicesAsync(this.testExperimentContext, this.testExperimentComponent)
                .GetAwaiter().GetResult();

            ExecutionResult result = flashProvider.ExecuteAsync(this.testExperimentContext, this.testExperimentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Error);
            Assert.IsTrue(result.Status == ExecutionStatus.Failed);
            this.fpgaManager.Verify(s => s.FlashFPGA(It.IsAny<IProcessExecution>(), imageName), Times.Never);
        }
    }
}
