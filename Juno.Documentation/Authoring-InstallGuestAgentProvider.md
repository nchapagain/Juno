<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Juno Experiment Steps</div>
<br/>

## InstallGuestAgentProvider
The following documentation illustrates how to define a Juno workflow step to install the Juno Guest agent on virtual machines in the environment 
as part of an experiment.

### Preliminaries
It is important to understand the basic schema of an experiment beforehand.

[Authoring Experiments](./Authoring-Experiments.md)  
[Juno Experiment Schema](./Authoring-ExperimentSchema.md)

### Dependencies

* Virtual machine deployment must be succeeded with all necessary resources.
* The resource group definition must exist in the experiment context with key ```resourceGroup_{0}``` where the placeholder will be replaced by group name

### Step Authoring
This section describes how to author an experiment workflow step to instruct Juno to install the Juno Guest Agent on virtual machines in the
environment.

##### Type
The 'type' must be ```Juno.Execution.Providers.Environment.InstallGuestAgentProvider```

##### Name and description
The 'name' and 'description' properties are used to describe steps in the experiment runtime workflow.  The values can be set to anything the experiment
author wishes but should generally be accurate descriptions of the purpose of the step.

##### Group
The 'group' defines the experiment group for which the runtime workflow step is associated (e.g. Group A, Group B).

##### Parameters
The following parameters will be used creating experiment step.

| Name                        | Required   | Data Type        | Description                |
| -------------------         | ---------- | ---------------- | -------------------------- |
| timeout         | No        | string/timespan  | Defines a timeout for verifying the Juno guest agent is successfully installed and heartbeating.
| packageVersion  | No        | string           | The guest agent package version to be installed.Default is ```latest```
| platform        | No        | string           | The OS platform on which the agent will run. Valid values include: win-x64 and linux-x64


##### Example Definitions
``` json
{
    "type": "Juno.Execution.Providers.Environment.InstallGuestAgentProvider",
    "name": "Install Juno Guest Agent",
    "description": "Installs the Juno Guest agent on virtual machines in the group to run Group A workloads",
    "group": "Group A",
    "parameters": {
        "timeout": "00:10:00"
    }
}

{
    "type": "Juno.Execution.Providers.Environment.InstallGuestAgentProvider",
    "name": "Install Juno Guest Agent",
    "description": "Installs the Juno Guest agent on virtual machines in the group to run Group A workloads",
    "group": "Group A",
    "parameters": {
        "timeout": "00:10:00",
        "packageVersion": "1.0.78-alpha"
    }
}

{
    "type": "Juno.Execution.Providers.Environment.InstallGuestAgentProvider",
    "name": "Install Juno Guest Agent",
    "description": "Installs the Juno Guest agent on virtual machines in the group to run Group A workloads",
    "group": "Group A",
    "parameters": {
        "timeout": "00:10:00",
        "packageVersion": "1.0.78-alpha",
        "platform": "linux-x64"
    }
}
```