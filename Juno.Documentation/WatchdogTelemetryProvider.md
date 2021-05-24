<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Juno Experiment Steps</div>
<br/>

## WatchdogTelemetryProvider
The following documentation illustrates how to define a Juno workflow step to trigger Juno watchdog on demand.

### Preliminaries
It is important to understand the basic schema of an experiment before attempting to author Juno experiment workflow steps.

[Authoring Experiments](./Authoring-Experiments.md)  
[Juno Experiment Schema](./Authoring-ExperimentSchema.md)

### Dependencies

* JunoWatchdog PF must have deployed.
* JunoHostAgent PF must have deployed.

### Step Authoring
This section describes how to author an experiment workflow step to instruct Juno to trigger watchdog that uploads blade configuration.

##### Type
The 'type' must be ```Juno.Execution.Providers.Environment.WatchdogTelemetryProvider```

##### Name and description
The 'name' and 'description' properties are used to describe steps in the experiment runtime workflow.  The values can be set to anything the experiment
author wishes but should generally be accurate descriptions of the purpose of the step.

##### Group
The 'group' defines the experiment group for which the runtime workflow step is associated (e.g. Group A, Group B).

##### Example Definitions
``` json
{
    "type": "Juno.Execution.Providers.Environment.WatchdogTelemetryProvider",
    "name": "Trigger Watchdog on demand",
    "description": "Ondemand upload of blade configuration",
    "group": "Group B"
}
```