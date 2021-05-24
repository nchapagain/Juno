namespace Juno.Execution.Providers.Watchdog
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Contracts;
    using Juno.Providers;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Identity;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class GenevaConfigProviderTests
    {
        private ProviderFixture mockFixture;
        private TestGenevaConfigProvider provider;
        private GenevaConfigProvider.GenevaConfigProviderState state;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new ProviderFixture(typeof(GenevaConfigProvider));
            this.mockFixture.SetupExperimentMocks();
            this.mockFixture.SetupCertificateMocks();

            this.mockFixture.Component.Parameters.Add("genevaEndpoint", "foo");
            this.mockFixture.Component.Parameters.Add("certificateKey", "foocert");
            this.mockFixture.Component.Parameters.Add("certificateThumbprint", Guid.NewGuid().ToString());
            this.mockFixture.Component.Parameters.Add("genevaTenantName", "crcteamtenant");
            this.mockFixture.Component.Parameters.Add("genevaAccountName", "crcteamaccount");
            this.mockFixture.Component.Parameters.Add("genevaNamespace", "crcteamnamespace");
            this.mockFixture.Component.Parameters.Add("genevaRegion", "wus2");
            this.mockFixture.Component.Parameters.Add("genevaConfigVersion", "2");
            this.mockFixture.Component.Parameters.Add("genevaRoleName", "foo");

            this.mockFixture.Services.AddSingleton(this.mockFixture.KeyVault.Object);
            this.mockFixture.Services.AddSingleton<IProviderDataClient>(this.mockFixture.DataClient.Object);
            this.mockFixture.Services.AddSingleton<ICertificateManager>(this.mockFixture.CertificateManager.Object);

            this.provider = new TestGenevaConfigProvider(this.mockFixture.Services);
            this.state = new GenevaConfigProvider.GenevaConfigProviderState
            {
                CertificateInstalled = false,
                StepInitializationTime = DateTime.UtcNow
            };

            this.InitializeDefaultMockBehaviors();
        }

        [Test]
        public void GenevaConfigProviderMaintainsStateInItsOwnIndividualStateObject()
        {
            this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, new CancellationToken(false))
                .GetAwaiter().GetResult();

            this.mockFixture.DataClient.Verify(client => client.GetOrCreateStateAsync<GenevaConfigProvider.GenevaConfigProviderState>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.Is<string>(stateId => !string.IsNullOrEmpty(stateId))));

            this.mockFixture.DataClient.Verify(client => client.SaveStateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<GenevaConfigProvider.GenevaConfigProviderState>(),
                It.IsAny<CancellationToken>(),
                It.Is<string>(stateId => !string.IsNullOrEmpty(stateId))));
        }

        [Test]
        public void GenevaConfigProviderBuildsConfigurationWithValidData()
        {
            var config = GenevaConfigProvider.BuildGenevaConfiguration(this.mockFixture.Component);

            Assert.AreEqual("1.0", config.ServiceArguments.Version);
            Assert.AreEqual("crcteamtenant", config.ConstantVariables.MonitoringTenant);
            Assert.AreEqual("%COMPUTERNAME%", config.ExpandVariables.MonitoringRoleInstance);
        }

        [Test]
        public void GenevaConfigProviderShouldCalculateTimeoutFromParameters()
        {
            this.state.CertificateInstalled = false;
            this.state.StepInitializationTime = DateTime.UtcNow;

            bool timedOut = this.provider.HasStepTimedOut(this.mockFixture.Component, this.state);
            // default timeout is 30 min so this should return false
            Assert.IsFalse(timedOut);

            this.state.StepInitializationTime = DateTime.UtcNow.Subtract(TimeSpan.FromHours(1));
            timedOut = this.provider.HasStepTimedOut(this.mockFixture.Component, this.state);
            // default timeout is 30 min so given start time is 1 hr ago, we should return true
            Assert.IsTrue(timedOut);

            this.state.StepInitializationTime = DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(30));
            this.mockFixture.Component.Parameters.Add("timeout", "0:00:00:01");
            timedOut = this.provider.HasStepTimedOut(this.mockFixture.Component, this.state);
            // requested timeout is 1 sec and we started 30s ago, so it should return true
            Assert.IsTrue(timedOut);
        }

        [Test]
        public void GenevaConfigProviderShouldInstallKVCertFromParameters()
        {
            // Ensure we download the expected certificate from the Key Vault.
            X509Certificate2 expectedCertificate = this.mockFixture.Create<X509Certificate2>();

            // Ensure we install the certificate downloaded from the Key Vault into the
            // certificate store expected.
            this.mockFixture.CertificateManager
                .Setup(mgr => mgr.InstallCertificateToStoreAsync(expectedCertificate, StoreName.My, StoreLocation.LocalMachine))
                .Returns(Task.CompletedTask)
                .Verifiable();

            var result = this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, new CancellationToken(false)).Result;
            Assert.AreEqual(ExecutionStatus.Succeeded, result.Status);

            this.mockFixture.KeyVault.Verify(kv => kv.GetCertificateAsync("foocert", It.IsAny<CancellationToken>(), true));
            this.mockFixture.CertificateManager.VerifyAll();
        }

        [Test]
        public void GenevaConfigProviderShouldNotInstallKVCertIfTimedOut()
        {
            // Ensure the step will show as timed out.
            this.state.StepInitializationTime = DateTime.UtcNow.Subtract(TimeSpan.FromHours(2));
            var result = this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, new CancellationToken(false)).Result;

            Assert.AreEqual(ExecutionStatus.Failed, result.Status);
            Assert.AreEqual(ErrorReason.Timeout, ((ProviderException)result.Error).Reason);

            this.mockFixture.KeyVault.Verify(s => s.GetCertificateAsync("foocert", It.IsAny<CancellationToken>(), It.IsAny<bool>()), Times.Never);
        }

        [Test]
        public void GenevaConfigProviderReturnsSuccessWhenPlatformIsLinux()
        {
            this.mockFixture.Component.Parameters.Add("platform", "linux-x64");

            ExecutionResult result = this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, new CancellationToken(false))
                .GetAwaiter().GetResult();

            Assert.AreEqual(ExecutionStatus.Succeeded, result.Status);
        }

        private void InitializeDefaultMockBehaviors()
        {
            this.mockFixture.DataClient.OnGetState<GenevaConfigProvider.GenevaConfigProviderState>()
                .Returns(Task.FromResult(this.state));

            this.mockFixture.DataClient.SetReturnsDefault(new HttpResponseMessage(HttpStatusCode.OK));

            this.mockFixture.KeyVault
                .Setup(kv => kv.GetCertificateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .Returns(Task.FromResult(this.mockFixture.Create<X509Certificate2>()));
        }

        private class TestGenevaConfigProvider : GenevaConfigProvider
        {
            public TestGenevaConfigProvider(IServiceCollection services)
                : base(services)
            {
            }

            public Func<ExperimentComponent, EventContext, bool> OnApplyConfigAsync { get; set; }

            internal override Task<bool> ApplyConfigAsync(ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
            {
                return this.OnApplyConfigAsync != null
                    ? Task.FromResult(this.OnApplyConfigAsync.Invoke(component, telemetryContext))
                    : Task.FromResult(true);
            }
        }
    }
}
