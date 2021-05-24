# Successful Experiments Precondition Provider

#### Successful Experiments Precondition Provider
The successful experiments provider evaluates how many successful experiment instances have ran to completion.

This is usually a precondition inside of a target goal. This is because when the number of experiments exceed the value given
by the parameter: `targetExperimentInstances` the precondition will evaluate to false, thus disallowing the execution of any associated actions.

#### Scope
- Data Availability Delay: 15m
- Dependencies: Kusto
- Target Goal States:
    - Should be executed
    - Should not be executed

#### Example Scheduler Definition
``` json
    {
        "type": "Juno.Scheduler.Preconditions.SuccessfulExperimentsProvider",
        "parameters": {
            "targetExperimentInstances": 170
        }
    }
```

##### Parameters
| Name | Values | Description |
|-|-|-|
| targetExperimentInstances | 0 - MaxInt | # of Successful experiments.
| daysAgo | 0 - 14 | Lookback duration.