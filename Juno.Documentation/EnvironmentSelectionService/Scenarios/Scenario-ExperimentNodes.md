<div style="font-size:24pt;font-weight:600;color:#1569C7">ESS Scenarios</div>
<br/>

# Scenario #3: Experiment Nodes
This scenario shows how to write an environment query that returns a set of nodes that can be used in an experiment.
Nodes that are ready to be used in an experiment have the following requirements:  
* The node is healthy
* The node belongs to a cluster that supports a deployment of another tip session
* The node belongs to a cluster that supports a deployment of another VM.


## Prerequisites
* External Region Name must be known
* Desired VM Sku must be known

## Contract Example
This scenario requires three filters: 
* VmSkuFilterProvider: This provider searches for clusters that can support another VM being deployed.
* TipClusterProvider: This provider searches for clusters that can support another Tip Session in a given list of regions.
* HealthyNodeProvider: This provider searches for healthy nodes in a given list of clusters.  
*Remove any inline comments if using filter for experiment.*
``` json
{
    // The name of the query should be unique, and descriptive.
    "name": "ExperimentNodeQuery",
    "nodeCount": 6,
    "nodeAffinity": "SameRack",
    "filters": [
        {
            // Vm Sku provider searches for clusters that can deploy one of the vms
            // in the includeVmSku which reside in one of the regions in the regionList
            "type": "Juno.EnvironmentSelection.ClusterSelectionFilters.VmSkuFilterProvider",
            "parameters": {
                // Refer to region parameter to reduce authoring mistakes across providers.
                "includeRegion": "$.external.regions",
                "includeVmSku": "Standard_D2s_v3"
            }
        },
        {
            // Tip Cluster provider searches for tippable clusters in the includeRegion list.
            "type": "Juno.EnvironmentSelection.ClusterSelectionFilters.TipClusterProvider",
            "parameters": {
                // Refer to region parameter to reduce authoring mistakes across providers.
                "includeRegion": $.external.regions"
            }
        },
        {
            // Healthy Node Provider searches for healthy nodes in the includeCluster list.
            "type": "Juno.EnvironmentSelection.NodeSelectionFilters.HealthyNodeProvider",
            "parameters": {
                // Use cluster(s) found from the tip cluster/vm sku providers.
                "includeCluster": "$.cluster.clusters"
            }
        }
    ],
    "parameters": {
        "regions": "East US 2"
    }
}
```