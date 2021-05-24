<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Juno Experiment Steps</div>
<br/>

## TipCleanupProvider
The following documentation illustrates how to define a Juno workflow step to cleanup TiP sessions associated with an experiment.

### Preliminaries
It is important to understand the basic schema of an experiment beforehand.

[Authoring Experiments](./Authoring-Experiments.md)  
[Juno Experiment Schema](./Authoring-ExperimentSchema.md)

### Dependencies

* None - If TiP sessions exist, they will be cleaned up. The provider will exit gracefully if TiP sessions do not exist.

### Step Authoring
This section describes how to author an experiment workflow step to instruct Juno to cleanup/delete any TiP sessions that exist for the experiment.

##### Type
The 'type' must be ```Juno.Execution.Providers.Environment.TipCleanupProvider```

##### Name and description
The 'name' and 'description' properties are used to describe steps in the experiment runtime workflow.  The values can be set to anything the experiment
author wishes but should generally be accurate descriptions of the purpose of the step.

##### Group
The 'group' defines the experiment group for which the runtime workflow step is associated (e.g. Group A, Group B).

##### Example Definitions
``` json
{
    "type": "Juno.Execution.Providers.Environment.TipCleanupProvider",
    "name": "Delete TiP Sessions",
    "description": "Deletes any TiP sessions for the experiment group returning them to the Azure production pool for reimaging.",
    "group": "Group A"
}
```