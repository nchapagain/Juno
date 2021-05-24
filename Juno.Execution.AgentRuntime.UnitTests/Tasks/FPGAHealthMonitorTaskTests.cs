namespace Juno.Execution.AgentRuntime.Tasks
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Execution.AgentRuntime.Contract;
    using Juno.Execution.AgentRuntime.Windows;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class FpgaHealthMonitorTaskTests
    {
        private FpgaHealthMonitorTask fpgaHealthMonitorTask;
        private Mock<IFirmwareReader<FpgaHealth>> fpgaReader;
        private Fixture mockFixture;
        private Mock<ILogger> mockLogger;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.fpgaReader = new Mock<IFirmwareReader<FpgaHealth>>();
            this.mockLogger = new Mock<ILogger>();
            var mockDependencies = new FixtureDependencies();
            ServiceCollection mockCollection = new ServiceCollection();
            mockCollection.AddSingleton<IFirmwareReader<FpgaHealth>>(this.fpgaReader.Object);
            mockCollection.AddSingleton<ILogger>(this.mockLogger.Object);
            this.fpgaHealthMonitorTask = new FpgaHealthMonitorTask(mockCollection, mockDependencies.Settings);
        }

        [Test]
        public async Task FpgaHealthMonitorTaskCapturesTheExpectedInformationFromTheHost()
        {
            this.fpgaReader.Setup(f => f.CanRead(typeof(FpgaHealth))).Returns(true);
            this.fpgaReader.Setup(f => f.Read()).Returns(this.mockFixture.Create<FpgaHealth>());
            this.mockLogger.Setup(l => l.Log<EventContext>(LogLevel.Information, It.IsAny<EventId>(), It.IsAny<EventContext>(), null, null))
                .Callback<LogLevel, EventId, EventContext, Exception, Func<EventContext, Exception, string>>((level, id, context, exception, func) =>
                {
                    if (id.Name == $"{nameof(SystemInformationMonitorTask)}.InfoStop")
                    {
                        Assert.IsTrue(context.Properties.ContainsKey("fpgaHealth"));
                        var fpgaHealth = (FpgaHealth)context.Properties["fpgaHealth"];
                        Assert.IsNotNull(fpgaHealth.FPGAConfig);
                        Assert.IsNotNull(fpgaHealth.FPGAID);
                        Assert.IsNotNull(fpgaHealth.FPGAClockReset);
                        Assert.IsNotNull(fpgaHealth.FPGATemperature);
                        Assert.IsNotNull(fpgaHealth.FPGANetwork);
                        Assert.IsNotNull(fpgaHealth.FPGAPcie);
                        Assert.IsNotNull(fpgaHealth.FPGADram);
                        Assert.IsNotNull(fpgaHealth.FPGACables);
                    }
                }).Verifiable();

            await this.fpgaHealthMonitorTask.ExecuteAsync(CancellationToken.None);

            this.mockLogger.Verify();
        }
    }
}
