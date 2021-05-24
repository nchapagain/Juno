<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Juno Experiment Steps</div>
<br/>

## ArmVmCleanupProvider
The following documentation illustrates how to define a Juno workflow step to delete virtual machines and resources groups in Azure subscriptions
used as part of a Juno experiment.
 
### Preliminaries
It is important to understand the basic schema of an experiment beforehand.

[Authoring Experiments](./Authoring-Experiments.md)  
[Juno Experiment Schema](./Authoring-ExperimentSchema.md)

### Dependencies

* None

### Step Authoring
This section describes how to author an experiment workflow step to instruct Juno to cleanup virtual machine resources
in the environment.

##### Type
The 'type' must be ```Juno.Execution.Providers.Dependencies.ArmVmCleanupProvider```

##### Name and Description
The 'name' and 'description' properties are used to describe steps in the experiment runtime workflow.  The values can be set to anything the experiment
author wishes but should generally be accurate descriptions of the purpose of the step.

##### Group
The 'group' defines the experiment group for which the runtime workflow step is associated (e.g. Group A, Group B).

##### Parameters
The following parameters will be used creating experiment step.

| Name                | Required   | Data Type        | Description                |
| ------------------- | ---------- | ---------------- | -------------------------- |
| timeout             | No         | string/timespan  | Timeout defines the amount of time it can take to delete resource group. Default is 20 minutes.

##### Example Definitions
``` json
{
    "type": "Juno.Execution.Providers.Environment.ArmVmCleanupProvider",
    "name": "Delete Virtual Machines",
    "description": "Delete virtual machines and resource groups containing them for group A.",
    "group": "Group A"
}
```
