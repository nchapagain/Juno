<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Juno Experiment Steps</div>
<br/>

The following documentation describes how to get started authoring Juno experiments.

### Terminology
The following terms are used throughout Juno experiment authoring documentation.

* **ARM/Arm**  
  The Azure Resource Manager service. This is a service that runs in the Azure data centers that is responsible for the deployment of resources
  (e.g. VMs, Key Vaults, Storage Accounts) to resource groups in user/team subscriptions.

* **Dependencies**  
  Dependencies required by Juno experiment workflow steps are covered by steps that run beforehand. Dependendencies may be met 
  by a user making manual operations but are typically met using steps in the Juno experiment workflow.

* **Payload**  
  A payload is a change that is applied to a system that represents the focus or purpose of the experiment. For example, a microcode update
  to a CPU is a payload. It represents a risk to the reliable functioning of a physical node and is thus the reason to run an experiment on
  a system to validate the net effect of that change.

* **TiP**  
  The "Test-in-Production Service". The TiP Service runs in the Azure data center fabric and enables the ability to isolate one or more physical
  nodes in the data center from having any customer workloads deployed on them. This is an important part of a Juno experiment workflow because this
  ensures that a Juno experiment does not impact customers. Additionally, the physical nodes are the fundamental infrastructure required to run virtual
  machines and workloads within those virtual machines as part of an experiment.

* **Treatment Group**  
  The treatment group refers to the experiment group(s) to which a payload/change is applied. This is the group in the experiment that is being changed
  in order to evaluate the net effect of the change on the system.

* **Workload**  
  A workload describes a set of operations running on a system that is representative of an Azure customer experience. For example, a workload might
  run a certain amount of CPU or I/O stress on the system that matches the type of stress a real customer application might induce. Workloads are
  designed to put the system in a state that can identify differences in functionality when a payload/change is applied.

### Experiment Walkthrough
The following section provides a walkthrough of a Juno experiment and shows how each of the steps in the workflow are authored.  In this example,
the experiment is an A/B experiment and the purpose of the experiment is to validate the effect of an Intel microcode update (IPU2020.1) on physical nodes
that have a specific CPU/processor.

Each of the steps shown below have documentation that provides additional details on the authoring requirements for that step.

### Example Experiment Definitions
The following documentation provides examples of the individual steps in the experiment workflow (in order).

##### Experiment Metadata
Every Juno experiment has a set of information and metadata at the beginning of the experiment definition that provide context into the
purpose of the experiment.

``` json
{
    "$schema": "https://junodev01execution.azurewebsites.net/v1.0/schema",
    "contentVersion": "1.0.0",
    "name": "Example A/B experiment",
    "description": "Validate the effect of IPU2020.1 on the Azure Gen 6 nodes running Intel Skylake processors.",
    "metadata": {
        "teamName": "CSI AIR",
        "email": "crcair@microsoft.com",
        "owners": "crcair"
    },
    "tags": {
        "nodeGeneration": "Gen6",
        "nodeCpuName": "Skylake"
    }

    ...
}
```
<br/>

##### Identify and Select Environment  
The very first step in this experiment selects a set of one or more clusters in Azure data centers across the planet that have nodes whose characteristics
match the criteria of the experiment. For this example experiment, the criteria is that the nodes must have Intel Skylake processors. Skylake processors
are part of Azure fleet Gen 6 nodes/blades. The ID of the Skylake CPU (as defined by Intel) is 50654. Additionally, we want to identify nodes in the
data center that can support a set of VM SKUs that can handle the workloads that will run on them.

``` json
{
    "type": "Juno.Execution.Providers.Environment.ClusterSelectionProvider",
    "name": "Select Clusters and Nodes",
    "description": "Select clusters that have physical nodes that can support the requirements of the experiment.",
    "group": "*",
    "parameters": {
        "cpuId": "50654",
        "vmSkus": "Standard_F4s_v2,Standard_F8s_v2"
    }
}
```
<br/>

##### Establish TiP Sessions
The next step once we've identified a set of clusters that have physical nodes whose characteristics match the requirements of the experiment is
to establish TiP sessions to 2 of those nodes so that we can run an A/B experiment. In this example A/B experiment, one node will have the Intel microcode
update applied (i.e. IPU20202.1) and the other will not. The node to which the change will be applied is called a "treatment group".

``` json
{
    "type": "Juno.Execution.Providers.Environment.TipCreationProvider",
    "name": "Create TiP sessions",
    "description": "Create TiP sessions for the A/B experiment groups to isolate physical nodes in the Azure fleet.",
    "group": "*",
    "parameters": {
        "duration": "12:00:00"
    }
}
```
<br/>

