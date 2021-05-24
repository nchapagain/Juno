# Control Goal Daily OFR Precondtion


##### Juno OFR Precondition Provider:

Juno Control Goal Preconditions provider ensures Scheduler doesn't negatively impact health of Azure and Juno.
Experiment target goals will only be triggered if all preconditions are satisfied.

<br>

##### Daily Juno OFR
Juno Scheduler monitors nodes that went to Out for Repair node-state because of JUNO system in last 24 hours.
If the count of OFR is higher than the threshold OFR, Juno will no longer trigger new target goals until the issue is investigated and targetGoals are re-enabled.

<br>

##### Scope:
- Daily Juno OFR threshold: 10 OFRs
- Data Availability Delay : 1 hr+
- Dependencies: *azurecm.kusto.windows.net*
- Node states – OutForRepair
- TiP States – all + during repave

<br>

##### Example Scheduler Definition:
``` json
{
  "type": "Juno.Scheduler.Preconditions.JunoDailyOFRPreconditionProvider",
  "parameters": {
    "dailyOFRThreshold": 5
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
The daily OFRs in the juno system have high volatility when a lot of different experiments are running. It is suggested to not include
this control goal unless there are several experiments across different execution goals with high affinity for eachother that have high sensitivity to OFRs. 

##### Query

[Juno OFR Query](../../Juno.Scheduler.Preconditions/Query/JunoOfr.txt)