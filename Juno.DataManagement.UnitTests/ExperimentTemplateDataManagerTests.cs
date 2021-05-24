namespace Juno.DataManagement
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Contracts;
    using Juno.DataManagement.Cosmos;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Contracts.OData;
    using Microsoft.Azure.CRC.Repository;
    using Microsoft.Azure.CRC.Repository.Cosmos;
    using Microsoft.Azure.Documents;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Newtonsoft.Json;
    using NuGet.ContentModel;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ExperimentTemplateDataManagerTests
    {
        private Fixture mockFixture;
        private ExperimentTemplateDataManager dataManager;
        private Experiment mockExperiment;
        private ExperimentItem mockExperimentItem;
        private ExperimentInstance mockExperimentInstance;
        private Mock<IDocumentStore<CosmosAddress>> mockDocumentStore;
        private Mock<ITableStore<CosmosTableAddress>> mockTableStore;
        private Mock<IExperimentStepFactory> mockStepFactory;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks(ExperimentType.AB);
            this.mockExperiment = this.mockFixture.Create<Experiment>();
            this.mockExperimentItem = this.mockFixture.Create<ExperimentItem>();
            this.mockExperimentInstance = this.mockFixture.Create<ExperimentInstance>();
            
            // The data manager uses various data store/repository instances to manage
            // the actual CRUD operations with experiment data including Cosmos DB,
            // Cosmos Table and Azure Queue.
            this.mockDocumentStore = new Mock<IDocumentStore<CosmosAddress>>();
            this.mockTableStore = new Mock<ITableStore<CosmosTableAddress>>();
            this.mockStepFactory = new Mock<IExperimentStepFactory>();
            this.dataManager = new ExperimentTemplateDataManager(this.mockDocumentStore.Object, null, NullLogger.Instance);
        }

        [Test]
        public void ExperimentTemplateDataManagerCreatesTheExpectedExperimentTemplate()
        {
            ExperimentItem expectedExperimentItem = this.mockExperimentItem;
            string documentId = expectedExperimentItem.Id;

            this.mockDocumentStore.Setup(store => store.SaveDocumentAsync(
                It.IsAny<CosmosAddress>(),
                It.IsAny<ExperimentItem>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
                .Callback<CosmosAddress, Item<Experiment>, CancellationToken, bool>((actualAddress, data, token, replace) =>
                {
                    Assert.IsTrue(expectedExperimentItem.Definition.ContentVersion.Equals(data.Definition.ContentVersion));
                    Assert.IsTrue(expectedExperimentItem.Definition.Schema == data.Definition.Schema);
                    Assert.IsTrue(JsonConvert.SerializeObject(expectedExperimentItem.Definition.Metadata).Equals(JsonConvert.SerializeObject(data.Definition.Metadata)));
                    Assert.IsTrue(expectedExperimentItem.Definition.Name.Equals(data.Definition.Name));
                    Assert.IsTrue(JsonConvert.SerializeObject(expectedExperimentItem.Definition.Parameters).Equals(JsonConvert.SerializeObject(data.Definition.Parameters)));
                    Assert.IsTrue(expectedExperimentItem.Definition.Workflow.Count() == data.Definition.Workflow.Count());
                })
                .Returns(Task.FromResult(this.mockExperimentItem.Id));

            ExperimentItem experimentItem = this.dataManager.CreateExperimentTemplateAsync(this.mockExperimentItem, documentId, false, CancellationToken.None)
            .GetAwaiter().GetResult();
            Assert.IsTrue(documentId == experimentItem.Id);
        }

        [Test]
        public void ExperimentDataManagerGetsTheExpectedExperimentTemplate()
        {
            ExperimentItem expectedExperimentItem = this.mockExperimentItem;
            Experiment expectedExperiment = this.mockExperimentItem.Definition;
            string documentId = this.mockExperimentItem.Id;

            this.mockDocumentStore.Setup(store => store.GetDocumentAsync<ExperimentItem>(
                            It.IsAny<CosmosAddress>(),
                            It.IsAny<CancellationToken>()))
                            .Callback<CosmosAddress, CancellationToken>((actualAddress, token) =>
                            {
                                CosmosAddress expectedAddress = ExperimentTemplateAddressFactory.CreateExperimentTemplateAddress(this.mockExperimentItem.Definition.Metadata["teamName"].ToString(), documentId);
                                Assert.IsTrue(expectedAddress.Equals(actualAddress));
                             })
                            .Returns(Task.FromResult(this.mockExperimentItem));

            ExperimentItem actualExperimentItem = this.dataManager.GetExperimentTemplateAsync(documentId, this.mockExperimentItem.Definition.Metadata["teamName"].ToString(), CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.AreEqual(expectedExperimentItem, actualExperimentItem);
        }

        [Test]
        public void ExperimentDataManagerDeletesTheExpectedExperiment()
        {
            ExperimentItem expectedExperimentItem = this.mockExperimentItem;
            string documentId = expectedExperimentItem.Id;

            this.mockDocumentStore.Setup(store => store.DeleteDocumentAsync(
                            It.IsAny<CosmosAddress>(),
                            It.IsAny<CancellationToken>()))
                            .Callback<CosmosAddress, CancellationToken>((actualAddress, token) =>
                            {
                                CosmosAddress expectedAddress = ExperimentTemplateAddressFactory.CreateExperimentTemplateAddress(this.mockExperimentItem.Definition.Metadata["teamName"].ToString(), this.mockExperimentItem.Id);
                                Assert.IsTrue(expectedAddress.Equals(actualAddress));
                            })
                            .Returns(Task.CompletedTask);

            this.dataManager.DeleteExperimentTemplateAsync(documentId, this.mockExperimentItem.Definition.Metadata["teamName"].ToString(), CancellationToken.None)
                .GetAwaiter().GetResult();
        }

        [Test]
        public void ExperimentTemplateDataManagerGetsTheExpectedExperimentTemplates()
        {
            var cosmosResult = this.mockExperimentItem;
            string documentid = this.mockExperimentItem.Id;
            this.mockDocumentStore.Setup(store => store.GetDocumentsAsync<ExperimentItem>(
                It.IsAny<CosmosAddress>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<IQueryFilter>()))
                .Callback<CosmosAddress, CancellationToken, IQueryFilter>((actualAddress, token, queryFilter) =>
                {
                    CosmosAddress expectedAddress = ExperimentTemplateAddressFactory.CreateExperimentTemplateAddress(Uri.EscapeUriString(this.mockExperimentItem.Definition.Metadata["teamName"].ToString()), null);
                    Assert.IsTrue(expectedAddress.ContainerId.Equals(actualAddress.ContainerId));
                    Assert.IsTrue(expectedAddress.DatabaseId.Equals(actualAddress.DatabaseId));
                    Assert.IsTrue(expectedAddress.PartitionKey.Equals(Uri.EscapeUriString(actualAddress.PartitionKey)));
                    Assert.IsTrue(expectedAddress.PartitionKeyPath.Equals(actualAddress.PartitionKeyPath));
                })
                .Returns(Task.FromResult(new List<ExperimentItem>() { cosmosResult } as IEnumerable<ExperimentItem>));

            List<ExperimentTemplateInfo> expectedExperimentTemplates = new List<ExperimentTemplateInfo>();
            expectedExperimentTemplates.Add(new ExperimentTemplateInfo() { Description = this.mockExperimentItem.Definition.Description, Id = this.mockExperimentItem.Id, TeamName = Uri.EscapeUriString(this.mockExperimentItem.Definition.Metadata["teamName"].ToString()) });
            List<ExperimentItem> actualExperimentTemplates = this.dataManager.GetExperimentTemplatesListAsync(this.mockExperimentItem.Definition.Metadata["teamName"].ToString(), CancellationToken.None)
                    .GetAwaiter().GetResult().ToList();
            Assert.AreEqual(expectedExperimentTemplates[0].TeamName, Uri.EscapeUriString(actualExperimentTemplates[0].Definition.Metadata["teamName"].ToString()));
            Assert.AreEqual(expectedExperimentTemplates[0].Description, actualExperimentTemplates[0].Definition.Description);
            Assert.AreEqual(expectedExperimentTemplates[0].Id, actualExperimentTemplates[0].Id);
        }
    }
}
