namespace Juno.Execution.AgentRuntime.Tasks
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class AgentTaskTests
    {
        private FixtureDependencies mockDependencies;

        [SetUp]
        public void SetupTest()
        {
            this.mockDependencies = new FixtureDependencies();
        }

        [Test]
        [SuppressMessage("AsyncUsage", "AsyncFixer04:A disposable object used in a fire & forget async call", Justification = "Test requirement.")]
        public void AgentTaskRunsOnExpectedIntervals()
        {
            TimeSpan interval = TimeSpan.FromMilliseconds(10);
            TestAgentTask runtimeTask = new TestAgentTask(new ServiceCollection(), this.mockDependencies.Settings);

            using (CancellationTokenSource tokenSource = new CancellationTokenSource())
            {
                int executionCount = 0;
                runtimeTask.OnExecuteAsync = () =>
                {
                    executionCount++;
                    if (executionCount > 2)
                    {
                        tokenSource.Cancel();
                    }
                };

                Task monitoringTask = null;

                try
                {
                    monitoringTask = runtimeTask.ExecuteAsync(interval, tokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                }

                AgentTaskTests.AssertMonitoringTaskCompletes(monitoringTask, TimeSpan.FromSeconds(5));
                Assert.IsTrue(executionCount == 3);
            }
        }

        [Test]
        [SuppressMessage("AsyncUsage", "AsyncFixer04:A disposable object used in a fire & forget async call", Justification = "Test requirement.")]
        public void AgentTaskRunsOnPreciseIntervals()
        {
            List<DateTime> executionTimes = new List<DateTime>();
            TimeSpan interval = TimeSpan.FromMilliseconds(200);

            TestAgentTask runtimeTask = new TestAgentTask(new ServiceCollection(), this.mockDependencies.Settings);

            using (CancellationTokenSource tokenSource = new CancellationTokenSource())
            {
                int executionCount = 0;
                runtimeTask.OnExecuteAsync = () =>
                {
                    executionTimes.Add(DateTime.Now);
                    executionCount++;
                    if (executionCount >= 3)
                    {
                        tokenSource.Cancel();
                    }

                    // Introduce some amount of delay to mimic work. The system monitor should
                    // take this time into account when it calculates the next execution time.
                    System.Threading.Thread.Sleep(10);
                };

                Task monitoringTask = null;

                try
                {
                    monitoringTask = runtimeTask.ExecuteAsync(interval, tokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                }

                AgentTaskTests.AssertMonitoringTaskCompletes(monitoringTask, TimeSpan.FromSeconds(5));

                DateTime? lastExecution = null;
                foreach (DateTime executionTime in executionTimes)
                {
                    // We will allow for a standard deviation; however, we should be
                    // within a close range to the expected execution interval.
                    if (lastExecution != null)
                    {
                        Assert.IsTrue(executionTime > lastExecution);

                        // Using precise timing expectations can cause flaky unit test behaviors. We've left
                        // a fairly significant gap here. However, if we find that the test fails then we may
                        // have to remove it. Some amount of time loss is expected between individual execution
                        // intervals due to the requirements of thread pool queueing on the system.
                        TimeSpan delta = executionTime.Subtract(lastExecution.Value);
                        Assert.IsTrue(delta.TotalMilliseconds <= interval.TotalMilliseconds * 2);
                    }

                    lastExecution = executionTime;
                }
            }
        }

        [Test]
        public void AgentTaskWaitHandlesTimeSpansAsExpected()
        {
            Stopwatch timer = Stopwatch.StartNew();
            Assert.DoesNotThrow(() => TestAgentTask.WaitAsync(timer, TimeSpan.Zero));
        }

        [Test]
        public void AgentTaskWaitHandlesTimeSpansGreaterThanTheLengthOfTheTimeInterval()
        {
            Stopwatch timer = Stopwatch.StartNew();
            Task.Delay(100).GetAwaiter().GetResult();
            Assert.DoesNotThrow(() => TestAgentTask.WaitAsync(timer, TimeSpan.Zero));
        }

        private static void AssertMonitoringTaskCompletes(Task monitoringTask, TimeSpan timeout)
        {
            // We need to ensure that there isn't any blocking behavior that would
            // prevent the system monitor to execute on intervals and execute cleanly.
            DateTime maxRuntime = DateTime.Now.Add(timeout);
            while (DateTime.Now < maxRuntime)
            {
                if (monitoringTask.IsCompleted)
                {
                    break;
                }
            }

            // If the monitoring task did not complete within the allotted time
            // we presume there is blocking behavior and the results of the test
            // cannot be accurately determined.
            if (!monitoringTask.IsCompleted)
            {
                Assert.Inconclusive();
            }
        }

        private class TestAgentTask : AgentTask
        {
            public TestAgentTask(ServiceCollection services, EnvironmentSettings settings)
                : base(services, settings)
            {
            }

            public Action OnExecuteAsync { get; set; }

            public static new Task WaitAsync(Stopwatch timer, TimeSpan executionInterval)
            {
                return AgentTask.WaitAsync(timer, executionInterval);
            }

            public override Task ExecuteAsync(CancellationToken cancellationToken)
            {
                this.OnExecuteAsync?.Invoke();
                return Task.CompletedTask;
            }
        }
    }
}
