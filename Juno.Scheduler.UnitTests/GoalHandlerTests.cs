namespace Juno.Scheduler.Management
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Providers;
    using Juno.Scheduler.Preconditions;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class GoalHandlerTests
    {
        private GoalHandler goalExecution;
        private Goal mockGoal;
        private ScheduleContext mockContext;
        private Mock<IPreconditionProvider> mockPrecondition;
        private Mock<IScheduleActionProvider> mockAction;

        [OneTimeSetUp]
        public void SetupTests()
        {
            IServiceCollection services = new ServiceCollection();
            this.mockPrecondition = new Mock<IPreconditionProvider>();
            this.mockAction = new Mock<IScheduleActionProvider>();

            services.AddSingleton<IPreconditionProvider>(this.mockPrecondition.Object);
            services.AddSingleton<IScheduleActionProvider>(this.mockAction.Object);
            services.AddSingleton<ILogger>(NullLogger.Instance);

            this.goalExecution = new GoalHandler(services);
            this.mockContext = new ScheduleContext(new Item<GoalBasedSchedule>("id", FixtureExtensions.CreateExecutionGoalFromTemplate()), FixtureExtensions.CreateTargetGoalTrigger(), new Mock<IConfiguration>().Object);
            this.mockGoal = new Goal(
                "goal", 
                new List<Precondition>() { new Precondition("some precondition", new Dictionary<string, IConvertible>()) },
                new List<ScheduleAction>() { new ScheduleAction("some action", new Dictionary<string, IConvertible>()) });
        }

        [SetUp]
        public void SetupDefaultBehavior()
        {
            this.mockContext = new ScheduleContext(new Item<GoalBasedSchedule>("id", FixtureExtensions.CreateExecutionGoalFromTemplate()), FixtureExtensions.CreateTargetGoalTrigger(), new Mock<IConfiguration>().Object);
            this.mockPrecondition.Reset();
            this.mockAction.Reset();
        }

        [Test]
        public void ExecuteGoalAsyncValidatesParameters()
        {
            Assert.ThrowsAsync<ArgumentException>(() => this.goalExecution.ExecuteGoalAsync(null, this.mockContext, CancellationToken.None));
            Assert.ThrowsAsync<ArgumentException>(() => this.goalExecution.ExecuteGoalAsync(FixtureExtensions.CreateTargetGoal(), null, CancellationToken.None));
        }

        [Test]
        public async Task ExecuteGoalAsyncReturnsExpectedResultIfPreconditionsAreSatisfied()
        {
            this.mockPrecondition.Setup(pc => pc.IsConditionSatisfiedAsync(It.IsAny<Precondition>(), It.IsAny<ScheduleContext>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));

            bool result = await this.goalExecution.ExecuteGoalAsync(this.mockGoal, this.mockContext, CancellationToken.None);
            Assert.IsTrue(result);

            this.mockPrecondition.Verify(pc => pc.IsConditionSatisfiedAsync(It.IsAny<Precondition>(), It.IsAny<ScheduleContext>(), It.IsAny<CancellationToken>()), Times.Once());
            this.mockAction.Verify(ac => ac.ExecuteActionAsync(It.IsAny<ScheduleAction>(), It.IsAny<ScheduleContext>(), It.IsAny<CancellationToken>()), Times.Once());
        }

        [Test]
        public async Task ExecuteGoalAsyncReturnsExpectedResultIfPreconditionsAreNotSatisfied()
        {
            this.mockPrecondition.Setup(pc => pc.IsConditionSatisfiedAsync(It.IsAny<Precondition>(), It.IsAny<ScheduleContext>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(false));

            bool result = await this.goalExecution.ExecuteGoalAsync(this.mockGoal, this.mockContext, CancellationToken.None);
            Assert.IsFalse(result);

            this.mockPrecondition.Verify(pc => pc.IsConditionSatisfiedAsync(It.IsAny<Precondition>(), It.IsAny<ScheduleContext>(), It.IsAny<CancellationToken>()), Times.Once());
            this.mockAction.Verify(ac => ac.ExecuteActionAsync(It.IsAny<ScheduleAction>(), It.IsAny<ScheduleContext>(), It.IsAny<CancellationToken>()), Times.Never());
        }

        [Test]
        public async Task ExecuteGoalAsyncDoesNotExecuteTimerTriggerPrecondition()
        {
            TargetGoal timerTriggerGoal = new TargetGoal(
                "some goal", 
                true,
                new List<Precondition>() { new Precondition(typeof(TimerTriggerProvider).FullName, new Dictionary<string, IConvertible>()) }, 
                new List<ScheduleAction>() { new ScheduleAction("some action", new Dictionary<string, IConvertible>()) });
            ScheduleContext localContext = new ScheduleContext(this.mockContext.ExecutionGoal, this.mockContext.TargetGoalTrigger, this.mockContext.Configuration);
            localContext.ExecutionGoal.Definition.TargetGoals.Add(timerTriggerGoal);
            bool result = await this.goalExecution.ExecuteGoalAsync(timerTriggerGoal, localContext, CancellationToken.None);

            Assert.IsTrue(result);
            this.mockPrecondition.Verify(pc => pc.IsConditionSatisfiedAsync(It.IsAny<Precondition>(), It.IsAny<ScheduleContext>(), It.IsAny<CancellationToken>()), Times.Never());
            this.mockAction.Verify(ac => ac.ExecuteActionAsync(It.IsAny<ScheduleAction>(), It.IsAny<ScheduleContext>(), It.IsAny<CancellationToken>()), Times.Once());
        }

        [Test]
        public async Task ExecuteGoalAsyncExecutesTimerTriggerWhenInsideControlGoal()
        {
            Goal controlGoal = new Goal(
                "some goal",
                new List<Precondition>() { new Precondition(typeof(TimerTriggerProvider).FullName, new Dictionary<string, IConvertible>()) },
                new List<ScheduleAction>() { new ScheduleAction("some action", new Dictionary<string, IConvertible>()) });
            this.mockPrecondition.Setup(pc => pc.IsConditionSatisfiedAsync(It.IsAny<Precondition>(), It.IsAny<ScheduleContext>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));

            ScheduleContext localContext = new ScheduleContext(this.mockContext.ExecutionGoal, this.mockContext.TargetGoalTrigger, this.mockContext.Configuration);
            localContext.ExecutionGoal.Definition.ControlGoals.Add(controlGoal);
            bool result = await this.goalExecution.ExecuteGoalAsync(controlGoal, localContext, CancellationToken.None);

            Assert.IsTrue(result);
            this.mockPrecondition.Verify(pc => pc.IsConditionSatisfiedAsync(It.IsAny<Precondition>(), It.IsAny<ScheduleContext>(), It.IsAny<CancellationToken>()), Times.Once());
            this.mockAction.Verify(ac => ac.ExecuteActionAsync(It.IsAny<ScheduleAction>(), It.IsAny<ScheduleContext>(), It.IsAny<CancellationToken>()), Times.Once());
        }
    }
}
