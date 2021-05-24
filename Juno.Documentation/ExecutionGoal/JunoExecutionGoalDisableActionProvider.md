# Control Action ExecutionGoal Disabler

##### Juno Execution Goal Disable Provider:

Juno Control Action Disabler provider ensures Juno Schedule is gracefully disabled thus no longer triggering further experiments.

<br>

##### Juno Edit Target Goal
Juno Scheduler calls this action provider when the experiment no longer meets its precondition.
Juno will no longer trigger new target goals until the issue is investigated and schedule is re-enabled.



##### Scope:
- Resource: junodev01scheduletables
- Table: executionGoalTriggers
- Column: Enabled

<br>

##### Example Scheduler Definition:
``` json
"actions": [
    {
      "type": "Juno.Scheduler.Actions.JunoExecutionGoalDisableProvider",
      "parameters": {
        "executionGoalName": "Qos.json"
      }
    }
]     
```
