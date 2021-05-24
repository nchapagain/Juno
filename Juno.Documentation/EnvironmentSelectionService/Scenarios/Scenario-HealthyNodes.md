<div style="font-size:24pt;font-weight:600;color:#1569C7">ESS Scenarios</div>
<br/>

# Scenario 1: Healthy Nodes
This scenario shows how to write an environment query that returns a set of healthy nodes in a cluster. 
Use this scenario when there are no attribute requirements for a node.  

## Prerequisites
* Cluster Id must be known.

## Contract Example
This scenario only requires one filter: HealthyNodeProvider. This provider searches for healthy nodes in a given
list of clusters.  
*Remove any inline comments if using filter for experiment.*
``` json
{
    // The name of the query should be unique, but also descriptive.
    "name": "HealthyNodeQuery",
    "nodeCount": 6,
    "nodeAffinity": "SameRack",
    "filters": [
        {
            // Healthy Node Provider searches for healthy nodes in the includeCluster list.
            "type": "Juno.EnvironmentSelection.NodeSelectionFilters.HealthyNodeProvider",
            "parameters": {
                // List out the known cluster(s)
                "includeCluster": "bnz10prdapp05"
            }
        }
    ],
    "parameters": {}
}
```