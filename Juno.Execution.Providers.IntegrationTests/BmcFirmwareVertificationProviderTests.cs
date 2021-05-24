namespace Juno.Execution.Providers.Verification
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using System.Threading;
    using AutoFixture;
    using Juno.Contracts;
    using Juno.Execution.Providers.Environment;
    using Juno.Execution.Providers.Payloads;
    using Juno.Execution.TipIntegration;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Rest;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;

    /// <summary>
    /// Integration test to check that VerifyBmcVersion provider works when the required tools are present in C:\\BladeFX\\BladeFX
    /// </summary>
    [TestFixture]
    [Category("Integration/Live")]
    public class BmcFirmwareVertificationProviderTests
    {
        private ProviderFixture mockFixture;       

        [SetUp]
        public void SetupTest()
        {
            TestDependencies.Initialize();

            this.mockFixture = new ProviderFixture(typeof(BmcVerificationProvider));
            this.mockFixture.SetupExperimentMocks(ExperimentType.AB);
            this.mockFixture.Services.AddSingleton(NullLogger.Instance);
            this.mockFixture.Services.AddSingleton(TestDependencies.KeyVaultClient);
        }

        /// <summary>
        /// Live integration test to check that BmcFirmwareVerification provider works when the required tools are present in C:\\BladeFX\\BladeFXs
        /// </summary>
        [Test]
        public void ProviderCanVerifyBmcVersionWhenToolsPresent()
        {
            this.mockFixture = new ProviderFixture(typeof(BmcVerificationProvider));
            this.mockFixture.SetupExperimentMocks(ExperimentType.AB);
            this.mockFixture.Component.Parameters.Add("bmcVersion", "1.2.4");

            var selectionProvider = new BmcVerificationProvider(this.mockFixture.Services);
            selectionProvider.ConfigureServicesAsync(this.mockFixture.Context, this.mockFixture.Component).GetAwaiter().GetResult();
            var result = selectionProvider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(ExecutionStatus.Succeeded, result.Status);
        }
    }
}
