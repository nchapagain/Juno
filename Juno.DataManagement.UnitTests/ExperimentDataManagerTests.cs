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
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Contracts.OData;
    using Microsoft.Azure.CRC.Repository;
    using Microsoft.Azure.CRC.Repository.Cosmos;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ExperimentDataManagerTests
    {
        private Fixture mockFixture;
        private ExperimentDataManager dataManager;
        private Experiment mockExperiment;
        private ExperimentInstance mockExperimentInstance;
        private ExperimentMetadata mockExperimentContext;
        private ExperimentMetadataInstance mockExperimentContextInstance;
        private IEnumerable<ExperimentStepInstance> mockExperimentSteps;
        private IEnumerable<ExperimentAgentTableEntity> mockExperimentAgentEntities;
        private Mock<IDocumentStore<CosmosAddress>> mockDocumentStore;
        private Mock<ITableStore<CosmosTableAddress>> mockTableStore;
        private Mock<IExperimentStepFactory> mockStepFactory;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks(ExperimentType.AB);
            this.mockExperiment = this.mockFixture.Create<Experiment>();
            this.mockExperimentInstance = new ExperimentInstance(Guid.NewGuid().ToString(), this.mockExperiment);
            this.mockExperimentContext = this.mockFixture.Create<ExperimentMetadata>();
            this.mockExperimentContextInstance = new ExperimentMetadataInstance(Guid.NewGuid().ToString(), this.mockExperimentContext);
            this.mockExperimentSteps = new List<ExperimentStepInstance>
            {
                this.mockFixture.Create<ExperimentStepInstance>(),
                this.mockFixture.Create<ExperimentStepInstance>()
            };

            // Agents are mapped to individual experiments so that the experiment agent steps
            // table can be partitioned by experiment ID while allowing Host and Guest agents to
            // be able to query for work/steps without having to know the experiment ID.
            this.mockExperimentAgentEntities = new List<ExperimentAgentTableEntity>
            {
                new ExperimentAgentTableEntity
                {
                    Id = this.mockExperimentInstance.Id,
                    AgentId = "AnyAgent",
                    ExperimentId = this.mockExperimentInstance.Id,
                    Created = DateTime.UtcNow.AddSeconds(-10),
                    PartitionKey = "AnyAgent",
                    RowKey = this.mockExperimentInstance.Id,
                    Timestamp = DateTime.UtcNow.AddSeconds(-10),
                    ETag = DateTime.UtcNow.ToString("o")
                }
            };

            // The data manager uses various data store/repository instances to manage
            // the actual CRUD operations with experiment data including Cosmos DB,
            // Cosmos Table and Azure Queue.
            this.mockDocumentStore = new Mock<IDocumentStore<CosmosAddress>>();
            this.mockTableStore = new Mock<ITableStore<CosmosTableAddress>>();
            this.mockStepFactory = new Mock<IExperimentStepFactory>();
            this.dataManager = new ExperimentDataManager(this.mockDocumentStore.Object, this.mockTableStore.Object, this.mockStepFactory.Object, NullLogger.Instance);
        }

        [Test]
        public void ExperimentDataManagerInlinesExperiments()
        {
            Experiment originalExperiment = this.mockExperiment;
            Experiment inlinedExperiment = this.mockExperiment.Inlined();

            this.mockDocumentStore.Setup(store => store.SaveDocumentAsync(
                It.IsAny<CosmosAddress>(),
                It.IsAny<ExperimentInstance>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
                .Callback<CosmosAddress, ExperimentInstance, CancellationToken, bool>((actualAddress, data, token, replace) =>
                {
                    Assert.AreNotEqual(originalExperiment, data.Definition);
                    Assert.AreEqual(inlinedExperiment, data.Definition);
                })
                .Returns(Task.FromResult(this.mockExperimentInstance));

            this.dataManager.CreateExperimentAsync(this.mockExperiment, CancellationToken.None)
                .GetAwaiter().GetResult();
        }

        [Test]
        public void ExperimentDataManagerCreatesTheExpectedExperiment()
        {
            Experiment expectedExperiment = this.mockExperiment.Inlined();

            this.mockDocumentStore.Setup(store => store.SaveDocumentAsync(
                It.IsAny<CosmosAddress>(),
                It.IsAny<ExperimentInstance>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
                .Callback<CosmosAddress, ExperimentInstance, CancellationToken, bool>((actualAddress, data, token, replace) =>
                {
                    Assert.IsTrue(expectedExperiment.Equals(data.Definition));
                })
                .Returns(Task.FromResult(this.mockExperimentInstance));

            this.dataManager.CreateExperimentAsync(this.mockExperiment, CancellationToken.None)
                .GetAwaiter().GetResult();
        }

        [Test]
        public void ExperimentDataManagerCreatesExperimentInstancesInTheExpectedDataStoreLocation()
        {
            this.mockDocumentStore.Setup(store => store.SaveDocumentAsync(
                It.IsAny<CosmosAddress>(),
                It.IsAny<ExperimentInstance>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
                .Callback<CosmosAddress, ExperimentInstance, CancellationToken, bool>((actualAddress, data, token, replace) =>
                {
                    CosmosAddress expectedAddress = ExperimentAddressFactory.CreateExperimentAddress(data.Id);
                    Assert.AreEqual(expectedAddress, actualAddress);
                })
                .Returns(Task.FromResult(this.mockExperimentInstance));

            this.dataManager.CreateExperimentAsync(this.mockExperiment, CancellationToken.None)
                .GetAwaiter().GetResult();
        }

        [Test]
        public void ExperimentDataManagerCreatesTheExpectedExperimentContext()
        {
            this.mockDocumentStore.Setup(store => store.SaveDocumentAsync(
                It.IsAny<CosmosAddress>(),
                It.IsAny<ExperimentMetadataInstance>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
                .Callback<CosmosAddress, ExperimentMetadataInstance, CancellationToken, bool>((actualAddress, data, token, replace) =>
                {
                    Assert.AreEqual(this.mockExperimentContext, data.Definition);
                })
                .Returns(Task.FromResult(this.mockExperimentContextInstance));

            this.dataManager.CreateExperimentContextAsync(this.mockExperimentContext, CancellationToken.None)
                .GetAwaiter().GetResult();
        }

        [Test]
        public void ExperimentDataManagerCreatesTheExpectedExperimentStepContext()
        {
            this.mockDocumentStore.Setup(store => store.SaveDocumentAsync(
                It.IsAny<CosmosAddress>(),
                It.IsAny<ExperimentMetadataInstance>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
                .Callback<CosmosAddress, ExperimentMetadataInstance, CancellationToken, bool>((actualAddress, data, token, replace) =>
                {
                    Assert.AreEqual(this.mockExperimentContext, data.Definition);
                    CosmosAddress expectedAddress = ExperimentAddressFactory.CreateExperimentContextAddress(this.mockExperimentContext.ExperimentId, this.mockExperimentSteps.First().Id);
                    Assert.IsTrue(expectedAddress.Equals(actualAddress));
                })
                .Returns(Task.FromResult(this.mockExperimentContextInstance));

            this.dataManager.CreateExperimentContextAsync(this.mockExperimentContext, CancellationToken.None, this.mockExperimentSteps.First().Id)
                .GetAwaiter().GetResult();
        }

        [Test]
        public void ExperimentDataManagerCreatesExperimentContextInstancesInTheExpectedDataStoreLocation()
        {
            this.mockDocumentStore.Setup(store => store.SaveDocumentAsync(
                It.IsAny<CosmosAddress>(),
                It.IsAny<ExperimentMetadataInstance>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
                .Callback<CosmosAddress, ExperimentMetadataInstance, CancellationToken, bool>((actualAddress, data, token, replace) =>
                {
                    CosmosAddress expectedAddress = ExperimentAddressFactory.CreateExperimentContextAddress(this.mockExperimentContext.ExperimentId);
                    Assert.IsTrue(expectedAddress.Equals(actualAddress));
                })
                .Returns(Task.FromResult(this.mockExperimentContextInstance));

            this.dataManager.CreateExperimentContextAsync(this.mockExperimentContext, CancellationToken.None)
                .GetAwaiter().GetResult();
        }

        [Test]
        public void ExperimentDataManagerCreatesTheExpectedExperimentAgentSteps()
        {
            List<ExperimentStepTableEntity> actualEntities = new List<ExperimentStepTableEntity>();

            string expectedAgentId = this.mockFixture.Create<AgentIdentification>().ToString();
            ExperimentStepInstance expectedParentStep = this.mockExperimentSteps.First();
            ExperimentComponent agentStep = this.mockExperiment.Workflow.First();

            // Behavior Setup:
            // The step factory used to create experiment steps should return a set of steps based upon the information
            // in the agent step/component definition.
            this.mockStepFactory
                .Setup(factory => factory.CreateAgentSteps(It.IsAny<ExperimentComponent>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                .Returns(new List<ExperimentStepInstance> { this.mockFixture.CreateExperimentStep(agentStep, expectedAgentId, expectedParentStep.Id) });

            // Behavior Setup:
            // The data manager must register/map the agent to the experiment. It will check to see if the agent/experiment mapping
            // already exists.
            this.mockTableStore.OnGetExperimentAgent()
                .Returns(Task.FromResult(this.mockExperimentAgentEntities.First()));

            // Behavior Setup:
            // The sequence of the new steps must follow the sequence of any previous steps so that the order of
            // execution is preserved/correct.
            this.mockTableStore.OnGetExperimentSteps()
                .Returns(Task.FromResult(null as IEnumerable<ExperimentStepTableEntity>));

            // Behavior Setup:
            // The steps will be saved to the Cosmos Table.
            this.mockTableStore.OnSaveExperimentStep()
                .Callback<CosmosTableAddress, ExperimentStepTableEntity, CancellationToken, bool>((actualAddress, entity, token, replace) =>
                {
                    actualEntities.Add(entity);
                    entity.ETag = Guid.NewGuid().ToString(); // the data manager will attempt to set the eTag on the step after saving it.
                });

            var stepsCreated = this.dataManager.CreateAgentStepsAsync(expectedParentStep, agentStep, expectedAgentId, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsTrue(stepsCreated.Count() == 1);

            // Note:
            // The mock steps are setup in the [SetUp] method above as part of mocking out the behavior
            // of the experiment step builder used by the data manager.
            Assert.IsTrue(stepsCreated.Count() == actualEntities.Count);

            for (int i = 0; i < stepsCreated.Count(); i++)
            {
                ExperimentStepInstance expectedStep = stepsCreated.ElementAt(i);
                ExperimentStepTableEntity actualStepEntity = actualEntities[i];

                Assert.AreEqual(expectedAgentId, actualStepEntity.AgentId);
                Assert.AreEqual(expectedParentStep.Id, actualStepEntity.ParentStepId);
                Assert.AreEqual(expectedStep.Attempts, actualStepEntity.Attempts);
                Assert.AreEqual(expectedStep.ExperimentId, actualStepEntity.ExperimentId);
                Assert.AreEqual(expectedStep.ExperimentGroup, actualStepEntity.ExperimentGroup);
                Assert.AreEqual(expectedStep.Definition.ToJson(), actualStepEntity.Definition);
                Assert.AreEqual(expectedStep.Sequence, actualStepEntity.Sequence);
                Assert.AreEqual(expectedStep.Status.ToString(), actualStepEntity.Status);
                Assert.AreEqual(expectedStep.StepType.ToString(), actualStepEntity.StepType);
            }
        }

        [Test]
        public void ExperimentDataManagerCreatesTheExpectedExperimentSteps()
        {
            List<ExperimentStepTableEntity> actualEntities = new List<ExperimentStepTableEntity>();

            this.mockStepFactory
                .Setup(factory => factory.CreateOrchestrationSteps(It.IsAny<IEnumerable<ExperimentComponent>>(), It.IsAny<string>(), null, false))
                .Returns(this.mockExperimentSteps);

            this.mockTableStore.Setup(store => store.SaveEntityAsync(
                It.IsAny<CosmosTableAddress>(),
                It.IsAny<ExperimentStepTableEntity>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
                .Callback<CosmosTableAddress, ExperimentStepTableEntity, CancellationToken, bool>((actualAddress, entity, token, replace) =>
                {
                    actualEntities.Add(entity);
                    entity.ETag = Guid.NewGuid().ToString(); // the data manager will attempt to set the eTag on the step after saving it.
                });

            var stepsCreated = this.dataManager.CreateExperimentStepsAsync(this.mockExperimentInstance, CancellationToken.None)
                .GetAwaiter().GetResult();

            // Note:
            // The mock steps are setup in the [SetUp] method above as part of mocking out the behavior
            // of the experiment step builder used by the data manager.
            Assert.IsTrue(stepsCreated.Count() == actualEntities.Count);

            for (int i = 0; i < stepsCreated.Count(); i++)
            {
                ExperimentStepInstance expectedStep = stepsCreated.ElementAt(i);
                ExperimentStepTableEntity actualStepEntity = actualEntities[i];

                Assert.AreEqual(expectedStep.Attempts, actualStepEntity.Attempts);
                Assert.AreEqual(expectedStep.ExperimentId, actualStepEntity.ExperimentId);
                Assert.AreEqual(expectedStep.ExperimentGroup, actualStepEntity.ExperimentGroup);
                Assert.AreEqual(expectedStep.Definition.ToJson(), actualStepEntity.Definition);
                Assert.AreEqual(expectedStep.Sequence, actualStepEntity.Sequence);
                Assert.AreEqual(expectedStep.Status.ToString(), actualStepEntity.Status);
                Assert.AreEqual(expectedStep.StepType.ToString(), actualStepEntity.StepType);
            }
        }

        [Test]
        public void ExperimentDataManagerCreatesTheExpectedExperimentStepsWhenAutoTriageDiagnosticsIsEnabled()
        {
            // Enable auto-triage diagnostics on the experiment.
            this.mockExperimentInstance.Definition.Metadata.Add("enableDiagnostics", true);

            List<ExperimentStepTableEntity> actualEntities = new List<ExperimentStepTableEntity>();

            this.mockStepFactory
                .Setup(factory => factory.CreateOrchestrationSteps(It.IsAny<IEnumerable<ExperimentComponent>>(), It.IsAny<string>(), null, true))
                .Returns(this.mockExperimentSteps)
                .Verifiable();

            this.mockTableStore.Setup(store => store.SaveEntityAsync(
                It.IsAny<CosmosTableAddress>(),
                It.IsAny<ExperimentStepTableEntity>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
                .Callback<CosmosTableAddress, ExperimentStepTableEntity, CancellationToken, bool>((actualAddress, entity, token, replace) =>
                {
                    entity.ETag = Guid.NewGuid().ToString(); // the data manager will attempt to set the eTag on the step after saving it.
                });

            this.dataManager.CreateExperimentStepsAsync(this.mockExperimentInstance, CancellationToken.None)
                .GetAwaiter().GetResult();

            this.mockStepFactory.VerifyAll();
        }

        [Test]
        public void ExperimentDataManagerSetsTheETagOnStepsNewlyCreated()
        {
            Guid expectedETag = Guid.NewGuid();

            this.mockStepFactory
                .Setup(factory => factory.CreateOrchestrationSteps(It.IsAny<ExperimentComponent>(), It.IsAny<string>(), 100))
                .Returns(this.mockExperimentSteps.Take(1));

            this.mockTableStore.Setup(store => store.SaveEntityAsync(
                It.IsAny<CosmosTableAddress>(),
                It.IsAny<ExperimentStepTableEntity>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
                .Callback<CosmosTableAddress, ExperimentStepTableEntity, CancellationToken, bool>((actualAddress, entity, token, replace) =>
                {
                    entity.ETag = expectedETag.ToString(); // the data manager will attempt to set the eTag on the step after saving it.
                });

            var stepsCreated = this.dataManager.CreateExperimentStepsAsync(this.mockExperimentInstance, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsFalse(stepsCreated.Any(step => step.GetETag() != expectedETag.ToString()));
        }

        [Test]
        public void ExperimentDataManagerDeletesTheExpectedAgentSteps()
        {
            this.mockTableStore.Setup(store => store.GetEntitiesAsync<ExperimentStepTableEntity>(It.IsAny<CosmosTableAddress>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(this.mockExperimentSteps.Select(step => step.ToTableEntity())));

            int currentStep = 0;
            this.mockTableStore.Setup(store => store.DeleteEntityAsync<ExperimentStepTableEntity>(
                It.IsAny<CosmosTableAddress>(),
                It.IsAny<CancellationToken>()))
                .Callback<CosmosTableAddress, CancellationToken>((actualAddress, token) =>
                {
                    string stepId = this.mockExperimentSteps.ElementAt(currentStep).Id;
                    CosmosTableAddress expectedAddress = ExperimentAddressFactory.CreateAgentStepAddress(this.mockExperimentInstance.Id, stepId);
                    Assert.IsTrue(expectedAddress.Equals(actualAddress));
                    currentStep++;
                })
                .Returns(Task.CompletedTask);

            this.dataManager.DeleteAgentStepsAsync(this.mockExperimentInstance.Id, CancellationToken.None)
                .GetAwaiter().GetResult();
        }

        [Test]
        public void ExperimentDataManagerDeletesTheExpectedExperiment()
        {
            this.mockDocumentStore.Setup(store => store.DeleteDocumentAsync(
                It.IsAny<CosmosAddress>(),
                It.IsAny<CancellationToken>()))
                .Callback<CosmosAddress, CancellationToken>((actualAddress, token) =>
                {
                    CosmosAddress expectedAddress = ExperimentAddressFactory.CreateExperimentAddress(this.mockExperimentInstance.Id);
                    Assert.IsTrue(expectedAddress.Equals(actualAddress));
                })
                .Returns(Task.CompletedTask);

            this.dataManager.DeleteExperimentAsync(this.mockExperimentInstance.Id, CancellationToken.None)
                .GetAwaiter().GetResult();
        }

        [Test]
        public void ExperimentDataManagerDeletesTheExpectedExperimentSharedContext()
        {
            this.mockDocumentStore.Setup(store => store.DeleteDocumentAsync(
                It.IsAny<CosmosAddress>(),
                It.IsAny<CancellationToken>()))
                .Callback<CosmosAddress, CancellationToken>((actualAddress, token) =>
                {
                    CosmosAddress expectedAddress = ExperimentAddressFactory.CreateExperimentContextAddress(this.mockExperimentInstance.Id);
                    Assert.IsTrue(expectedAddress.Equals(actualAddress));
                })
                .Returns(Task.CompletedTask);

            this.dataManager.DeleteExperimentContextAsync(this.mockExperimentInstance.Id, CancellationToken.None)
                .GetAwaiter().GetResult();
        }

        [Test]
        public void ExperimentDataManagerDeletesTheExpectedExperimentStepContext()
        {
            this.mockDocumentStore.Setup(store => store.DeleteDocumentAsync(
                It.IsAny<CosmosAddress>(),
                It.IsAny<CancellationToken>()))
                .Callback<CosmosAddress, CancellationToken>((actualAddress, token) =>
                {
                    CosmosAddress expectedAddress = ExperimentAddressFactory.CreateExperimentContextAddress(this.mockExperimentInstance.Id, this.mockExperimentSteps.First().Id);
                    Assert.IsTrue(expectedAddress.Equals(actualAddress));
                })
                .Returns(Task.CompletedTask);

            this.dataManager.DeleteExperimentContextAsync(this.mockExperimentInstance.Id, CancellationToken.None, this.mockExperimentSteps.First().Id)
                .GetAwaiter().GetResult();
        }

        [Test]
        public void ExperimentDataManagerDeletesTheExpectedExperimentSteps()
        {
            this.mockTableStore.Setup(store => store.DeleteEntityAsync<ExperimentStepTableEntity>(
                It.IsAny<CosmosTableAddress>(),
                It.IsAny<CancellationToken>()))
                .Callback<CosmosTableAddress, CancellationToken>((actualAddress, token) =>
                {
                    CosmosTableAddress expectedAddress = ExperimentAddressFactory.CreateExperimentStepAddress(this.mockExperimentInstance.Id);
                    Assert.IsTrue(expectedAddress.Equals(actualAddress));
                })
                .Returns(Task.CompletedTask);

            this.dataManager.DeleteExperimentStepsAsync(this.mockExperimentInstance.Id, CancellationToken.None)
                .GetAwaiter().GetResult();
        }

        [Test]
        public void ExperimentDataManagerGetsTheExpectedExperiment()
        {
            this.mockDocumentStore.Setup(store => store.GetDocumentAsync<ExperimentInstance>(
                It.IsAny<CosmosAddress>(),
                It.IsAny<CancellationToken>()))
                .Callback<CosmosAddress, CancellationToken>((actualAddress, token) =>
                {
                    CosmosAddress expectedAddress = ExperimentAddressFactory.CreateExperimentAddress(this.mockExperimentInstance.Id);
                    Assert.IsTrue(expectedAddress.Equals(actualAddress));
                })
                .Returns(Task.FromResult(this.mockExperimentInstance));

            ExperimentInstance actualInstance = this.dataManager.GetExperimentAsync(this.mockExperimentInstance.Id, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.AreEqual(this.mockExperimentInstance, actualInstance);
        }

        [Test]
        public void ExperimentDataManagerGetsTheExpectedExperimentSharedContext()
        {
            this.mockDocumentStore.Setup(store => store.GetDocumentAsync<ExperimentMetadataInstance>(
                It.IsAny<CosmosAddress>(),
                It.IsAny<CancellationToken>()))
                .Callback<CosmosAddress, CancellationToken>((actualAddress, token) =>
                {
                    CosmosAddress expectedAddress = ExperimentAddressFactory.CreateExperimentContextAddress(this.mockExperimentInstance.Id);
                    Assert.IsTrue(expectedAddress.Equals(actualAddress));
                })
                .Returns(Task.FromResult(this.mockExperimentContextInstance));

            ExperimentMetadataInstance actualInstance = this.dataManager.GetExperimentContextAsync(this.mockExperimentInstance.Id, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.AreEqual(this.mockExperimentContextInstance, actualInstance);
        }

        [Test]
        public void ExperimentDataManagerGetsTheExpectedExperimentStepContext()
        {
            this.mockDocumentStore.Setup(store => store.GetDocumentAsync<ExperimentMetadataInstance>(
                It.IsAny<CosmosAddress>(),
                It.IsAny<CancellationToken>()))
                .Callback<CosmosAddress, CancellationToken>((actualAddress, token) =>
                {
                    CosmosAddress expectedAddress = ExperimentAddressFactory.CreateExperimentContextAddress(this.mockExperimentInstance.Id, this.mockExperimentSteps.First().Id);
                    Assert.IsTrue(expectedAddress.Equals(actualAddress));
                })
                .Returns(Task.FromResult(this.mockExperimentContextInstance));

            ExperimentMetadataInstance actualInstance = this.dataManager.GetExperimentContextAsync(this.mockExperimentInstance.Id, CancellationToken.None, this.mockExperimentSteps.First().Id)
                .GetAwaiter().GetResult();

            Assert.AreEqual(this.mockExperimentContextInstance, actualInstance);
        }

        [Test]
        public void ExperimentDataManagerGetsTheExpectedSingleExperimentStep()
        {
            string expectedStepId = Guid.NewGuid().ToString();

            this.mockTableStore.Setup(store => store.GetEntityAsync<ExperimentStepTableEntity>(
                It.IsAny<CosmosTableAddress>(),
                It.IsAny<CancellationToken>()))
                .Callback<CosmosTableAddress, CancellationToken>((actualAddress, token) =>
                {
                    CosmosTableAddress expectedAddress = ExperimentAddressFactory.CreateExperimentStepAddress(
                        this.mockExperimentInstance.Id,
                        stepId: expectedStepId);

                    Assert.IsTrue(expectedAddress.Equals(actualAddress));
                })
                .Returns(Task.FromResult(this.mockExperimentSteps.First().ToTableEntity()));

            this.dataManager.GetExperimentStepAsync(
                this.mockExperimentInstance.Id,
                expectedStepId,
                CancellationToken.None)
                .GetAwaiter().GetResult();
        }

        [Test]
        public void ExperimentDataManagerGetsTheExpectedExperimentForAGivenAgent()
        {
            string expectedAgentId = "Cluster01,Node01";

            // Mock Setup:
            // The data manager has to first get the ID of the experiment from the table
            // that maps the experiment to the agent.
            this.mockTableStore
                .Setup(store => store.GetEntitiesAsync<ExperimentAgentTableEntity>(
                    It.IsAny<CosmosTableAddress>(),
                    It.IsAny<CancellationToken>()))
                .Callback<CosmosTableAddress, CancellationToken>((actualAddress, token) =>
                {
                    Assert.IsTrue(actualAddress.PartitionKey == expectedAgentId.ToLowerInvariant());
                    Assert.IsNull(actualAddress.RowKey);
                })
                .Returns(Task.FromResult(this.mockExperimentAgentEntities));

            // Mock Setup:
            // Then the data manager has to get the experiment instance from the document store.
            this.mockDocumentStore
                .Setup(store => store.GetDocumentAsync<ExperimentInstance>(
                    It.IsAny<CosmosAddress>(),
                    It.IsAny<CancellationToken>()))
                .Callback<CosmosAddress, CancellationToken>((address, token) =>
                {
                    // For the case that there are more than one experiment for which an agent ID is associated,
                    // we take the one with the latest timestamp (e.g. the latest experiment).
                    ExperimentAgentTableEntity expectedExperimentAgent = this.mockExperimentAgentEntities
                        .OrderByDescending(entity => entity.Timestamp).First();

                    // Experiment document partitions are the first 4 characters of the full ID
                    Assert.IsTrue(address.PartitionKey == expectedExperimentAgent.ExperimentId.Substring(0, 4));
                    Assert.IsTrue(address.DocumentId == expectedExperimentAgent.ExperimentId);
                })
                .Returns(Task.FromResult(this.mockExperimentInstance));

            this.dataManager.GetAgentExperimentAsync(expectedAgentId, CancellationToken.None)
                .GetAwaiter().GetResult();
        }

        [Test]
        public void ExperimentDataManagerGetsTheExpectedAgentSteps()
        {
            // Mock Setup:
            // Setup the call to get experiments for which the agent is related. In practice this should
            // only ever be one if the ID of an agent is guaranteed unique.
            this.mockTableStore.Setup(store => store.GetEntitiesAsync<ExperimentAgentTableEntity>(It.IsAny<CosmosTableAddress>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(this.mockExperimentAgentEntities as IEnumerable<ExperimentAgentTableEntity>));

            // Mock Setup:
            // Setup the call to get agent steps for the experiment in which the agent is running.
            this.mockTableStore.Setup(store => store.GetEntitiesAsync<ExperimentStepTableEntity>(
               It.IsAny<CosmosTableAddress>(),
               It.IsAny<IQueryFilter>(),
               It.IsAny<CancellationToken>()))
               .Callback<CosmosTableAddress, IQueryFilter, CancellationToken>((actualAddress, filter, token) =>
               {
                   CosmosTableAddress expectedAddress = ExperimentAddressFactory.CreateAgentStepAddress(
                       this.mockExperimentAgentEntities.First().ExperimentId);

                   Assert.IsTrue(expectedAddress.Equals(actualAddress));
               })
               .Returns(Task.FromResult(this.mockExperimentSteps.Select(step => step.ToTableEntity())));

            IEnumerable<ExperimentStepInstance> actualSteps = this.dataManager.GetAgentStepsAsync(
                this.mockExperimentAgentEntities.First().AgentId, 
                CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsNotEmpty(actualSteps);

            foreach (var step in this.mockExperimentSteps)
            {
                ExperimentStepInstance actualStep = actualSteps.Where(s => s.Id == step.Id).First();
                Assert.AreEqual(actualStep.AgentId, step.AgentId);
                Assert.AreEqual(actualStep.Created, step.Created);
                Assert.AreEqual(actualStep.ExperimentId, step.ExperimentId);
                Assert.AreEqual(actualStep.Extensions, step.Extensions);
                Assert.AreEqual(actualStep.ExperimentGroup, step.ExperimentGroup);
            }
        }

        [Test]
        public void ExperimentDataManagerUsesTheExpectedQueryFilterForTheAgentId()
        {
            string agentId = this.mockExperimentAgentEntities.First().AgentId;

            // Mock Setup:
            // Setup the call to get experiments for which the agent is related. In practice this should
            // only ever be one if the ID of an agent is guaranteed unique.
            this.mockTableStore.Setup(store => store.GetEntitiesAsync<ExperimentAgentTableEntity>(It.IsAny<CosmosTableAddress>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(this.mockExperimentAgentEntities as IEnumerable<ExperimentAgentTableEntity>));

            // Mock Setup:
            // Setup the call to get agent steps for the experiment in which the agent is running.
            this.mockTableStore.Setup(store => store.GetEntitiesAsync<ExperimentStepTableEntity>(
               It.IsAny<CosmosTableAddress>(),
               It.IsAny<IQueryFilter>(),
               It.IsAny<CancellationToken>()))
               .Callback<CosmosTableAddress, IQueryFilter, CancellationToken>((actualAddress, filter, token) =>
               {
                   Assert.AreEqual($"(AgentId eq '{agentId}')", filter.CreateExpression());
               })
               .Returns(Task.FromResult(this.mockExperimentSteps.Select(step => step.ToTableEntity())));

            this.dataManager.GetAgentStepsAsync(agentId, CancellationToken.None)
                .GetAwaiter().GetResult();
        }

        [Test]
        public void ExperimentDataManagerUsesTheExpectedQueryFilterWhenAdditionalFilterCriteriaIsProvided1()
        {
            string agentId = this.mockExperimentAgentEntities.First().AgentId;

            // Mock Setup:
            // Setup the call to get experiments for which the agent is related. In practice this should
            // only ever be one if the ID of an agent is guaranteed unique.
            this.mockTableStore.Setup(store => store.GetEntitiesAsync<ExperimentAgentTableEntity>(It.IsAny<CosmosTableAddress>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(this.mockExperimentAgentEntities as IEnumerable<ExperimentAgentTableEntity>));

            // Mock Setup:
            // Setup the call to get agent steps for the experiment in which the agent is running.
            this.mockTableStore.Setup(store => store.GetEntitiesAsync<ExperimentStepTableEntity>(
               It.IsAny<CosmosTableAddress>(),
               It.IsAny<IQueryFilter>(),
               It.IsAny<CancellationToken>()))
               .Callback<CosmosTableAddress, IQueryFilter, CancellationToken>((actualAddress, filter, token) =>
               {
                   Assert.AreEqual($"((Status eq 'Failed')) and (AgentId eq '{agentId}')", filter.CreateExpression());
               })
               .Returns(Task.FromResult(this.mockExperimentSteps.Select(step => step.ToTableEntity())));

            QueryFilter additionalFilter = new QueryFilter()
                .Set("Status", ComparisonType.Equal, "Failed");

            this.dataManager.GetAgentStepsAsync(agentId, CancellationToken.None, additionalFilter)
                .GetAwaiter().GetResult();
        }

        [Test]
        public void ExperimentDataManagerUsesTheExpectedQueryFilterWhenAdditionalFilterCriteriaIsProvided2()
        {
            string agentId = this.mockExperimentAgentEntities.First().AgentId;

            // Mock Setup:
            // Setup the call to get experiments for which the agent is related. In practice this should
            // only ever be one if the ID of an agent is guaranteed unique.
            this.mockTableStore.Setup(store => store.GetEntitiesAsync<ExperimentAgentTableEntity>(It.IsAny<CosmosTableAddress>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(this.mockExperimentAgentEntities as IEnumerable<ExperimentAgentTableEntity>));

            // Mock Setup:
            // Setup the call to get agent steps for the experiment in which the agent is running.
            this.mockTableStore.Setup(store => store.GetEntitiesAsync<ExperimentStepTableEntity>(
               It.IsAny<CosmosTableAddress>(),
               It.IsAny<IQueryFilter>(),
               It.IsAny<CancellationToken>()))
               .Callback<CosmosTableAddress, IQueryFilter, CancellationToken>((actualAddress, filter, token) =>
               {
                   Assert.AreEqual($"((Status ne 'Failed') and (Status ne 'Cancelled')) and (AgentId eq '{agentId}')", filter.CreateExpression());
               })
               .Returns(Task.FromResult(this.mockExperimentSteps.Select(step => step.ToTableEntity())));

            QueryFilter additionalFilter = new QueryFilter()
                .And("Status", ComparisonType.NotEqual, "Failed")
                .And("Status", ComparisonType.NotEqual, "Cancelled");

            this.dataManager.GetAgentStepsAsync(agentId, CancellationToken.None, additionalFilter)
                .GetAwaiter().GetResult();
        }

        [Test]
        public void ExperimentDataManagerAddsTheNewETagToUpdatedAgentSteps()
        {
            ExperimentStepInstance agentStep = this.mockFixture.CreateExperimentStep(
                this.mockExperimentSteps.First().Definition, "Cluster,Node,VM", Guid.NewGuid().ToString());

            this.mockTableStore.Setup(store => store.SaveEntityAsync<ExperimentStepTableEntity>(
                It.IsAny<CosmosTableAddress>(),
                It.IsAny<ExperimentStepTableEntity>(),
                It.IsAny<CancellationToken>(),
                true))
                .Callback<CosmosTableAddress, ExperimentStepTableEntity, CancellationToken, bool>((address, entity, token, replace) =>
                {
                    // Mimic the Cosmos table behavior where the eTag will be set
                    // upon the entity being updated by the table store.
                    entity.ETag = "Expected ETag Value";
                })
                .Returns(Task.CompletedTask);

            ExperimentStepInstance updatedStep = this.dataManager.UpdateAgentStepAsync(
                agentStep,
                CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsNotNull(updatedStep.GetETag());
            Assert.AreEqual("Expected ETag Value", updatedStep.GetETag());
        }

        [Test]
        public void ExperimentDataManagerAddsTheNewETagToUpdatedExperimentSteps()
        {
            this.mockTableStore.Setup(store => store.SaveEntityAsync<ExperimentStepTableEntity>(
                It.IsAny<CosmosTableAddress>(),
                It.IsAny<ExperimentStepTableEntity>(),
                It.IsAny<CancellationToken>(),
                true))
                .Callback<CosmosTableAddress, ExperimentStepTableEntity, CancellationToken, bool>((address, entity, token, replace) =>
                {
                    // Mimic the Cosmos table behavior where the eTag will be set
                    // upon the entity being updated by the table store.
                    entity.ETag = "Expected ETag Value";
                })
                .Returns(Task.CompletedTask);

            ExperimentStepInstance experimentStep = this.dataManager.UpdateExperimentStepAsync(
                this.mockExperimentSteps.First(),
                CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsNotNull(experimentStep.GetETag());
            Assert.AreEqual("Expected ETag Value", experimentStep.GetETag());
        }
    }
}
