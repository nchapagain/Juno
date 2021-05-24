namespace Juno.Execution.Providers.Payloads
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
    public class SetRegistryKeyProviderTests
    {
        private ProviderFixture mockFixture;
        private SetRegistryKeyProvider provider;
        private Mock<IRegistry> mockRegistry;

        [SetUp]
        public void SetupTest()
        {
            this.mockRegistry = new Mock<IRegistry>();
            this.mockRegistry.Setup(r => r.Read<IConvertible>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IConvertible>())).Returns(string.Empty);
            this.mockFixture = new ProviderFixture(typeof(SetRegistryKeyProvider));
            this.mockFixture.Component.Parameters.Clear();
            this.mockFixture.Component.Parameters.AddRange(new Dictionary<string, IConvertible>
            {
                { "keyName", @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Hypervisor" },
                { "valueName", "hypervisorloadoptions" },
                { "value", "disablepostedinterrupts" },
                { "type", "System.String" },
                { "shouldAppend", "true" }
            });
            this.mockRegistry.Setup(r => r.Read<dynamic>(
                @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Hypervisor",
                "hypervisorloadoptions",
                string.Empty)).Returns("ENABLEMAKEROOTUNCACHEABLE");
        }

        [Test]
        public async Task SetRegistryKeyProviderReturnsSuccessWhenKeyExists()
        {
            this.mockRegistry.Setup(r => r.Write(
                @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Hypervisor",
                "hypervisorloadoptions",
                "ENABLEMAKEROOTUNCACHEABLE disablepostedinterrupts"))
                .Verifiable();
            this.mockFixture.Services.AddSingleton(this.mockRegistry.Object);
            this.provider = new SetRegistryKeyProvider(this.mockFixture.Services);
            await this.provider.ConfigureServicesAsync(this.mockFixture.Context, this.mockFixture.Component);

            ExecutionResult result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            Assert.AreEqual(ExecutionStatus.Succeeded, result.Status);
            this.mockRegistry.Verify();
        }

        [Test]
        public async Task SetRegistryKeyProviderReturnsFailureWhenKeyDoesNotExist()
        {
            this.mockFixture.Component.Parameters.Clear();
            this.mockFixture.Component.Parameters.AddRange(new Dictionary<string, IConvertible>
            {
                { "keyName", @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\nonexistentkey" },
                { "valueName", "hypervisorloadoptions" },
                { "value", "disablepostedinterrupts" },
                { "type", "System.String" },
                { "shouldAppend", "true" }
            });
            this.mockRegistry.Setup(r => r.Write(
                @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\nonexistentkey",
                "hypervisorloadoptions",
                "disablepostedinterrupts"))
                .Throws(new Exception())
                .Verifiable();
            this.mockFixture.Services.AddSingleton(this.mockRegistry.Object);
            this.provider = new SetRegistryKeyProvider(this.mockFixture.Services);
            await this.provider.ConfigureServicesAsync(this.mockFixture.Context, this.mockFixture.Component);

            ExecutionResult result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            Assert.AreEqual(ExecutionStatus.Failed, result.Status);
            this.mockRegistry.Verify();
        }

        [Test]
        public async Task SetRegistryKeyProviderReturnsSuccessAndOverwritesWhenAppendIsFalse()
        {
            this.mockFixture.Component.Parameters.Clear();
            this.mockFixture.Component.Parameters.AddRange(new Dictionary<string, IConvertible>
            {
                { "keyName", @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Hypervisor" },
                { "valueName", "hypervisorloadoptions" },
                { "value", "disablepostedinterrupts" },
                { "type", "System.String" },
                { "shouldAppend", "false" }
            });
            this.mockRegistry.Setup(r => r.Write(
                @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Hypervisor",
                "hypervisorloadoptions",
                "disablepostedinterrupts"))
                .Verifiable();
            this.mockFixture.Services.AddSingleton(this.mockRegistry.Object);
            this.provider = new SetRegistryKeyProvider(this.mockFixture.Services);
            await this.provider.ConfigureServicesAsync(this.mockFixture.Context, this.mockFixture.Component);

            ExecutionResult result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            Assert.AreEqual(ExecutionStatus.Succeeded, result.Status);
            this.mockRegistry.Verify();
        }

        [Test]
        public async Task SetRegistryKeyProviderReturnsSuccessAndCreatesValueWhenNotExists()
        {
            this.mockFixture.Component.Parameters.Clear();
            this.mockFixture.Component.Parameters.AddRange(new Dictionary<string, IConvertible>
            {
                { "keyName", @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Hypervisor" },
                { "valueName", "nonexistentvaluename" },
                { "value", "disablepostedinterrupts" },
                { "type", "System.String" }
            });
            this.mockRegistry.Setup(r => r.Write(
                @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Hypervisor",
                "nonexistentvaluename",
                "disablepostedinterrupts"))
                .Verifiable();
            this.mockFixture.Services.AddSingleton(this.mockRegistry.Object);
            this.provider = new SetRegistryKeyProvider(this.mockFixture.Services);
            await this.provider.ConfigureServicesAsync(this.mockFixture.Context, this.mockFixture.Component);

            ExecutionResult result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            Assert.AreEqual(ExecutionStatus.Succeeded, result.Status);
            this.mockRegistry.Verify();
        }

        [Test]
        public async Task SetRegistryKeyProviderReturnsSuccessWhenValueIsInt()
        {
            this.mockFixture.Component.Parameters.Clear();
            this.mockFixture.Component.Parameters.AddRange(new Dictionary<string, IConvertible>
            {
                { "keyName", @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Hypervisor" },
                { "valueName", "intvaluename" },
                { "value", "1" },
                { "type", "System.Int32" }
            });
            this.mockRegistry.Setup(r => r.Read<dynamic>(
                @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Hypervisor",
                "intvaluename",
                string.Empty)).Returns(0);
            this.mockRegistry.Setup(r => r.Write(
                @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Hypervisor",
                "intvaluename",
                1))
                .Verifiable();
            this.mockFixture.Services.AddSingleton(this.mockRegistry.Object);
            this.provider = new SetRegistryKeyProvider(this.mockFixture.Services);
            await this.provider.ConfigureServicesAsync(this.mockFixture.Context, this.mockFixture.Component);

            ExecutionResult result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            Assert.AreEqual(ExecutionStatus.Succeeded, result.Status);
            this.mockRegistry.Verify();
        }

        [Test]
        public async Task SetRegistryKeyProviderReturnsSuccessWhenValueIsArray()
        {
            this.mockFixture.Component.Parameters.Clear();
            this.mockFixture.Component.Parameters.AddRange(new Dictionary<string, IConvertible>
            {
                { "keyName", @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Hypervisor" },
                { "valueName", "arrayvaluename" },
                { "value", "01" },
                { "type", "System.Array" }
            });
            this.mockRegistry.Setup(r => r.Read<dynamic>(
                @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Hypervisor",
                "arrayvaluename",
                string.Empty)).Returns(new byte[] { 48, 0, 49, 0 });
            this.mockRegistry.Setup(r => r.Write(
                @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Hypervisor",
                "arrayvaluename",
                new byte[] { 48, 0, 49, 0 }))
                .Verifiable();
            this.mockFixture.Services.AddSingleton(this.mockRegistry.Object);
            this.provider = new SetRegistryKeyProvider(this.mockFixture.Services);
            await this.provider.ConfigureServicesAsync(this.mockFixture.Context, this.mockFixture.Component);

            ExecutionResult result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            Assert.AreEqual(ExecutionStatus.Succeeded, result.Status);
            this.mockRegistry.Verify();
        }
    }
}
