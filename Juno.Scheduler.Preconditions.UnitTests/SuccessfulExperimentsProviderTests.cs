namespace Juno.Scheduler.Preconditions
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.Providers;
    using Juno.Scheduler.Preconditions.Manager;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class SuccessfulExperimentsProviderTests
    {
        private const string TargetInstanceParameter = "targetExperimentInstances";

        private Fixture mockFixture;
        private IServiceCollection mockServices;
        private IConfiguration mockConfiguration;
        private Mock<IKustoManager> mockKustoManager;

        [SetUp]
        public void SetupTests()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetUpGoalBasedScheduleMocks();
            this.mockServices = new ServiceCollection();
            this.mockKustoManager = new Mock<IKustoManager>();
            this.mockConfiguration = new ConfigurationBuilder().SetBasePath(Path.Combine(
                 Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                 @"Configuration"))
                .AddJsonFile($"juno-dev01.environmentsettings.json")
                .Build();

            this.mockServices.AddSingleton<IKustoManager>(this.mockKustoManager.Object);
        }

        [Test]
        public void ConfigureServicesValidatesParameters()
        {
            GoalComponent component = this.mockFixture.Create<Precondition>();
            ScheduleContext context = new ScheduleContext(this.mockFixture.Create<GoalBasedSchedule>(), this.mockFixture.Create<TargetGoalTrigger>(), this.mockConfiguration);

            SuccessfulExperimentsProvider provider = new SuccessfulExperimentsProvider(this.mockServices);
            
            Assert.ThrowsAsync<ArgumentException>(() => provider.ConfigureServicesAsync(component, null));
            Assert.ThrowsAsync<ArgumentException>(() => provider.ConfigureServicesAsync(null, context));
        }

        [Test]
        public void IsConditionSatisfiedAsyncValidatesParameters()
        {
            Precondition componenet = this.mockFixture.Create<Precondition>();
            componenet.Parameters.Add(SuccessfulExperimentsProviderTests.TargetInstanceParameter, 5);
            ScheduleContext context = new ScheduleContext(this.mockFixture.Create<GoalBasedSchedule>(), this.mockFixture.Create<TargetGoalTrigger>(), this.mockConfiguration);

            PreconditionProvider provider = new SuccessfulExperimentsProvider(this.mockServices);

            Assert.ThrowsAsync<ArgumentException>(() => provider.IsConditionSatisfiedAsync(null, context, CancellationToken.None));
            Assert.ThrowsAsync<ArgumentException>(() => provider.IsConditionSatisfiedAsync(componenet, null, CancellationToken.None));
        }

        [Test]
        public void IsConditionSatisfiedAsyncReturnsExpectedResultWhenStringParameterIsPassed()
        {
            string stringTargetInstanceParameter = "15";

            Precondition component = this.mockFixture.Create<Precondition>();
            component.Parameters.Add(SuccessfulExperimentsProviderTests.TargetInstanceParameter, stringTargetInstanceParameter);
            ScheduleContext context = new ScheduleContext(this.mockFixture.Create<GoalBasedSchedule>(), this.mockFixture.Create<TargetGoalTrigger>(), this.mockConfiguration);

            this.mockKustoManager.Setup(mgr => mgr.GetKustoResponseAsync(It.IsAny<string>(), It.IsAny<KustoSettings>(), It.IsAny<string>(), It.IsAny<double?>()))
                .Returns(Task.FromResult(SuccessfulExperimentsProviderTests.GetValidKustoResponse()));

            PreconditionProvider provider = new SuccessfulExperimentsProvider(this.mockServices);
            bool result = provider.IsConditionSatisfiedAsync(component, context, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(result);
        }

        [Test]
        public void IsConditionSatisfiedAsyncReturnsExpectedResultWhenConditionIsSatisfied()
        {
            Precondition component = this.mockFixture.Create<Precondition>();
            component.Parameters.Add(SuccessfulExperimentsProviderTests.TargetInstanceParameter, 15);
            ScheduleContext context = new ScheduleContext(this.mockFixture.Create<GoalBasedSchedule>(), this.mockFixture.Create<TargetGoalTrigger>(), this.mockConfiguration);

            this.mockKustoManager.Setup(mgr => mgr.GetKustoResponseAsync(It.IsAny<string>(), It.IsAny<KustoSettings>(), It.IsAny<string>(), It.IsAny<double?>()))
                .Returns(Task.FromResult(SuccessfulExperimentsProviderTests.GetValidKustoResponse()));

            PreconditionProvider provider = new SuccessfulExperimentsProvider(this.mockServices);
            bool result = provider.IsConditionSatisfiedAsync(component, context, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(result);
        }

        [Test]
        public void IsConditionSatisfiedAsyncReturnsExpectedResultWhenConditionIsNotSatisfied()
        {
            Precondition component = this.mockFixture.Create<Precondition>();
            component.Parameters.Add(SuccessfulExperimentsProviderTests.TargetInstanceParameter, 5);
            ScheduleContext context = new ScheduleContext(this.mockFixture.Create<GoalBasedSchedule>(), this.mockFixture.Create<TargetGoalTrigger>(), this.mockConfiguration);

            this.mockKustoManager.Setup(mgr => mgr.GetKustoResponseAsync(It.IsAny<string>(), It.IsAny<KustoSettings>(), It.IsAny<string>(), It.IsAny<double?>()))
                .Returns(Task.FromResult(SuccessfulExperimentsProviderTests.GetValidKustoResponse()));

            PreconditionProvider provider = new SuccessfulExperimentsProvider(this.mockServices);
            bool result = provider.IsConditionSatisfiedAsync(component, context, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsFalse(result);
        }

        [Test]
        public void IsConditionSatisfiedAsyncReturnsExpectedResultWhenErrorOccurs()
        {
            Precondition component = this.mockFixture.Create<Precondition>();
            component.Parameters.Add(SuccessfulExperimentsProviderTests.TargetInstanceParameter, 10);
            ScheduleContext context = new ScheduleContext(this.mockFixture.Create<GoalBasedSchedule>(), this.mockFixture.Create<TargetGoalTrigger>(), this.mockConfiguration);

            this.mockKustoManager.Setup(mgr => mgr.GetKustoResponseAsync(It.IsAny<string>(), It.IsAny<KustoSettings>(), It.IsAny<string>(), It.IsAny<double?>()))
                .Throws(new Exception());

            PreconditionProvider provider = new SuccessfulExperimentsProvider(this.mockServices);
            Assert.ThrowsAsync<Exception>(() => provider.IsConditionSatisfiedAsync(component, context, CancellationToken.None));
        }

        [Test]
        public void IsConditionSatisfiedAsyncAccessesCorrectCacheKey()
        {
            Precondition component = this.mockFixture.Create<Precondition>();
            component.Parameters.Add(SuccessfulExperimentsProviderTests.TargetInstanceParameter, 15);
            TargetGoalTrigger targetGoal = this.mockFixture.Create<TargetGoalTrigger>();
            ScheduleContext context = new ScheduleContext(this.mockFixture.Create<GoalBasedSchedule>(), targetGoal, this.mockConfiguration);

            this.mockKustoManager.Setup(mgr => mgr.GetKustoResponseAsync(
                It.Is<string>(value => value.Equals(string.Concat("SuccessfulExperiments", targetGoal.TargetGoal), StringComparison.Ordinal)),
                It.IsAny<KustoSettings>(),
                It.IsAny<string>(), 
                It.IsAny<double?>()))
                .Returns(Task.FromResult(SuccessfulExperimentsProviderTests.GetValidKustoResponse()));

            PreconditionProvider provider = new SuccessfulExperimentsProvider(this.mockServices);
            bool result = provider.IsConditionSatisfiedAsync(component, context, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(result);

            this.mockKustoManager.Verify(mgr => mgr.GetKustoResponseAsync(
                It.Is<string>(value => value.Equals(string.Concat("SuccessfulExperiments", targetGoal.TargetGoal), StringComparison.Ordinal)),
                It.IsAny<KustoSettings>(),
                It.IsAny<string>(),
                It.IsAny<double?>()),
                Times.Once());
        }

        private static DataTable GetValidKustoResponse()
        {
            DataTable result = new DataTable();
            DataColumn count = new DataColumn("experimentCount");
            result.Columns.Add(count);

            DataRow row = result.NewRow();
            row["experimentCount"] = 10;
            result.Rows.Add(row);

            return result;
        }
    }
}
