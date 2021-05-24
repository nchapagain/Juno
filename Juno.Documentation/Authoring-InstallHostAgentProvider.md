<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Juno Experiment Steps</div>
<br/>

## InstallHostAgentProvider
The following documentation illustrates how to define a Juno workflow step to install the Juno Host agent on physical nodes in the environment as part
of Juno experiment.

### Preliminaries
It is important to understand the basic schema of an experiment beforehand.

[Authoring Experiments](./Authoring-Experiments.md)  
[Juno Experiment Schema](./Authoring-ExperimentSchema.md)

### Dependencies

* Clusters and nodes that match the requirements of the experiment must be selected and identified.
* Physical nodes in Azure data center environments that match the selection critieria must be isolated via TiP sessions.
* The Host Agent installation package must exist on the official build share as a PF/PilotFish service formatted package.

### Step Authoring
This section describes how to author an experiment workflow step to instruct Juno to install the Juno Host Agent on physical nodes in the
environment.

##### Type
The 'type' must be ```Juno.Execution.Providers.Environment.InstallHostAgentProvider```

##### Name and description
The 'name' and 'description' properties are used to describe steps in the experiment runtime workflow.  The values can be set to anything the experiment
author wishes but should generally be accurate descriptions of the purpose of the step.

##### Group
The 'group' defines the experiment group for which the runtime workflow step is associated (e.g. Group A, Group B).

##### Parameters
The following parameters will be used creating experiment step.

| Name                 | Required   | Data Type         | Description                |
| -------------------- | ---------- | ----------------- | -------------------------- |
| pilotfishServicePath | No         | string/path       | Defines the location of the Pilotfish service on the official build share to be installed (e.g.\\\\reddog\builds\branch\..\JunoHostAgent)
| timeout              | No         | string/timespan   | Defines a timeout for verifying the Juno Host agent is successfully installed and heartbeating.


##### Example Definitions
``` json
{
    "type": "Juno.Execution.Providers.Environment.InstallHostAgentProvider",
    "name": "Install Juno Host Agent",
    "description": "Installs the Juno Host agent on physical nodes in Group A.",
    "group": "Group A"
}

{
    "type": "Juno.Execution.Providers.Environment.InstallHostAgentProvider",
    "name": "Install Juno Host Agent",
    "description": "Installs the Juno Host agent on physical nodes in Group A.",
    "group": "Group A",
    "parameters": {
        "pilotfishServicePath": "\\\\reddog\\Builds\\branches\\git_csi_crc_air_hostagent_master_latest\\release-x64\\Deployment\\Prod\\App\\JunoHostAgent"
    }
}

{
    "type": "Juno.Execution.Providers.Environment.InstallHostAgentProvider",
    "name": "Install Juno Host Agent",
    "description": "Installs the Juno Host agent on physical nodes in Group B.",
    "group": "Group B",
    "parameters": {
        "pilotfishServicePath": "\\\\reddog\\Builds\\branches\\git_csi_crc_air_hostagent_master_latest\\release-x64\\Deployment\\Prod\\App\\JunoHostAgent",
        "timeout": "00:10:00"
    }
}
```