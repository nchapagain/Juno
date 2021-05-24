namespace Juno.Execution.AgentRuntime.Tasks
{
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class AgentSystemMonitorTests
    {
        private FixtureDependencies mockDependencies;

        [SetUp]
        public void SetupTest()
        {
            this.mockDependencies = new FixtureDependencies();
        }

        [Test]
        public async Task SystemMonitorInvokesTheExpectedResultsEvent()
        {
            bool eventInvoked = false;

            TestAgentMonitorTask monitor = new TestAgentMonitorTask(new ServiceCollection(), this.mockDependencies.Settings);
            monitor.Results += (sender, args) => eventInvoked = true;

            await monitor.ExecuteAsync(CancellationToken.None);

            Assert.IsTrue(eventInvoked);
        }

        private class TestAgentMonitorTask : AgentMonitorTask<string>
        {
            public TestAgentMonitorTask(ServiceCollection services, EnvironmentSettings settings)
                : base(services, settings)
            {
            }

            public override Task ExecuteAsync(CancellationToken cancellationToken)
            {
                this.OnResults(new ExecutionEventArgs<string>("Snapshot event data"));
                return Task.CompletedTask;
            }
        }
    }
}
