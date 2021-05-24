namespace Juno.DataManagement
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using global::Kusto.Cloud.Platform.Utils;
    using Juno.Contracts;
    using Juno.DataManagement.Cosmos;
    using Microsoft.Azure.CRC.Repository;
    using Microsoft.Azure.CRC.Repository.Cosmos;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ScheduleTimerDataManagerTests
    {
        private Fixture mockFixture;
        private Mock<ITableStore<CosmosTableAddress>> mockTableStore;
        private TargetGoalTableEntity mockTableEntity;
        private IEnumerable<TargetGoalTableEntity> mockTableEntities;
        private TargetGoalTrigger mockTargetGoal;
        private IScheduleTimerDataManager dataManager;
        private string currentExecutionGoalVersion;

        [SetUp]
        public void SetUp()
        {
            this.mockFixture = new Fixture();

            this.currentExecutionGoalVersion = "2021-01-01";
            this.mockFixture.SetUpGoalBasedScheduleMocks();
            this.mockTableEntity = this.mockFixture.Create<TargetGoalTableEntity>();
            this.mockTargetGoal = this.mockFixture.Create<TargetGoalTrigger>();
            this.mockTableEntities = new List<TargetGoalTableEntity>()
            {
                new TargetGoalTableEntity
                {
                    Id = this.mockTableEntity.Id,
                    PartitionKey = this.mockTableEntity.PartitionKey,
                    RowKey = this.mockTableEntity.RowKey,
                    CronExpression = this.mockTableEntity.CronExpression,
                    Enabled = this.mockTableEntity.Enabled,
                    ExperimentName = this.mockTableEntity.ExperimentName,
                    TeamName = this.mockTableEntity.TeamName,
                    ExecutionGoal = this.mockTableEntity.ExecutionGoal,
                    Created = DateTime.UtcNow.AddSeconds(-10),
                    Timestamp = DateTime.UtcNow.AddSeconds(-10),
                    ETag = DateTime.UtcNow.ToString("o")
                }
            };

            this.mockTableStore = new Mock<ITableStore<CosmosTableAddress>>();
            this.dataManager = new ScheduleTimerDataManager(this.mockTableStore.Object, NullLogger.Instance);
        }

        [Test]
        public void ScheduleTimerDataManagerCreatesExpectedTargetGoals()
        {
            Goal targetGoal = FixtureExtensions.CreateTargetGoal();
            GoalBasedSchedule executionGoal = FixtureExtensions.CreateExecutionGoalFromTemplate(targetGoals: new List<Goal>() { targetGoal });

            TargetGoalTableEntity expectedEntity = FixtureExtensions.CreateTargetTableEntityFromTemplates(executionGoal, targetGoal);
            IDictionary<CosmosTableAddress, TargetGoalTableEntity> expectedDictionary = new Dictionary<CosmosTableAddress, TargetGoalTableEntity>()
            { 
                [ScheduleAddressFactory.CreateTargetGoalTriggerAddress(this.currentExecutionGoalVersion, expectedEntity.RowKey)] = expectedEntity
            };
            this.mockTableStore.Setup(tableStore => tableStore.SaveEntitiesAsync(
                It.IsAny<IEnumerable<KeyValuePair<CosmosTableAddress, TargetGoalTableEntity>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
                .Callback<IEnumerable<KeyValuePair<CosmosTableAddress, TargetGoalTableEntity>>, CancellationToken, bool>((actualDictionary, cancellationToken, replace) =>
                {
                    foreach (KeyValuePair<CosmosTableAddress, TargetGoalTableEntity> currentKeyValue in actualDictionary)
                    {
                        Assert.IsTrue(expectedDictionary.ContainsKey(currentKeyValue.Key));
                        TargetGoalTableEntity expectedEntityFromDict = expectedDictionary.GetOrDefault(currentKeyValue.Key, null);
                        TargetGoalTableEntity actualEntity = currentKeyValue.Value;
                        Assert.AreEqual(expectedEntityFromDict.PartitionKey, actualEntity.PartitionKey);
                        Assert.AreEqual(expectedEntityFromDict.RowKey, actualEntity.RowKey);
                    }
                });
            this.dataManager.CreateTargetGoalsAsync(executionGoal, CancellationToken.None).GetAwaiter().GetResult();
        }

        [Test]
        public void ScheduleTimerDataManagerRetrievesExpectedTargetGoals()
        {
            IList<CosmosTableAddress> expectedAddresses = ScheduleAddressFactory.CreateAllTargetGoalTriggerAddress();
            List<TargetGoalTrigger> expectedResult = new List<TargetGoalTrigger>();

            foreach (CosmosTableAddress expectedAddress in expectedAddresses)
            {
                this.mockTableEntities.ForEach(entity => expectedResult.Add(entity.ToTargetGoalTrigger()));
            }

            this.mockTableStore.Setup(store => store.GetEntitiesAsync<TargetGoalTableEntity>(
                It.IsAny<CosmosTableAddress>(),
                It.IsAny<CancellationToken>()))
                .Callback<CosmosTableAddress, CancellationToken>((actualAddress, cancellationToken) =>
                {
                    Assert.IsTrue(expectedAddresses.Contains(actualAddress));
                })
                .Returns(Task.FromResult(this.mockTableEntities as IEnumerable<TargetGoalTableEntity>));

            IEnumerable<TargetGoalTrigger> actualResult = this.dataManager.GetTargetGoalTriggersAsync(CancellationToken.None)
                .GetAwaiter().GetResult();

            this.mockTableStore.Verify(
                store => store.GetEntitiesAsync<TargetGoalTableEntity>(
                It.IsAny<CosmosTableAddress>(),
                It.IsAny<CancellationToken>()), 
                Times.Exactly(expectedAddresses.Count));

            Assert.AreEqual(expectedResult as IEnumerable<TargetGoalTrigger>, actualResult);

        }

        [Test]
        public void ScheduleTimerDataManagerRetrievesExpectedTargetGoal()
        {
            this.mockTableStore.Setup(store => store.GetEntityAsync<TargetGoalTableEntity>(
                It.IsAny<CosmosTableAddress>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(this.mockTableEntity)
                .Callback<CosmosTableAddress, CancellationToken>((actualAddress, token) =>
                {
                    CosmosTableAddress expectedAddress = ScheduleAddressFactory.CreateTargetGoalTriggerAddress(this.currentExecutionGoalVersion);
                    Assert.AreEqual(expectedAddress.TableName, actualAddress.TableName);
                    Assert.AreEqual(actualAddress.RowKey, this.mockTableEntity.RowKey);
                });

            TargetGoalTrigger expectedResult = this.mockTableEntity.ToTargetGoalTrigger();

            var actualResult = this.dataManager.GetTargetGoalTriggerAsync(this.currentExecutionGoalVersion, this.mockTableEntity.RowKey, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.AreEqual(expectedResult, actualResult);
        }
                
        [Test]
        public void ScheduleTimerDataManagerUpdatesExecutionGoalTimerTrigger()
        { 
            this.mockTableStore.Setup(x => x.SaveEntityAsync(
                It.IsAny<CosmosTableAddress>(),
                It.IsAny<TargetGoalTableEntity>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
                .Callback<CosmosTableAddress, TargetGoalTableEntity, CancellationToken, bool>((actualAddress, updatedTableEntity, token, replaceIfExist) =>
                  {
                      CosmosTableAddress expectedAddress = ScheduleAddressFactory.CreateTargetGoalTriggerAddress(this.mockTargetGoal.Version);
                      Assert.AreEqual(expectedAddress.TableName, actualAddress.TableName);
                      Assert.AreEqual(expectedAddress.PartitionKey, actualAddress.PartitionKey);
                      Assert.AreEqual(updatedTableEntity.RowKey, actualAddress.RowKey);
                  });

            this.dataManager.UpdateTargetGoalTriggerAsync(this.mockTargetGoal, CancellationToken.None)
                .GetAwaiter().GetResult();
        }

        [Test]
        public void UpdateTargetGoalsAyncUpdatesExpectedTargetGoals()
        {
            Goal targetGoal = FixtureExtensions.CreateTargetGoal();
            GoalBasedSchedule executionGoal = FixtureExtensions.CreateExecutionGoalFromTemplate(targetGoals: new List<Goal>() { targetGoal });

            TargetGoalTableEntity expectedEntity = FixtureExtensions.CreateTargetTableEntityFromTemplates(executionGoal, targetGoal);
            IDictionary<CosmosTableAddress, TargetGoalTableEntity> expectedDictionary = new Dictionary<CosmosTableAddress, TargetGoalTableEntity>()
            {
                [ScheduleAddressFactory.CreateTargetGoalTriggerAddress(this.currentExecutionGoalVersion, expectedEntity.RowKey)] = expectedEntity
            };
            this.mockTableStore.Setup(tableStore => tableStore.SaveEntitiesAsync(
                It.IsAny<IEnumerable<KeyValuePair<CosmosTableAddress, TargetGoalTableEntity>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
                .Callback<IEnumerable<KeyValuePair<CosmosTableAddress, TargetGoalTableEntity>>, CancellationToken, bool>((actualDictionary, cancellationToken, replace) =>
                {
                    foreach (KeyValuePair<CosmosTableAddress, TargetGoalTableEntity> currentKeyValue in actualDictionary)
                    {
                        Assert.IsTrue(expectedDictionary.ContainsKey(currentKeyValue.Key));
                        TargetGoalTableEntity expectedEntityFromDict = expectedDictionary.GetOrDefault(currentKeyValue.Key, null);
                        TargetGoalTableEntity actualEntity = currentKeyValue.Value;
                        Assert.AreEqual(expectedEntityFromDict.PartitionKey, actualEntity.PartitionKey);
                        Assert.AreEqual(expectedEntityFromDict.RowKey, actualEntity.RowKey);

                    }
                });
            this.dataManager.UpdateTargetGoalTriggersAsync(executionGoal, CancellationToken.None).GetAwaiter().GetResult();
        }

        [Test]
        public void ScheduleTimerDataManagerDeletesExpectedTargetGoals()
        {
            Goal targetGoal = FixtureExtensions.CreateTargetGoal();
            GoalBasedSchedule executionGoal = FixtureExtensions.CreateExecutionGoalFromTemplate(targetGoals: new List<Goal>() { targetGoal });

            TargetGoalTableEntity expectedEntity = FixtureExtensions.CreateTargetTableEntityFromTemplates(executionGoal, targetGoal);
            string id = executionGoal.ExecutionGoalId;
            IList<TargetGoalTableEntity> targetGoals = new List<TargetGoalTableEntity>() { expectedEntity };
            executionGoal.TargetGoals.ForEach(goal => targetGoals.Add(goal.ToTableEntity(executionGoal)));

            this.mockTableStore.Setup(mgr => mgr.GetEntitiesAsync<TargetGoalTableEntity>(It.IsAny<CosmosTableAddress>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(targetGoals as IEnumerable<TargetGoalTableEntity>));

            IList<CosmosTableAddress> expectedAddresses = new List<CosmosTableAddress>();
            targetGoals.ForEach(goal => expectedAddresses.Add(ScheduleAddressFactory.CreateTargetGoalTriggerAddress(this.currentExecutionGoalVersion, goal.RowKey)));

            this.mockTableStore.Setup(mgr => mgr.DeleteEntityAsync<TargetGoalTableEntity>(It.IsAny<CosmosTableAddress>(), It.IsAny<CancellationToken>()))
                .Callback<CosmosTableAddress, CancellationToken>((actualAddress, token) =>
                {
                    Assert.IsTrue(expectedAddresses.Contains(actualAddress));
                })
                .Returns(Task.CompletedTask);

            this.dataManager.DeleteTargetGoalTriggersAsync(id, CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}
