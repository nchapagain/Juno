<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Goal Components</div>
<br/>

# Schedule Action: SelectEnvironmentAndCreateExperiment


## Select Environment And Create Experiment Provider:

This provider submits an environment query to ESS and populates several parameters at runtime and submits the experiment to the 
Juno Experiments API.
<br>

## Example Scheduler Definition:
``` json
"actions": [
    {
      "type": "Juno.Scheduler.Actions.SelectEnvironmentAndCreateExperimentProvider",
      "parameters": {
        "vmSku": "Standard_F2s_v2",
        "profile": "PERF_IO.v1",
        "vmDensity": 10,
        "experimentTemplateFileName": "MPU2019.2_Patrol_Scrubber_No_Watson.Template.json"
      }
    }
]
```

## Runtime Parameter Population 
GBS retrieves certain parameters from external systems at runtime (ESS). To appropiately pass this information into the experiment definition
GBS takes the results and stores them in the parameter dictionary for the provider at runtime. There are __three__ parameters that are populated at
runtime:

| Parameter Name | Description |
|-----------------|------------|
| nodes | List of node Ids returned from ESS |
| subscription | Subscription ID |
| vmSku | List of vmSkus that each node can support|

These parameters __WILL NOT__ be populated if they are already present in the parameter dictionary at runtime.

The parameter `vmSku` is validated at runtime with the following rule: If the parameter vmSku is not provided in the parameter dictionary before execution
the value for `vmSku` is derived between the set intersection of all nodes supported vmSku list. If there are no results after the aggregate set intersection
the error: `"There are no VMs that all nodes in candidate node list support."`.  
If you are running experiments that do not need vmSkus please put  
`vmSku: N/A` for the kv pair in the dictionary.