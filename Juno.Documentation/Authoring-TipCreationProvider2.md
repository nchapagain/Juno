<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Juno Experiment Steps</div>
<br/>

## TipCreationProvider
The following documentation illustrates how to define a Juno workflow step to cleanup TiP sessions associated with an experiment.

### Preliminaries
It is important to understand the basic schema of an experiment beforehand.

[Authoring Experiments](./Authoring-Experiments.md)  
[Juno Experiment Schema](./Authoring-ExperimentSchema.md)

### Dependencies

* Clusters and nodes that match the requirements of the experiment must be selected and identified.

### Step Authoring
This section describes how to author an experiment workflow step to instruct Juno to create a TiP session to isolate one or more physical
nodes in the environment for the experiment.

##### Type
The 'type' must be ```Juno.Execution.Providers.Environment.TipCreationProvider2```

##### Name and description
The 'name' and 'description' properties are used to describe steps in the experiment runtime workflow.  The values can be set to anything the experiment
author wishes but should generally be accurate descriptions of the purpose of the step.

##### Group
The 'group' must be defined as '*'. The ```TipCreationProvider``` is designed to establish TiP sessions for ALL groups in the experiment.

##### Parameters
The following parameters will be used creating experiment step.

| Name                | Required   | Data Type        | Description                |
| ------------------- | ---------- | ---------------- | -------------------------- |
| nodeAffinity        | No         | string/enum      | Specifies the selection strategy/affinity for TiP nodes that will be acquired as part of the experiment (see below).
| isAmberNodeRequest  | No         | bool             | Specifies whether the tip sessions should be created on Amber or probation nodes. Default is false.

##### Node Affinity
The following describes the valid options for defining the TiP node affinity. In essence, this instructs the Juno system as to how "close together" nodes should
be to satisfy the requirements of the experiment.

| Affinity            | Description                |
| ------------------- | -------------------------- |
| SameRack            | Default. This option specifies that the nodes selected for the groups in the experiment (e.g. Group A, Group B) must be on the same rack within the cluster.
| SameCluster         | This option specifies that the nodes selected for the groups in the experiment (e.g. Group A, Group B) must be in the same cluster but NOT on the same rack.
| DifferentCluster    | This option specifies that the nodes selected for the groups in the experiment must exist in different clusters entirely.


##### Example Definitions
``` json
{
    "type": "Juno.Execution.Providers.Environment.TipCreationProvider2",
    "name": "Create TiP Sessions",
    "description": "Creates TiP sessions for all experiment groups to isolate a set of physical nodes from customer workloads in an Azure data center.",
    "group": "*"
}

{
    "type": "Juno.Execution.Providers.Environment.TipCreationProvider2",
    "name": "Create TiP Sessions",
    "description": "Creates TiP sessions for all experiment groups to isolate a set of physical nodes from customer workloads in an Azure data center.",
    "group": "*",
    "parameters": {
        "nodeAffinity": "SameRack"
    }
}

{
    "type": "Juno.Execution.Providers.Environment.TipCreationProvider2",
    "name": "Create TiP Sessions",
    "description": "Creates TiP sessions for all experiment groups to isolate a set of physical nodes from customer workloads in an Azure data center.",
    "group": "*",
    "parameters": {
        "nodeAffinity": "SameCluster"
    }
}

{
    "type": "Juno.Execution.Providers.Environment.TipCreationProvider2",
    "name": "Create TiP Sessions",
    "description": "Creates TiP sessions for all experiment groups to isolate a set of physical nodes from customer workloads in an Azure data center.",
    "group": "*",
    "parameters": {
        "nodeAffinity": "DifferentCluster"
    }
}

{
    "type": "Juno.Execution.Providers.Environment.TipCreationProvider2",
    "name": "Create TiP Sessions",
    "description": "Creates TiP sessions for all experiment groups to isolate a set of physical nodes from customer workloads in an Azure data center.",
    "group": "*",
    "parameters": {
        "isAmberNodeRequest": true
    }
}

{
    "type": "Juno.Execution.Providers.Environment.TipCreationProvider2",
    "name": "Create TiP Sessions",
    "description": "Creates TiP sessions for all experiment groups to isolate a set of physical nodes from customer workloads in an Azure data center.",
    "group": "*",
    "parameters": {
        "isAmberNodeRequest": true,
        "nodeAffinity": "SameRack"
    }
}
```

Whenever the author is using explicit node affinities, there is a dependency on any Execution Goals used in the scheduling of the experiment. In short, whatever node affinity
is defined in the provider step MUST also be defined in the Execution Goal. The following example shows a portion of an Execution Goal where the target goals are defined. The
node affinity must be set in the environment query portion of the target goal definition.

See the 'ExecutionGoals' folder within this documentation for more detailed information on authoring Execution Goals.

``` json
"targetGoals": [
    {
        "name": "Test_NodeAffinity_Selection_SameRack",
        "preconditions": [
            {
                "type": "Juno.Scheduler.Preconditions.TimerTriggerProvider",
                "parameters": {
                    "cronExpression": "15 */1 * * *"
                }
            }
        ],
        "actions": [
            {
                "type": "Juno.Scheduler.Actions.CreateDistinctGroupExperimentProvider",
                "parameters": {
                    "experimentTemplateFile": "Test_NodeAffinity_Selection.Template.json",
                    "experiment.name": "Test_NodeAffinity_Selection",
                    "metadata.experimentType": "Test",
                    "metadata.generation": "Gen5",
                    "metadata.nodeCpuId": "406f1",
                    "metadata.payload": "N/A",
                    "metadata.payloadType": "N/A",
                    "metadata.payloadVersion": "N/A",
                    "metadata.payloadPFVersion": "N/A",
                    "metadata.workload": "PERF-CPU-V1",
                    "metadata.workloadType": "VirtualClient",
                    "metadata.workloadVersion": "N/A",
                    "workQueue": "experimentnotices-bryan",
                    "nodeAffinity": "SameRack",
                    "vmCount": 1,
                    "osPublisher": "MicrosoftWindowsServer",
                    "osOffer": "WindowsServer",
                    "osSku": "2019-Datacenter",
                    "osVersion": "latest",
                    "dataDiskCount": 2,
                    "dataDiskSizeInGB": 32,
                    "diskSku": "Standard_LRS",
                    "nodeList": {
                        "parameterType": "Juno.Contracts.EnvironmentQuery",
                        "definition": {
                            "name": "Test_NodeAffinity_Selection_Query",
                            "nodeCount": 6,
                            "nodeAffinity": "SameRack",  // Note that the node affinity here must match
                            "filters": [
                                {
                                    "type": "Juno.EnvironmentSelection.SubscriptionFilters.QuotaLimitFilterProvider",
                                    "parameters": {
                                        "requiredInstances": 1,
                                        "includeVmSku": "Standard_D2s_v3,Standard_D2_v3"
                                    }
                                },
                                {
                                    "type": "Juno.EnvironmentSelection.ClusterSelectionFilters.TipClusterProvider",
                                    "parameters": {
                                        "includeRegion": "$.subscription.regionSearchSpace"
                                    }
                                },
                                {
                                    "type": "Juno.EnvironmentSelection.ClusterSelectionFilters.VmSkuFilterProvider",
                                    "parameters": {
                                        "includeRegion": "$.subscription.regionSearchSpace",
                                        "includeVmSku": "$.subscription.vmSkuSearchSpace"
                                    }
                                }
                            ],
                            "parameters": {}
                        }
                    }
                }
            }
        ]
    }
]
```