##### Install the Juno Host Agent
After TiP sessions are successfully established for each of the experiment groups (A and B), we need to deploy the Juno Host agent. The Juno system uses
simple agent executables (.exe) that run as services on nodes and VMs to manage work associated with experiment steps on those individual systems.
The Juno Host agent runs on physical nodes associated with the experiment and is responsible for handling the application of the Intel microcode update
as part of this experiment. The Juno Host agent additionally monitors certain aspects of the system as it is running producing data that can be analyzed
after the experiment runs.

``` json
{
    "type": "Juno.Execution.Providers.Environment.InstallHostAgentProvider",
    "name": "Install Juno Host Agent",
    "description": "Install the Juno Host agent on experiment Group A nodes.",
    "group": "Group A"
},
{
    "type": "Juno.Execution.Providers.Environment.InstallHostAgentProvider",
    "name": "Install Juno Host Agent",
    "description": "Install the Juno Host agent on experiment Group B nodes.",
    "group": "Group B"
}
```
<br/>

##### Create Virtual Machines
Once we have TiP sessions established we can request the Azure Resource Manager (ARM) Service to deploy virtual machines to each of the nodes
in the experiment. Virtual machines are used to run workloads as part of a Juno experiment. Workloads are often ran on virtual machines versus
the physical node because this represents the real scenario experienced by Azure customers.

Below, we are requesting to deploy 2 virtual machines, each a Standard_F4 SKU to the nodes in each of the experiment groups (A and B). This should
result in 2 VMs running on each of the nodes in Group A and 2 VMs running on each of the nodes in Group B.

``` json
{
    "type": "Juno.Execution.Providers.Environment.ArmVmProvider",
    "name": "Create Virtual Machines for Group A",
    "description": "Create virtual machines to run workloads for experiment Group A.",
    "group": "Group A",
    "parameters": {
        "subscriptionId": "A266345D-124F-2DC9-BBE2-D8B171E6FA65",
        "vmSize": "Standard_F4s_v2",
        "vmCount": 2,
        "osDiskStorageAccountType": "Premium_LRS",
        "osPublisher": "MicrosoftWindowsServer",
        "osOffer": "WindowsServer",
        "osSku": "2016-Datacenter",
        "osVersion": "latest",
        "dataDiskCount": 2,
        "dataDiskSizeInGB": 32,
        "dataDiskSku": "Premium_LRS",
        "dataDiskStorageAccountType": "Premium_LRS"
    }
},
{
    "type": "Juno.Execution.Providers.Environment.ArmVmProvider",
    "name": "Create Virtual Machines for Group B",
    "description": "Create virtual machines to run workloads for experiment Group B.",
    "group": "Group B",
    "parameters": {
        "subscriptionId": "A266345D-124F-2DC9-BBE2-D8B171E6FA65",
        "vmSize": "Standard_F4s_v2",
        "vmCount": 2,
        "osDiskStorageAccountType": "Premium_LRS",
        "osPublisher": "MicrosoftWindowsServer",
        "osOffer": "WindowsServer",
        "osSku": "2016-Datacenter",
        "osVersion": "latest",
        "dataDiskCount": 2,
        "dataDiskSizeInGB": 32,
        "dataDiskSku": "Premium_LRS",
        "dataDiskStorageAccountType": "Premium_LRS"
    }
}
```

##### Install the Juno Guest Agent
Once virtual machines have been deployed to the nodes in the experiment groups and are running, we are ready to deploy the Juno Guest agent to each
of the virtual machines in the experiment groups. The Juno Guest agent runs on virtual machines associated with the experiment and is responsible for handling 
the application of the workloads as part of this experiment. The Juno Guest agent additionally monitors certain aspects of the system as it is running producing 
data that can be analyzed after the experiment runs.

``` json
{
    "type": "Juno.Execution.Providers.Environment.InstallGuestAgentProvider",
    "name": "Install Juno Guest Agent",
    "description": "Installs the Juno Guest agent on the VMs for group A.",
    "group": "Group A"
},
{
    "type": "Juno.Execution.Providers.Environment.InstallGuestAgentProvider",
    "name": "Install Juno Guest Agent",
    "description": "Installs the Juno Guest agent on the VMs for group B.",
    "group": "Group B"
}
```

##### Apply Payload
Before we run any workloads on the VMs in the experiment groups, we want to apply the payload to the "treatment group". The payload represents
the change for which the experiment is validating (i.e. how the application of the microcode affects the system).

