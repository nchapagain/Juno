# Control Goal Experiment OFR Precondtion


##### Juno OFR Precondition Provider:

Juno Control Goal Preconditions provider ensures Scheduler doesn't negatively impact health of Azure and Juno.
Experiment target goals will only be triggered if all preconditions are satisfied.

<br>

##### Experiment Goal OFR:
Juno Scheduler monitors nodes that went to Out for Repair node-state because of the given experiment Juno system was running in last 48 hours.
If the count of OFR is higher than the threshold OFR, Juno will no longer trigger new target goals until the targetGoal is investigated and re-enabled.

<br>

##### Scope:
- Experiment Goal OFR threshold: 2 OFRs
- Data Availability Delay : 2 hr+
- Dependencies: *azurecm.kusto.windows.net* & *azurecrc.westus2.kusto.windows.net*
- Node states – OutForRepair
- TiP States – all + during repave

<br>

##### Example Scheduler Definition:
``` json
{
  "type": "Juno.Scheduler.Preconditions.JunoExperimentGoalOFRPreconditionProvider",
  "parameters": {
    "experimentOFRThreshold": 3
  }
}
```

<br>

##### Example Output Parameters
The following parameters will be used creating experiment step.

| TIMESTAMP                  | tipNodeSessionId	                    | nodeId	                            | experimentName                        | experimentId                          |
| -------------------------- | ------------------------------------ | ------------------------------------- | ------------------------------------- | ------------------------------------- |
|2020-06-15 14:51:01.8016737 | gc2f1307-638a-47da-9a4e-13ee711c6587 | 26dc2b66-df2e-405c-b1ad-796dae9c5519  | 82gc2b66-df2e-405c-b1ad-796dae9c5519  | SSDPreDeployment_NoCleanup            |



	
29660e43-ae44-4931-a8aa-68dfbd1be3f0	
29660e43-ae44-4931-a8aa-68dfbd1be3f0	SSDPreDeployment_NoCleanup

<br>

##### Control Goal Best Practices
This control goal helps in monitoring the experiment and its individual impact on the azure fleet. Unlike daily and overall OFRs this provider monitors
at the granularity of an experiment. That is why this control goal should have the tightest threshold. It is recomended that this threshold should be set
to 20. _Of course if the tolerance for OFRs is much lower (Firmware/Hardward experiments) the experiment author must adjust accordingly._

##### Query

[Juno Experiment OFR Query](../../Juno.Scheduler.Preconditions/Query/ExperimentOfr.txt)