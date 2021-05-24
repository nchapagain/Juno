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
    public class MsrVerificationProviderTests
    {
        private ProviderFixture fixture;
        private MsrVerificationProvider provider;
        private Mock<IMsr> msr;

        [SetUp]
        public void SetupTest()
        {
            this.fixture = new ProviderFixture(typeof(MsrVerificationProvider));
            this.msr = new Mock<IMsr>();
            this.fixture.Services.AddSingleton(this.msr.Object);
            this.provider = new MsrVerificationProvider(this.fixture.Services);
        }

        [SetUp]
        public void SetupDefualtMockBehavior()
        {
            this.fixture.Component.Parameters.Clear();
            this.fixture.Component.Parameters.AddRange(new Dictionary<string, IConvertible>
            {
                { "msrRegister", "0x102" },
                { "expectedMsrValue", "0x0000000000000002" },
            });

            this.msr.Reset();
            this.msr.Setup(r => r.Read(It.IsAny<string>(), It.IsAny<string>()))
                .Returns("0x0000000000000002");
        }

        [Test]
        public async Task ExecuteAsyncPostsCorrectValuesToReader()
        {
            string expectedMsrRegister = Guid.NewGuid().ToString();
            string expectedCpuNumber = Guid.NewGuid().ToString();
            this.fixture.Component.Parameters["msrRegister"] = expectedMsrRegister;
            this.fixture.Component.Parameters.Add("cpuNumber", expectedCpuNumber);

            this.msr.Setup(r => r.Read(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((register, number) => 
                {
                    Assert.AreEqual(expectedMsrRegister, register);
                    Assert.AreEqual(expectedCpuNumber, number);
                })
                .Returns("0x0000000000000002");

            await this.provider.ExecuteAsync(this.fixture.Context, this.fixture.Component, CancellationToken.None).ConfigureAwait(false);

            this.msr.Verify(r => r.Read(It.IsAny<string>(), It.IsAny<string>()), Times.Once());
        }

        [Test]
        public async Task ExecuteAsyncReturnsExpectedResultWhenMsrRegisterValuesAreNotTheSame()
        {
            this.fixture.Component.Parameters["expectedMsrValue"] = Guid.NewGuid().ToString();

            ExecutionResult result = await this.provider.ExecuteAsync(this.fixture.Context, this.fixture.Component, CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual(ExecutionStatus.Failed, result.Status);

            this.msr.Verify(r => r.Read(It.IsAny<string>(), It.IsAny<string>()), Times.Once());
        }

        [Test]
        public async Task ExecuteAsyncReturnsExpectedResultWhenMsrRegisterValuesAreTheSame()
        {
            ExecutionResult result = await this.provider.ExecuteAsync(this.fixture.Context, this.fixture.Component, CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(ExecutionStatus.Succeeded, result.Status);

            this.msr.Verify(r => r.Read(It.IsAny<string>(), It.IsAny<string>()), Times.Once());
        }
    }
}
