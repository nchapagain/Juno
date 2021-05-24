namespace Juno.GarbageCollector
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.DataManagement;
    using Juno.Execution.TipIntegration;
    using Microsoft.Azure.CRC.Kusto;
    using Microsoft.Azure.CRC.Repository.KeyVault;
    using Microsoft.Azure.CRC.Subscriptions;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class GarbageCollectorExecutionTests
    {
        private IList<IGarbageCollector> garbageCollectors;
        private IServiceCollection services;
        private IConfiguration configuration;
        private ILogger logger;
        private IExperimentClient experimentClient;
        private IAzureKeyVault keyVault;
        private IExperimentTemplateDataManager experimentTemplateDataManager;
        private Mock<IKustoQueryIssuer> kustoIssuer;
        private Mock<ITipClient> tipClient;
        private IList<ISubscriptionManager> subscriptionManagers;
        private Mock<IGarbageCollector> resourceGroupGarbageCollector;

        [SetUp]
        public void SetupTest()
        {
            this.services = new ServiceCollection();
            this.logger = NullLogger.Instance;

            this.garbageCollectors = new List<IGarbageCollector>();
            this.configuration = new Mock<IConfiguration>().Object;
            this.kustoIssuer = new Mock<IKustoQueryIssuer>();
            this.keyVault = new Mock<IAzureKeyVault>().Object;
            this.experimentClient = new Mock<IExperimentClient>().Object;
            this.tipClient = new Mock<ITipClient>();
            this.experimentTemplateDataManager = new Mock<IExperimentTemplateDataManager>().Object;
            this.subscriptionManagers = new List<ISubscriptionManager>();

            this.resourceGroupGarbageCollector = new Mock<IGarbageCollector>();
            this.garbageCollectors.Add(this.resourceGroupGarbageCollector.Object);
            this.garbageCollectors.Add(this.resourceGroupGarbageCollector.Object);
            this.garbageCollectors.Add(this.resourceGroupGarbageCollector.Object);

            this.services.AddSingleton<IConfiguration>(this.configuration);
            this.services.AddSingleton<IKustoQueryIssuer>(this.kustoIssuer.Object);
            this.services.AddSingleton<ILogger>(this.logger);
            this.services.AddSingleton<IExperimentClient>(this.experimentClient);
            this.services.AddSingleton<IAzureKeyVault>(this.keyVault);
            this.services.AddSingleton<ITipClient>(this.tipClient.Object);
            this.services.AddSingleton<IExperimentTemplateDataManager>(this.experimentTemplateDataManager);
            this.services.AddSingleton<IList<ISubscriptionManager>>(this.subscriptionManagers);
            this.services.AddSingleton<IList<IGarbageCollector>>(this.garbageCollectors);
        }

        [Test]
        public void GarbageCollectorExecutesOnlyWhenRequiredServicesAreProvided()
        {
            var leakedResourceOutput = new Dictionary<string, LeakedResource>();
            leakedResourceOutput.Add("1", this.GetLeakedResource());

            var cleanResourceOutput = new Dictionary<string, string>();
            cleanResourceOutput.Add("1", "1");

            this.resourceGroupGarbageCollector.Setup(x => x.GetLeakedResourcesAsync(CancellationToken.None)).ReturnsAsync(leakedResourceOutput);
            this.resourceGroupGarbageCollector.Setup(x => x.CleanupLeakedResourcesAsync(leakedResourceOutput, CancellationToken.None)).ReturnsAsync(cleanResourceOutput);

            var gcExecution = new GarbageCollectorExecution(this.services);
            var response = gcExecution.RunAsync(CancellationToken.None).GetAwaiter();
            response.GetResult();
            Assert.IsTrue(response.IsCompleted);

            this.resourceGroupGarbageCollector.Verify(x => x.GetLeakedResourcesAsync(It.IsAny<CancellationToken>()),
                Times.Exactly(this.garbageCollectors.Count));
            this.resourceGroupGarbageCollector.Verify(x => x.CleanupLeakedResourcesAsync(leakedResourceOutput, It.IsAny<CancellationToken>()),
                Times.Exactly(this.garbageCollectors.Count));
        }

        [Test]
        public void GarbageCollectorCleansResourcesOnlyWhenThereAreLeakedResources()
        {
            var leakedResourceOutput = new Dictionary<string, LeakedResource>();
            this.resourceGroupGarbageCollector.Setup(x => x.GetLeakedResourcesAsync(CancellationToken.None)).ReturnsAsync(leakedResourceOutput);

            var cleanResourceOutput = new Dictionary<string, string>();
            cleanResourceOutput.Add(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());

            var gcExecution = new GarbageCollectorExecution(this.services);
            var response = gcExecution.RunAsync(CancellationToken.None).GetAwaiter();
            Assert.IsTrue(response.IsCompleted);

            this.resourceGroupGarbageCollector.Verify(x => x.GetLeakedResourcesAsync(It.IsAny<CancellationToken>()),
                Times.Exactly(this.garbageCollectors.Count));
            this.resourceGroupGarbageCollector.Verify(x => x.CleanupLeakedResourcesAsync(leakedResourceOutput, It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Test]
        public void GarbageCollectorValidatesServices()
        {
            GarbageCollectorExecution gcExecution;
            Assert.Throws<ArgumentException>(() => gcExecution = new GarbageCollectorExecution(null));
        }

        [Test]
        public void GarbageCollectorValidatesRequiredParameters()
        {
            GarbageCollectorExecution gcExecution;
            Assert.Throws<Exception>(() => gcExecution = new GarbageCollectorExecution(new ServiceCollection()));
        }

        [Test]
        public void GarbageCollectorFailsWhenOneParameterIsMissing()
        {
            GarbageCollectorExecution gcExecution;
            this.services.RemoveAt(2);
            Assert.Throws<Exception>(() => gcExecution = new GarbageCollectorExecution(this.services));
        }

        private LeakedResource GetLeakedResource()
        {
            return new LeakedResource(
                DateTime.UtcNow.AddDays(-5d),
                Guid.NewGuid().ToString(),
                "ResourceType",
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString(),
                5,
                Guid.NewGuid().ToString(),
                "Mock experimentName",
                ImpactType.None,
                "MockCluster",
                "MockSubscriptionId",
                LeakedResourceSource.AzureResourceGroupManagement);
        }
    }
}