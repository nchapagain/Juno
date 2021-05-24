namespace Juno.Execution.Providers.Payloads
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Execution.AgentRuntime.Windows;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class WriteMsrProviderTests
    {
        private ProviderFixture fixture;
        private WriteMsrProvider provider;
        private Mock<IMsr> mockMsr;

        [SetUp]
        public void SetupTest()
        {
            this.fixture = new ProviderFixture(typeof(WriteMsrProvider));
            this.mockMsr = new Mock<IMsr>();
            this.fixture.Services.AddTransient<IMsr>((p) => this.mockMsr.Object);
            this.provider = new WriteMsrProvider(this.fixture.Services);

            this.fixture.Component.Parameters.Clear();
            this.fixture.Component.Parameters.AddRange(new Dictionary<string, IConvertible> 
            {
                { "msrRegister", "0x102" },
                { "value", "0x0000000000000002" },
                { "processorCount", "80" }
            });

            this.mockMsr.Reset();
            this.mockMsr.Setup(m => m.Write(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()));
        }

        [Test]
        public async Task WriteMsrProviderSucceedsWhenAgentIsAbleToWriteToMsrsSuccessfully()
        {
            ExecutionResult result = await this.provider.ExecuteAsync(this.fixture.Context, this.fixture.Component, CancellationToken.None);

            Assert.AreEqual(ExecutionStatus.Succeeded, result.Status);
        }

        [Test]
        public async Task WriteMsrProviderFailsWhenAgentIsUnableToWriteToMsrsSuccessfully()
        {
            this.mockMsr.Setup(m => m.Write(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Throws(new ProcessExecutionException());

            ExecutionResult result = await this.provider.ExecuteAsync(this.fixture.Context, this.fixture.Component, CancellationToken.None);

            Assert.AreEqual(ExecutionStatus.Failed, result.Status);
        }
    }
}
