namespace Juno.Execution.AgentRuntime.Tasks
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts.Configuration;
    using Juno.Execution.AgentRuntime.Properties;
    using Microsoft.Extensions.DependencyInjection;
    using NUnit.Framework;
    using Polly;

    [TestFixture]
    [Category("Unit")]
    public class SelLogMonitorTaskTests
    {
        private TestSelLogMonitor selLogMonitor;
        private FixtureDependencies mockDependencies;

        [SetUp]
        public void SetupTest()
        {
            this.mockDependencies = new FixtureDependencies();
            this.selLogMonitor = new TestSelLogMonitor(new ServiceCollection(), this.mockDependencies.Settings);
        }

        [Test]
        public async Task SelLogMonitorTaskInvokesTheExpectedEventWhenLogDataIsAvailable()
        {
            using (CancellationTokenSource tokenSource = new CancellationTokenSource())
            {
                bool eventInvoked = false;
                this.selLogMonitor.Results += (sender, args) => eventInvoked = true;

                await this.selLogMonitor.ExecuteAsync(tokenSource.Token);

                Assert.IsTrue(eventInvoked);
            }
        }

        [Test]
        public async Task SelLogMonitorTaskPassesSelLogDataToSubscribersOfTheEventWhenSelLogDataIsAvailable()
        {
            using (CancellationTokenSource tokenSource = new CancellationTokenSource())
            {
                bool eventInvoked = false;
                this.selLogMonitor.Results += (sender, args) =>
                {
                    eventInvoked = true;
                    Assert.AreEqual(this.selLogMonitor.SelLogData, args.Results);
                };

                await this.selLogMonitor.ExecuteAsync(tokenSource.Token);

                Assert.IsTrue(eventInvoked);
            }
        }

        [Test]
        public async Task SelLogMonitorTaskAppliesTheRetryPolicyDefinedOnFailureToReadTheSelLogSuccessfully()
        {
            int expectedRetryCount = 3;
            IAsyncPolicy expectedRetryPolicy = Policy.Handle<Exception>().RetryAsync(retryCount: expectedRetryCount);

            this.selLogMonitor = new TestSelLogMonitor(new ServiceCollection(), this.mockDependencies.Settings, expectedRetryPolicy)
            {
                // The SEL log monitor task does not consider the results valid unless
                // the terms 'Completed Successfully' are in the output.
                SelLogData = "Error: cannot open IPMI driver"
            };

            using (CancellationTokenSource tokenSource = new CancellationTokenSource())
            {
                int attempts = 0;
                this.selLogMonitor.OnExecuteProcess = (command, args) =>
                {
                    attempts++;
                };

                await this.selLogMonitor.ExecuteAsync(tokenSource.Token);

                // 1 attempt + 3 retries -> expectedRetryCount + 1
                Assert.IsTrue(attempts == expectedRetryCount + 1);
            }
        }

        private class TestSelLogMonitor : SelLogMonitorTask
        {
            public TestSelLogMonitor(ServiceCollection services, EnvironmentSettings settings, IAsyncPolicy retryPolicy = null)
               : base(services, settings, retryPolicy)
            {
            }

            // Note:
            // The format of the SEL log data must be a valid format. The logic of the
            // SEL log monitor checks for output that indicates success. The output must
            // have the terms 'Completed Successfully' or the monitor will retry up to 
            // a certain number of times.
            public string SelLogData { get; set; } = Resources.ExampleSelLogOutput;

            public Action<string, string> OnExecuteProcess { get; set; }

            protected override void ExecuteProcess(string exePath, string arguments, out string standardOutput)
            {
                standardOutput = this.SelLogData;
                this.OnExecuteProcess?.Invoke(exePath, arguments);
            }
        }
    }
}
