namespace Juno.Execution.Providers.Verification
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Contracts;
    using Juno.Execution.AgentRuntime.Contract;
    using Juno.Execution.AgentRuntime.Windows;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class FpgaVerificationProviderTests
    {
        private FpgaVerificationProvider provider;
        private Mock<IFirmwareReader<FpgaHealth>> reader;
        private ProviderFixture fixture;

        [OneTimeSetUp]
        public void SetupTests()
        {
            this.fixture = new ProviderFixture(typeof(FpgaVerificationProvider));
            this.reader = new Mock<IFirmwareReader<FpgaHealth>>();
            this.fixture.Services.AddSingleton(this.reader.Object);

            this.provider = new FpgaVerificationProvider(this.fixture.Services);
        }

        [SetUp]
        public void SetupDefaultMockBehavior()
        {
            string defaultBoardName = Guid.NewGuid().ToString();
            string defaultRoleId = Guid.NewGuid().ToString();
            string defaultRoleVersion = Guid.NewGuid().ToString();
            bool defaultIsGolden = true;
            this.fixture.Component.Parameters.Clear();
            this.fixture.Component.Parameters.AddRange(new Dictionary<string, IConvertible>()
            {
                { "boardName", defaultBoardName },
                { "roleId", defaultRoleId },
                { "roleVersion", defaultRoleVersion },
                { "isGolden", defaultIsGolden }
            });

            this.reader.Reset();
            this.reader.Setup(r => r.Read())
                .Returns(this.CreateFpgaHealth(defaultBoardName, defaultRoleId, defaultRoleVersion, defaultIsGolden));
        }

        [Test]
        [TestCase("boardName", "boardName")]
        [TestCase("roleId", "roleId")]
        [TestCase("roleVersion", "roleVersion")]
        [TestCase("isGolden", false)]
        public async Task ExecuteAsyncReturnsFailedWhenFpgaConfigurationsDoNotMatch(string parameter, IConvertible value)
        {
            this.fixture.Component.Parameters[parameter] = value;
            ExecutionResult result = await this.provider.ExecuteAsync(this.fixture.Context, this.fixture.Component, CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual(ExecutionStatus.Failed, result.Status);

            this.reader.Verify(r => r.Read(), Times.Once());
        }

        [Test]
        public async Task ExecuteAsyncReturnsSuceededWhenFpgaConfigurationMatch()
        {
            ExecutionResult result = await this.provider.ExecuteAsync(this.fixture.Context, this.fixture.Component, CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(ExecutionStatus.Succeeded, result.Status);

            this.reader.Verify(r => r.Read(), Times.Once());
        }

        private FpgaHealth CreateFpgaHealth(string boardName = "defaultBoardName", string roleId = "defaultRoleId", string roleVersion = "defaultRoleVersion", bool isGolden = false)
        {
            return new FpgaHealth(
                new FpgaConfig(true, boardName, roleId, roleVersion, string.Empty, string.Empty, isGolden),
                this.fixture.Create<FpgaTemperature>(),
                this.fixture.Create<FpgaNetwork>(),
                this.fixture.Create<FpgaID>(),
                this.fixture.Create<FpgaClockReset>(),
                this.fixture.Create<FpgaPcie>(),
                this.fixture.Create<FpgaDram>(),
                this.fixture.Create<FpgaCables>());
        }
    }
}
