namespace Juno.Execution.Providers.Certification
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Contracts;
    using Juno.Execution.AgentRuntime.Windows;
    using Juno.Execution.Providers.Payloads;
    using Juno.Providers;
    using Microsoft.Azure.CRC;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class CRCBladeCertificationProviderTests
    {
        private Mock<IProviderDataClient> mockDataClient;
        private ExperimentContext testExperimentContext;
        private ExperimentComponent testExperimentComponent;
        private Fixture mockFixture;
        private FixtureDependencies mockDependencies;
        private IServiceCollection providerServices;
        private Mock<ICertificationManager> certificationMgr;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks();
            this.mockDependencies = new FixtureDependencies();

            // A valid experiment component definition for the provider.
            this.testExperimentComponent = new ExperimentComponent(
                typeof(FPGAReconfigProvider).FullName,
                "Certify the node",
                "Intel Gen6 CPU",
                "Group B");

            this.certificationMgr = new Mock<ICertificationManager>();
            this.mockDataClient = new Mock<IProviderDataClient>();
            this.providerServices = new ServiceCollection()
                .AddSingleton<ICertificationManager>(this.certificationMgr.Object)
                .AddSingleton<IProviderDataClient>(this.mockDataClient.Object);

            this.testExperimentContext = new ExperimentContext(
              this.mockFixture.Create<ExperimentInstance>(),
              this.mockFixture.CreateExperimentStep(this.testExperimentComponent),
              this.mockDependencies.Configuration);
        }

        [Test]
        public void ProviderFailsWhenCertificationFails()
        {
            var certProvider = new CRCBladeCertificationProvider(this.providerServices);
            var error = string.Empty;
            this.certificationMgr.Setup(s => s.Certify(It.IsAny<IProcessExecution>(), out error)).Returns(false);
            certProvider.ConfigureServicesAsync(this.testExperimentContext, this.testExperimentComponent)
                .GetAwaiter().GetResult();

            ExecutionResult result = certProvider.ExecuteAsync(this.testExperimentContext, this.testExperimentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Error);
            Assert.IsTrue(result.Status == ExecutionStatus.Failed);
            this.certificationMgr.Verify(s => s.Certify(It.IsAny<IProcessExecution>(), out error), Times.Once);
        }

        [Test]
        public void ProviderSucceedsWhenCertificationSucceeds()
        {
            var certProvider = new CRCBladeCertificationProvider(this.providerServices);
            var error = string.Empty;
            this.certificationMgr.Setup(s => s.Certify(It.IsAny<IProcessExecution>(), out error)).Returns(true);
            certProvider.ConfigureServicesAsync(this.testExperimentContext, this.testExperimentComponent)
                .GetAwaiter().GetResult();

            ExecutionResult result = certProvider.ExecuteAsync(this.testExperimentContext, this.testExperimentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.Succeeded);
            this.certificationMgr.Verify(s => s.Certify(It.IsAny<IProcessExecution>(), out error), Times.Once);
        }
    }
}
