namespace Juno.Execution.Providers.Verification
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Execution.AgentRuntime.Windows;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class RegistryVerificationProviderTests
    {
        private ProviderFixture mockFixture;
        private RegistryVerificationProvider provider;
        private Mock<IRegistryHelper> mockRegistry;

        [OneTimeSetUp]
        public void SetupTest()
        {
            this.mockRegistry = new Mock<IRegistryHelper>();           
            this.mockFixture = new ProviderFixture(typeof(RegistryVerificationProvider));

            this.mockFixture.Services.AddSingleton(this.mockRegistry.Object);
            this.provider = new RegistryVerificationProvider(this.mockFixture.Services);
        }

        [SetUp]
        public void SetupDefaultBehavior()
        {
            string expectedValue = "someExpectedValue";
            this.mockFixture.Component.Parameters.Clear();
            this.mockFixture.Component.Parameters.AddRange(new Dictionary<string, IConvertible>
            {
                { "keyName", @"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0" },
                { "valueName", "VendorIdentifier" },
                { "type", "System.String" },
                { "expectedValue", expectedValue }
            });

            this.mockRegistry.Reset();
            this.mockRegistry.Setup(r => r.ReadRegistryKeyByType(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Type>()))
                .Returns(expectedValue);
        }

        [Test]
        public async Task ExecuteAsyncPostsCorrectParametersToRegistryHelper()
        {
            string expectedKeyName = Guid.NewGuid().ToString();
            string expectedValueName = Guid.NewGuid().ToString();
            string expectedType = "Sytem.String";
            this.mockFixture.Component.Parameters["keyName"] = expectedKeyName;
            this.mockFixture.Component.Parameters["valueName"] = expectedValueName;
            this.mockFixture.Component.Parameters["type"] = expectedType;

            this.mockRegistry.Setup(r => r.ReadRegistryKeyByType(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Type>()))
                .Callback<string, string, Type>((key, value, type) =>
                {
                    Assert.AreEqual(expectedKeyName, key);
                    Assert.AreEqual(expectedValueName, value);
                    Assert.AreEqual(typeof(string), type);
                }).Returns(Guid.NewGuid().ToString());

            ExecutionResult result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None).ConfigureAwait(false);

            Assert.IsNotNull(result);

            this.mockRegistry.Verify(r => r.ReadRegistryKeyByType(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Type>()), Times.Once());
        }

        [Test]
        public async Task ExecuteAsyncReturnsExpectedResultWhenRegistryDoesNotMatch()
        {
            this.mockRegistry.Setup(r => r.ReadRegistryKeyByType(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Type>()))
                .Returns(Guid.NewGuid().ToString());

            ExecutionResult result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(ExecutionStatus.Failed, result.Status);

            this.mockRegistry.Verify(r => r.ReadRegistryKeyByType(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Type>()), Times.Once());
        }

        [Test]
        public async Task ExecuteAsyncReturnsExpectedResultWhenRegistryValuesMatch()
        {
            ExecutionResult result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(ExecutionStatus.Succeeded, result.Status);

            this.mockRegistry.Verify(r => r.ReadRegistryKeyByType(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Type>()), Times.Once());
        }
    }
}
