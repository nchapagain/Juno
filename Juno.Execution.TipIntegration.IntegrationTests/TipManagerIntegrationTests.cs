namespace Juno.Execution.TipIntegration
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts.Configuration;
    using Microsoft.Azure.CRC.Identity;
    using Microsoft.Extensions.Configuration;
    using NUnit.Framework;
    using TipGateway.Entities;

    [TestFixture]
    [Category("Integration/Live")]
    public class TipManagerIntegrationTests
    {
        private IConfiguration configuration;
        private TipClient tipManager;

        [SetUp]
        public void Setup()
        {
            this.configuration = new ConfigurationBuilder()
                .SetBasePath(Path.Combine(
                 Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                 @"Configuration"))
                .AddJsonFile($"juno-dev01.environmentsettings.json")
                .Build();

            this.tipManager = new TipClient(this.configuration);
        }

        [Test]
        [Ignore("For one-off debugging only. Live call to TiP service")]
        public async Task InvokeNodeExecuteCommand()
        {
            string command = $@"mkdir D:\\Juno\\ &amp;&amp; echo testToken >> D:\\Juno\\jwt.txt";
            var response = await this.tipManager.ExecuteNodeCommandAsync(
                "0e20adb2-a1c6-4597-85dd-fff4d0c40cec", 
                "aef9cf6d-16a0-45d1-b8ad-ba8627156193",
                command, 
                TimeSpan.FromSeconds(60), 
                CancellationToken.None).ConfigureAwait(false);
            Assert.IsTrue(response != null);
        }

        [Test]
        [Ignore("For one-off debugging only. Live call to TiP service")]
        public async Task IsTipChangeFailed()
        {
            bool response = await this.tipManager.IsTipSessionChangeFailedAsync(
                "3dd0545c-3b88-43b6-9490-461a5df54f53",
                "dd45ffad-85b8-4bdd-8e14-5698dbf7c3a2",
                CancellationToken.None).ConfigureAwait(false);
        }

        [Test]
        [Ignore("For one-off debugging only. Live call to TiP service")]
        public async Task DeleteOneTipSession()
        {
            var deleteSession = await this.tipManager.DeleteTipSessionAsync("0f59d885-433c-439e-88a5-e286ee6eae6a", CancellationToken.None).ConfigureAwait(false);
            Assert.IsTrue(deleteSession != null);
        }

        [Test]
        [Ignore("For one-off debugging only. Live call to TiP service")]
        public async Task GetOneTipSession()
        {
            var getSession = await this.tipManager.GetTipSessionAsync("e252c697-4667-4a6a-8fe1-d4d068173456", CancellationToken.None).ConfigureAwait(false);
            Assert.IsTrue(getSession != null);
        }

        [Test]
        [Ignore("Token Generating Code")]
        public async Task GetTipToken()
        {
            AadAuthenticationProvider authProvider = new AadAuthenticationProvider(
                new System.Uri("https://login.windows.net/72f988bf-86f1-41af-91ab-2d7cd011db47"),
                "0df8a261-a98e-4cbd-bf8c-d0cc66986cea",
                "0b07bd20-3fd4-4767-b838-735397438fdb",
                "196C526E84F8B7526A56C57C650A15594DE6D803");
            var token = await authProvider.AuthenticateAsync().ConfigureAwait(false);
            Console.WriteLine(token.AccessToken);
        }

        [Test]
        [Ignore("Token Generating Code")]
        public async Task GetJunoGuestAgentInstallerToken()
        {
            EnvironmentSettings settings = EnvironmentSettings.Initialize(this.configuration);
            AadPrincipalSettings guestAgent = settings.AgentSettings.AadPrincipals.Get(Setting.GuestAgent);
            string keyVaultAuthenticationResourceId = "https://vault.azure.net";
            AadAuthenticationProvider authProvider = new AadAuthenticationProvider(
                guestAgent.AuthorityUri,
                guestAgent.PrincipalId,
                keyVaultAuthenticationResourceId,
                guestAgent.PrincipalCertificateThumbprint);

            var token = await authProvider.AuthenticateAsync().ConfigureAwait(false);
            Console.WriteLine(token.AccessToken);
        }

        [Test]
        [Ignore("Token Generating Code")]
        public async Task GetJunoToken()
        {
            EnvironmentSettings settings = EnvironmentSettings.Initialize(this.configuration);
            AadPrincipalSettings clientApp = settings.SchedulerSettings.AadPrincipals.Get(Setting.Scheduler);
            AadPrincipalSettings resourceApp = settings.ExecutionSettings.AadPrincipals.Get(Setting.ExperimentsApi);
            AadAuthenticationProvider authProvider = new AadAuthenticationProvider(
                clientApp.AuthorityUri,
                clientApp.PrincipalId,
                resourceApp.PrincipalId,
                clientApp.PrincipalCertificateThumbprint);
            var token = await authProvider.AuthenticateAsync().ConfigureAwait(false);
            Console.WriteLine(token.AccessToken);
        }
    }
}