``` json
{
    "type": "Juno.Execution.Providers.Environment.MicrocodeUpdateProvider",
    "name": "Apply IPU2020.2 Microcode Update",
    "description": "Applies the IPU2020.2 microcode update to the physical nodes in Group B.",
    "group": "Group B",
    "parameters": {
        "microcodeProvider": "Intel",
        "microcodeVersion": "2000069",
        "pfServiceName": "IPU2020.1",
        "pfServicePath": "\\\\reddog\\Builds\\branches\\git_azure_compute_move_feature_csifw_release\\10.8.5006.7\\retail-amd64\\app\\CSIMicrocodeUpdate"
    }
}
```

##### Run Workloads
Now that we have physical nodes isolated (via TiP sessions), virtual machines deployed to them and agents running on all, we can begin the actual
experiment work. As part of this example experiment, workloads will be running on the VMs when the Intel microcode update is applied (e.g. the payload).

In the example below, the experiment is using the VirtualClient.exe to run workloads. The VirtualClient.exe is a specialized host for different
types of workloads that match a profile. The VirtualClient.exe itself is downloaded to the system as a NuGet package.

``` json

{
    "type": "Juno.Execution.Providers.Workloads.VirtualClientWorkloadProvider",
    "name": "Run performance and IO Workload",
    "description": "Run performance and IO workload on the VMs in Group A",
    "group": "Group A",
    "parameters": {
        "command": "{NuGetPackagePath}\\VirtualClient\\1.0.1157.32\\content\\win-x64\\VirtualClient.exe",
        "commandArguments": "--profile=PERF-IO-V1.json --platform=Juno",
        "timeout": "00:02:00"
    },
    "dependencies": [
        {
            "type": "Juno.Execution.Providers.Dependencies.NuGetPackageProvider",
            "name": "Virtual Client NuGet Package",
            "description": "Download the Virtual Client NuGet package to the VM.",
            "group": "Group A",
            "parameters": {
                "feedUri": "https://msazure.pkgs.visualstudio.com/_packaging/CRC/nuget/v3/index.json",
                "packageName": "VirtualClient",
                "packageVersion": "1.0.1157.32",
                "personalAccessToken": "[secret:keyvault]=NugetAccessToken"
            }
        }
    ]
},
{
    "type": "Juno.Execution.Providers.Workloads.VirtualClientWorkloadProvider",
    "name": "Run performance and IO Workload",
    "description": "Run performance and IO workload on the VMs in Group B",
    "group": "Group B",
    "parameters": {
        "command": "{NuGetPackagePath}\\VirtualClient\\1.0.1157.32\\content\\win-x64\\VirtualClient.exe",
        "commandArguments": "--profile=PERF-IO-V1.json --platform=Juno",
        "timeout": "00:02:00"
    },
    "dependencies": [
        {
            "type": "Juno.Execution.Providers.Dependencies.NuGetPackageProvider",
            "name": "Virtual Client NuGet Package",
            "description": "Download the Virtual Client NuGet package to the VM.",
            "group": "Group B",
            "parameters": {
                "feedUri": "https://msazure.pkgs.visualstudio.com/_packaging/CRC/nuget/v3/index.json",
                "packageName": "VirtualClient",
                "packageVersion": "1.0.1157.32",
                "personalAccessToken": "[secret:keyvault]=NugetAccessToken"
            }
        }
    ]
}
```

##### Cleanup the Environment
After the experiment completes (i.e. payload is applied, workloads run their course), the environment must be cleaned up. In this experiment, there are
two sets of resources that are environment infrastructure that must be cleaned up: virtual machines and TiP sessions/nodes. To ensure there are not any unnecessary
costs associated with the virtual machines, we want to explicitly delete them. To ensure the physical nodes used as part of the experiment are returned to the
Azure production pool, we need to delete the TiP sessions.

``` json
{
    "type": "Juno.Execution.Providers.Environment.ArmVmCleanupProvider",
    "name": "Cleanup VM Resources",
    "description": "Cleanup VMs and related Azure subscription resources.",
    "group": "Group A"
},
{
    "type": "Juno.Execution.Providers.Environment.ArmVmCleanupProvider",
    "name": "Cleanup VM Resources",
    "description": "Cleanup VMs and related Azure subscription resources.",
    "group": "Group B"
},
{
    "type": "Juno.Execution.Providers.Environment.TipCleanupProvider",
    "name": "Release TiP Sessions/Nodes",
    "description": "Release nodes associated with TiP sessions back to the Azure production fleet.",
    "group": "Group A"
},
{
    "type": "Juno.Execution.Providers.Environment.TipCleanupProvider",
    "name": "Release TiP Sessions/Nodes",
    "description": "Release nodes associated with TiP sessions back to the Azure production fleet.",
    "group": "Group B"
}
```