## Schedule Action: CreateDistinctGroupExperimentProvider


##### Juno Create Distinct Group Experiment Provider:

Launches an experiment using the environment queries supplied and assiging the results of mentioned enviornment
queries to the parameter that it was supplied with.
<br>

##### Create Experiment Instance:
Juno Scheduler calls Experiment API to launch new experiment instance with appropriate parameters for the experiment.
This provider is triggered when Juno Scheduler meets criteria for both control goals and target goals defined by the user.
<br>

##### Example Scheduler Definition:
``` json
"actions": [
    {
      "type": "Juno.Scheduler.Actions.CreateDistinctGroupProvider",
      "parameters": {
        "experimentTemplateFileName": "MPU2019.2_Patrol_Scrubber_No_Watson.Template.json",
        "nodeListA": {
            "parameterType": "Juno.Contracts.EnvironmentQuery",
            "definition": {
                ... your environment query ...
            }
        }
      }
    }
]
```