<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Juno Experiment Steps</div>
<br/>

## TipCreationProvider
The following documentation illustrates how to define a Juno workflow step to cleanup TiP sessions associated with an experiment.

### Preliminaries
It is important to understand the basic schema of an experiment beforehand.

[Authoring Experiments](./Authoring-Experiments.md)  
[Juno Experiment Schema](./Authoring-ExperimentSchema.md)

### Dependencies

* Clusters and nodes that match the requirements of the experiment must be selected and identified.

### Step Authoring
This section describes how to author an experiment workflow step to instruct Juno to create a TiP session to isolate one or more physical
nodes in the environment for the experiment.

##### Type
The 'type' must be ```Juno.Execution.Providers.Environment.TipCreationProvider```

##### Name and description
The 'name' and 'description' properties are used to describe steps in the experiment runtime workflow.  The values can be set to anything the experiment
author wishes but should generally be accurate descriptions of the purpose of the step.

##### Group
The 'group' must be defined as '*'. The ```TipCreationProvider``` is designed to establish TiP sessions for ALL groups in the experiment.

##### Parameters
The following parameters will be used creating experiment step.

| Name                | Required   | Data Type        | Description                |
| ------------------- | ---------- | ---------------- | -------------------------- |
| isAmberNodeRequest  | No         | bool             | Specifies whether the tip sessions should be created on Amber or probation nodes. Default is false.
| nodeTag             | No         | string           | Comma/semicolumn delimited tags which will be tagged on tip sessions. (e.g. "T1,T2,T3")
| count               | No         | int              | Defines the number of TiP nodes to create/acquire for the group(s). Defaults to 1.


##### Example Definitions
``` json
{
    "type": "Juno.Execution.Providers.Environment.TipCreationProvider",
    "name": "Create TiP Sessions",
    "description": "Creates TiP sessions for all experiment groups to isolate a set of physical nodes from customer workloads in an Azure data center.",
    "group": "*"
}

{
    "type": "Juno.Execution.Providers.Environment.TipCreationProvider",
    "name": "Create TiP Sessions",
    "description": "Creates TiP sessions for all experiment groups to isolate a set of physical nodes from customer workloads in an Azure data center.",
    "group": "*",
    "parameters": {
        "isAmberNodeRequest": true,
        "count": 2
        "nodeTag": "A1,A2,B1,B2"
    }
}
```