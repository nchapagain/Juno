namespace Juno.Execution.AgentRuntime.Tasks
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Execution.AgentRuntime.Contract;
    using Juno.Execution.AgentRuntime.Windows;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class SystemInformationMonitorTaskTests
    {
        private SystemInformationMonitorTask testMonitor;
        private FixtureDependencies mockDependencies;
        private Mock<ILogger> mockLogger;
        private Mock<ISystemPropertyReader> mockSystemPropertyReader;
        private Mock<IMsr> mockMsr;
        private Mock<IFirmwareReader<BiosInfo>> mockBiosPropertyReader;

        [SetUp]
        public void SetupTest()
        {
            this.mockDependencies = new FixtureDependencies();
            this.mockLogger = new Mock<ILogger>();
            this.mockSystemPropertyReader = new Mock<ISystemPropertyReader>();
            this.mockMsr = new Mock<IMsr>();
            this.mockBiosPropertyReader = new Mock<IFirmwareReader<BiosInfo>>();
            ServiceCollection mockCollection = new ServiceCollection();
            mockCollection.AddSingleton<ILogger>(this.mockLogger.Object);
            mockCollection.AddSingleton<ISystemPropertyReader>(this.mockSystemPropertyReader.Object);
            mockCollection.AddSingleton<IMsr>(this.mockMsr.Object);
            mockCollection.AddSingleton<IFirmwareReader<BiosInfo>>(this.mockBiosPropertyReader.Object);
            this.testMonitor = new SystemInformationMonitorTask(mockCollection, this.mockDependencies.Settings);
        }

        [Test]
        public async Task SystemInformationMonitorTaskCapturesTheExpectedInformationFromTheHost()
        {
            this.mockSystemPropertyReader.Setup(h => h.Read(It.IsAny<AzureHostProperty>())).Returns("hostPropertyData");
            this.mockMsr.Setup(m => m.Read(It.IsAny<string>(), It.IsAny<string>())).Returns("msrinfo");
            this.mockBiosPropertyReader.Setup(m => m.Read()).Returns(new BiosInfo("info", "more info", "even more info"));

            this.mockLogger.Setup(l => l.Log<EventContext>(LogLevel.Information, It.IsAny<EventId>(), It.IsAny<EventContext>(), null, null))
                .Callback<LogLevel, EventId, EventContext, Exception, Func<EventContext, Exception, string>>((level, id, context, exception, func) =>
                {
                    if (id.Name == $"{nameof(SystemInformationMonitorTask)}.InfoStop")
                    {
                        Assert.IsTrue(context.Properties.ContainsKey("osInfo"));
                        Assert.IsTrue(context.Properties.ContainsKey("biosInfo"));
                        Assert.IsTrue(context.Properties.ContainsKey("nodeInfo"));
                        Assert.IsTrue(context.Properties.ContainsKey("cpuInfo"));
                    }
                }).Verifiable();

            await this.testMonitor.ExecuteAsync(CancellationToken.None);

            this.mockLogger.Verify();
        }
    }
}
