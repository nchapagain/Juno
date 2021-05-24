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
    public class BmcVerificationProviderTests
    {
        private ProviderFixture mockFixture;
        private BmcVerificationProvider provider;
        private Mock<IFirmwareReader<BmcInfo>> mockFirmwareReader;

        [OneTimeSetUp]
        public void SetupTest()
        {
            this.mockFixture = new ProviderFixture(typeof(BmcVerificationProvider));
            this.mockFirmwareReader = new Mock<IFirmwareReader<BmcInfo>>();
            this.mockFixture.Services.AddSingleton(this.mockFirmwareReader.Object);
            this.provider = new BmcVerificationProvider(this.mockFixture.Services);
        }

        [SetUp]
        public void SetupDefaultMockBehavior()
        {
            this.mockFixture.Component.Parameters.Clear();
            this.mockFixture.Component.Parameters.AddRange(new Dictionary<string, IConvertible>
            {
                { "bmcVersion", "1.8.5" }
            });

            this.mockFirmwareReader.Reset();
            this.mockFirmwareReader.Setup(r => r.Read())
                .Returns(new BmcInfo("1.8.5"));
        }

        [Test]
        public async Task VerifyBmcVersionProviderReturnsSuccesseOnMatchingBmcVersion()
        {
            ExecutionResult result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(ExecutionStatus.Succeeded, result.Status);

            this.mockFirmwareReader.Verify(r => r.Read(), Times.Once());
        }

        [Test]
        public async Task VerifyBmcVersionProviderReturnsFailedOnNotMatchingBmcVersion()
        {
            this.mockFixture.Component.Parameters["bmcVersion"] = "1.8.4";
            var result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);
            Assert.AreEqual(ExecutionStatus.Failed, result.Status);

            this.mockFirmwareReader.Verify(r => r.Read(), Times.Once());
        }
    }
}
