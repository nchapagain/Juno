<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Juno Experiment Steps</div>
<br/>

## BiosSettingsProvider
The following documentation illustrates how to define a Juno workflow step to collect BIOS properties and upload to storage blob on physical host as part of Juno experiment.

### Preliminaries
It is important to understand the basic schema of an experiment beforehand.

[Authoring Experiments](./Authoring-Experiments.md)  
[Juno Experiment Schema](./Authoring-ExperimentSchema.md)

### Dependencies

* The Juno Host agent must have been deployed to the target physical host in the environment. Juno Host Agent is deployed to all experiments by default.

##### Type
The 'type' must be ```Juno.Execution.Providers.Diagnostics.BiosSettingsProvider```

##### Name and description
The 'name' and 'description' properties are used to describe steps in the experiment runtime workflow.  The values can be set to anything the experiment
author wishes but should generally be accurate descriptions of the purpose of the step.

##### Group
The 'group' defines the experiment group for which the runtime workflow step is associated (e.g. Group A, Group B).

##### Parameters
None

##### Example Definitions
``` json
{
    "type": "Juno.Execution.Providers.Diagnostics.BiosSettingsProvider",
    "name": "Collect BIOS Properties",
    "description": "Collect BIOS Properties and Upload to blob Storage",
    "group": "Group A"
}
```

