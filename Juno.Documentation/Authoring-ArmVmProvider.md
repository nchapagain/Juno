<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Juno Experiment Steps</div>
<br/>

## ArmVmProvider
The following documentation illustrates how to define a Juno workflow step to deploy virtual machines on physical nodes in the environment
using the Azure Resource Manager (ARM) service.

### Preliminaries
It is important to understand the basic schema of an experiment beforehand.

[Authoring Experiments](./Authoring-Experiments.md)  
[Juno Experiment Schema](./Authoring-ExperimentSchema.md)

The <strong>ArmVmProvider</strong> is responsible for creating resource group, virtual machines within the resource group with all necessary resources such as network interface, availability-set, public ip address etc.

### Dependencies
* Physical nodes in Azure data center environments must have been isolated (e.g. via TiP sessions).

### Step Authoring
This section describes how to author an experiment workflow step to instruct Juno to install a NuGet package as a dependency of an experiment
workflow/workflow step.

##### Type
The 'type' must be ```Juno.Execution.Providers.Environment.ArmVmProvider```

##### Name and description
The 'name' and 'description' properties are used to describe steps in the experiment runtime workflow.  The values can be set to anything the experiment
author wishes but should generally be accurate descriptions of the purpose of the step.

##### Group
The 'group' defines the experiment group for which the runtime workflow step is associated (e.g. Group A, Group B).

##### Parameters
The following parameters will be used creating experiment step.

<div style="color:Green">
<div style="font-weight:600">Note on Data Disks:</div>
Attaching data disk is optional, but if you want to data disk 'dataDiskCount' and 'dataDiskSku' parameters are required to be
defined together.
</div>
<br/>


| Name                        | Required   | Data Type         | Description                |
| --------------------------  | ---------- | ----------------- | -------------------------- |
| subscriptionId              | Yes        | Guid              | Defines the ID of the target subscription where the virtual machine resources (and related resources) will be created.
| osDiskStorageAccountType    | Yes        | string            | Specifies the storage account type for the managed <strong>OS</strong> disk. NOTE: UltraSSD_LRS can only be used with data disks, it cannot be used with OS Disk. - Standard_LRS, Premium_LRS, StandardSSD_LRS, UltraSSD_LRS.
| osPublisher                 | Yes        | string            | The image publisher.
| osOffer                     | Yes        | string            | Specifies the offer of the platform image or marketplace image used to create the virtual machine.
| osSku                       | Yes        | string            | The image SKU.
| vmSize                      | Yes        | string            | Specifies the size of the virtual machine. For more information about virtual machine sizes, see <a href="https://docs.microsoft.com/en-us/azure/virtual-machines/windows/sizes?toc=%2Fazure%2Fvirtual-machines%2Fwindows%2Ftoc.json" >Sizes for virtual machines</a>
| vmCount                     | No         | int               | Specifies the number of VMs to create. Each VM will have the same specifications. Default = 1.
| osVersion                   | No         | string            | Specifies the version of the platform image or marketplace image used to create the virtual machine. The allowed formats are Major.Minor.Build or 'latest'. Major, Minor, and Build are decimal numbers. Default is ```latest```
| platform                    | No         | string            | OS platform the VMs run on. Default 'win-x64'.
| dataDiskCount               | No         | int               | The number of data disk to attach to the virtual machine
| dataDiskSizeInGB            | No         | int               | The size of each data disk in GB
| dataDiskSku                 | No         | string            | The data disk sku when creating disk resources. See more at <a href="https://docs.microsoft.com/en-us/azure/templates/microsoft.compute/2019-07-01/disks#DiskSku">data disk compute resource </a>
| dataDiskStorageAccountType  | No         | string            | Specifies the storage account type for the managed <strong>data</strong> disk. NOTE: UltraSSD_LRS can only be used with data disks, it cannot be used with OS Disk. - Standard_LRS, Premium_LRS, StandardSSD_LRS, UltraSSD_LRS.
| enableAcceleratedNetworking | No         | bool              | Specifies whether the VM should use accelerated networking. Not all VMs support accelerated networking. More info at <a href="https://docs.microsoft.com/en-us/azure/virtual-network/create-vm-accelerated-networking-cli">Azure Accelerated Networking docs</a>
| nodeTag                     | No         | string            | If specified, the ARM provider will deploy the VM(s) on the TiP node/session having the same tag.
| role                        | No         | string            | Specifies the role of the VM which could be referenced in later steps.
| region                      | (see description) | string     | Defines the data center region where the virtual machines will be deployed (e.g. West US 2,Central US). This setting allows the step to be run independent of a TiP session (i.e. VM creation does not require any active TiP sessions). When this setting is used, 'useTipSession' MUST be set to 'false'. See the links below for more details.
| useTipSession               | (see description) | bool       | True/False whether a TiP session should be used to define the region, cluster and node where the VMs will be created. This expects an active TiP session to exist and the step will fail if not. Default = true.
| pinnedCluster               | (see description) | string     | Only valid when UseTipSession=false. Optionally provide a cluster to pin to for non-TiP runs.

