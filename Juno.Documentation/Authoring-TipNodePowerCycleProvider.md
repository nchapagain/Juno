<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Juno Experiment Steps</div>
<br/>

## TipNodePowerCycleProvider
The following documentation illustrates how to define a Juno step to power cycle the node. Power cycle should only be done
from impactful experiments where impactType=ImpactfulAutoCleanup or impactType=ImpactfulManualCleanup.

To successfully power cycle the node, Juno will first reset the node health, request power cycle and then wait for the
node to report ready and InGoalState.

This provider Should not be used from non-impactful experiments that do not touch firmware.

You can change the impactType of the experiment in the metadata section as shown in the example below.

```json
"metadata": {
        "teamName": "CRC AIR",
        "email": "crcair@microsoft.com",
        "experimentType": "QoS",
        "payload": "LongsPeak",
        "payloadType": "FPGAGold",
        "payloadVersion": "2000069",
        "targetGeneration": "Gen6",
        "impactType": "ImpactfulAutoCleanup"
    },
```
### Preliminaries
It is important to understand the basic schema of an experiment before attempting to author Juno experiment workflow steps.

[Authoring Experiments](./Authoring-Experiments.md)  
[Juno Experiment Schema](./Authoring-ExperimentSchema.md)

### Dependencies

* Active TiP sessions must have been created before this step executes. It dependes upon the existence of TiP nodes in order to determine
  which nodes to powercycle.

### Step Authoring
This section describes how to author an experiment workflow step to instruct Juno to deploy an Intel microcode update (IPU) to Azure
physical nodes associated with an experiment group.

##### Type
The 'type' must be ```Juno.Execution.Providers.Payloads.TipNodePowerCycleProvider```

##### Name and description
The 'name' and 'description' properties are used to describe steps in the experiment runtime workflow.  The values can be set to anything the experiment
author wishes but should generally be accurate descriptions of the purpose of the step.

##### Group
The 'group' defines the experiment group for which the runtime workflow step is associated (e.g. Group A, Group B).

##### Example Definitions
The following section provides examples of the structure of a valid experiment step.


``` json
{
    "type": "Juno.Execution.Providers.Payloads.TipNodePowerCycleProvider",
    "name": "Power cycle the node",
    "description": "Power cycle the node to apply FPGA image",
    "group": "Group B",
    "parameters": (}
}

```