<div style="font-size:24pt;font-weight:600;color:#1569C7">Juno Experiment Telemetry</div>
<br/>


## Juno Telemetry Principles
All telemetry events emitted by the Juno system structure the event data definitions the same way. This makes it much easier to query the data in the backing telemetry 
stores. The Juno system follows the same principles that guide Microsoft-wide initiatives to structure telemetry data so that it can provide very rich insights into the
operations of a service. In fact, some of the members of the CRC AIR team were a part the initiatives in the COSINE/Windows organization and contributed to the foundations
of the principles that are used today across the organization. Telemetry data when structured well should enable service owners and customers of the Juno system to answer
just about any question they might have about the operations of the system or about the state of experiments running within it.

The following section describes the principles that guide the way telemetry is emitted from the Juno system.

* Events must have a consistent structure/schema.
* Events must use consistent naming conventions
* Events must enable correlation in a distributed architecture.
* Events must enable correlation in an individual process.


##### Event Structure
Every event that is emitted from the Juno system is structured in a very specific way. CT

## Application Insights
The Juno system uses Application Insights to store telemetry data emitted during the execution of an experiment. The following section provides information on
the structure of the telemetry events and the type of data within.

## Kusto Queries
The following section provides examples of common Kusto queries that can be used with Application Insights or Kusto data stores to surface information about the operations
of the Juno system.

##### Experiment Execution
This query aggregates all of the E2E events that are emitted to describe the execution of individual steps associated with a Juno experiment.

```
let experiment = "7a44c642-ca79-41f5-b025-ac92d50ed736";
let experimentSteps = traces
| where timestamp >= ago(1d)
| where message == "ExecutionManager.StepEnd"
| extend context = todynamic(customDimensions)
| extend step = todynamic(tostring(context.stepInstance))
| extend stepDefinition = todynamic(step.definition) 
| extend experimentId = trim("\"", tostring(context.experimentId))
| where experimentId == experiment
| project timestamp, experimentId, message, step, stepDefinition;
experimentSteps
| extend stepId = step.id
| extend stepName = stepDefinition.name
| extend stepGroup = step.experimentGroup
| extend stepBegin = step.startTime
| extend stepEnd = step.endTime
| extend stepDuration = todatetime(stepEnd) - todatetime(stepBegin)
| project timestamp, experimentId, stepId, stepGroup, stepName, stepBegin, stepEnd, stepDuration
| order by timestamp asc
```



