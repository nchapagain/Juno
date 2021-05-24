<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Juno Experiment Steps</div>
<br/>

## DeviceValidationProvider
The following documentation illustrates how to define a Juno workflow step to check that a device exists on virtual machines running as part of Juno experiment.

### Preliminaries
It is important to understand the basic schema of an experiment beforehand.

[Authoring Experiments](./Authoring-Experiments.md)  
[Juno Experiment Schema](./Authoring-ExperimentSchema.md)

### Dependencies

* Virtual machines must have been deployed in the environment that can host the Juno Guest agent.
* The Juno Guest agent must have been deployed to the target virtual machines in the environment.
* The subscription must use the default Azure security policies that install MA

### Step Authoring
This section describes how to author an experiment workflow step to instruct Juno to check a device

##### Type
The 'type' must be ```Juno.Execution.Providers.Workloads.DeviceValidationProvider```

##### Name and description
The 'name' and 'description' properties are used to describe steps in the experiment runtime workflow.  The values can be set to anything the experiment
author wishes but should generally be accurate descriptions of the purpose of the step.

##### Group
The 'group' defines the experiment group for which the runtime workflow step is associated (e.g. Group A, Group B).

##### Parameters
The following parameters will be used when authoring the experiment step.

| Name                   | Required   | Data Type        | Description                |
| ---------------------- | ---------- | ---------------- | -------------------------- |
| deviceName             | Yes        | string           | The device name to look for
| deviceClass            | Yes        | string           | The [device class](https://docs.microsoft.com/en-us/windows/win32/cimwin32prov/computer-system-hardware-classes) as defined in WMIC
| timeout                | No         | timespan         | Timeout for the step to give up on configuring the geneva monitoring agent

##### Example Definitions
``` json
{
    "type": "Juno.Execution.Providers.Workloads.DeviceValidationProvider",
    "name": "Configure MA",
    "description": "Validate AN Mellanox Device",
    "group": "Group A",
    "parameters": {
        "deviceClass":"Win32_NetworkAdapter",
        "deviceName":"Mellanox ConnectX-3 Virtual Function Ethernet Adapter",
        "timeout": "00.00:05:00"
    }
}

```

