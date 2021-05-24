<div style="font-size:24pt;font-weight:600;color:#1569C7">ESS Scenarios</div>
<br/>

# Scenario 2: Tip-Able Nodes
This scenario shows how to write an environment query that returns a set of healthy nodes whom belong to a cluster
that can support more tip sessions in a region. Use this scenario when there are no attribute requirements for a node to have, 
but the node should belong to a cluster that can support another tip session.


## Prerequisites
* External Region Name must be known.

## Contract Example
This scenario only requires two filters: 
* TipClusterProvider: This provider search for clusters that can support another Tip Session in a given list of regions.
* HealthyNodeProvider: This provider searches for healthy nodes in a given list of clusters.  
*Remove any inline comments if using filter for experiment.*
``` json
{
    // The name of the query should be unique, and descriptive.
    "name": "HealthyTipaleNodeQuery",
    "nodeCount": 6,
    "nodeAffinity": "SameRack",
    "filters": [
        {
            // Tip Cluster provider searches for tippable clusters in the includeRegion list.
            "type": "Juno.EnvironmentSelection.ClusterSelectionFilters.TipClusterProvider",
            "parameters": {
                "includeRegion": "East US 2"
            }
        },
        {
            // Healthy Node Provider searches for healthy nodes in the includeCluster list.
            "type": "Juno.EnvironmentSelection.NodeSelectionFilters.HealthyNodeProvider",
            "parameters": {
                // Use cluster(s) found from the tip cluster provider.
                "includeCluster": "$.cluster.clusters"
            }
        }
    ],
    "parameters": {}
}
```