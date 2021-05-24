namespace Juno.Execution.ArmIntegration
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Microsoft.Azure.CRC.Rest;
    using Microsoft.Extensions.Configuration;
    using NuGet.Protocol;
    using NUnit.Framework;

    [TestFixture]
    [Category("Integration/Live")]
    public class ArmTemplateManagerIntegrationTests
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
        public async Task CreateOneVMOnTipSession()
        {
            using (var armManager = new ArmTemplateManager(this.configuration))
            {
                var vmSpecifications = new List<AzureVmSpecification>();
                var vmSpec = new AzureVmSpecification("Standard_LRS", "Standard_D2_v3", "MicrosoftWindowsServer", "WindowsServer", "2016-Datacenter");
                vmSpecifications.Add(vmSpec);
                VmResourceGroupDefinition rg = null;
                var rgDef = new VmResourceGroupDefinition(
                    "juno-dev01",
                    "d4ff5cd8-3d46-4277-8b7a-ef286eac44ce",
                    Guid.NewGuid().ToString(),
                    Guid.NewGuid().ToString(),
                    vmSpecifications,
                    "EastUS",
                    "BLAPrdApp15",
                    "030829af-23c3-4edc-893d-136c049ca6f8",
                    "bf56c7ab-6c73-4996-8554-cfccae809fe4",
                    new ConcurrentDictionary<string, string>());

                while (!rgDef.VirtualMachines.Any(s => s.IsDeploymentFinished()))
                {
                    rg = await armManager.DeployResourceGroupAndVirtualMachinesAsync(rgDef, new CancellationToken(false)).ConfigureAwait(false);
                    await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                }

                Assert.NotNull(rg);
                Assert.IsNotNull(rg.VirtualMachines.First().CorrelationId);
                Assert.IsNotEmpty(rg.VirtualMachines.First().CorrelationId);
            }
        }

        [Test]
        public void CreateOneVMOnTipSessionErrorIsHandled()
        {
            using (var armManager = new ArmTemplateManager(this.configuration))
            {
                var vmSpecifications = new List<AzureVmSpecification>();
                var vmSpec = new AzureVmSpecification("Standard_LRS", "Standard_DS2_v2", "MicrosoftWindowsServer", "WindowsServer", "2016-Datacenter");
                vmSpecifications.Add(vmSpec);
                VmResourceGroupDefinition rg = null;
                var rgDef = new VmResourceGroupDefinition(
                    "juno-dev01",
                    "d4ff5cd8-3d46-4277-8b7a-ef286eac44ce",
                    Guid.NewGuid().ToString(),
                    Guid.NewGuid().ToString(),
                    vmSpecifications,
                    "EastUS",
                    "BLAPrdApp15",
                    "4bf1a8ff-48ec-4f63-910a-5e1a5fa6650d",
                    "d6a9a363-81a4-47d1-9960-fb19ce20cd64",
                    new ConcurrentDictionary<string, string>());

                var exception = Assert.Throws<ProviderException>(() =>
                {
                    while (!rgDef.VirtualMachines.Any(s => s.IsDeploymentFinished()))
                    {
                        rg = armManager.DeployResourceGroupAndVirtualMachinesAsync(rgDef, new CancellationToken(false))
                            .GetAwaiter().GetResult();
                        Task.Delay(TimeSpan.FromSeconds(5)).Wait();
                    }
                });
                Assert.AreEqual(ErrorReason.ArmDeploymentFailure, exception.Reason);
                Assert.That(exception.Message.Contains("InvalidParameter"));
            }
        }

        [Test]
        public async Task CreateOneVMWithAcceleratedNetworking()
        {
            using (var armManager = new ArmTemplateManager(this.configuration))
            {
                var vmSpecifications = new List<AzureVmSpecification>();
                var vmSpec = new AzureVmSpecification("Standard_LRS", "Standard_D2_v2", "MicrosoftWindowsServer", "WindowsServer", "2016-Datacenter", null, null, null, null, null, true);
                vmSpecifications.Add(vmSpec);
                VmResourceGroupDefinition rg = null;
                var rgDef = new VmResourceGroupDefinition(
                    "juno-prod01",
                    "d4ff5cd8-3d46-4277-8b7a-ef286eac44ce",
                    Guid.NewGuid().ToString(),
                    Guid.NewGuid().ToString(),
                    vmSpecifications,
                    "West US",
                    "BY3PrdApp19",
                    "da2d6d61-11d7-4e58-8884-923a47c7560d",
                    "e3527bb7-1489-43b6-ae97-ba4ad964d3a4",
                    new ConcurrentDictionary<string, string>());

                while (!rgDef.VirtualMachines.Any(s => s.IsDeploymentFinished()))
                {
                    rg = await armManager.DeployResourceGroupAndVirtualMachinesAsync(rgDef, new CancellationToken(false)).ConfigureAwait(false);
                    await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                }

                Assert.NotNull(rg);
                Assert.IsNotNull(rg.VirtualMachines.First().CorrelationId);
                Assert.IsNotEmpty(rg.VirtualMachines.First().CorrelationId);
            }
        }

        [Test]
        public async Task CreateOneVMWitPIRImage()
        {
            using (var armManager = new ArmTemplateManager(this.configuration))
            {
                var vmSpecifications = new List<AzureVmSpecification>();
                var vmSpec = new AzureVmSpecification("Standard_LRS", "Standard_D2_v3", "MicrosoftWindowsServer", "WindowsServer", "2016-Datacenter", null, null, null, null, null, false);
                vmSpecifications.Add(vmSpec);
                VmResourceGroupDefinition rg = null;
                var rgDef = new VmResourceGroupDefinition(
                    "juno-prod01",
                    "d4ff5cd8-3d46-4277-8b7a-ef286eac44ce",
                    Guid.NewGuid().ToString(),
                    Guid.NewGuid().ToString(),
                    vmSpecifications,
                    "East US");

                while (!rgDef.VirtualMachines.Any(s => s.IsDeploymentFinished()))
                {
                    rg = await armManager.DeployResourceGroupAndVirtualMachinesAsync(rgDef, new CancellationToken(false)).ConfigureAwait(false);
                    await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                }

                Assert.NotNull(rg);
                Assert.IsNotNull(rg.VirtualMachines.First().CorrelationId);
                Assert.IsNotEmpty(rg.VirtualMachines.First().CorrelationId);
            }
        }

        [Test]
        public async Task CreateOneVMWithSigImage()
        {
            using (var armManager = new ArmTemplateManager(this.configuration))
            {
                var vmSpecifications = new List<AzureVmSpecification>();
                var vmSpec = new AzureVmSpecification("Standard_LRS", "Standard_D2_v2", null, null, null, null, null, null, null, null, true, "/subscriptions/57fa6412-3caa-4476-9b43-eaa948d9951a/resourceGroups/crc_fe_server_demo/providers/Microsoft.Compute/galleries/crc_server_image_gallery/images/17784.1068.rs5_release_svc_hci.200716-1400_server_serverdatacenter_en-us_vl/versions/17784.1068.200716");
                vmSpecifications.Add(vmSpec);
                VmResourceGroupDefinition rg = null;
                var rgDef = new VmResourceGroupDefinition(
                    "juno-dev01",
                    "d4ff5cd8-3d46-4277-8b7a-ef286eac44ce",
                    Guid.NewGuid().ToString(),
                    Guid.NewGuid().ToString(),
                    vmSpecifications,
                    "East US");

                while (!rgDef.VirtualMachines.Any(s => s.IsDeploymentFinished()))
                {
                    rg = await armManager.DeployResourceGroupAndVirtualMachinesAsync(rgDef, new CancellationToken(false)).ConfigureAwait(false);
                    await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                }

                Assert.NotNull(rg);
                Assert.IsNotNull(rg.VirtualMachines.First().CorrelationId);
                Assert.IsNotEmpty(rg.VirtualMachines.First().CorrelationId);
            }
        }

        [Test]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "called below")]
        public async Task GetActivityLogsAsync()
        {
            // The following parameters should be modified to get correct activity logs
            string subscriptionId = "d4ff5cd8-3d46-4277-8b7a-ef286eac44ce"; 
            string resourceGroupName = "rg-123456";
            DateTime startTime = DateTime.UtcNow.AddHours(-6);
            DateTime endTime = DateTime.UtcNow;
            
            // setup for the arm client and environment
            using (var armManager = new ArmTemplateManager(this.configuration))
            {
                EnvironmentSettings settings = EnvironmentSettings.Initialize(this.configuration);
                AadPrincipalSettings executionSvcPrincipal = settings.ExecutionSettings.AadPrincipals.Get(Setting.ExecutionSvc);
                AadPrincipalSettings guestAgentPrincipal = settings.AgentSettings.AadPrincipals.Get(Setting.GuestAgent);
                AgentSettings agentSettings = settings.AgentSettings;
                ExecutionSettings executionSettings = settings.ExecutionSettings;

                IRestClient restClient = new RestClientBuilder()
                .WithAutoRefreshToken(
                    executionSvcPrincipal.AuthorityUri,
                    executionSvcPrincipal.PrincipalId,
                    "https://management.core.windows.net/",
                    executionSvcPrincipal.PrincipalCertificateThumbprint)
                .AddAcceptedMediaType(MediaType.Json)
                .Build();
                ArmClient armClient = new ArmClient(restClient);
                List<IDisposable> disposables = new List<IDisposable> { restClient };
               
                // filter used by Diagnostic provider
                string filter = $" eventTimestamp ge '{startTime.ToUniversalTime()}' and eventTimestamp le '{endTime.ToUniversalTime()}' and resourceGroupName eq '{resourceGroupName}'";
                // fields used by diagnostic provider
                IEnumerable<string> fields = new List<string>() 
                { 
                    "eventTimestamp",
                    "correlationId", 
                    "level", 
                    "operationName", 
                    "resourceGroupName", 
                    "resourceType", 
                    "properties", 
                    "status" 
                };
                CancellationToken token = CancellationToken.None;
                // api call
                HttpResponseMessage response = await armClient.GetSubscriptionActivityLogsAsync(subscriptionId, filter, token, fields).ConfigureAwait(false);

                if (response.IsSuccessStatusCode && response.Content != null)
                {
                    string jsonString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    ArmActivityLogEntry result = jsonString.FromJson<ArmActivityLogEntry>();
                    if (result.Value?.Any() == true)
                    {
                        foreach (EventLogDataValues log in result.Value)
                        {
                            if (log.Level.Contains("Error", StringComparison.OrdinalIgnoreCase) || log.Level.Contains("Critical", StringComparison.OrdinalIgnoreCase))
                            {
                                // can be modified to other properties
                                string s = string.Join(",", log.Properties);
                                Console.WriteLine(s);
                            }
                        }
                    }
                }
                else
                {
                    string jsonString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    Console.WriteLine("Error: " + jsonString);
                }

                restClient.Dispose();
            }
        }
    }
}