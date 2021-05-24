﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Juno.Execution.Providers.Properties {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "16.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Juno.Execution.Providers.Properties.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to let rGName = $rGName$;
        ///let startTime = $startTime$;
        ///let endTime = $endTime$;
        ///cluster(&quot;ARMProd.kusto.windows.net&quot;).database(&quot;ARMProd&quot;).DeploymentOperations
        ///| where resourceGroupName == rGName 
        ///| where TIMESTAMP &gt;= startTime and TIMESTAMP &lt;= endTime 
        ///| project TIMESTAMP, tenantId, resourceGroupName, executionStatus, statusCode, statusMessage.
        /// </summary>
        internal static string Diagnostics_ArmProdDeploymentOperationsQuery {
            get {
                return ResourceManager.GetString("Diagnostics_ArmProdDeploymentOperationsQuery", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to let rGName = $rGName$;
        ///let startTime = $startTime$;
        ///let endTime = $endTime$;
        ///cluster(&quot;azcrp.kusto.windows.net&quot;).database(&quot;crp_allprod&quot;).VMApiQosEvent 
        ///| where resourceGroupName == rGName
        ///| where TIMESTAMP &gt; startTime and TIMESTAMP &lt; endTime
        ///| project TIMESTAMP, correlationId, operationId, resourceGroupName, resourceName, subscriptionId, exceptionType, errorDetails, vMId, vMSize, oSType, oSDiskStorageAccountType, availabilitySet, fabricCluster, allocationAction.
        /// </summary>
        internal static string Diagnostics_AZCRPVMApiQosEventsQuery {
            get {
                return ResourceManager.GetString("Diagnostics_AZCRPVMApiQosEventsQuery", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to let tipSessionId = $tipSessionId$;
        ///cluster(&quot;AzureCM.kusto.windows.net&quot;).database(&quot;AzureCM&quot;).LogNodeSnapshot
        ///| where tipNodeSessionId == tipSessionId
        ///| project TIMESTAMP, nodeState, nodeAvailabilityState, faultInfo, hostingEnvironment, faultDomain, lastStateChangeTime, nsProgressHealthStatus, tipNodeSessionId, healthSignals.
        /// </summary>
        internal static string Diagnostics_AzureCMLogNodeSnapshotQuery {
            get {
                return ResourceManager.GetString("Diagnostics_AzureCMLogNodeSnapshotQuery", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to let nodeId = $nodeId$;
        ///let startTime = $startTime$;
        ///let endTime = $endTime$;
        ///cluster(&quot;AzureCM.kusto.windows.net&quot;).database(&quot;AzureCM&quot;).CSIMicrocodeEvents
        ///| where resourceId == nodeId
        ///| where env_time &gt;= startTime and env_time &lt;= endTime
        ///| project env_time, resultType, resultSignature, resultDescription.
        /// </summary>
        internal static string Diagnostics_MicrocodeUpdateEventsQuery {
            get {
                return ResourceManager.GetString("Diagnostics_MicrocodeUpdateEventsQuery", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to let tipSessionId = $tipSessionId$;
        ///cluster(&quot;AzureCM.kusto.windows.net&quot;).database(&quot;AzureCM&quot;).LogTipNodeSessionStatusEventMessages
        ///| where tipNodeSessionId == tipSessionId
        ///| project TIMESTAMP, tipNodeSessionId, AvailabilityZone, Tenant, message.
        /// </summary>
        internal static string Diagnostics_TipSessionStatusEventsQuery {
            get {
                return ResourceManager.GetString("Diagnostics_TipSessionStatusEventsQuery", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to let nodes = $nodesList$;
        ///let vmSkus = $vmSkus$;
        ///let beginTime = ago($searchPeriod$h);
        ///// Step 1: get node&apos;s clusterId
        ///let supportedSkus = cluster(&apos;cirrus.kusto.windows.net&apos;).database(&apos;cirrus&apos;).VMSKU
        ///| distinct FabricVMSkuName, VMSKUName
        ///| project VmSku = tolower(FabricVMSkuName), ExternalVMSKU = VMSKUName
        ///| where (isempty(vmSkus) or ExternalVMSKU in~ (vmSkus)) 
        ///| distinct VmSku, ExternalVMSKU
        ///| join kind = inner
        ///    (cluster(&apos;azurecm.kusto.windows.net&apos;).database(&apos;AzureCM&apos;).AllocableVmCount
        ///    |  [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string DistinctNodeInfoQuery {
            get {
                return ResourceManager.GetString("DistinctNodeInfoQuery", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to // IERR_Repro_Query
        ///let externalSkus = $vmSkus$;
        ///let cpuIDList = $cpuIDList$;
        ///let regions = $regions$;
        ///let beginTime = ago(30min);
        ///let internalVmSkus=
        ///    cluster(&apos;cirrus.kusto.windows.net&apos;).database(&apos;cirrus&apos;).VMSKU
        ///    | distinct FabricVMSkuName, VMSKUName
        ///    | project VmSku = tolower(FabricVMSkuName), ExternalVMSKU = VMSKUName
        ///    | where isempty(externalSkus) or ExternalVMSKU in~ (externalSkus)
        ///    | distinct VmSku;
        ///let clusterMetadata = cluster(&apos;azuredcmkpi.westus2.kusto.windows.net&apos;).databa [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string IERR_Repro_Query {
            get {
                return ResourceManager.GetString("IERR_Repro_Query", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to // IERR_Repro_Query_1_65
        ///let externalSkus = $vmSkus$;
        ///let cpuIDList = $cpuIDList$;
        ///let regions = $regions$;
        ///let beginTime = ago(30min);
        ///let internalVmSkus=
        ///    cluster(&apos;cirrus.kusto.windows.net&apos;).database(&apos;cirrus&apos;).VMSKU
        ///    | distinct FabricVMSkuName, VMSKUName
        ///    | project VmSku = tolower(FabricVMSkuName), ExternalVMSKU = VMSKUName
        ///    | where isempty(externalSkus) or ExternalVMSKU in~ (externalSkus)
        ///    | distinct VmSku;
        ///let clusterMetadata = cluster(&apos;azuredcmkpi.westus2.kusto.windows.net&apos;).d [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string IERR_Repro_Query_1_65 {
            get {
                return ResourceManager.GetString("IERR_Repro_Query_1_65", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to // IERR_Repro_Query_AllOs
        ///let externalSkus = $vmSkus$;
        ///let cpuIDList = $cpuIDList$;
        ///let regions = $regions$;
        ///let beginTime = ago(30min);
        ///let internalVmSkus=
        ///    cluster(&apos;cirrus.kusto.windows.net&apos;).database(&apos;cirrus&apos;).VMSKU
        ///    | distinct FabricVMSkuName, VMSKUName
        ///    | project VmSku = tolower(FabricVMSkuName), ExternalVMSKU = VMSKUName
        ///    | where isempty(externalSkus) or ExternalVMSKU in~ (externalSkus)
        ///    | distinct VmSku;
        ///let clusterMetadata = cluster(&apos;azuredcmkpi.westus2.kusto.windows.net&apos;). [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string IERR_Repro_Query_AllOs {
            get {
                return ResourceManager.GetString("IERR_Repro_Query_AllOs", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to //Replace nodes with the real nodes we get from Tony. These are for testing purposes.
        ///let nodes = dynamic([&apos;0244af2c-1b80-4d9a-9e7e-0dc4abb5d3b0&apos;,&apos;0bc1507c-f961-4f58-9be2-6932add5619f&apos;,&apos;1181df9e-8bfc-412b-9a36-a6b680f5cc5e&apos;,&apos;172240d5-7aca-45fa-9c0a-d2aa0deb6be9&apos;,&apos;2d38cae8-eaf7-4d78-bf69-c54fd74ce4f0&apos;,&apos;4ead002b-7476-418f-accb-bc809ab430d9&apos;,&apos;53fe0ee0-e225-4ff7-9ee2-0528d77666b1&apos;,&apos;82034b5e-a83f-46a6-a68f-a5f96e4d9f8c&apos;,&apos;841787a5-9ee5-4e1c-9409-cbab9ad2cc8d&apos;,&apos;9122c883-e193-4c14-a1f7-de73fa86e0b3&apos;,&apos;98239349-057d [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string IERRProbationNodesQuery {
            get {
                return ResourceManager.GetString("IERRProbationNodesQuery", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to // MCU2019_2_Gen4_Query
        ///let externalSkus = $vmSkus$;
        ///let cpuIDList = $cpuIDList$;
        ///let regions = $regions$;
        ///let beginTime = ago(2h);
        ///let internalVmSkus=
        ///    cluster(&apos;cirrus.kusto.windows.net&apos;).database(&apos;cirrus&apos;).VMSKU
        ///    | distinct FabricVMSkuName, VMSKUName
        ///    | project VmSku = tolower(FabricVMSkuName), ExternalVMSKU = VMSKUName
        ///    | where isempty(externalSkus) or ExternalVMSKU in~ (externalSkus)
        ///    | distinct VmSku;
        ///let clusterMetadata = cluster(&apos;azuredcmkpi.westus2.kusto.windows.net&apos;).datab [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string MCU2019_2_Gen4_Query {
            get {
                return ResourceManager.GetString("MCU2019_2_Gen4_Query", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to // MCU2019_2_Refresh_Mitigation_Query
        ///let externalSkus = $vmSkus$;
        ///let cpuIDList = $cpuIDList$;
        ///let regions = $regions$;
        ///let beginTime = ago(2h);
        ///let internalVmSkus=
        ///    cluster(&apos;cirrus.kusto.windows.net&apos;).database(&apos;cirrus&apos;).VMSKU
        ///    | distinct FabricVMSkuName, VMSKUName
        ///    | project VmSku = tolower(FabricVMSkuName), ExternalVMSKU = VMSKUName
        ///    | where isempty(externalSkus) or ExternalVMSKU in~ (externalSkus)
        ///    | distinct VmSku;
        ///let clusterMetadata = cluster(&apos;azuredcmkpi.westus2.kusto.windo [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string MCU2019_2_Refresh_Mitigation_Query {
            get {
                return ResourceManager.GetString("MCU2019_2_Refresh_Mitigation_Query", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to // MCU2020_1_Query
        ///let externalSkus = $vmSkus$;
        ///let cpuIDList = $cpuIDList$;
        ///let regions = $regions$;
        ///let beginTime = ago(4h);
        ///let internalVmSkus = cluster(&apos;cirrus.kusto.windows.net&apos;).database(&apos;cirrus&apos;).VMSKU
        ///    | distinct FabricVMSkuName, VMSKUName
        ///    | project VmSku = tolower(FabricVMSkuName), ExternalVMSKU = VMSKUName
        ///    | where isempty(externalSkus) or ExternalVMSKU in~ (externalSkus)
        ///    | distinct VmSku;
        ///let clusterMetadata = cluster(&apos;azuredcmkpi.westus2.kusto.windows.net&apos;).database(&apos;Azur [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string MCU2020_1_Query {
            get {
                return ResourceManager.GetString("MCU2020_1_Query", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to // MCU2020_2_Query
        ///let externalSkus = $vmSkus$;
        ///let cpuIDList = $cpuIDList$;
        ///let regions = $regions$;
        ///let beginTime = ago(1h);
        ///let internalVmSkus=
        ///    cluster(&apos;cirrus.kusto.windows.net&apos;).database(&apos;cirrus&apos;).VMSKU
        ///    | distinct FabricVMSkuName, VMSKUName
        ///    | project VmSku = tolower(FabricVMSkuName), ExternalVMSKU = VMSKUName
        ///    | where isempty(externalSkus) or ExternalVMSKU in~ (externalSkus)
        ///    | distinct VmSku;
        ///let clusterMetadata = cluster(&apos;azuredcmkpi.westus2.kusto.windows.net&apos;).database(&apos; [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string MCU2020_2_Query {
            get {
                return ResourceManager.GetString("MCU2020_2_Query", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to // MCU2019_2_RefreshQuery
        ///let externalSkus = $vmSkus$;
        ///let cpuIDList = $cpuIDList$;
        ///let regions = $regions$;
        ///let beginTime = ago(2h);
        ///let internalVmSkus=
        ///    cluster(&apos;cirrus.kusto.windows.net&apos;).database(&apos;cirrus&apos;).VMSKU
        ///    | distinct FabricVMSkuName, VMSKUName
        ///    | project VmSku = tolower(FabricVMSkuName), ExternalVMSKU = VMSKUName
        ///    | where isempty(externalSkus) or ExternalVMSKU in~ (externalSkus)
        ///    | distinct VmSku;
        ///let clusterMetadata = cluster(&apos;azuredcmkpi.westus2.kusto.windows.net&apos;).dat [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string MPU2019_2_RefreshQuery {
            get {
                return ResourceManager.GetString("MPU2019_2_RefreshQuery", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to let nodes = $nodesList$;
        ///let vmSkus = $vmSkus$;
        ///let beginTime = ago(4h);
        ///// Step 1: get node&apos;s clusterId
        ///let supportedSkus = cluster(&apos;cirrus.kusto.windows.net&apos;).database(&apos;cirrus&apos;).VMSKU
        ///| distinct FabricVMSkuName, VMSKUName
        ///| project VmSku = tolower(FabricVMSkuName), ExternalVMSKU = VMSKUName
        ///| where (isempty(vmSkus) or ExternalVMSKU in~ (vmSkus)) 
        ///| distinct VmSku, ExternalVMSKU
        ///| join kind = inner
        ///    (cluster(&apos;Azurecm&apos;).database(&apos;AzureCM&apos;).AllocableVmCount
        ///    | where PreciseTimeStamp &gt; beginT [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string NodeInfoQuery {
            get {
                return ResourceManager.GetString("NodeInfoQuery", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to {
        ///  &quot;FriendlyName&quot;: &quot;IO Performance Benchmark V2&quot;,
        ///  &quot;ShuffleActions&quot;: &quot;true&quot;,
        ///  &quot;Metadata&quot;: {
        ///  },
        ///  &quot;Actions&quot;: [
        ///    {
        ///      &quot;Type&quot;: &quot;FormattingDiskAction&quot;,
        ///      &quot;Arguments&quot;: []
        ///    },
        ///    {
        ///      &quot;Type&quot;: &quot;FioExecutor&quot;,
        ///      &quot;Arguments&quot;: [
        ///        {
        ///          &quot;Name&quot;: &quot;CommandLine&quot;,
        ///          &quot;Value&quot;: &quot;--name=fio_randrw_4GB_64k_1_16_buffered --ioengine=[win32nt=windowsaio,unix=posixaio] --size=4GB --rw=randrw --bs=64k --iodepth=1 --buffered=1 --thread=16 --group_reporting --filename={FIO.F [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string PERF_IO_V2_Bios {
            get {
                return ResourceManager.GetString("PERF_IO_V2_Bios", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to {
        ///  &quot;FriendlyName&quot;: &quot;IO Performance Benchmark V2&quot;,
        ///  &quot;ShuffleActions&quot;: &quot;true&quot;,
        ///  &quot;Metadata&quot;: {
        ///  },
        ///  &quot;Actions&quot;: [
        ///    {
        ///      &quot;Type&quot;: &quot;FormattingDiskAction&quot;,
        ///      &quot;Arguments&quot;: []
        ///    },
        ///    {
        ///      &quot;Type&quot;: &quot;FioExecutor&quot;,
        ///      &quot;Arguments&quot;: [
        ///        {
        ///          &quot;Name&quot;: &quot;CommandLine&quot;,
        ///          &quot;Value&quot;: &quot;--name=fio_readwrite_4GB_64k_16_1_direct --ioengine=windowsaio --size=4GB --rw=readwrite --bs=64k --iodepth=16 --direct=1 --thread=1 --group_reporting {FIO.FILEPATH} --output-format=json&quot;
        ///  [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string PERF_IO_V2_Hotfix {
            get {
                return ResourceManager.GetString("PERF_IO_V2_Hotfix", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to {
        ///  &quot;FriendlyName&quot;: &quot;Quick IO Performance Benchmark V2&quot;,
        ///  &quot;ShuffleActions&quot;: &quot;true&quot;,
        ///  &quot;Metadata&quot;: {
        ///  },
        ///  &quot;Actions&quot;: [
        ///    {
        ///      &quot;Type&quot;: &quot;FormattingDiskAction&quot;,
        ///      &quot;Arguments&quot;: []
        ///    },
        ///    {
        ///      &quot;Type&quot;: &quot;FioExecutor&quot;,
        ///      &quot;Arguments&quot;: [
        ///        {
        ///          &quot;Name&quot;: &quot;CommandLine&quot;,
        ///          &quot;Value&quot;: &quot;--name=fio_readwrite_4GB_4k_1_1_buffered --ioengine=[win32nt=windowsaio,unix=posixaio] --size=4GB --rw=readwrite --bs=4k --iodepth=1 --buffered=1 --thread=1 --group_reporting {FIO.FILE [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string PERF_IO_V2_short {
            get {
                return ResourceManager.GetString("PERF_IO_V2_short", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to ams22prdapp20
        ///blz22prdapp16
        ///bn9prdapp08
        ///bn9prdapp18
        ///cpq22prdapp04
        ///cpq24prdapp01
        ///dsm06prdapp02
        ///fra21prdapp09
        ///lon23prdapp20
        ///sat20prdapp09
        ///mwh02prdapp16
        ///sjc20prdapp26
        ///sn7prdapp06.
        /// </summary>
        internal static string PinnedClusters {
            get {
                return ResourceManager.GetString("PinnedClusters", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to // TipRackQuery
        ///let externalSkus = $vmSkus$;
        ///let cpuIDList = $cpuIDList$;
        ///let regions = $regions$;
        ///let beginTime = ago(1h);
        ///let internalVmSkus=
        ///    cluster(&apos;cirrus.kusto.windows.net&apos;).database(&apos;cirrus&apos;).VMSKU
        ///    | distinct FabricVMSkuName, VMSKUName
        ///    | project VmSku = tolower(FabricVMSkuName), ExternalVMSKU = VMSKUName
        ///    | where isempty(externalSkus) or ExternalVMSKU in~ (externalSkus)
        ///    | distinct VmSku;
        ///let clusterMetadata = cluster(&apos;azuredcmkpi.westus2.kusto.windows.net&apos;).database(&apos;Azu [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string TipRackQuery {
            get {
                return ResourceManager.GetString("TipRackQuery", resourceCulture);
            }
        }
    }
}