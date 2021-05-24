namespace Juno.Execution.TipIntegration
{
    using System.Collections.Generic;
    using NUnit.Framework;
    using TipGateway.Entities;

    [TestFixture]
    [Category("Unit")]
    public class TipIntegrationExtensionsTests
    {
        // The set of error messages/parts for which the provider is expected to retry
        // agent installation. These are all errors we've seen as part of running Juno experiments
        // that caused experiment failures.
        private static IEnumerable<string> expectedRetryableErrors = new List<string>
        {
            @"System.Exception: Building Job for service: JunoHostAgent and build location: " +
            @"\\rd\Builds\branches\any\release-x64\Deployment\Prod\App\JunoHostAgent failed with error message: " +
            @"remoteJob failed for service: JunoHostAgent and label: TipNode_061091de-b875-4c0d-a877-139b5f9e5a93 with error message: proxy 1.2.3.4 reported error: DP SJC201021202055: " +
            @"EDP010196: Error 121: The semaphore timeout period has expired.   [by SJC201021202055]",

            @"System.Exception: Building Job for service: JunoHostAgent and build location: \\rd\Builds\branches\any\release-x64\Deployment\Prod\App\JunoHostAgent " +
            @"failed with error message: remoteJob failed for service: JunoHostAgent and label: TipNode_217b16b8-b180-4a87-b6f1-71e09b1b6e87 with error message: Failed to upload chunk to " +
            @"dynamic storage: offs 42190193, size 10607/10607",

            @"System.Exception: Building Job for service: JunoHostAgent and build location: \\rd\Builds\branches\any\release-x64\Deployment\Prod\App\JunoHostAgent " +
            @"failed with error message: remoteJob failed for service: JunoHostAgent and label: TipNode_f7f5315d-1a34-4ade-ae48-f4f2da3d7262 with error message: proxy 25.66.144.205 reported error: DP CH1PHY104010401: " +
            @"EDP010358: CreateFile('\\?\UNC\reddog\Builds\branches\any\release-x64\Deployment\Prod\App\JunoHostAgent\Microsoft.Extensions.Localization.Abstractions.dll') " +
            @"failed: Error 64: The specified network name is no longer available.   [by CH1PHY104010401]",

            @"Failed to deploy HostingEnvironment 'OSHostPlugin' at '\\rd\Builds\branches\any\144.0.10.220\retail-amd64\RDTools\Deploy\Packages\144.0.10.220.OsHostPlugin-rel_m3bmc_rs18_upgr_dev.200312-1639.zip," +
            @"\\rd\builds\branches\any\144.0.10.220\retail-amd64\RDTools\Deploy\Config\OSHostPlugin\144.0.10.220.OsHostPlugin-rel_m3bmc_rs18_upgr_dev.200312-1639.HostPluginsConfigTemplate.xml," +
            @"\\rd\builds\branches\any\144.0.10.220\retail-amd64\RDTools\Deploy\Config\OSHostPlugin\144.0.10.220.OsHostPlugin-rel_m3bmc_rs18_upgr_dev.200312-1639.OSHostPlugin.PluginSetup.xml," +
            @"\\rd\builds\branches\any\144.0.10.220\retail-amd64\RDTools\Deploy\Config\OSHostPlugin\144.0.10.220.OsHostPlugin-rel_m3bmc_rs18_upgr_dev.200312-1639.OSHostPlugin.PluginSpecific.xml' " +
            @"on Session '8ad11953-2228-47ca-a888-3802ab237055' with error 'Microsoft.Azure.Compute.Services.TipNodeService.Common.Exceptions.AddImageException: Failed to add image Image " +
            @"144.0.10.220.OsHostPlugin-rel_m3bmc_rs18_upgr_dev.200312-1639.zip from \\rd\Builds\branches\any\144.0.10.220\retail-amd64\RDTools\Deploy\Packages with error " +
            @"Microsoft.Azure.Compute.Services.TipNodeService.Common.Exceptions.AddImageException Add image failed. /nException:System.ServiceModel.FaultException`1[RD.Fabric.Controller.TransferredDataSizeMismatchFault]: " +
            @"Expected image TipNode_8ad11953-2228-47ca-a888-3802ab237055_144.0.10.220.OsHostPlugin-rel_m3bmc_rs18_upgr_dev.200312-1639.zip to be of size 262469761, but got image of size 0 (Fault Detail is equal to An ExceptionDetail",

            @"Failed to apply pilotfish 'JunoBladewatchdog' on Session 'a5e98b44-21e6-43ca-879d-76a8ba1eb5ec' with error 'System.Exception: Building Job for service: JunoBladewatchdog and build location: " +
            @"\\rd\Builds\branches\any\release-x64\Deployment\Prod\App\JunoBladeWatchdog failed with error message: remoteJob failed for service: JunoBladewatchdog and label: TipNode_95c25280-989e-4b21-af86-f7665f9064c0 " +
            @"with error message: Failed to send DeliveryPath to dynamic storage: offs -12, size 1161/1161, data CRC 0000000000000000  (DP: 25.108.193.196, by SN3PNPF00002EB2); . IsOverlake: False",

            @"Failed to apply pilotfish 'JunoBladewatchdog' on Session '95dcf4d6-49ae-41e5-998f-a2fd2a46262c' with error 'System.Exception: Building Job for service: JunoBladewatchdog and build location: " +
            @"\\rd\Builds\branches\any\release-x64\Deployment\Prod\App\JunoBladeWatchdog failed with error message: remoteJob failed for service: JunoBladewatchdog and label: TipNode_7d4d6713-c60f-4aa9-9183-820b989270fb with " +
            @"error message: Error querying for '\\rd\Builds\branches\any\release-x64\Deployment\Prod\App\JunoBladeWatchdog\' - 11 attempts failed; . IsOverlake: False",

            @"Failed to install PilotFish service 'JunoBladewatchdog' on node for TiP session ID '6496756a-1338-41fb-a7c9-99202701f469' with error 'System.Exception: Building Job for service: JunoBladewatchdog and build location: " +
            @"\\rd\Builds\branches\any\release-x64\Deployment\Prod\App\JunoBladeWatchdog failed with error message: remoteJob failed for service: JunoBladewatchdog and label: " +
            @"TipNode_cb677999-c8a5-40e1-b323-ed7f2fd6f3d6 with error message: Proxy error: not all data was received (DP: 25.66.144.47, by AM4PNPF00000849); . "
        };

        [Test]
        public void IsRetryableScenarioExtensionIdentifiesExpectedRetryableTiPServiceFailures()
        {
            foreach (string retryableFailureScenario in TipIntegrationExtensionsTests.expectedRetryableErrors)
            {
                TipNodeSessionChangeDetails changeDetails = new TipNodeSessionChangeDetails
                {
                    ChangeType = TipNodeSessionChangeType.Create,
                    Result = TipNodeSessionChangeResult.Failed,
                    ErrorMessage = retryableFailureScenario
                };

                Assert.IsTrue(changeDetails.IsRetryableScenario());
            }
        }
    }
}
