namespace Juno.Execution.AgentRuntime.Tasks
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class VmUptimeMonitorTaskTests
    {
        private VmUptimeMonitorTask testMonitor;
        private FixtureDependencies mockDependencies;
        private Mock<ILogger> mockLogger;

        [SetUp]
        public void SetupTest()
        {
            this.mockDependencies = new FixtureDependencies();
            this.mockLogger = new Mock<ILogger>();
            ServiceCollection mockCollection = new ServiceCollection();
            mockCollection.AddSingleton<ILogger>(this.mockLogger.Object);
            this.testMonitor = new VmUptimeMonitorTask(mockCollection, this.mockDependencies.Settings);
        }

        [Test]
        public async Task VmUptimeMonitorCanReadTimeProperly()
        {
            this.mockLogger.Setup(l => l.Log<EventContext>(LogLevel.Information, It.IsAny<EventId>(), It.IsAny<EventContext>(), null, null))
                .Callback<LogLevel, EventId, EventContext, Exception, Func<EventContext, Exception, string>>((level, id, context, exception, func) =>
                {
                    if (id.Name == $"{nameof(VmUptimeMonitorTask)}Start")
                    {
                        Assert.IsTrue(context.Properties.ContainsKey("lastRtc"));
                        Assert.IsTrue(context.Properties.ContainsKey("lastCpuUptime"));
                    }
                    else if (id.Name == $"{nameof(VmUptimeMonitorTask)}Stop")
                    {
                        Assert.IsTrue(context.Properties.ContainsKey("lastRtc"));
                        Assert.IsTrue(context.Properties.ContainsKey("lastCpuUptime"));
                        Assert.IsTrue(context.Properties.ContainsKey("newRtc"));
                        Assert.IsTrue(context.Properties.ContainsKey("newCpuUptime"));

                        Assert.IsTrue(TimeSpan.TryParse((string)context.Properties["difference"], out TimeSpan result));
                        Console.WriteLine(result);
                    }
                }).Verifiable();

            for (int iteration = 0; iteration < 50; iteration++)
            {
                await this.testMonitor.ExecuteAsync(CancellationToken.None);
            }
            
            this.mockLogger.Verify();
        }
    }
}
