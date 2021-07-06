namespace Juno.Scheduler.Management
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Juno.DataManagement;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Repository.KeyVault;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class GoalBasedSchedulerExecutionTests
    {
        private GoalBasedSchedulerExecution goalBasedSchedulerExecution;
        private TestExecutionGoalExecution mockExecutionGoalExecution;
        private Mock<IScheduleDataManager> mockDataManager;
        private Mock<IScheduleTimerDataManager> mockTimerDataManager;

        [SetUp]
        public void SetupTests()
        {
            IServiceCollection services = new ServiceCollection();
            services.AddSingleton<ILogger>(NullLogger.Instance);
            this.mockExecutionGoalExecution = new TestExecutionGoalExecution(services);
            this.mockDataManager = new Mock<IScheduleDataManager>();
            this.mockTimerDataManager = new Mock<IScheduleTimerDataManager>();

            services.AddSingleton<ExecutionGoalHandler>(this.mockExecutionGoalExecution);
            services.AddSingleton<IScheduleDataManager>(this.mockDataManager.Object);
            services.AddSingleton<IScheduleTimerDataManager>(this.mockTimerDataManager.Object);

            services.AddSingleton<IExperimentClient>(new Mock<IExperimentClient>().Object);
            services.AddSingleton<IExperimentTemplateDataManager>(new Mock<IExperimentTemplateDataManager>().Object);
            services.AddSingleton<IAzureKeyVault>(new Mock<IAzureKeyVault>().Object);

            this.goalBasedSchedulerExecution = new GoalBasedSchedulerExecution(services, new Mock<IConfiguration>().Object);
        }

        public void SetupDefaultBehavior()
        {
            this.mockTimerDataManager.Setup(mgr => mgr.GetTargetGoalTriggersAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<TargetGoalTrigger>() { FixtureExtensions.CreateTargetGoalTrigger() });

            this.mockDataManager.Setup(mgr => mgr.GetExecutionGoalAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Item<GoalBasedSchedule>(Guid.NewGuid().ToString(), FixtureExtensions.CreateExecutionGoalFromTemplate()));
        }

        [Test]
        [Ignore("Cron expression causes test to be inconsistent")]
        public async Task TriggerSchedulesAsyncRetrievesExpectedExecutionGoals()
        {
            string expectedExecutionGoalId = Guid.NewGuid().ToString();
            string expectedTeamName = Guid.NewGuid().ToString();

            TargetGoalTrigger targetGoal = new ("id", expectedExecutionGoalId, "target goal", "* * * * *", true, expectedTeamName, "version", DateTime.UtcNow, DateTime.UtcNow);
            this.mockTimerDataManager.Setup(mgr => mgr.GetTargetGoalTriggersAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<TargetGoalTrigger>() { targetGoal });

            this.mockDataManager.Setup(mgr => mgr.GetExecutionGoalAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, CancellationToken>((actualExecutionGoalId, actualTeamName, cancellationToken) => 
                {
                    Assert.AreEqual(expectedExecutionGoalId, actualExecutionGoalId);
                    Assert.AreEqual(expectedTeamName, actualTeamName);
                })
                .ReturnsAsync(new Item<GoalBasedSchedule>(Guid.NewGuid().ToString(), FixtureExtensions.CreateExecutionGoalFromTemplate()));

            await this.goalBasedSchedulerExecution.TriggerSchedulesAsync(CancellationToken.None).ConfigureAwait(false);

            this.mockTimerDataManager.Verify(mgr => mgr.GetTargetGoalTriggersAsync(It.IsAny<CancellationToken>()), Times.Once());
            this.mockDataManager.Verify(mgr => mgr.GetExecutionGoalAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once());
        }

        [Test]
        public async Task TriggerScheduleAsyncDoesNotTriggerNotEnabledTargetGoals()
        {
            TargetGoalTrigger targetGoal = new ("id", "execgoal", "target goal", "* * * * *", false, "teamName", "version", DateTime.UtcNow, DateTime.UtcNow);
            this.mockTimerDataManager.Setup(mgr => mgr.GetTargetGoalTriggersAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<TargetGoalTrigger>() { targetGoal });
            
            await this.goalBasedSchedulerExecution.TriggerSchedulesAsync(CancellationToken.None).ConfigureAwait(false);

            this.mockTimerDataManager.Verify(mgr => mgr.GetTargetGoalTriggersAsync(It.IsAny<CancellationToken>()), Times.Once());
            this.mockDataManager.Verify(mgr => mgr.GetExecutionGoalAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never());
        }

        [Test]
        public async Task TriggerScheduleAsyncDoesNotTriggerNotEligibleTargetGoals()
        {
            // Cron expression definition: https://crontab.guru/#0_0_31_2_*
            TargetGoalTrigger targetGoal = new ("id", "execgoal", "target goal", "0 0 31 2 *", true, "teamName", "version", DateTime.UtcNow, DateTime.UtcNow);
            this.mockTimerDataManager.Setup(mgr => mgr.GetTargetGoalTriggersAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<TargetGoalTrigger>() { targetGoal });

            await this.goalBasedSchedulerExecution.TriggerSchedulesAsync(CancellationToken.None).ConfigureAwait(false);

            this.mockTimerDataManager.Verify(mgr => mgr.GetTargetGoalTriggersAsync(It.IsAny<CancellationToken>()), Times.Once());
            this.mockDataManager.Verify(mgr => mgr.GetExecutionGoalAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never());
        }

        [Test]
        [Ignore("Cron expression causes test to be inconsistent")]
        public async Task TriggerScheduleAsyncPassesCorrectGoalBasedScheduleToExecutionGoalExecution()
        {
            GoalBasedSchedule expectedExecutionGoal = FixtureExtensions.CreateExecutionGoalFromTemplate();
            this.mockExecutionGoalExecution.OnExecuteSchedule = (actualExecutionGoal, context, token) =>
            {
                Assert.AreEqual(expectedExecutionGoal, actualExecutionGoal);
                return Task.CompletedTask;
            };

            TargetGoalTrigger targetGoal = new ("id", Guid.NewGuid().ToString(), "target goal", "* * * * *", 
                true, expectedExecutionGoal.TeamName, "version", DateTime.UtcNow, DateTime.UtcNow);
            
            this.mockTimerDataManager.Setup(mgr => mgr.GetTargetGoalTriggersAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<TargetGoalTrigger>() { targetGoal });

            this.mockDataManager.Setup(mgr => mgr.GetExecutionGoalAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Item<GoalBasedSchedule>(Guid.NewGuid().ToString(), expectedExecutionGoal));

            await this.goalBasedSchedulerExecution.TriggerSchedulesAsync(CancellationToken.None).ConfigureAwait(false);

            this.mockTimerDataManager.Verify(mgr => mgr.GetTargetGoalTriggersAsync(It.IsAny<CancellationToken>()), Times.Once());
            this.mockDataManager.Verify(mgr => mgr.GetExecutionGoalAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once());
        }

        private class TestExecutionGoalExecution : ExecutionGoalHandler
        {
            public TestExecutionGoalExecution(IServiceCollection services)
                : base(services)
            { 
            }

            public Func<GoalBasedSchedule, ScheduleContext, CancellationToken, Task> OnExecuteSchedule { get; set; }

            public override Task ExecuteExecutionGoalAsync(GoalBasedSchedule executionGoal, ScheduleContext scheduleContext, CancellationToken token)
            {
                return this.OnExecuteSchedule == null
                    ? Task.CompletedTask
                    : this.OnExecuteSchedule(executionGoal, scheduleContext, token);
            }
        }
    }
}
