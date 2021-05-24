namespace Juno.Execution.AgentRuntime
{
    using System;
    using System.Threading;
    using Juno.Execution.AgentRuntime.Contract;
    using Juno.Execution.Providers.Payloads;
    using Microsoft.Extensions.Logging;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class MicrocodeActivatorTests
    {
        private CancellationTokenSource cancellationTokensource;
        private Mock<ISystemPropertyReader> mockPropertyReader;
        private Mock<ILogger> mockLogger;
        private TimeSpan mockRegistryReadOnly;
    
        [SetUp]
        public void SetupTest()
        {
            this.cancellationTokensource = new CancellationTokenSource();
            this.mockPropertyReader = new Mock<ISystemPropertyReader>();
            this.mockPropertyReader.Setup(r => r.Read(AzureHostProperty.CpuMicrocodeVersion)).Returns("0b000039");
            this.mockPropertyReader.Setup(r => r.Read(AzureHostProperty.CpuMicrocodeUpdateStatus)).Returns("0");
            this.mockRegistryReadOnly = TimeSpan.Zero;
            this.mockLogger = new Mock<ILogger>();
        }

        [Test]
        public void MicrocodeActivatorReturnsTheExpectedResultWhenTheMicrocodeVersionIsConfirmedToBeActivated()
        {
            var expectedMicrocodeVersion = "b000039";

            IPayloadActivator validMicrocode = new MicrocodeActivator(
                expectedMicrocodeVersion,
                TimeSpan.Zero, 
                this.mockPropertyReader.Object,
                this.mockLogger.Object);

            var result = validMicrocode.ActivateAsync(this.cancellationTokensource.Token).GetAwaiter().GetResult();

            Assert.AreNotEqual(result.ActivationTime.ToString(), DateTime.MinValue.ToString());
            Assert.AreEqual(result.IsActivated, true);
            Assert.IsNotNull(result.ActivationTime);
        }

        [Test]
        public void MicrocodeActivatorReturnsTheExpectedResultWhenTheMicrocodeVersionIsNotConfirmedToBeActivated()
        {
            // Use a microcode version that will not match the one returned by
            // the activator
            var expectedMicrocodeVersion = "y000036";

            IPayloadActivator validMicrocode = new MicrocodeActivator(
                expectedMicrocodeVersion,
                TimeSpan.Zero,
                this.mockPropertyReader.Object,
                this.mockLogger.Object);

            var result = validMicrocode.ActivateAsync(this.cancellationTokensource.Token).GetAwaiter().GetResult();

            Assert.AreEqual(result.IsActivated, false);
            Assert.IsNull(result.ActivationTime);
        }
    }
}
