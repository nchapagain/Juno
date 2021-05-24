namespace Juno.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Text;
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
    public class ScheduleActionProviderTests
    {
        private GoalProviderFixture mockFixture;
        private TestScheduleActionProvider provider;
        private IServiceCollection services;
        private ScheduleContext mockContext;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new GoalProviderFixture(typeof(TestScheduleActionProvider));
            this.services = new ServiceCollection();
            Mock<IConfiguration> mockConfiguration = new Mock<IConfiguration>();
            this.provider = new TestScheduleActionProvider(this.services);
            this.mockFixture = (GoalProviderFixture)this.mockFixture.SetUpGoalBasedScheduleMocks();
            this.mockContext = new ScheduleContext(this.mockFixture.Create<GoalBasedSchedule>(), this.mockFixture.Create<TargetGoalTrigger>(), mockConfiguration.Object);
        }

        [Test]
        public async Task ScheduleActionProviderReturnsTheExpectedResult()
        {
            ExecutionResult expectedResult = new ExecutionResult(ExecutionStatus.InProgress);
            this.provider.OnExecuteAsync = (component, telemetryContext, token) => expectedResult;

            ExecutionResult actualResult = await this.provider.ExecuteActionAsync(this.mockFixture.ScheduleActionComponent, this.mockContext, CancellationToken.None);

            Assert.IsNotNull(actualResult);
            Assert.IsNull(actualResult.Error);
            Assert.AreEqual(expectedResult.Status, actualResult.Status);
        }

        [Test]
        public async Task ScheduleActionProviderReturnsTheExpectedExecutionResultWhenCancelled()
        {
            using (CancellationTokenSource tokenSource = new CancellationTokenSource())
            {
                tokenSource.Cancel();
                ExecutionResult actualResult = await this.provider.ExecuteActionAsync(this.mockFixture.ScheduleActionComponent, this.mockContext, tokenSource.Token);

                Assert.IsNotNull(actualResult);
                Assert.IsNull(actualResult.Error);
                Assert.IsTrue(actualResult.Status == ExecutionStatus.Cancelled);
            }
        }

        [Test]
        public async Task ScheduleActionProviderReturnsTheExpectedExecutionResultWhenExceptionOccurs()
        {
            ProviderException expectedException = new ProviderException(ErrorReason.ProviderDefinitionInvalid);
            this.provider.OnExecuteAsync = (component, telemetryContext, token) => throw expectedException;

            ExecutionResult actualResult = await this.provider.ExecuteActionAsync(this.mockFixture.ScheduleActionComponent, this.mockContext, CancellationToken.None);

            Assert.IsNotNull(actualResult);
            Assert.IsNotNull(actualResult.Error);
            Assert.IsTrue(object.ReferenceEquals(expectedException, actualResult.Error));
            Assert.IsTrue(actualResult.Status == ExecutionStatus.Failed);
        }

        private class TestScheduleActionProvider : ScheduleActionProvider
        {
            public TestScheduleActionProvider(IServiceCollection services)
                : base(services)
            {
            }

            public Func<ScheduleAction, EventContext, CancellationToken, ExecutionResult> OnExecuteAsync { get; set; }

            public override Task ConfigureServicesAsync(GoalComponent component, ScheduleContext context)
            {
                return Task.CompletedTask;
            }

            protected override Task<ExecutionResult> ExecuteActionAsync(ScheduleAction component, ScheduleContext scheduleContext, EventContext telemetryContext, CancellationToken cancellationToken)
            {
                return this.OnExecuteAsync != null
                    ? Task.FromResult(this.OnExecuteAsync.Invoke(component, telemetryContext, cancellationToken))
                    : Task.FromResult(new ExecutionResult(ExecutionStatus.Succeeded));
            }
        }
    }
}
