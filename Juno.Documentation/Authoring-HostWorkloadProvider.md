<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Juno Experiment Steps</div>
<br/>

## HostWorkloadProvider
The following documentation illustrates how to define a Juno workflow step to run workload on physical host as part of Juno experiment.

### Preliminaries
It is important to understand the basic schema of an experiment beforehand.

[Authoring Experiments](./Authoring-Experiments.md)  
[Juno Experiment Schema](./Authoring-ExperimentSchema.md)

### Dependencies

* The Juno Host agent must have been deployed to the target physical host in the environment. Juno Host Agent is deployed to all experiments by default.

##### Type
The 'type' must be ```Juno.Execution.Providers.Workloads.HostWorkloadProvider```

##### Name and description
The 'name' and 'description' properties are used to describe steps in the experiment runtime workflow.  The values can be set to anything the experiment
author wishes but should generally be accurate descriptions of the purpose of the step.

##### Group
The 'group' defines the experiment group for which the runtime workflow step is associated (e.g. Group A, Group B).

##### Parameters
The following parameters will be used when authoring the experiment step.

| Name                | Required   | Data Type        | Description                |
| ------------------- | ---------- | ---------------- | -------------------------- |
| command             | Yes        | string           | The relative path to the VirtualClient.exe (Absolute path will be based on tipsessionchangeid folder e.g D:\\App\\JunoHostAgent.Tip<TipchangeId>\\VirtualClient\\VirtualClient.exe).
| commandArguments    | Yes        | string           | The command-line arguments to supply to the VirtualClient.exe (e.g. --profile=PERF-IO-V1.json).
| duration            | Yes        | timespan         | A timespan representing the amount of time the VirtualClient.exe should be allowed to run before it will be stopped/terminated (e.g. 01.00:00:00).
| timeout             | Yes        | timespan         | A timespan that defines the absolute timeout for the step as a whole (e.g. 01.01:00:00). This takes priority over the 'duration' parameter.

##### Example Definitions
``` json
{
    "type": "Juno.Execution.Providers.Workloads.HostWorkloadProvider",
    "name": "Run Virtual client",
    "description": "Run Virtual client",
    "group": "Group A",
    "parameters": {
        "command": "VirtualClient\\VirtualClient.exe",
        "commandArguments": "--profile=PERF-IO-V1.json --platform=Juno",
        "duration": "01.00:00:00",
        "timeout": "01.01:00:00"
    }
}
```

