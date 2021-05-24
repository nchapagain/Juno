namespace Juno.Providers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class PreconditionProviderTests
    {
        private GoalProviderFixture mockFixture;
        private TestPreconditionProvider provider;
        private IServiceCollection services;
        private ScheduleContext mockContext;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new GoalProviderFixture(typeof(TestPreconditionProvider));
            this.services = new ServiceCollection();
            Mock<IConfiguration> mockConfiguration = new Mock<IConfiguration>();
            this.provider = new TestPreconditionProvider(this.services);
            this.mockFixture = (GoalProviderFixture)this.mockFixture.SetUpGoalBasedScheduleMocks();
            this.mockContext = new ScheduleContext(this.mockFixture.Create<GoalBasedSchedule>(), this.mockFixture.Create<TargetGoalTrigger>(), mockConfiguration.Object);
        }

        [Test]
        public async Task PreconditionProviderReturnsTheExpectedResult()
        {
            PreconditionResult expectedResult = new PreconditionResult(ExecutionStatus.InProgress);
            this.provider.OnExecuteAsync = (component, telemetryContext, token) => expectedResult;

            PreconditionResult actualResult = await this.provider.IsConditionSatisfiedAsync(this.mockFixture.PreconditionComponent, this.mockContext, CancellationToken.None);

            Assert.IsNotNull(actualResult);
            Assert.IsNull(actualResult.Error);
            Assert.IsNull(actualResult.Satisfied);
            Assert.AreEqual(expectedResult.Status, actualResult.Status);
        }

        [Test]
        public async Task PreconditionProviderReturnsTheExpectedPreconditionResultWhenCancelled()
        {
            using (CancellationTokenSource tokenSource = new CancellationTokenSource())
            {
                tokenSource.Cancel();
                PreconditionResult actualResult = await this.provider.IsConditionSatisfiedAsync(this.mockFixture.PreconditionComponent, this.mockContext, tokenSource.Token);

                Assert.IsNotNull(actualResult);
                Assert.IsNull(actualResult.Error);
                Assert.IsNull(actualResult.Satisfied);
                Assert.IsTrue(actualResult.Status == ExecutionStatus.Cancelled);
            }
        }

        [Test]
        public async Task PreconditionproviderReturnsTheExpectedPreconditionResultWhenExceptionOccurs()
        {
            ProviderException expectedError = new ProviderException(ErrorReason.ProviderDefinitionInvalid);
            this.provider.OnExecuteAsync = (component, telemetryContext, token) => throw expectedError;

            PreconditionResult actualResult = await this.provider.IsConditionSatisfiedAsync(this.mockFixture.PreconditionComponent, this.mockContext, CancellationToken.None);

            Assert.IsNotNull(actualResult);
            Assert.IsNotNull(actualResult.Error);
            Assert.IsTrue(object.ReferenceEquals(expectedError, actualResult.Error));
            Assert.IsTrue(actualResult.Status == ExecutionStatus.Failed);
        }

        private class TestPreconditionProvider : PreconditionProvider
        {
            public TestPreconditionProvider(IServiceCollection services)
                : base(services)
            { 
            }

            public Func<Precondition, EventContext, CancellationToken, PreconditionResult> OnExecuteAsync { get; set; }

            public override Task ConfigureServicesAsync(GoalComponent component, ScheduleContext context)
            {
                return Task.CompletedTask;
            }

            protected override Task<PreconditionResult> IsConditionSatisfiedAsync(Precondition component, ScheduleContext scheduleContext, EventContext telemetryContext, CancellationToken cancellationToken)
            {
                return this.OnExecuteAsync != null
                    ? Task.FromResult(this.OnExecuteAsync.Invoke(component, telemetryContext, cancellationToken))
                    : Task.FromResult(new PreconditionResult(ExecutionStatus.Succeeded, false));
            }
        }
    }
}
