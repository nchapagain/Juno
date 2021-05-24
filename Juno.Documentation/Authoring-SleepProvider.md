<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Juno Experiment Steps</div>
<br/>

## SleepProvider
The following documentation illustrates how to define a Juno workflow step to sleep for a duration.

### Preliminaries
It is important to understand the basic schema of an experiment beforehand.

[Authoring Experiments](./Authoring-Experiments.md)  
[Juno Experiment Schema](./Authoring-ExperimentSchema.md)

### Dependencies

None

### Step Authoring
This section describes how to author an experiment workflow step to instruct Juno to sleep for a duration.

##### Type
The 'type' must be ```Juno.Execution.Providers.Diagnostics.SleepProvider```

##### Name and description
The 'name' and 'description' properties are used to describe steps in the experiment runtime workflow.  The values can be set to anything the experiment
author wishes but should generally be accurate descriptions of the purpose of the step.

##### Group
The 'group' doesn't impact the entities in their respective group. It is recommended to put '*' as the group as this step will sleep on that sequence regardless of whatever group has steps on that sequence.

##### Parameters
The following parameters will be used creating experiment step.

| Name                | Required   | Data Type        | Description                |
| ------------------- | ---------- | ---------------- | -------------------------- |
| duration            | Yes        | string/timespan  | Defines the time/duration to sleep.|
| option              | No         | string/enum      | The sleep option for the provider (e.g. Always, WhenAnyPreviousStepsFailed...see below). Default = Always. |

##### Sleep Options
The following options are available to use on the provider. Not that the default 'option' is 'Always'. If the 'option' parameter
is not defined, the provider will sleep on ALL states of the experiment or previous steps.

| Name | Description |
| ---- | ----------- |
| Always                        | The provider will sleep for the duration specified regardless of the state/status of previous experiment steps. |
| WhenAnyPreviousStepsFailed    | The provider will sleep if any 1 or more previous steps have failed (i.e. status = Failed). |
| WhenNoPreviousStepsFailed     | The provider will sleep ONLY if there are no previous steps that have failed (i.e. status = Failed). |

##### Example Definitions
``` json
{
    "type": "Juno.Execution.Providers.Diagnostics.SleepProvider",
    "name": "Sleep for a while",
    "description": "Sleep for an hour",
    "group": "*",
    "parameters": {
        "duration": "01:00:00"
    }
}

{
    "type": "Juno.Execution.Providers.Diagnostics.SleepProvider",
    "name": "Sleep only for debugging",
    "description": "Leave time for triaging if previous step failed.",
    "group": "*",
    "parameters": {
        "duration": "04:00:00",
        "option": "WhenAnyPreviousStepsFailed"
    }
}

{
    "type": "Juno.Execution.Providers.Diagnostics.SleepProvider",
    "name": "Sleep only for debugging",
    "description": "Only sleep if there aren't any previous steps that have failed.",
    "group": "*",
    "parameters": {
        "duration": "04:00:00",
        "option": "WhenNoPreviousStepsFailed"
    }
}
```