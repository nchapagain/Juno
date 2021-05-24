namespace Juno.Execution.Providers.Verification
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Execution.AgentRuntime.Contract;
    using Juno.Execution.AgentRuntime.Windows;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class BiosVerificationProviderTests
    {
        private ProviderFixture fixture;
        private BiosVerificationProvider provider;
        private Mock<IFirmwareReader<BiosInfo>> mockReader;

        [OneTimeSetUp]
        public void SetupTest()
        {
            this.fixture = new ProviderFixture(typeof(BiosVerificationProvider));
            this.mockReader = new Mock<IFirmwareReader<BiosInfo>>();
            this.fixture.Services.AddSingleton(this.mockReader.Object);
            this.provider = new BiosVerificationProvider(this.fixture.Services);
        }

        [SetUp]
        public void SetupDefaultMockBehavior()
        {
            this.fixture.Component.Parameters.Clear();
            this.fixture.Component.Parameters.AddRange(new Dictionary<string, IConvertible>
            {
                { "biosVersion", @"C2010.BS.3F38.GN3" },
            });

            this.mockReader.Reset();
            this.mockReader.Setup(r => r.Read())
                .Returns(new BiosInfo("C2010.BS.3f38.GN3", "vendor", "spsVersion"));
        }

        [Test]
        [TestCase("C2011.BS.3f38.GN3")]
        [TestCase("C2010.BF.3f38.GN3")]
        [TestCase("C2010.BS.3f39.GN3")]
        [TestCase("C2010.BS.3f38.GN4")]
        public async Task ExecuteAysncReturnsExpectedResultWhenAPartIsDifferent(string biosVersion)
        {
            this.fixture.Component.Parameters["biosVersion"] = biosVersion;
            ExecutionResult result = await this.provider.ExecuteAsync(this.fixture.Context, this.fixture.Component, CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual(ExecutionStatus.Failed, result.Status);
            this.mockReader.Verify(r => r.Read(), Times.Once());
        }

        [Test]
        [TestCase("C2010.Bs.3f38.GN3")]
        [TestCase("C2010.BS.3f38.AN3")]
        [TestCase("C2010.BS.3f38.GB3")]
        public async Task ExecuteAsyncReturnsExpectedResultWhenAllPartsAreTheSame(string biosVersion)
        {
            this.fixture.Component.Parameters["biosVersion"] = biosVersion;
            ExecutionResult result = await this.provider.ExecuteAsync(this.fixture.Context, this.fixture.Component, CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual(ExecutionStatus.Succeeded, result.Status);
            this.mockReader.Verify(r => r.Read(), Times.Once());
        }
    }
}
