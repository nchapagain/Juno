<div style="font-size:24pt;font-weight:600;color:#1569C7">Juno Alerting - TIP</div>
<br/>

## Alerts
- [Leaked Sessions](#Leaked-Sessions)
 

## Leaked Sessions
This alert will fire if we have TiP sessions running longer than a day. 
This typically means we failed to delete session.
We have categorized experiments into impactful and nonimpactful in experimentMetadata.
At the moment, they have same conditions for ICM for both types of experiment.

- Sev3: >5 leaked sessions

### Links
- [Geneva Monitor: leakedTiPSessions](https://jarvis-west.dc.ad.msft.net/settings/mdm?page=settings&mode=mdm&account=crcair&namespace=crcairKustoToMetrics&metric=leakedSession&tab=monitors&monitor=LeakedTipSessions)
- [Geneva Monitor: leakedTiPSessionsForImpactfulExperiment](https://jarvis-west.dc.ad.msft.net/settings/mdm?page=settings&mode=mdm&account=crcair&namespace=crcairKustoToMetrics&metric=leakedSession&tab=monitors&monitor=LeakedTipSessions)
- [KustoToMetricsConnector: Juno-Dev01](https://jarvis-west.dc.ad.msft.net/settings/connectors)
    - Account: crcair
    - Namespace: crcairKustoToMetrics
    - Rules: leakedTiPSessions, leakedTiPSessionsForImpactfulExperiment
    
### Query
```kusto
Cluster: https://azurecm.kusto.windows.net, https://azurecrc.westus2.kusto.windows.net
Database: AzureCM, Azure CRC
Filter:
    - Impactful Experiment Filter: where impactType == "ImpactfulManualCleanup"
    - NonImpactful Experiment Filter: where impactType != "ImpactfulManualCleanup"

let junoUsers = dynamic (["yanpan@microsoft.com", "junosvc@microsoft.com"]);
let newTs= cluster('azurecm.kusto.windows.net').database('AzureCM').LogTipNodeSessionSnapShot
| where TIMESTAMP > ago(10d)
| where createdBy in (junoUsers) 
| where reason == "NewTipNodeSession" 
| project creationTime = TIMESTAMP, tipNodeSessionId;
let stillRunning = cluster('azurecm.kusto.windows.net').database('AzureCM').LogNodeSnapshot
| where TIMESTAMP > ago(1h);
let longrunning = newTs | join stillRunning on tipNodeSessionId
|distinct tipNodeSessionId, creationTime
| where creationTime < ago(27h) | project tipNodeSessionId, creationTime;
let experimentTipData = cluster('azurecrc.westus2.kusto.windows.net').database('JunoIngestion').getTipAttempts(now(-10d),now()) 
| extend experimentId = tostring(parse_json(customDimensions).experimentId)
| extend temeletryTipStartTime = ingestion_time()
|project experimentId, tipSessionId, temeletryTipStartTime;
let leakedSession = longrunning
| join kind = leftouter experimentTipData on $left.tipNodeSessionId == $right.tipSessionId
| join kind = leftouter cluster('azurecrc.westus2.kusto.windows.net').database('JunoIngestion').getExperiments(now(-10d), now()) on $left.experimentId == $right.experimentId
| extend impactType = parse_json(experimentMetadata).impactType
| extend daysLeaked = datetime_diff('day', now(), creationTime)
| project creationTime,tipNodeSessionId,daysLeaked, experimentId, experimentName, impactType;
leakedSession
|extend m_leakedSession=tipNodeSessionId, Timestamp=now(), d_impactType = impactType, d_daysLeaked=daysLeaked
| project Timestamp, d_daysLeaked, m_leakedSession, d_impactType
```