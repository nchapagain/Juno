<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Juno Experiment Steps</div>
<br/>

## DeployHostingEnvironmentProvider
The following documentation illustrates how to define a Juno workflow step to deploy an Hosting Environment (HE) component on physical nodes in the environment as part
of Juno experiment.

### Preliminaries
It is important to understand the basic schema of an experiment beforehand.

[Authoring Experiments](./Authoring-Experiments.md)  
[Juno Experiment Schema](./Authoring-ExperimentSchema.md)

### Dependencies

* Clusters and nodes that match the requirements of the experiment must be selected and identified.
* Physical nodes in Azure data center environments that match the selection critieria must be isolated via TiP sessions.

### Step Authoring
This section describes how to author an experiment workflow step to instruct Juno to deploy an HE component on physical nodes in the
environment.

##### Type
The 'type' must be ```Juno.Execution.Providers.Environment.DeployHostingEnvironmentProvider```

##### Name and description
The 'name' and 'description' properties are used to describe steps in the experiment runtime workflow.  The values can be set to anything the experiment
author wishes but should generally be accurate descriptions of the purpose of the step.

##### Group
The 'group' defines the experiment group for which the runtime workflow step is associated (e.g. Group A, Group B).

##### Parameters
The following parameters will be used creating experiment step.

| Name              | Required   | Data Type        | Description                |
| ----------------- | ---------- | ---------------- | -------------------------- |
| componentType     | Yes        | string ([Eligible values](https://msazure.visualstudio.com/One/_git/EngSys-Performance-TipGateway?path=%2Fsrc%2FDLL%2FEntities%2FHostingEnvironmentComponent.cs))           | Defines the type of the HE component to deploy.|
| componentLocation | Yes        | string           | Defines the location of the HE component to be deployed.|
| timeout           | No         | string/timespan  | Defines a timeout for verifying the HE component is successfully deployed.|


##### Example Definitions
``` json
{
    "type": "Juno.Execution.Providers.Environment.DeployHostingEnvironmentProvider",
    "name": "Upgrade to RS1.8",
    "description": "Upgrade the OS on physical nodes in Group A to RS1.8.",
    "group": "Group A",
    "parameters": {
        "componentType": "OSHostPlugin",
        "componentLocation": "\\\\reddog\\Builds\\branches\\git_azure_compute_oshostplugin_rel_m3bmc_rs18_upgr_dev\\144.0.10.220\\retail-amd64\\RDTools\\Deploy\\Packages\\144.0.10.220.OsHostPlugin-rel_m3bmc_rs18_upgr_dev.200312-1639.zip,\\\\reddog\\builds\\branches\\git_azure_compute_oshostplugin_rel_m3bmc_rs18_upgr_dev\\144.0.10.220\\retail-amd64\\RDTools\\Deploy\\Config\\OSHostPlugin\\144.0.10.220.OsHostPlugin-rel_m3bmc_rs18_upgr_dev.200312-1639.HostPluginsConfigTemplate.xml,\\\\reddog\\builds\\branches\\git_azure_compute_oshostplugin_rel_m3bmc_rs18_upgr_dev\\144.0.10.220\\retail-amd64\\RDTools\\Deploy\\Config\\OSHostPlugin\\144.0.10.220.OsHostPlugin-rel_m3bmc_rs18_upgr_dev.200312-1639.OSHostPlugin.PluginSetup.xml,\\\\reddog\\builds\\branches\\git_azure_compute_oshostplugin_rel_m3bmc_rs18_upgr_dev\\144.0.10.220\\retail-amd64\\RDTools\\Deploy\\Config\\OSHostPlugin\\144.0.10.220.OsHostPlugin-rel_m3bmc_rs18_upgr_dev.200312-1639.OSHostPlugin.PluginSpecific.xml"
    }
}

{
    "type": "Juno.Execution.Providers.Environment.DeployHostingEnvironmentProvider",
    "name": "Image host to RS1.5 LKG",
    "description": "Image physical nodes in Group B to RS1.5 LKG with VHD.",
    "group": "Group B",
    "parameters": {
        "timeout": "00:10:00",
        "componentType": "ServerStandardCore_HVBaseName",
        "componentLocation": "\\\\ESRAzureRel01\\Azurebuilds\\rs1.5_LKG\\6002.19782.amd64fre.longhorn_rd_os144.171208-1750\\RDOS\\"
    }
}
```