namespace Juno.Scheduler.Management
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;
    
    [TestFixture]
    [Category("Unit")]
    public class ExecutionGoalHandlerTests
    {
        private ExecutionGoalHandler executionGoalExecution;
        private TestGoalExecution mockGoalExecution;
        private IServiceCollection mockServices;

        private ScheduleContext mockContext;
        private GoalBasedSchedule mockExecutionGoal;
        private Fixture mockFixture;

        [SetUp]
        public void SetupTests()
        {
            this.mockServices = new ServiceCollection();
            this.mockServices.AddSingleton<ILogger>(NullLogger.Instance);
            this.mockGoalExecution = new TestGoalExecution(this.mockServices);
            this.mockServices.AddSingleton<GoalHandler>(this.mockGoalExecution);

            this.executionGoalExecution = new ExecutionGoalHandler(this.mockServices);

            TargetGoalTrigger targetGoalTrigger = FixtureExtensions.CreateTargetGoalTrigger();
            TargetGoal targetGoal = FixtureExtensions.CreateTargetGoal(targetGoalTrigger.Name);
            this.mockExecutionGoal = FixtureExtensions.CreateExecutionGoalFromTemplate(targetGoals: new List<TargetGoal>() { targetGoal });

            this.mockFixture = new Fixture();
            this.mockFixture.SetUpGoalBasedScheduleMocks();
            Mock<IConfiguration> mockConfiguration = new Mock<IConfiguration>();
            this.mockContext = new ScheduleContext(new Item<GoalBasedSchedule>("id", this.mockFixture.Create<GoalBasedSchedule>()), this.mockFixture.Create<TargetGoalTrigger>(), mockConfiguration.Object);
        }

        [Test]
        public void ExecuteScheduleAsyncValidatesParameters()
        {
            Assert.ThrowsAsync<ArgumentException>(() => this.executionGoalExecution.ExecuteExecutionGoalAsync(null, this.mockContext, CancellationToken.None));
            Assert.ThrowsAsync<ArgumentException>(() => this.executionGoalExecution.ExecuteExecutionGoalAsync(this.mockExecutionGoal, null, CancellationToken.None));
        }

        [Test]
        public async Task ExecuteScheduleAsyncExecutesTargetGoalsWhenControlGoalsAreNotSatisfied()
        {
            string targetGoalname = this.mockExecutionGoal.TargetGoals.Select(tg => tg.Name).First();
            bool executedTargetGoal = false;
            this.mockGoalExecution.OnExecuteGoalAsync = (goal, context, token) =>
            {
                if (goal.Name.Equals(targetGoalname))
                {
                    executedTargetGoal = true;
                }

                return Task.FromResult(false);
            };

            await this.executionGoalExecution.ExecuteExecutionGoalAsync(this.mockExecutionGoal, this.mockContext, CancellationToken.None);

            Assert.IsTrue(executedTargetGoal);
        }

        [Test]
        public async Task ExecuteScheduleAsyncDoesNotExecuteTargetGoalsWhenControlGoalsAreSatisfied()
        {
            string targetGoalname = this.mockExecutionGoal.TargetGoals.Select(tg => tg.Name).First();
            bool executedTargetGoal = false;
            this.mockGoalExecution.OnExecuteGoalAsync = (goal, context, token) =>
            {
                if (goal.Name.Equals(targetGoalname))
                {
                    executedTargetGoal = true;
                }

                return Task.FromResult(true);
            };

            await this.executionGoalExecution.ExecuteExecutionGoalAsync(this.mockExecutionGoal, this.mockContext, CancellationToken.None);

            Assert.IsFalse(executedTargetGoal);
        }

        [Test]
        public void ExecuteScheduleAsyncDoesNotThrowExceptionWhenGoalExecutionFails()
        {
            this.mockGoalExecution.OnExecuteGoalAsync = (goal, context, token) => throw new Exception();

            Assert.DoesNotThrowAsync(() => this.executionGoalExecution.ExecuteExecutionGoalAsync(this.mockExecutionGoal, this.mockContext, CancellationToken.None));
        }

        private class TestGoalExecution : GoalHandler
        {
            public TestGoalExecution(IServiceCollection services)
                : base(services)
            { 
            }

            public Func<Goal, ScheduleContext, CancellationToken, Task<bool>> OnExecuteGoalAsync { get; set; }

            public override Task<bool> ExecuteGoalAsync(Goal goal, ScheduleContext scheduleContext, CancellationToken token)
            {
                return this.OnExecuteGoalAsync == null
                    ? Task.FromResult(true)
                    : this.OnExecuteGoalAsync(goal, scheduleContext, token);
            }
        }
    }
}
