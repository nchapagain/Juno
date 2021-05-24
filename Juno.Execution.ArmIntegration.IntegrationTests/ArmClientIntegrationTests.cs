namespace Juno.Execution.NuGetIntegration
{
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Net.Http;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts.Configuration;
    using Juno.Execution.ArmIntegration;
    using Microsoft.Azure.CRC.Rest;
    using Microsoft.Extensions.Configuration;
    using NUnit.Framework;

    [TestFixture]
    [Category("Integration/Live")]
    public class ArmClientIntegrationTests
    {
        private IConfiguration configuration;

        [SetUp]
        public void Setup()
        {
            this.configuration = new ConfigurationBuilder()
                .SetBasePath(Path.Combine(
                 Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                 @"Configuration"))
                .AddJsonFile($"juno-dev01.environmentsettings.json")
                .Build();
        }

        [Test]
        [Ignore("For manual deletion of Resource Groups only. Live call to Azure Management")]
        public async Task DeleteResourceGroups()
        {
            string subscriptionId = "d4ff5cd8-3d46-4277-8b7a-ef286eac44ce";
            ArmClient restClient = new ArmClient(this.CreateClient(), null);
            List<string> resourceGroups = new List<string>()
            {
                "rg-5688c1cfe2d",
                "rg-2a078e4ab5b",
                "rg-df6c392cd8b"
            };

            foreach (string resourceGroup in resourceGroups)
            {
                var resonse = await restClient.DeleteResourceGroupAsync(subscriptionId, resourceGroup, CancellationToken.None).ConfigureAwait(false);
                if (!resonse.IsSuccessStatusCode)
                {
                    throw new ArmException($"StatusCode: {resonse.StatusCode}; ReasonPhrase: {resonse.ReasonPhrase}; RequestMessage: {resonse.RequestMessage}");
                }
            }
        }

        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Disposed where used.")]
        private IRestClient CreateClient()
        {
            string armResourceId = "https://management.core.windows.net/";
            EnvironmentSettings settings = EnvironmentSettings.Initialize(this.configuration);
            var executionSvcPrincipal = settings.ExecutionSettings.AadPrincipals.Get(Setting.ExecutionSvc);
            var guestAgentPrincipal = settings.AgentSettings.AadPrincipals.Get(Setting.GuestAgent);

            return new RestClientBuilder()
            .WithAutoRefreshToken(
                executionSvcPrincipal.AuthorityUri,
                executionSvcPrincipal.PrincipalId,
                armResourceId,
                executionSvcPrincipal.PrincipalCertificateThumbprint)
                .AddAcceptedMediaType(MediaType.Json)
                .Build();
        }
    }
}
