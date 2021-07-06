namespace Juno.DataManagement
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Repository;
    using Microsoft.Azure.CRC.Repository.Cosmos;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class SchedulerDataManagerTests
    {
        private Fixture mockFixture;
        private Item<GoalBasedSchedule> mockExecutionGoal;
        private TargetGoalTrigger mockTargetGoal;
        private ScheduleDataManager dataManager;
        private Mock<IDocumentStore<CosmosAddress>> mockDocumentStore;

        [SetUp]
        public void SetUp()
        {
            this.mockFixture = new Fixture();

            this.mockFixture.SetUpGoalBasedScheduleMocks();
            this.mockFixture.SetupEnvironmentSelectionMocks();

            this.mockExecutionGoal = new Item<GoalBasedSchedule>(Guid.NewGuid().ToString(), this.mockFixture.Create<GoalBasedSchedule>());
            this.mockTargetGoal = this.mockFixture.Create<TargetGoalTrigger>();

            this.mockDocumentStore = new Mock<IDocumentStore<CosmosAddress>>();
            this.dataManager = new ScheduleDataManager(this.mockDocumentStore.Object, NullLogger.Instance);
        }

        [Test]
        public void ScheduleDataManagerCreateTheExpectedExecutionGoal()
        {
            string executionGoalId = this.mockExecutionGoal.Id;
            this.mockDocumentStore.Setup(store => store.SaveDocumentAsync(
                It.IsAny<CosmosAddress>(),
                It.IsAny<Item<GoalBasedSchedule>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
                .Callback<CosmosAddress, Item<GoalBasedSchedule>, CancellationToken, bool>((actualAddress, data, token, replace) =>
                {
                    Assert.AreEqual(this.mockExecutionGoal.Definition, data.Definition);
                    Assert.AreEqual(this.mockExecutionGoal.Id, data.Id);
                })
                .Returns(Task.FromResult(this.mockExecutionGoal));

            this.dataManager.CreateExecutionGoalAsync(this.mockExecutionGoal, CancellationToken.None)
                .GetAwaiter().GetResult();
        }

        [Test]
        public void ScheduleDataManagerRetrievesExpectedExecutionGoal()
        {
            this.mockDocumentStore.Setup(store => store.GetDocumentAsync<Item<GoalBasedSchedule>>(
                It.IsAny<CosmosAddress>(),
                It.IsAny<CancellationToken>()))
                .Callback<CosmosAddress, CancellationToken>((actualAddress, token) => 
                {
                    CosmosAddress expectedAddress = ScheduleAddressFactory.CreateExecutionGoalAddress(this.mockTargetGoal.TeamName, this.mockTargetGoal.ExecutionGoal);
                    Assert.IsTrue(expectedAddress.Equals(actualAddress));
                })
                .Returns(Task.FromResult(this.mockExecutionGoal));

            Item<GoalBasedSchedule> actualInstance = this.dataManager.GetExecutionGoalAsync(this.mockTargetGoal.ExecutionGoal, this.mockTargetGoal.TeamName, CancellationToken.None)
                .GetAwaiter().GetResult();
            Assert.AreEqual(actualInstance, this.mockExecutionGoal);
        }

        [Test]
        public async Task GetExecutionGoalsAsyncRetrievesExpectedExecutionGoals()
        {
            this.mockDocumentStore.Setup(store => store.GetDocumentsAsync<Item<GoalBasedSchedule>>(
                It.IsAny<CosmosAddress>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<IQueryFilter>()))
                .Callback<CosmosAddress, CancellationToken, IQueryFilter>((actualAddress, token, queryFilter) =>
                {
                    CosmosAddress expectedAddress = ScheduleAddressFactory.CreateExecutionGoalAddress(this.mockTargetGoal.TeamName);
                    Assert.IsTrue(expectedAddress.Equals(actualAddress));
                })
                .Returns(Task.FromResult(new List<Item<GoalBasedSchedule>>() { this.mockExecutionGoal } as IEnumerable<Item<GoalBasedSchedule>>));

            IEnumerable<Item<GoalBasedSchedule>> actualExecutionGoals = await this.dataManager.GetExecutionGoalsAsync(CancellationToken.None, "teamName");
            Assert.AreEqual(this.mockExecutionGoal, actualExecutionGoals.FirstOrDefault());
        }

        [Test]
        public async Task GetExecutionGoalsAsyncRetrievesExpectedExecutionGoalsWithoutTeamName()
        {
            this.mockDocumentStore.Setup(store => store.GetDocumentsAsync<Item<GoalBasedSchedule>>(
                It.IsAny<CosmosAddress>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<IQueryFilter>()))
                .Callback<CosmosAddress, CancellationToken, IQueryFilter>((actualAddress, token, queryFilter) =>
                {
                    CosmosAddress expectedAddress = ScheduleAddressFactory.CreateExecutionGoalAddress();
                    Assert.IsTrue(expectedAddress.Equals(actualAddress));
                })
                .Returns(Task.FromResult(new List<Item<GoalBasedSchedule>>() { this.mockExecutionGoal } as IEnumerable<Item<GoalBasedSchedule>>));

            IEnumerable<Item<GoalBasedSchedule>> actualExecutionGoals = await this.dataManager.GetExecutionGoalsAsync(CancellationToken.None);
            Assert.AreEqual(this.mockExecutionGoal, actualExecutionGoals.FirstOrDefault());
        }

        [Test]
        public void UpdateExecutionGoalAsyncUpdatesTheExpectedExecutionGoal()
        {
            string executionGoalId = this.mockExecutionGoal.Id;
            Item<GoalBasedSchedule> expectedGoal = this.mockExecutionGoal;
            expectedGoal.SetETag("Thisismyetag");
            this.mockDocumentStore.Setup(store => store.SaveDocumentAsync(
                It.IsAny<CosmosAddress>(),
                It.IsAny<Item<GoalBasedSchedule>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
                .Callback<CosmosAddress, Item<GoalBasedSchedule>, CancellationToken, bool>((actualAddress, data, token, replace) =>
                {
                    Assert.AreEqual(expectedGoal.Definition, data.Definition);
                    Assert.AreEqual(expectedGoal.Id, data.Id);
                })
                .Returns(Task.FromResult(expectedGoal));

            this.mockDocumentStore.Setup(store => store.GetDocumentAsync<Item<GoalBasedSchedule>>(
                It.IsAny<CosmosAddress>(),
                It.IsAny<CancellationToken>()))
                .Callback<CosmosAddress, CancellationToken>((actualAddress, token) =>
                {
                    Assert.AreEqual(actualAddress, ScheduleAddressFactory.CreateExecutionGoalAddress(this.mockExecutionGoal.Definition.TeamName, this.mockExecutionGoal.Id));
                })
                .Returns(Task.FromResult(expectedGoal));

            this.dataManager.UpdateExecutionGoalAsync(expectedGoal, CancellationToken.None)
                .GetAwaiter().GetResult();
        }

        [Test]
        public void ScheduleDataManagerDeletesTheExpectedExperiment()
        {
            string expectedId = this.mockExecutionGoal.Id;
            this.mockDocumentStore.Setup(store => store.DeleteDocumentAsync(
                It.IsAny<CosmosAddress>(),
                It.IsAny<CancellationToken>()))
                .Callback<CosmosAddress, CancellationToken>((actualAddress, token) =>
                {
                    CosmosAddress expectedAddress = ScheduleAddressFactory.CreateExecutionGoalAddress(this.mockExecutionGoal.Definition.TeamName, expectedId);
                    Assert.IsTrue(expectedAddress.Equals(actualAddress));
                })
                .Returns(Task.CompletedTask);

            this.dataManager.DeleteExecutionGoalAsync(expectedId, this.mockExecutionGoal.Definition.TeamName, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
        }

        [Test]
        public void ScheduleDataMangerGetsExpectedExecutionGoalTemplate()
        {
            string id = Guid.NewGuid().ToString();
            Item<GoalBasedSchedule> expectedResult = this.mockExecutionGoal;
            this.mockDocumentStore.Setup(store => store.GetDocumentAsync<Item<GoalBasedSchedule>>(
                It.IsAny<CosmosAddress>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(expectedResult));

            Item<GoalBasedSchedule> actualResult = this.dataManager.GetExecutionGoalTemplateAsync(id, "anyTeam", CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsNotNull(actualResult);
            Assert.AreEqual(expectedResult.Definition, actualResult.Definition);
        }

        [Test]
        public void ScheduleDataManagerGetsExpectedExecutionGoalsInfo()
        {
            string id1 = Guid.NewGuid().ToString();
            string id2 = Guid.NewGuid().ToString();
            IEnumerable<Item<GoalBasedSchedule>> cosmosReturnValue = new List<Item<GoalBasedSchedule>>()
            {
                new Item<GoalBasedSchedule>(id1, this.mockExecutionGoal.Definition),
                new Item<GoalBasedSchedule>(id2, this.mockExecutionGoal.Definition)
            };
            this.mockDocumentStore.Setup(mgr => mgr.GetDocumentsAsync<Item<GoalBasedSchedule>>(
                It.IsAny<CosmosAddress>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<IQueryFilter>()))
                .Returns(Task.FromResult(cosmosReturnValue));

            IEnumerable<ExecutionGoalSummary> actualResult = this.dataManager.GetExecutionGoalTemplateInfoAsync(CancellationToken.None, "A string").GetAwaiter().GetResult();

            foreach (ExecutionGoalSummary info in actualResult)
            {
                Assert.IsTrue(info.Id == id1 || info.Id == id2);
                Assert.IsTrue(info.Description == this.mockExecutionGoal.Definition.Description);
                Assert.IsTrue(info.TeamName == this.mockExecutionGoal.Definition.TeamName);
            }
        }

        [Test]
        public void ScheduleDataManagerGetsExpectedExecutionGoalInfo()
        {
            Item<GoalBasedSchedule> cosmosReturnValue = this.mockExecutionGoal;
                
            this.mockDocumentStore.Setup(mgr => mgr.GetDocumentAsync<Item<GoalBasedSchedule>>(
                It.IsAny<CosmosAddress>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(cosmosReturnValue));

            IEnumerable<ExecutionGoalSummary> result = this.dataManager.GetExecutionGoalTemplateInfoAsync(CancellationToken.None, "a string", "an id").GetAwaiter().GetResult();
            ExecutionGoalSummary actualResult = result.First();

            Assert.IsNotNull(actualResult);
            Assert.AreEqual(this.mockExecutionGoal.Id, actualResult.Id);
        }

        [Test]
        public void ScheduleDataManagerGetsExpectedExecutionGoalInfoWhenParametersArePresent()
        {
            string id = Guid.NewGuid().ToString();
            Item<GoalBasedSchedule> cosmosReturnValue = new Item<GoalBasedSchedule>(id, FixtureExtensions.CreateExecutionGoalTemplate());
            ExecutionGoalSummary expectedResult = new ExecutionGoalSummary(
                id, 
                cosmosReturnValue.Definition.Description, 
                cosmosReturnValue.Definition.TeamName, 
                cosmosReturnValue.Definition.GetParametersFromTemplate());

            this.mockDocumentStore.Setup(mgr => mgr.GetDocumentAsync<Item<GoalBasedSchedule>>(
                It.IsAny<CosmosAddress>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(cosmosReturnValue));

            IEnumerable<ExecutionGoalSummary> result = this.dataManager.GetExecutionGoalTemplateInfoAsync(CancellationToken.None, "a string", id).GetAwaiter().GetResult();
            ExecutionGoalSummary actualResult = result.First();

            Assert.IsTrue(result.Count() == 1);
            Assert.AreEqual(expectedResult, actualResult);
        }

        [Test]
        public void ScheduleDataManagerGetsExpectedExecutionGoalInfoWithSummaryView()
        {
            Item<GoalBasedSchedule> cosmosReturnValue = this.mockExecutionGoal;

            this.mockDocumentStore.Setup(mgr => mgr.GetDocumentAsync<Item<GoalBasedSchedule>>(
                It.IsAny<CosmosAddress>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(cosmosReturnValue));

            IEnumerable<ExecutionGoalSummary> result = this.dataManager.GetExecutionGoalsInfoAsync(CancellationToken.None, "a string", "an id").GetAwaiter().GetResult();
            ExecutionGoalSummary actualResult = result.First();

            Assert.IsNotNull(actualResult);
            Assert.AreEqual(this.mockExecutionGoal.Id, actualResult.Id);
        }
    }
}
