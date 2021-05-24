<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Juno Experiment Steps</div>
<br/>

## ClusterSelectionProvider
The following documentation illustrates how to define a Juno workflow step to select a set of Azure data center clusters (and ultimately physical nodes within the clusters)
as options for running experiments.

### Preliminaries
It is important to understand the basic schema of an experiment beforehand.

[Authoring Experiments](./Authoring-Experiments.md)  
[Juno Experiment Schema](./Authoring-ExperimentSchema.md)

### Dependencies
This step is typically one of the first steps in a Juno experiment workflow. It has not special dependencies other than those related to quota and availability
restrictions inside individual data center regions (e.g. supported VM families and SKUs).

### Step Authoring
This section describes how to author an experiment workflow step to instruct Juno to query for environment clusters and nodes that can support the
requirements of the experiment.

##### Type
The 'type' must be ```Juno.Execution.Providers.Environment.ClusterSelectionProvider```

##### Name and description
The 'name' and 'description' properties are used to describe steps in the experiment runtime workflow.  The values can be set to anything the experiment
author wishes but should generally be accurate descriptions of the purpose of the step.

##### Group
The 'group' defines the experiment group for which the runtime workflow step is associated (e.g. Group A, Group B). For this step, the group can
be defined for a specific group (Group A, Group B) or as all groups '*'. When applying the step to all groups, the selected clusters and nodes will be a 
pool from which other steps will select when isolating physical nodes in the environment.

##### Parameters
The following parameters will be used when authoring the experiment step.

| Name                | Required   | Data Type        | Description                |
| ------------------- | ---------- | ---------------- | -------------------------- |
| cpuId               | Yes        | string           | Defines the CPU requirement of the physical nodes for the experiment. This is the ID of the CPU that the physical nodes must have to in order to take part in the experiment (e.g. 406F1). See the links below for more details.
| vmSkus              | Yes        | string/delimited | Defines the virtual machine SKU requirement for the experiment. This is a comma-delimited list of one or more Azure virtual machine SKUs for which the physical nodes must support in order to take part in the experiment (e.g. Standard_D4,Standard_F16). See the links below for more details.
| regions             | No         | string/delimited | Defines the data center region requirement for the experiment. This is a comma-delimited list of one or more data center regions where the physical nodes must exist in order to take part in the experiment (e.g. West US 2,Central US). See the links below for more details.

##### Data Center Nomenclature
The following links can be used to identify valid values for the parameters noted above:

* CPU ID  
  [Intel CPU IDs](https://msazure.visualstudio.com/One/_wiki/wikis/One.wiki/38383/CRC-AIR-Experiments)

* Data Center VM SKUs  
  [Azure VM SKU](https://msazure.visualstudio.com/One/_git/Compute-CPlat-Core?path=%2Fsrc%2FCRP%2FDev%2FComputeResourceProvider%2FConfig%2FCommon%2FCRP.Configs.VMSizes.Global.xml&version=GBmaster&_a=contents)

* Data Center Regions  
  [Azure Data Center Regions](https://azure.microsoft.com/en-us/global-infrastructure/regions/)  

##### Example Definitions
``` json
// Note:
// 50654 is the ID of the Intel Skylake processor that runs in some of the Gen 6 hardware
// across the Azure fleet.

{
    "type": "Juno.Execution.Providers.Environment.ClusterSelectionProvider",
    "name": "Select Environment Clusters",
    "description": "Selects clusters in the Azure fleet that can support the requirements of the experiment.",
    "group": "*",
    "parameters": {
        "cpuId": "50654",
        "vmSkus": "Standard_D4"
    }
}

{
    "type": "Juno.Execution.Providers.Environment.ClusterSelectionProvider",
    "name": "Select Environment Clusters",
    "description": "Selects clusters in the Azure fleet that can support the requirements of the experiment.",
    "group": "*",
    "parameters": {
        "cpuId": "50654",
        "vmSkus": "Standard_D4,Standard_F4"
    }
}

{
    "type": "Juno.Execution.Providers.Environment.ClusterSelectionProvider",
    "name": "Select Environment Clusters",
    "description": "Selects clusters in the Azure fleet that can support the requirements of the experiment.",
    "group": "*",
    "parameters": {
        "cpuId": "50654",
        "vmSkus": "Standard_D4,Standard_F4",
        "regions": "West US 2"
    }
}

{
    "type": "Juno.Execution.Providers.Environment.ClusterSelectionProvider",
    "name": "Select Environment Clusters",
    "description": "Selects clusters in the Azure fleet that can support the requirements of the experiment.",
    "group": "*",
    "parameters": {
        "cpuId": "50654",
        "vmSkus": "Standard_D4,Standard_F4",
        "regions": "West US,West US 2,West Central US,Japan East,Japan West"
    }
}
```

