namespace Juno.Scheduler.Actions
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Threading;
    using AutoFixture;
    using Juno.Contracts;
    using Juno.DataManagement;
    using Juno.Providers;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class JunoExecutionGoalDisableProviderTests
    {
        private static string executionGoalName = "executionGoalName";
        private CancellationToken cancellationToken = CancellationToken.None;
        private Mock<IScheduleTimerDataManager> scheduleDataMgr;
        private Fixture mockFixture;
        private IServiceCollection services;
        private TargetGoalTrigger gbScheduleTrigger;
        private ScheduleContext mockContext;

        [SetUp]
        public void SetupTest()
        {
            this.scheduleDataMgr = new Mock<IScheduleTimerDataManager>();
            this.mockFixture = new Fixture();
            this.mockFixture.SetUpGoalBasedScheduleMocks();

            this.services = new ServiceCollection();
            this.services.AddSingleton<IScheduleTimerDataManager>(this.scheduleDataMgr.Object);

            this.gbScheduleTrigger = this.mockFixture.Create<TargetGoalTrigger>();
            this.mockContext = new ScheduleContext(this.mockFixture.Create<GoalBasedSchedule>(), this.mockFixture.Create<TargetGoalTrigger>(), new Mock<IConfiguration>().Object);
        }

        [Test]
        public void ProviderReturnsTheExpectedResponseWhenExecutionGoalDisableIsCalled()
        {
            string executionGoalName = "ScheduleName";
            ScheduleAction component = this.mockFixture.Create<ScheduleAction>();
            component.Parameters.Add(JunoExecutionGoalDisableProviderTests.executionGoalName, executionGoalName);

            // Creating cosmos table that gets ExecutionGoal Timer Trigger
            this.scheduleDataMgr.Setup(x => x.GetTargetGoalTriggersAsync(this.cancellationToken))
                .ReturnsAsync(new List<TargetGoalTrigger>
                {
                    this.gbScheduleTrigger
                });

            // Created cosmos table that is returned after updating ExecutionGoal Timer Trigger
            this.scheduleDataMgr.Setup(x => x.UpdateTargetGoalTriggerAsync(this.gbScheduleTrigger, this.cancellationToken))
                .Callback<TargetGoalTrigger, CancellationToken>((entity, token) => 
                {
                    Assert.IsFalse(entity.Enabled);      
                });

            ScheduleActionProvider provider = new JunoExecutionGoalDisableProvider(this.services);

            var response = provider.ExecuteActionAsync(component, this.mockContext, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(response.Status, ExecutionStatus.Succeeded);
        }
    }
}
