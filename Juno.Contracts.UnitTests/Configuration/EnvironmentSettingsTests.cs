namespace Microsoft.Azure.CRC.Configuration
{
    using Juno.Contracts.Configuration;
    using Microsoft.Extensions.Configuration;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class EnvironmentSettingsTests
    {
        [Test]
        [TestCase(@".\Configuration\juno-dev01.environmentsettings.json")]
        [TestCase(@".\Configuration\juno-prod01.environmentsettings.json")]
        public void EnvironmentSettingsAreSuccessfullyLoadedFromAConfigurationDefinition(string configurationFile)
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddJsonFile(configurationFile)
                .Build();

            EnvironmentSettings settings = EnvironmentSettings.Initialize(configuration);

            Assert.IsNotNull(settings);
            Assert.IsNotNull(settings.Id);
            Assert.IsNotNull(settings.AgentSettings);
            Assert.IsNotNull(settings.EventHubSettings);
            Assert.IsNotNull(settings.AppInsightsSettings);
            Assert.IsNotNull(settings.CosmosSettings);
            Assert.IsNotNull(settings.ExecutionSettings);
            Assert.IsNotNull(settings.Environment);
            Assert.IsNotNull(settings.KeyVaultSettings);
            Assert.IsNotNull(settings.KustoSettings);
            Assert.IsNotNull(settings.StorageAccountSettings);

            Assert.IsNotEmpty(settings.AppInsightsSettings);
            Assert.IsNotEmpty(settings.CosmosSettings);
            Assert.IsNotEmpty(settings.KeyVaultSettings);
            Assert.IsNotEmpty(settings.KustoSettings);
            Assert.IsNotEmpty(settings.StorageAccountSettings);
        }

        [Test]
        [TestCase(@".\Configuration\juno-dev01.environmentsettings.json")]
        [TestCase(@".\Configuration\juno-prod01.environmentsettings.json")]
        public void AgentSettingsAreSuccessfullyLoadedFromAConfigurationDefinition(string configurationFile)
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddJsonFile(configurationFile)
                .Build();

            EnvironmentSettings settings = EnvironmentSettings.Initialize(configuration);

            Assert.IsNotNull(settings.AgentSettings);
            Assert.IsNotNull(settings.AgentSettings.Apis);
            Assert.IsNotNull(settings.AgentSettings.AadPrincipals);
            Assert.IsNotNull(settings.AgentSettings.HostAgentCertificateName);
            Assert.IsNotNull(settings.AgentSettings.GuestAgentCertificateName);
            Assert.IsNotNull(settings.AgentSettings.HeartbeatInterval);
            Assert.IsNotNull(settings.AgentSettings.WorkPollingInterval);
            Assert.IsNotNull(settings.AgentSettings.AgentMonitoringSettings);
            Assert.IsNotNull(settings.AgentSettings.AgentMonitoringSettings.SelLogMonitorSettings);
            Assert.IsNotNull(settings.AgentSettings.AgentMonitoringSettings.SelLogMonitorSettings.IpmiUtilExePath);
            Assert.IsNotNull(settings.AgentSettings.AgentMonitoringSettings.SystemInfoMonitorSettings);
            Assert.IsNotNull(settings.AgentSettings.AgentMonitoringSettings.SystemInfoMonitorSettings.MonitorInterval);
            Assert.IsNotNull(settings.AgentSettings.AgentMonitoringSettings.VmUptimeMonitorSettings);
            Assert.IsNotNull(settings.AgentSettings.AgentMonitoringSettings.VmUptimeMonitorSettings.Enabled);

            Assert.IsNotEmpty(settings.AgentSettings.AadPrincipals);
            foreach (AadPrincipalSettings aadPrincipalSettings in settings.AgentSettings.AadPrincipals)
            {
                Assert.IsNotNull(aadPrincipalSettings.Id);
                Assert.IsNotNull(aadPrincipalSettings.AuthorityUri);
                Assert.IsNotNull(aadPrincipalSettings.PrincipalCertificateThumbprint);
                Assert.IsNotNull(aadPrincipalSettings.PrincipalId);
                Assert.IsNotNull(aadPrincipalSettings.EnterpriseObjectId);
                Assert.IsNotNull(aadPrincipalSettings.PrincipalName);
                Assert.IsNotNull(aadPrincipalSettings.TenantId);
            }

            foreach (ApiSettings apiSettings in settings.AgentSettings.Apis)
            {
                Assert.IsNotNull(apiSettings.Id);
                Assert.IsNotNull(apiSettings.Uri);
            }
        }

        [Test]
        [TestCase(@".\Configuration\juno-dev01.environmentsettings.json")]
        [TestCase(@".\Configuration\juno-prod01.environmentsettings.json")]
        public void AppInsightsSettingsAreSuccessfullyLoadedFromAConfigurationDefinition(string configurationFile)
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddJsonFile(configurationFile)
                .Build();

            EnvironmentSettings settings = EnvironmentSettings.Initialize(configuration);

            Assert.IsNotNull(settings.AppInsightsSettings);
            foreach (AppInsightsSettings appInsightsSettings in settings.AppInsightsSettings)
            {
                Assert.IsNotNull(appInsightsSettings.Id);
                Assert.IsNotNull(appInsightsSettings.InstrumentationKey);
            }
        }

        [Test]
        [TestCase(@".\Configuration\juno-dev01.environmentsettings.json")]
        [TestCase(@".\Configuration\juno-prod01.environmentsettings.json")]
        public void CosmosSettingsAreSuccessfullyLoadedFromAConfigurationDefinition(string configurationFile)
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddJsonFile(configurationFile)
                .Build();

            EnvironmentSettings settings = EnvironmentSettings.Initialize(configuration);

            Assert.IsNotNull(settings.CosmosSettings);
            foreach (CosmosSettings cosmosSettings in settings.CosmosSettings)
            {
                Assert.IsNotNull(cosmosSettings.Id);
                Assert.IsNotNull(cosmosSettings.AccountKey);
                Assert.IsNotNull(cosmosSettings.Uri);
            }
        }

        [Test]
        [TestCase(@".\Configuration\juno-dev01.environmentsettings.json")]
        [TestCase(@".\Configuration\juno-prod01.environmentsettings.json")]
        public void EventHubSettingsAreSuccessfullyLoadedFromAConfigurationDefinition(string configurationFile)
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddJsonFile(configurationFile)
                .Build();

            EnvironmentSettings settings = EnvironmentSettings.Initialize(configuration);

            Assert.IsNotNull(settings.EventHubSettings);
            foreach (EventHubSettings eventHubSettings in settings.EventHubSettings)
            {
                Assert.IsNotNull(eventHubSettings.Id);
                Assert.IsNotNull(eventHubSettings.ConnectionString);
                Assert.IsNotNull(eventHubSettings.EventHub);
            }
        }

        [Test]
        [TestCase(@".\Configuration\juno-dev01.environmentsettings.json")]
        [TestCase(@".\Configuration\juno-prod01.environmentsettings.json")]
        public void ExecutionSettingsAreSuccessfullyLoadedFromAConfigurationDefinition(string configurationFile)
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddJsonFile(configurationFile)
                .Build();

            EnvironmentSettings settings = EnvironmentSettings.Initialize(configuration);

            Assert.IsNotNull(settings.ExecutionSettings);
            Assert.IsNotNull(settings.ExecutionSettings.ExecutionApiUri);
            Assert.IsNotNull(settings.ExecutionSettings.EnvironmentApiUri);
            Assert.IsNotNull(settings.ExecutionSettings.AadPrincipals);
            Assert.IsNotNull(settings.ExecutionSettings.WorkQueueName);
            Assert.IsNotNull(settings.ExecutionSettings.WorkPollingInterval);

            Assert.IsNotEmpty(settings.ExecutionSettings.AadPrincipals);
            foreach (AadPrincipalSettings aadPrincipalSettings in settings.ExecutionSettings.AadPrincipals)
            {
                Assert.IsNotNull(aadPrincipalSettings.Id);
                Assert.IsNotNull(aadPrincipalSettings.AuthorityUri);
                Assert.IsNotNull(aadPrincipalSettings.PrincipalCertificateThumbprint);
                Assert.IsNotNull(aadPrincipalSettings.PrincipalId);
                Assert.IsNotNull(aadPrincipalSettings.EnterpriseObjectId);
                Assert.IsNotNull(aadPrincipalSettings.PrincipalName);
                Assert.IsNotNull(aadPrincipalSettings.TenantId);
            }

            Assert.IsNotEmpty(settings.ExecutionSettings.Installers);
            foreach (InstallerSettings installerSettings in settings.ExecutionSettings.Installers)
            {
                Assert.IsNotNull(installerSettings.Id);
                Assert.IsNotNull(installerSettings.Uri);
            }

            Assert.IsNotEmpty(settings.ExecutionSettings.NuGetFeeds);
            foreach (NuGetFeedSettings feedSettings in settings.ExecutionSettings.NuGetFeeds)
            {
                Assert.IsNotNull(feedSettings.Id);
                Assert.IsNotNull(feedSettings.Uri);
                Assert.IsNotNull(feedSettings.AccessToken);
            }
        }

        [Test]
        [TestCase(@".\Configuration\juno-dev01.environmentsettings.json")]
        [TestCase(@".\Configuration\juno-prod01.environmentsettings.json")]
        public void KeyVaultSettingsAreSuccessfullyLoadedFromAConfigurationDefinition(string configurationFile)
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddJsonFile(configurationFile)
                .Build();

            EnvironmentSettings settings = EnvironmentSettings.Initialize(configuration);

            Assert.IsNotNull(settings.KeyVaultSettings);
            foreach (KeyVaultSettings keyVaultSettings in settings.KeyVaultSettings)
            {
                Assert.IsNotNull(keyVaultSettings.Id);
                Assert.IsNotNull(keyVaultSettings.Uri);
            }
        }

        [Test]
        [TestCase(@".\Configuration\juno-dev01.environmentsettings.json")]
        [TestCase(@".\Configuration\juno-prod01.environmentsettings.json")]
        public void KustoSettingsAreSuccessfullyLoadedFromAConfigurationDefinition(string configurationFile)
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddJsonFile(configurationFile)
                .Build();

            EnvironmentSettings settings = EnvironmentSettings.Initialize(configuration);

            Assert.IsNotNull(settings.KustoSettings);
            foreach (KustoSettings kustoSettings in settings.KustoSettings)
            {
                Assert.IsNotNull(kustoSettings.Id);
                Assert.IsNotNull(kustoSettings.AadPrincipals);
                Assert.IsNotNull(kustoSettings.ClusterDatabase);
                Assert.IsNotNull(kustoSettings.ClusterUri);

                Assert.IsNotEmpty(kustoSettings.AadPrincipals);
                foreach (AadPrincipalSettings aadPrincipalSettings in kustoSettings.AadPrincipals)
                {
                    Assert.IsNotNull(aadPrincipalSettings.Id);
                    Assert.IsNotNull(aadPrincipalSettings.AuthorityUri);
                    Assert.IsNotNull(aadPrincipalSettings.PrincipalCertificateThumbprint);
                    Assert.IsNotNull(aadPrincipalSettings.PrincipalId);
                    Assert.IsNotNull(aadPrincipalSettings.EnterpriseObjectId);
                    Assert.IsNotNull(aadPrincipalSettings.PrincipalName);
                    Assert.IsNotNull(aadPrincipalSettings.TenantId);
                }
            }
        }

        [Test]
        [TestCase(@".\Configuration\juno-dev01.environmentsettings.json")]
        [TestCase(@".\Configuration\juno-prod01.environmentsettings.json")]
        public void StorageAccountSettingsAreSuccessfullyLoadedFromAConfigurationDefinition(string configurationFile)
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddJsonFile(configurationFile)
                .Build();

            EnvironmentSettings settings = EnvironmentSettings.Initialize(configuration);

            Assert.IsNotNull(settings.StorageAccountSettings);
            foreach (StorageAccountSettings storageSettings in settings.StorageAccountSettings)
            {
                Assert.IsNotNull(storageSettings.Id);
                Assert.IsNotNull(storageSettings.AccountKey);
                Assert.IsNotNull(storageSettings.Uri);
            }
        }

        [Test]
        [TestCase(@".\Configuration\juno-dev01.environmentsettings.json")]
        [TestCase(@".\Configuration\juno-prod01.environmentsettings.json")]
        public void TipSettingsAreSuccessfullyLoadedFromAConfigurationDefinition(string configurationFile)
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddJsonFile(configurationFile)
                .Build();

            EnvironmentSettings settings = EnvironmentSettings.Initialize(configuration);

            Assert.IsNotNull(settings.TipSettings);
            TipSettings tipSetting = settings.TipSettings;

            Assert.IsNotNull(tipSetting.HostAgentBootstrapPilotfishName);
            Assert.IsNotNull(tipSetting.HostAgentBootstrapPilotfishPath);

            foreach (AadPrincipalSettings aadPrincipalSettings in tipSetting.AadPrincipals)
            {
                Assert.IsNotNull(aadPrincipalSettings.Id);
                Assert.IsNotNull(aadPrincipalSettings.AuthorityUri);
                Assert.IsNotNull(aadPrincipalSettings.PrincipalCertificateThumbprint);
                Assert.IsNotNull(aadPrincipalSettings.PrincipalId);
                Assert.IsNotNull(aadPrincipalSettings.PrincipalName);
                Assert.IsNotNull(aadPrincipalSettings.TenantId);
            }
        }
    }
}