<div style="color:Green">
<div style="font-weight:600">Note on the use TiP Sessions:</div>
When the requirements of an experiment dictate that the workloads be run on nodes/blades that have have specific hardware traits
(e.g. a specific Intel CPU), it is necessary to isolate the nodes using TiP sessions. This prevents Juno experiments from impacting
real customers. This is the typical scenario with Juno experiments.  However, there are experiments (e.g. TDP) that involve the deployment
of VMs as the focus of the experiment. The ability to support the creation of VMs using TiP sessions or without them is intended to address the
wider range of scenarios.
</div>

##### Data Center Nomenclature
The following links can be used to identify valid values for the parameters noted above:

* OS/Data Disk SKUs  
  [ARM API Disk SKU Types](https://docs.microsoft.com/en-us/rest/api/storagerp/srp_sku_types)

* Data Center VM SKUs  
  [Azure VM SKU](https://msazure.visualstudio.com/One/_git/Compute-CPlat-Core?path=%2Fsrc%2FCRP%2FDev%2FComputeResourceProvider%2FConfig%2FCommon%2FCRP.Configs.VMSizes.Global.xml&version=GBmaster&_a=contents)

* Data Center Regions  
  [Azure Data Center Regions](https://azure.microsoft.com/en-us/global-infrastructure/regions/)  

##### Example Definitions
``` json
// Scenario: Using TiP Sessions
// ----------------------------------------------------------------------------
{
    "type": "Juno.Execution.Providers.Environment.ArmVmProvider",
    "name": "Create Virtual Machines",
    "description": "Create virtual machines to run Group A workloads",
    "group": "Group A",
    "parameters": {
        "subscriptionId": "94f4f5c5-3526-4f0d-83e5-2e7946a41b75",
        "osPublisher": "MicrosoftWindowsServer",
        "osOffer": "WindowsServer",
        "osSku": "2016-Datacenter",
        "osDiskStorageAccountType": "Premium_LRS",
        "vmSize": "Standard_F4s_v2",
        "vmCount": 5
    }
}

{
    "type": "Juno.Execution.Providers.Environment.ArmVmProvider",
    "name": "Create Virtual Machines",
    "description": "Create virtual machines to run Group A workloads",
    "group": "Group A",
    "parameters": {
        "subscriptionId": "94f4f5c5-3526-4f0d-83e5-2e7946a41b75",
        "osPublisher": "MicrosoftWindowsServer",
        "osOffer": "WindowsServer",
        "osSku": "2016-Datacenter",
        "osVersion": "latest",
        "osDiskStorageAccountType": "Premium_LRS",
        "dataDiskCount": 2,
        "dataDiskSizeInGB": 32,
        "dataDiskSku": "Premium_LRS",
        "dataDiskStorageAccountType": "Premium_LRS",
        "vmSize": "Standard_F4s_v2",
        "vmCount": 5
    }
}

// Scenario: Without using TiP Sessions (data center region-based)
// ----------------------------------------------------------------------------
{
    "type": "Juno.Execution.Providers.Environment.ArmVmProvider",
    "name": "Create Virtual Machines",
    "description": "Create virtual machines to run Group A workloads",
    "group": "Group A",
    "parameters": {
        "subscriptionId": "94f4f5c5-3526-4f0d-83e5-2e7946a41b75",
        "region": "westus2",
        "useTipSession": false,
        "osPublisher": "MicrosoftWindowsServer",
        "osOffer": "WindowsServer",
        "osSku": "2016-Datacenter",
        "osDiskStorageAccountType": "Premium_LRS",
        "vmSize": "Standard_F4s_v2",
        "vmCount": 5
    }
}

{
    "type": "Juno.Execution.Providers.Environment.ArmVmProvider",
    "name": "Create Virtual Machines",
    "description": "Create virtual machines to run Group A workloads",
    "group": "Group A",
    "parameters": {
        "subscriptionId": "94f4f5c5-3526-4f0d-83e5-2e7946a41b75",
        "region": "eastus",
        "useTipSession": false,
        "osPublisher": "MicrosoftWindowsServer",
        "osOffer": "WindowsServer",
        "osSku": "2016-Datacenter",
        "osVersion": "latest",
        "osDiskStorageAccountType": "Premium_LRS",
        "dataDiskCount": 2,
        "dataDiskSizeInGB": 32,
        "dataDiskSku": "Premium_LRS",
        "dataDiskStorageAccountType": "Premium_LRS",
        "vmSize": "Standard_F4s_v2",
        "vmCount": 5
    }
}

// Scenario: Linux VM
// ----------------------------------------------------------------------------
{
    "type": "Juno.Execution.Providers.Environment.ArmVmProvider",
    "name": "Create Virtual Machines for Group A",
    "description": "Create virtual machines to run workloads for experiment Group A.",
    "group": "Group A",
    "parameters": {
        "subscriptionId": "94f4f5c5-3526-4f0d-83e5-2e7946a41b75",
        "osDiskStorageAccountType": "Premium_LRS",
        "osPublisher": "Canonical",
        "osOffer": "UbuntuServer",
        "osSku": "18.04-LTS",
        "osVersion": "latest",
        "dataDiskCount": 2,
        "dataDiskSizeInGB": 32,
        "dataDiskSku": "Premium_LRS",
        "dataDiskStorageAccountType": "Premium_LRS",
        "vmSize": "Standard_F4s_v2",
        "vmCount": 2,
        "regions": "East US 2,East US,South Central US",
        "useTipSession": false,
        "platform": "linux-x64"
    }
}
```
