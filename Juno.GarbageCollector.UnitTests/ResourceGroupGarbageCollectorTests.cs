namespace Juno.GarbageCollector
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using AutoFixture;
    using Castle.Core.Internal;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.DataManagement;
    using Juno.Execution.TipIntegration;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Identity;
    using Microsoft.Azure.CRC.Kusto;
    using Microsoft.Azure.CRC.Subscriptions;
    using Microsoft.Azure.Management.Fluent;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Newtonsoft.Json;
    using NUnit.Framework;
    using TipGateway.Entities;

    [TestFixture]
    [Category("Unit")]
    public class ResourceGroupGarbageCollectorTests
    {
        private ILogger logger;
        private IServiceCollection services;
        private IList<LeakedResource> leakedResource;
        private Mock<IExperimentTemplateDataManager> experimentTemplateDataManager;
        private Mock<IExperimentClient> experimentClientManager;
        private ExperimentItem garbageCollectorExperimentItem;
        private Fixture mockFixtureGC;
        private ResourceGroupGarbageCollector resourceGroupGarbageCollector;
        private Mock<ISubscriptionManager> subscriptionManager;
       
        [SetUp]
        public void SetupTest()
        {
            this.services = new ServiceCollection();
            this.logger = NullLogger.Instance;
            this.leakedResource = new List<LeakedResource>();
            this.leakedResource.Add(TipGarbageCollectorTests.CreateMockNonImpactfulExpiredLeakedResource());
            this.experimentTemplateDataManager = new Mock<IExperimentTemplateDataManager>();
            this.mockFixtureGC = new Fixture();
            this.experimentClientManager = new Mock<IExperimentClient>();
            this.subscriptionManager = new Mock<ISubscriptionManager>();

            this.services.AddSingleton<ILogger>(this.logger);
            this.services.AddSingleton<IExperimentTemplateDataManager>(this.experimentTemplateDataManager.Object);
            this.services.AddSingleton<IExperimentClient>(this.experimentClientManager.Object);
            this.services.AddSingleton<ISubscriptionManager>(this.subscriptionManager.Object);

            this.mockFixtureGC.SetupExperimentMocks();
            this.garbageCollectorExperimentItem = this.mockFixtureGC.Create<ExperimentItem>();

            this.resourceGroupGarbageCollector = new ResourceGroupGarbageCollector(this.services);
        }

        [Test]
        public void ProviderValidatesRequiredParameters()
        {
            IGarbageCollector resourceGroupGarbageCollector;
            Assert.Throws<ArgumentNullException>(() => resourceGroupGarbageCollector = new ResourceGroupGarbageCollector(null));
        }

        [Test]
        public void LeakedResourcesIsSerializable()
        {
            SerializationAssert.IsJsonSerializable(this.leakedResource);
        }

        [Test]
        public void ResourceGroupGarbageCollectorReturnsExpectedResponseWhenRequiredServicesAreProvided()
        {
            IList<AzureResourceGroup> resourceList = new List<AzureResourceGroup>()
            {
                this.CreateMockAzureResourceGroup(-5),
                this.CreateMockAzureResourceGroup(-10),
                this.CreateMockAzureResourceGroup(-15)
            };

            this.subscriptionManager.Setup(x => x.GetAllResourceGroupsAsync(CancellationToken.None)).ReturnsAsync(resourceList);
            var response = this.resourceGroupGarbageCollector.GetLeakedResourcesAsync(CancellationToken.None).Result;

            Assert.AreEqual(response.Count, resourceList.Count);
        }

        [Test]
        public void ResourceGroupGarbageCollectorMarksReturnsAllExpiredResourcesAsNonImpactful()
        {
            IList<AzureResourceGroup> resourceList = new List<AzureResourceGroup>()
            {
                this.CreateMockAzureResourceGroup(-5),
                this.CreateMockAzureResourceGroup(-10),
                this.CreateMockAzureResourceGroup(-15)
            };

            this.subscriptionManager.Setup(x => x.GetAllResourceGroupsAsync(CancellationToken.None)).ReturnsAsync(resourceList);
            var response = this.resourceGroupGarbageCollector.GetLeakedResourcesAsync(CancellationToken.None).Result;

            foreach (var item in response)
            {
                Assert.AreEqual(item.Value.ImpactType, ImpactType.None);
            }
        }

        [Test]
        public void ResourceGroupGarbageCollectorReturnsOnlyLeakedResources()
        {
            IList<AzureResourceGroup> resourceList = new List<AzureResourceGroup>()
            {
                this.CreateMockAzureResourceGroup(-5),
                this.CreateMockAzureResourceGroup(-10),
                this.CreateMockAzureResourceGroup(-15),
                this.CreateMockAzureResourceGroup(-1), // not leaked 
                this.CreateMockAzureResourceGroup(-2),
                this.CreateMockAzureResourceGroup(+5), // not leaked 
                this.CreateMockAzureResourceGroup(+3), // not leaked
                this.CreateMockAzureResourceGroup(-4),
            };

            this.subscriptionManager.Setup(x => x.GetAllResourceGroupsAsync(CancellationToken.None)).ReturnsAsync(resourceList);

            var response = this.resourceGroupGarbageCollector.GetLeakedResourcesAsync(CancellationToken.None).Result;
            Assert.AreEqual(response.Count, resourceList.Count - 3);
        }

        [Test]
        public void ResourceGroupGarbageCollectorReturnsExpectedResponseAzureResourceGroupsAreEmpty()
        {
            IList<AzureResourceGroup> resourceList = new List<AzureResourceGroup>();

            this.subscriptionManager.Setup(x => x.GetAllResourceGroupsAsync(CancellationToken.None)).ReturnsAsync(resourceList);

            var response = this.resourceGroupGarbageCollector.GetLeakedResourcesAsync(CancellationToken.None).Result;
            Assert.IsFalse(response.Any());
        }

        [Test]
        public void ResourceGroupGarbageCollectorCleansOnlyExpiredResources()
        {
            Dictionary<string, LeakedResource> leakedResource = new Dictionary<string, LeakedResource>();
            leakedResource.Add("1", TipGarbageCollectorTests.CreateMockImpactfulUnExpiredLeakedResource());
            leakedResource.Add("2", TipGarbageCollectorTests.CreateMockImpactfulUnExpiredLeakedResource());
            leakedResource.Add("3", TipGarbageCollectorTests.CreateMockImpactfulUnExpiredLeakedResource());

            var output = this.resourceGroupGarbageCollector.CleanupLeakedResourcesAsync(leakedResource, CancellationToken.None).Result;

            Assert.IsTrue(output.IsNullOrEmpty());

            this.experimentClientManager.Verify(x => x.CreateExperimentFromTemplateAsync(It.IsAny<ExperimentTemplate>(), CancellationToken.None, It.IsAny<string>()), Times.Never);

            this.experimentTemplateDataManager.Verify(x => x.GetExperimentTemplateAsync(
                It.IsAny<string>(), It.IsAny<string>(), CancellationToken.None, It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void ResourceGroupGarbageCollectorCleansExpectedResponseWhenLeakedResourcesAreProvided()
        {
            Dictionary<string, LeakedResource> leakedResource = new Dictionary<string, LeakedResource>();
            leakedResource.Add("1", TipGarbageCollectorTests.CreateMockNonImpactfulExpiredLeakedResource());
            leakedResource.Add("2", TipGarbageCollectorTests.CreateMockNonImpactfulExpiredLeakedResource());
            leakedResource.Add("3", TipGarbageCollectorTests.CreateMockNonImpactfulExpiredLeakedResource());

            this.experimentTemplateDataManager.Setup(x => x.GetExperimentTemplateAsync(It.IsAny<string>(), It.IsAny<string>(), CancellationToken.None, It.IsAny<string>()))
                .ReturnsAsync(this.garbageCollectorExperimentItem);

            this.experimentClientManager.Setup(x => x.CreateExperimentFromTemplateAsync(It.IsAny<ExperimentTemplate>(), CancellationToken.None, It.IsAny<string>()))
                .ReturnsAsync(this.CreateMockHttpResponse(HttpStatusCode.OK));

            var output = this.resourceGroupGarbageCollector.CleanupLeakedResourcesAsync(leakedResource, CancellationToken.None).Result;
            foreach (var cleanedResource in output)
            {
                Assert.IsTrue(leakedResource.ContainsKey(cleanedResource.Key));
            }

            this.experimentClientManager.Verify(x => x.CreateExperimentFromTemplateAsync(It.IsAny<ExperimentTemplate>(), CancellationToken.None, It.IsAny<string>()), Times.Exactly(3));

            this.experimentTemplateDataManager.Verify(x => x.GetExperimentTemplateAsync(
                It.IsAny<string>(), It.IsAny<string>(), CancellationToken.None, It.IsAny<string>()), Times.Once);
        }

        [Test]
        public void ResourceGroupGarbageCollectorValidatesWhenEmptyLeakedResourcesAreProvided()
        {
            Dictionary<string, LeakedResource> leakedResource = new Dictionary<string, LeakedResource>();
            IDictionary<string, string> response;

            var abc = new ResourceGroupGarbageCollector(this.services);
            Assert.Throws<ArgumentException>(() => response = abc.CleanupLeakedResourcesAsync(leakedResource, CancellationToken.None)
             .GetAwaiter().GetResult());
        }

        [Test]
        public void ResourceGroupGarbageCollectorWillAttemptToCleanOtherResourcesEvenAfterFailingtoDeleteFirstResource()
        {
            Dictionary<string, LeakedResource> leakedResource = new Dictionary<string, LeakedResource>();
            leakedResource.Add("1", TipGarbageCollectorTests.CreateMockNonImpactfulExpiredLeakedResource());
            leakedResource.Add("2", TipGarbageCollectorTests.CreateMockNonImpactfulExpiredLeakedResource());
            leakedResource.Add("3", TipGarbageCollectorTests.CreateMockNonImpactfulExpiredLeakedResource());

            this.experimentTemplateDataManager.Setup(x => x.GetExperimentTemplateAsync(It.IsAny<string>(), It.IsAny<string>(), CancellationToken.None, It.IsAny<string>()))
                .ReturnsAsync(this.garbageCollectorExperimentItem);

            this.experimentClientManager.Setup(x => x.CreateExperimentFromTemplateAsync(It.IsAny<ExperimentTemplate>(), CancellationToken.None, It.IsAny<string>()))
                .ReturnsAsync(this.CreateMockHttpResponse(HttpStatusCode.BadRequest));

            var output = this.resourceGroupGarbageCollector.CleanupLeakedResourcesAsync(leakedResource, CancellationToken.None).Result;
            foreach (var a in output)
            {
                Assert.IsTrue(leakedResource.ContainsKey(a.Key));
                Assert.IsEmpty(a.Value);
            }

            this.experimentClientManager.Verify(x => x.CreateExperimentFromTemplateAsync(It.IsAny<ExperimentTemplate>(), CancellationToken.None, It.IsAny<string>()), Times.Exactly(3));

            this.experimentTemplateDataManager.Verify(x => x.GetExperimentTemplateAsync(
                It.IsAny<string>(), It.IsAny<string>(), CancellationToken.None, It.IsAny<string>()), Times.Once);
        }

        private AzureResourceGroup CreateMockAzureResourceGroup(int daysOld)
        {
            AzureResourceGroup azureResource = new AzureResourceGroup(
                resourceName: "MockName" + Guid.NewGuid().ToString(),
                resourceId: "Mock ID" + Guid.NewGuid().ToString(),
                region: "Mock Region",
                provisioningState: "Mock State",
                createdDate: DateTime.UtcNow.AddDays(daysOld), // Uses createdDate to determine leaked vs non-leaked
                expirationDate: DateTime.UtcNow,
                resourceTags: new Dictionary<string, string>(),
                subscriptionId: Guid.NewGuid().ToString());

            return azureResource;
        }

        private HttpResponseMessage CreateMockHttpResponse(HttpStatusCode statusCode)
        {
            return new HttpResponseMessage { StatusCode = statusCode, Content = new StringContent(JsonConvert.SerializeObject(this.garbageCollectorExperimentItem)) };
        }
    }
}