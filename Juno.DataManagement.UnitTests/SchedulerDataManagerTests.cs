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
        private GoalBasedSchedule mockExecutionGoal;
        private TargetGoalTrigger mockTargetGoal;
        private ScheduleDataManager dataManager;
        private Mock<IDocumentStore<CosmosAddress>> mockDocumentStore;

        [SetUp]
        public void SetUp()
        {
            this.mockFixture = new Fixture();

            this.mockFixture.SetUpGoalBasedScheduleMocks();
            this.mockFixture.SetupEnvironmentSelectionMocks();

            this.mockExecutionGoal = this.mockFixture.Create<GoalBasedSchedule>();
            this.mockTargetGoal = this.mockFixture.Create<TargetGoalTrigger>();

            this.mockDocumentStore = new Mock<IDocumentStore<CosmosAddress>>();
            this.dataManager = new ScheduleDataManager(this.mockDocumentStore.Object, NullLogger.Instance);
        }

        [Test]
        public void ScheduleDataManagerCreateTheExpectedExecutionGoal()
        {
            string executionGoalId = this.mockExecutionGoal.ExecutionGoalId;
            Item<GoalBasedSchedule> expectedGoal = new Item<GoalBasedSchedule>(executionGoalId, this.mockExecutionGoal);
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

            this.dataManager.CreateExecutionGoalAsync(expectedGoal, CancellationToken.None)
                .GetAwaiter().GetResult();
        }

        [Test]
        public void ScheduleDataManagerRetrievesExpectedExecutionGoal()
        {
            var cosmosResult = new Item<GoalBasedSchedule>(Guid.NewGuid().ToString(), this.mockExecutionGoal);
            this.mockDocumentStore.Setup(store => store.GetDocumentAsync<Item<GoalBasedSchedule>>(
                It.IsAny<CosmosAddress>(),
                It.IsAny<CancellationToken>()))
                .Callback<CosmosAddress, CancellationToken>((actualAddress, token) => 
                {
                    CosmosAddress expectedAddress = ScheduleAddressFactory.CreateExecutionGoalAddress(this.mockTargetGoal.TeamName, this.mockTargetGoal.ExecutionGoal);
                    Assert.IsTrue(expectedAddress.Equals(actualAddress));
                })
                .Returns(Task.FromResult(cosmosResult));

            Item<GoalBasedSchedule> actualInstance = this.dataManager.GetExecutionGoalAsync(this.mockTargetGoal.ExecutionGoal, this.mockTargetGoal.TeamName, CancellationToken.None)
                .GetAwaiter().GetResult();
            Assert.AreEqual(actualInstance, cosmosResult);
        }

        [Test]
        public async Task GetExecutionGoalsAsyncRetrievesExpectedExecutionGoals()
        {
            var cosmosResult = new Item<GoalBasedSchedule>(Guid.NewGuid().ToString(), this.mockExecutionGoal);
            this.mockDocumentStore.Setup(store => store.GetDocumentsAsync<Item<GoalBasedSchedule>>(
                It.IsAny<CosmosAddress>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<IQueryFilter>()))
                .Callback<CosmosAddress, CancellationToken, IQueryFilter>((actualAddress, token, queryFilter) =>
                {
                    CosmosAddress expectedAddress = ScheduleAddressFactory.CreateExecutionGoalAddress(this.mockTargetGoal.TeamName);
                    Assert.IsTrue(expectedAddress.Equals(actualAddress));
                })
                .Returns(Task.FromResult(new List<Item<GoalBasedSchedule>>() { cosmosResult } as IEnumerable<Item<GoalBasedSchedule>>));

            IEnumerable<Item<GoalBasedSchedule>> actualExecutionGoals = await this.dataManager.GetExecutionGoalsAsync(CancellationToken.None, "teamName");
            Assert.AreEqual(cosmosResult, actualExecutionGoals.FirstOrDefault());
        }

        [Test]
        public async Task GetExecutionGoalsAsyncRetrievesExpectedExecutionGoalsWithoutTeamName()
        {
            var cosmosResult = new Item<GoalBasedSchedule>(Guid.NewGuid().ToString(), this.mockExecutionGoal);
            this.mockDocumentStore.Setup(store => store.GetDocumentsAsync<Item<GoalBasedSchedule>>(
                It.IsAny<CosmosAddress>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<IQueryFilter>()))
                .Callback<CosmosAddress, CancellationToken, IQueryFilter>((actualAddress, token, queryFilter) =>
                {
                    CosmosAddress expectedAddress = ScheduleAddressFactory.CreateExecutionGoalAddress();
                    Assert.IsTrue(expectedAddress.Equals(actualAddress));
                })
                .Returns(Task.FromResult(new List<Item<GoalBasedSchedule>>() { cosmosResult } as IEnumerable<Item<GoalBasedSchedule>>));

            IEnumerable<Item<GoalBasedSchedule>> actualExecutionGoals = await this.dataManager.GetExecutionGoalsAsync(CancellationToken.None);
            Assert.AreEqual(cosmosResult, actualExecutionGoals.FirstOrDefault());
        }

        [Test]
        public void UpdateExecutionGoalAsyncUpdatesTheExpectedExecutionGoal()
        {
            string executionGoalId = this.mockExecutionGoal.ExecutionGoalId;
            Item<GoalBasedSchedule> expectedGoal = new Item<GoalBasedSchedule>(executionGoalId, this.mockExecutionGoal);
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
                    Assert.AreEqual(actualAddress, ScheduleAddressFactory.CreateExecutionGoalAddress(this.mockExecutionGoal.TeamName, this.mockExecutionGoal.ExecutionGoalId));
                })
                .Returns(Task.FromResult(expectedGoal));

            this.dataManager.UpdateExecutionGoalAsync(expectedGoal, CancellationToken.None)
                .GetAwaiter().GetResult();
        }

        [Test]
        public void ScheduleDataManagerDeletesTheExpectedExperiment()
        {
            string expectedId = this.mockExecutionGoal.ExecutionGoalId;
            this.mockDocumentStore.Setup(store => store.DeleteDocumentAsync(
                It.IsAny<CosmosAddress>(),
                It.IsAny<CancellationToken>()))
                .Callback<CosmosAddress, CancellationToken>((actualAddress, token) =>
                {
                    CosmosAddress expectedAddress = ScheduleAddressFactory.CreateExecutionGoalAddress(this.mockExecutionGoal.TeamName, expectedId);
                    Assert.IsTrue(expectedAddress.Equals(actualAddress));
                })
                .Returns(Task.CompletedTask);

            this.dataManager.DeleteExecutionGoalAsync(expectedId, this.mockExecutionGoal.TeamName, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
        }

        [Test]
        public void ScheduleDataMangerGetsExpectedExecutionGoalTemplate()
        {
            string id = Guid.NewGuid().ToString();
            Item<GoalBasedSchedule> expectedResult = new Item<GoalBasedSchedule>(id, this.mockExecutionGoal);
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
                new Item<GoalBasedSchedule>(id1, this.mockExecutionGoal),
                new Item<GoalBasedSchedule>(id2, this.mockExecutionGoal)
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
                Assert.IsTrue(info.Description == this.mockExecutionGoal.Description);
                Assert.IsTrue(info.TeamName == this.mockExecutionGoal.TeamName);
            }
        }

        [Test]
        public void ScheduleDataManagerGetsExpectedExecutionGoalInfo()
        {
            string id1 = Guid.NewGuid().ToString();
            string id2 = Guid.NewGuid().ToString();
            Item<GoalBasedSchedule> cosmosReturnValue = new Item<GoalBasedSchedule>(id1, this.mockExecutionGoal);
                
            this.mockDocumentStore.Setup(mgr => mgr.GetDocumentAsync<Item<GoalBasedSchedule>>(
                It.IsAny<CosmosAddress>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(cosmosReturnValue));

            IEnumerable<ExecutionGoalSummary> result = this.dataManager.GetExecutionGoalTemplateInfoAsync(CancellationToken.None, "a string", id1).GetAwaiter().GetResult();
            ExecutionGoalSummary actualResult = result.First();

            Assert.IsNotNull(actualResult);
            Assert.AreEqual(id1, actualResult.Id);
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
            string id1 = Guid.NewGuid().ToString();
            string id2 = Guid.NewGuid().ToString();
            Item<GoalBasedSchedule> cosmosReturnValue = new Item<GoalBasedSchedule>(id1, this.mockExecutionGoal);

            this.mockDocumentStore.Setup(mgr => mgr.GetDocumentAsync<Item<GoalBasedSchedule>>(
                It.IsAny<CosmosAddress>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(cosmosReturnValue));

            IEnumerable<ExecutionGoalSummary> result = this.dataManager.GetExecutionGoalsInfoAsync(CancellationToken.None, "a string", id1).GetAwaiter().GetResult();
            ExecutionGoalSummary actualResult = result.First();

            Assert.IsNotNull(actualResult);
            Assert.AreEqual(id1, actualResult.Id);
        }
    }
}
