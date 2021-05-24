# Scenario 7: Limited Scale Experiments
This scenario should be used when the number of nodes expected to be retrieved from ESS does not come close to the amount of quota
available for the desired vm sku to be launched on the environments. For example if there is to be 10 nodes utilized with 10 vms deployed 
on each node, such that each vm is comprised of 10 cores, the total quota that would be cosumed in this case is: 1000 cores. If the total
number of cores allocated for the quota (aggregate across all regions) is: 10,000 cores, then this use case fits the definiton of a low 
scale experiment. 

The largest difference between a common use case experiment and a limited scale experiment is that there is no use of the quota limit filter.
Since the subscription is trivial in this case, there is no need to search for a suitable one.

Although if the use case requires that a subscription is supplied be sure to find the right subscription by either running a serperate
environment query before hand to find a suitable subscription, or logging on to the portal to find sufficient region and subscription combinations
for the desired use case.

## Prerequisites
* Use case fits definition of "Low Scale"
* A desired vm sku must be known.
* Regions to target must be known.

[See Scenario 7](./Scenario-LowScaleEnvironments.md)

## Contract Example
This scenario requires six filters: 
* VmSkuFilterProvider: This provider searches for clusters that can support another VM being deployed.
* TipClusterProvider: This provider searches for clusters that can support another Tip Session in a given list of regions.
* HealthyNodeProvider: This provider searches for healthy nodes in a given list of clusters.  
*Remove any inline comments if using filter for experiment.*
``` json
{
    // The name of the query should be unique, and descriptive.
    "name": "LowScaleQuery",
    "nodeCount": 6,
    "nodeAffinity": "SameRack",
    "filters": [
        {
            // Vm Sku provider searches for clusters that can deploy one of the vms
            // in the includeVmSku which reside in one of the regions in the regionList
            "type": "Juno.EnvironmentSelection.ClusterSelectionFilters.VmSkuFilterProvider",
            "parameters": {
                // Refer to regions supplied by the property parameters.
                "includeRegion": "$.external.regionSearchSpace",
                // Refer to vm skus supplied by the subscription providers to only include vm skus
                // that the environment has quota for.
                "includeVmSku": "Standard_D2_v3"
            }
        },
        {
            // Tip Cluster provider searches for tippable clusters in the includeRegion list.
            "type": "Juno.EnvironmentSelection.ClusterSelectionFilters.TipClusterProvider",
            "parameters": {
                // Refer to regions supplied by the property parameters.
                "includeRegion": "$.external.regionSearchSpace",
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