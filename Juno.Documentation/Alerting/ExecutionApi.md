<div style="font-size:24pt;font-weight:600;color:#1569C7">Juno Alerting - ExecutionApi</div>
<br/>

## Alerts
- [HeartBeat](#HeartBeat)
 

## HeartBeat
This alert will fire if Execution API does not send "ExperimentNotificationManager.GetNoticeStop", which means the Execution API is either down or stopped. 
Another possibility is EOS is down so that nothing is quering execution API, there would be a seperate alert for EOS down.
- Sev3: no heartbeat in last 30 minutes

### Links
- [Azure Monitor Rule: Juno-dev01](https://ms.portal.azure.com/#blade/Microsoft_Azure_Monitoring/UpdateVNextAlertRuleBlade/ruleInputs/%7B%22alertId%22%3A%22%2Fsubscriptions%2F94f4f5c5-3526-4f0d-83e5-2e7946a41b75%2FresourceGroups%2Fjuno-dev01%2Fproviders%2Fmicrosoft.insights%2Fscheduledqueryrules%2FJuno-dev01%20ExecutionApi%20down%22%7D)

### Query
```kusto
traces
| where message contains "ExperimentNotificationManager.GetNoticeStop" 
```