# Control Goal Overall OFR Precondtion


##### Juno OFR Precondition Provider:

Juno Control Goal Preconditions provider ensures Scheduler doesn't negatively impact health of Azure and Juno.
Experiment target goals will only be triggered if all preconditions are satisfied.

<br>

##### Overall Juno OFR
Juno Scheduler monitors nodes that went to Out for Repair node-state because of JUNO system in last 7 days.
If the count of OFR is higher than the threshold OFR, Juno will no longer trigger new target goals until the issue is investigated and targetGoals are re-enabled.

<br>

##### Scope:
- Overall Juno OFR threshold: 15 OFRs
- Data Availability Delay : 1 hr+
- Dependencies: *azurecm.kusto.windows.net*
- Node states – OutForRepair
- TiP States – all + during repave

<br>

##### Example Scheduler Definition:
``` json
{
  "type": "Juno.Scheduler.Preconditions.JunoOverallOFRPreconditionProvider",
  "parameters": {
    "overallOFRThreshold": 10
  }
}
```

<br>

##### Example Output Parameters
The following parameters will be used creating experiment step.

| TIMESTAMP                  | tipNodeSessionId	                    | nodeId	                            |
| -------------------------- | ------------------------------------ | ------------------------------------- |
|2020-06-15 14:51:01.8016737 | dc2f1307-638a-47da-9a4e-13ee711c6587 | 96dc2b66-df2e-405c-b1ad-796dae9c5519  |

<br>

##### Control Goal Best Practices
The overall OFRs in the juno system should have a higher tolerance since this control goal encapsulates all OFRs produced by any experiment. 
That is why it is recommended to put the `overallOFRThreshold` to atleast a value of 100.

##### Query
[Juno OFR Query](../../Juno.Scheduler.Preconditions/Query/JunoOfr.txt)