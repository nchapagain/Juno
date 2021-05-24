# Action Goal Launch Experiment Instances


##### Juno Create Experiment Provider:

Juno Create Experiment Provider ensures Experiment is launched using Juno Experiment REST API client.
<br>

##### Create Experiment Instance:
Juno Scheduler calls Experiment API to launch new experiment instance with appropriate parameters for the experiment.
This provider is triggered when Juno Scheduler meets criteria for both control goals and target goals defined by the user.
<br>

##### Example Scheduler Definition:
``` json
"actions": [
    {
      "type": "Juno.Scheduler.Actions.CreateExperimentProvider",
      "parameters": {
        "vmSku": "Standard_F2s_v2",
        "profile": "PERF_IO.v1",
        "vmDensity": 10,
        "subscription": "sub1",
        "experimentTemplateFileName": "MPU2019.2_Patrol_Scrubber_No_Watson.Template.json"
      }
    }
]
```
