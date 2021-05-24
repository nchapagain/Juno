<div style="font-size:24pt;font-weight:600;color:#1569C7">Juno Alerting - ARM</div>
<br/>

## Alerts
- [Clean up failure](#Clean-up-failure)
 

## Clean up failure
This alert will fire if Juno failed to clean up ARM resources.

- Sev3: There is more than 0 "ArmVmCleanupProvider.ExecuteError" message in the last 4 hours.

### Links
- [Azure Monitor Rule: Juno-dev01](https://ms.portal.azure.com/#blade/Microsoft_Azure_Monitoring/UpdateVNextAlertRuleBlade/ruleInputs/%7B%22alertId%22%3A%22%2Fsubscriptions%2F94f4f5c5-3526-4f0d-83e5-2e7946a41b75%2FresourceGroups%2Fjuno-dev01%2Fproviders%2Fmicrosoft.insights%2Fscheduledqueryrules%2FJuno%20VM%20clean%20up%20failed%22%7D)

### Query
```kusto
traces
| where message == "ArmVmCleanupProvider.ExecuteError"
```