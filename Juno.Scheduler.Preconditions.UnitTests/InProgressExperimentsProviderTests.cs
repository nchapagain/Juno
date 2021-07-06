namespace Juno.Scheduler.Preconditions
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Contracts;
    using Juno.DataManagement;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class InProgressExperimentsProviderTests
    {
        private const string TargetExperimentInstances = nameof(InProgressExperimentsProviderTests.TargetExperimentInstances);

        private Fixture mockFixture;
        private IServiceCollection mockServices;
        private IConfiguration mockConfiguration;
        private Mock<IExperimentDataManager> mockExperimentDataManager;
        private ScheduleContext mockContext;

        [SetUp]
        public void SetupTests()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetUpGoalBasedScheduleMocks();
            this.mockServices = new ServiceCollection();
            this.mockExperimentDataManager = new Mock<IExperimentDataManager>();
            this.mockConfiguration = new ConfigurationBuilder().SetBasePath(Path.Combine(
                 Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                 @"Configuration"))
                .AddJsonFile($"juno-dev01.environmentsettings.json")
                .Build();
            this.mockServices.AddSingleton<IExperimentDataManager>(this.mockExperimentDataManager.Object);
            this.mockContext = new ScheduleContext(new Item<GoalBasedSchedule>("id", this.mockFixture.Create<GoalBasedSchedule>()), this.mockFixture.Create<TargetGoalTrigger>(), this.mockConfiguration);
        }

        [Test]
        public async Task IsConditionSatisfiedAsyncReturnsExpectedResultWhenConditionIsStatisfied()
        {
            Precondition component = this.mockFixture.Create<Precondition>();
            component.Parameters.Add(InProgressExperimentsProviderTests.TargetExperimentInstances, 5);
            InProgressExperimentsProvider provider = new InProgressExperimentsProvider(this.mockServices);
            IEnumerable<JObject> queryResult = new List<JObject>()
            {
                JObject.Parse(@"{""Count"": 0 }")
            };
            this.mockExperimentDataManager.Setup(mgr => mgr.QueryExperimentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(queryResult));

            bool result = await provider.IsConditionSatisfiedAsync(component, this.mockContext, CancellationToken.None);
            Assert.IsTrue(result);
        }

        [Test]
        public async Task IsConditionSatisfiedAsyncReturnsExpectedResultWhenConditionIsNotStatisfied()
        {
            Precondition component = this.mockFixture.Create<Precondition>();
            component.Parameters.Add(InProgressExperimentsProviderTests.TargetExperimentInstances, 5);
            InProgressExperimentsProvider provider = new InProgressExperimentsProvider(this.mockServices);
            IEnumerable<JObject> queryResult = new List<JObject>()
            {
                JObject.Parse(@"{""Count"": 5 }")
            };
            this.mockExperimentDataManager.Setup(mgr => mgr.QueryExperimentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(queryResult));

            bool result = await provider.IsConditionSatisfiedAsync(component, this.mockContext, CancellationToken.None);
            Assert.IsFalse(result);
        }

        [Test]
        public void IsConditionSatisfiedAsyncReturnsExpectedResultWhenAnExceptionOccurs()
        {
            Precondition component = this.mockFixture.Create<Precondition>();
            component.Parameters.Add(InProgressExperimentsProviderTests.TargetExperimentInstances, 5);
            InProgressExperimentsProvider provider = new InProgressExperimentsProvider(this.mockServices);
            this.mockExperimentDataManager.Setup(mgr => mgr.QueryExperimentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Throws(new Exception());

            Assert.ThrowsAsync<Exception>(() => provider.IsConditionSatisfiedAsync(component, this.mockContext, CancellationToken.None));
        }
    }
}
