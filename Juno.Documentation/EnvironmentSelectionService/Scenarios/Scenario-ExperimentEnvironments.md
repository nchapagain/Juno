<div style="font-size:24pt;font-weight:600;color:#1569C7">ESS Scenarios</div>
<br/>

# Scenario #4: Experiment Environments
This scenario shows how to write an environment query that returns a set of nodes and a subscription that can be used
in an experiment.  
Nodes that are ready to be used in an experiment have the following requirements:  
* The node is healthy
* The node belongs to a cluster that supports a deployment of another tip session
* The node belongs to a cluster that supports a deployment of another VM.

A subscription is experiment ready if it can support the deployment of the desired count of VMs.

## Prerequisites
* Desired VM Sku must be known

## Contract Example
This scenario requires five filters: 
* QuotaLimitFilterProvider: This provider searches for subscription that contain enough quota for another deplyoment.
* ResourceGroupFilterProvider: This provider searches for subscriptions that can support another resource group.
* VmSkuFilterProvider: This provider searches for clusters that can support another VM being deployed.
* TipClusterProvider: This provider searches for clusters that can support another Tip Session in a given list of regions.
* HealthyNodeProvider: This provider searches for healthy nodes in a given list of clusters.  
*Remove any inline comments if using filter for experiment.*
``` json
{
    // The name of the query should be unique, and descriptive.
    "name": "ExperimentEnvironmentQuery",
    "nodeCount": 6,
    "nodeAffinity": "SameRack",
    "filters": [
        {
            // Quota limit Filter provider searches for subscriptions, regions, and vmskus that
            // have enough quota in the execution environment
            "type": "Juno.EnvironmentSelection.SubscriptionFilters.QuotaLimitFilterProvider",
            "parameters": {
                // Supply hints to the Quota limit filter provider on which vmsku to search for.
                "includeVmSku": "Standard_D2s_v3"
            }
        },
        {
            // Resource Group Filter provider searches for subscriptions that have enough capacity left
            // to support another resource group.
            "type": "Juno.EnvironmentSelection.SubscriptionFilters.ResourceGroupFilterProvider"
        },
        {
            // Vm Sku provider searches for clusters that can deploy one of the vms
            // in the includeVmSku which reside in one of the regions in the regionList
            "type": "Juno.EnvironmentSelection.ClusterSelectionFilters.VmSkuFilterProvider",
            "parameters": {
                // Refer to regions supplied by the subscription providers.
                "includeRegion": "$.subscription.regionSearchSpace",
                // Refer to vm skus supplied by the subscription providers to only include vm skus
                // that the environment has quota for.
                "includeVmSku": "$.subscription.vmSkuSearchSpace"
            }
        },
        {
            // Tip Cluster provider searches for tippable clusters in the includeRegion list.
            "type": "Juno.EnvironmentSelection.ClusterSelectionFilters.TipClusterProvider",
            "parameters": {
                // Refer to regions supplied by the subscription providers.
                "includeRegion": "$.subscription.regionSearchSpace"
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
    "parameters": {}
}
```