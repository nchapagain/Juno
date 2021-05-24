# Control Goal Daily OFR Precondtion


##### Failure Rate Precondition Provider:

Juno Control Goal Preconditions provider ensures Scheduler doesn't negatively impact health of Azure and Juno.
Experiment target goals will only be triggered if all preconditions are satisfied.

<br>

##### Failure Rate
This provider monitors an individual target goal's failure rate. The failure rate is calculated by the number of failed experiments divided by the
number of successful experiments plus the number of failed experiments plus the number of in progress experiments. 

<br>

##### Scope:
- Data Availability Delay : 15m


<br>

##### Example Scheduler Definition:
``` json
{
    "type": "Juno.Scheduler.Preconditions.FailureRatePreconditionProvider",
    "parameters": {
        "minimumExperimentInstance": 2,
        "targetFailureRate": 75,
        "daysAgo": 2
    }
}
```

<br>

##### Control Goal Best Practices
The failure rate precondition provider monitors the failure rate of each target goal. To allow for errors in smoke testing to be neglected
it is suggested that the parameter: `minimumExperimentInstance` be set to 15 - 20 (or the scale of the smoke test). The target failure rate should be
set to the expected failure rate. For example as of January 11th 2021, historically, experiments have had failure rates from 75%+ to 30%. This varies extremely 
per experiment.

##### Parameters
| Name | Values | Description |
|-|-|
| minimumExperimentInstance | 0 - IntMax | Number of experiments that must have completed before evaluation.
| targetFailureRate | 0 - 100 | The failure rate threshold.
| daysAgo | 0 - 14d | The look back time for query 

##### Query

[Juno OFR Query](../../Juno.Scheduler.Preconditions/Query/JunoOfr.txt)