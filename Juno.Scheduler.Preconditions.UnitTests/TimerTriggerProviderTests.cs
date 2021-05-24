namespace Juno.Scheduler.Preconditions
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Contracts;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using NCrontab;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class TimerTriggerProviderTests
    {
        private IServiceCollection mockServices;
        private TimeSpan mockInterval;
        private Fixture mockFixture;
        private ScheduleContext mockContext;

        [SetUp]
        public void SetupTests()
        {
            this.mockServices = new ServiceCollection();
            this.mockInterval = TimeSpan.FromMinutes(10);
            this.mockFixture = new Fixture();
            this.mockFixture.SetUpGoalBasedScheduleMocks();
            this.mockContext = new ScheduleContext(this.mockFixture.Create<GoalBasedSchedule>(), this.mockFixture.Create<TargetGoalTrigger>(), new Mock<IConfiguration>().Object);
        }

        [Test]
        public void ProviderValidatesRequiredParameters()
        {
            var provider = new TimerTriggerProvider(this.mockServices);
            Assert.ThrowsAsync<ArgumentException>(() => provider.IsConditionSatisfiedAsync(null, this.mockContext, CancellationToken.None));
        }

        [Test]
        public async Task ProviderReturnsSatisfiedWhenComponentShouldFireAsync()
        {
            Precondition component = new Precondition(
                type: typeof(TimerTriggerProvider).FullName,
                parameters: new Dictionary<string, IConvertible>()
                {
                    ["startTime"] = DateTime.UtcNow,
                    ["endTime"] = DateTime.UtcNow.Add(this.mockInterval),
                    ["cronExpression"] = "*/1 * * * *"
                });
            var provider = new TimerTriggerProvider(this.mockServices);
            PreconditionResult result = await provider.IsConditionSatisfiedAsync(component, this.mockContext, CancellationToken.None)
                .ConfigureAwait(false);

            Assert.AreEqual(ExecutionStatus.Succeeded, result.Status);
            Assert.IsTrue(result.Satisfied);
        }

        [Test]
        public async Task ProviderReturnsNotSatisfiedWhenCompononentShouldNotFireAsync()
        {
            Precondition component = new Precondition(
                type: typeof(TimerTriggerProvider).FullName,
                parameters: new Dictionary<string, IConvertible>()
                {
                    ["startTime"] = DateTime.UtcNow,
                    ["endTime"] = DateTime.UtcNow.Add(this.mockInterval),
                    ["cronExpression"] = "0 0 31 2 0" // February 31st which should never occur
                });
            var provider = new TimerTriggerProvider(this.mockServices);
            PreconditionResult result = await provider.IsConditionSatisfiedAsync(component, this.mockContext, CancellationToken.None)
                .ConfigureAwait(false);

            Assert.AreEqual(ExecutionStatus.Failed, result.Status);
            Assert.IsFalse(result.Satisfied);
        }

        [Test]
        [TestCase("60 * * * *")]
        [TestCase("* 24 * * *")]
        [TestCase("* * 32 * *")]
        [TestCase("* * * 13 *")]
        [TestCase("* * * * 7")]
        public async Task ProviderReturnsFailedWhenGivenIncorrectlyFormattedCron(string invalidCronExpression)
        {
            Precondition component = new Precondition(
                type: typeof(TimerTriggerProvider).FullName,
                parameters: new Dictionary<string, IConvertible>()
                { 
                    ["startTime"] = DateTime.UtcNow,
                    ["endTime"] = DateTime.UtcNow,
                    ["cronExpression"] = invalidCronExpression
                });
            var provider = new TimerTriggerProvider(this.mockServices);
            PreconditionResult expectedResult = new PreconditionResult(ExecutionStatus.Failed, error: new CrontabException());
            PreconditionResult actualResult = await provider.IsConditionSatisfiedAsync(component, this.mockContext, CancellationToken.None)
                .ConfigureAwait(false);

            Assert.AreEqual(expectedResult.Status, actualResult.Status);
            Assert.AreEqual(expectedResult.Error.GetType(), actualResult.Error.GetType());
        }
    }
}
