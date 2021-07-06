namespace Juno.Scheduler.Preconditions
{
    using System;
    using System.Data;
    using System.IO;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.Providers;
    using Juno.Scheduler.Preconditions.Manager;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class FailureRatePreconditionProviderTests
    {
        private const string MinimumRuns = "minimumExperimentInstance";
        private const int MinimumExperimentInstance = 20; // required parameter for Kusto

        private Fixture mockFixture;
        private IServiceCollection mockServices;
        private IConfiguration mockConfiguration;
        private Mock<IKustoManager> mockKustoManager;
        private ScheduleContext mockContext;

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
            this.mockContext = new ScheduleContext(new Item<GoalBasedSchedule>("id", this.mockFixture.Create<GoalBasedSchedule>()), this.mockFixture.Create<TargetGoalTrigger>(), this.mockConfiguration);
        }

        [Test]
        public void ConfigureServicesValidatesParameters()
        {
            GoalComponent component = this.mockFixture.Create<Precondition>();
            FailureRatePreconditionProvider provider = new FailureRatePreconditionProvider(this.mockServices);

            Assert.ThrowsAsync<ArgumentException>(() => provider.ConfigureServicesAsync(component, null));
            Assert.ThrowsAsync<ArgumentException>(() => provider.ConfigureServicesAsync(null, this.mockContext));
        }

        [Test]
        public void IsConditionSatisfiedAsyncValidatesParameters()
        {
            Precondition componenet = this.mockFixture.Create<Precondition>();
            componenet.Parameters.Add(FailureRatePreconditionProviderTests.MinimumRuns, FailureRatePreconditionProviderTests.MinimumExperimentInstance);
            PreconditionProvider provider = new FailureRatePreconditionProvider(this.mockServices);

            Assert.ThrowsAsync<ArgumentException>(() => provider.IsConditionSatisfiedAsync(null, this.mockContext, CancellationToken.None));
            Assert.ThrowsAsync<ArgumentException>(() => provider.IsConditionSatisfiedAsync(componenet, null, CancellationToken.None));
        }

        [Test]
        public void IsConditionSatisfiedAsyncReturnsExpectedResultWhenConditionIsSatisfied()
        {
            Precondition component = this.mockFixture.Create<Precondition>();
            component.Parameters.Add(FailureRatePreconditionProviderTests.MinimumRuns, FailureRatePreconditionProviderTests.MinimumExperimentInstance);
            this.mockKustoManager.Setup(mgr => mgr.GetKustoResponseAsync(It.IsAny<string>(), It.IsAny<KustoSettings>(), It.IsAny<string>(), It.IsAny<double?>()))
                .Returns(Task.FromResult(FailureRatePreconditionProviderTests.GetValidKustoResponse(21)));

            PreconditionProvider provider = new FailureRatePreconditionProvider(this.mockServices);
            bool result = provider.IsConditionSatisfiedAsync(component, this.mockContext, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsTrue(result);
        }

        [Test]
        public void IsConditionSatisfiedAsyncReturnsExpectedResultWhenConditionIsSatisfiedWithOptionalParameter()
        {
            int targetFailureRate = 30;
            Precondition component = this.mockFixture.Create<Precondition>();
            component.Parameters.Add(FailureRatePreconditionProviderTests.MinimumRuns, FailureRatePreconditionProviderTests.MinimumExperimentInstance);
            component.Parameters.Add(nameof(targetFailureRate), targetFailureRate);

            this.mockKustoManager.Setup(mgr => mgr.GetKustoResponseAsync(It.IsAny<string>(), It.IsAny<KustoSettings>(), It.IsAny<string>(), It.IsAny<double?>()))
                .Returns(Task.FromResult(FailureRatePreconditionProviderTests.GetValidKustoResponse(targetFailureRate + 1)));

            PreconditionProvider provider = new FailureRatePreconditionProvider(this.mockServices);
            bool result = provider.IsConditionSatisfiedAsync(component, this.mockContext, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsTrue(result);
        }

        [Test]
        public void IsConditionSatisfiedAsyncReturnsExpectedResultWhenConditionIsNotSatisfied()
        {
            Precondition component = this.mockFixture.Create<Precondition>();
            component.Parameters.Add(FailureRatePreconditionProviderTests.MinimumRuns, FailureRatePreconditionProviderTests.MinimumExperimentInstance);

            this.mockKustoManager.Setup(mgr => mgr.GetKustoResponseAsync(It.IsAny<string>(), It.IsAny<KustoSettings>(), It.IsAny<string>(), It.IsAny<double?>()))
                .Returns(Task.FromResult(FailureRatePreconditionProviderTests.GetValidKustoResponse(19)));

            PreconditionProvider provider = new FailureRatePreconditionProvider(this.mockServices);
            bool result = provider.IsConditionSatisfiedAsync(component, this.mockContext, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsFalse(result);
        }

        [Test]
        public void IsConditionSatisfiedAsyncReturnsExpectedResultWhenConditionIsNotSatisfiedWithOptionalParameter()
        {
            int targetFailureRate = 30;
            Precondition component = this.mockFixture.Create<Precondition>();
            component.Parameters.Add(FailureRatePreconditionProviderTests.MinimumRuns, FailureRatePreconditionProviderTests.MinimumExperimentInstance);
            component.Parameters.Add(nameof(targetFailureRate), targetFailureRate);

            this.mockKustoManager.Setup(mgr => mgr.GetKustoResponseAsync(It.IsAny<string>(), It.IsAny<KustoSettings>(), It.IsAny<string>(), It.IsAny<double?>()))
                .Returns(Task.FromResult(FailureRatePreconditionProviderTests.GetValidKustoResponse(targetFailureRate - 1)));

            PreconditionProvider provider = new FailureRatePreconditionProvider(this.mockServices);
            bool result = provider.IsConditionSatisfiedAsync(component, this.mockContext, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsFalse(result);
        }

        [Test]
        public void IsConditionSatisfiedAsyncReturnsExpectedResultWhenErrorOccurs()
        {
            Precondition component = this.mockFixture.Create<Precondition>();
            component.Parameters.Add(FailureRatePreconditionProviderTests.MinimumRuns, FailureRatePreconditionProviderTests.MinimumExperimentInstance);

            this.mockKustoManager.Setup(mgr => mgr.GetKustoResponseAsync(It.IsAny<string>(), It.IsAny<KustoSettings>(), It.IsAny<string>(), It.IsAny<double?>()))
                .Throws(new Exception());

            PreconditionProvider provider = new FailureRatePreconditionProvider(this.mockServices);
            Assert.ThrowsAsync<Exception>(() => provider.IsConditionSatisfiedAsync(component, this.mockContext, CancellationToken.None));
        }

        [Test]
        public void IsConditionSatisfiedAsyncAccessesCorrectCacheKey()
        {
            Precondition component = this.mockFixture.Create<Precondition>();
            component.Parameters.Add(FailureRatePreconditionProviderTests.MinimumRuns, FailureRatePreconditionProviderTests.MinimumExperimentInstance);
            TargetGoalTrigger targetGoal = this.mockFixture.Create<TargetGoalTrigger>();
            ScheduleContext context = new ScheduleContext(this.mockContext.ExecutionGoal, targetGoal, this.mockConfiguration);

            this.mockKustoManager.Setup(mgr => mgr.GetKustoResponseAsync(
                It.Is<string>(value => value.Equals(string.Concat("FailureRate", targetGoal.Name), StringComparison.Ordinal)),
                It.IsAny<KustoSettings>(),
                It.IsAny<string>(),
                It.IsAny<double?>()))
                .Returns(Task.FromResult(FailureRatePreconditionProviderTests.GetValidKustoResponse(21)));

            PreconditionProvider provider = new FailureRatePreconditionProvider(this.mockServices);
            bool result = provider.IsConditionSatisfiedAsync(component, this.mockContext, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsTrue(result);

            this.mockKustoManager.Verify(mgr => mgr.GetKustoResponseAsync(
                It.Is<string>(value => value.Equals(string.Concat("FailureRate", targetGoal.Name), StringComparison.Ordinal)),
                It.IsAny<KustoSettings>(),
                It.IsAny<string>(),
                It.IsAny<double?>()),
                Times.Once());
        }

        private static DataTable GetValidKustoResponse(int failureRate)
        {
            DataTable result = new DataTable();
            DataColumn count = new DataColumn("failureRate");
            result.Columns.Add(count);

            DataRow row = result.NewRow();
            row["failureRate"] = failureRate;
            result.Rows.Add(row);

            return result;
        }
    }
}
