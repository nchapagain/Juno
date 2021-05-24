# Scenario 6: Targeted Cluster Environment 
This scenario should be used when the nodes that should be returned have to belong to a known set of clusters. (The definition of known
in this context is specific Cluster Names are known). The example filter displayed below can be used with a combination of other 
specific attribute filters, this pattern can be seen in [Specific Experiment Environemtns](./Scenario-SpecificExperimentEnvironment.md)
This scenario returns a subscription, a node and and eligible vm sku.

This pattern allows an author to introduce a set of known clusters but also validate that they belong to other sets as well: Clusters that can
support another tip session, clusters that can support another vm sku of type x. Since ESS uses a set intersection between a group of filters
of the same category, only clusters that are a part of all cluster filters can be passed to the node selection filters.

## Prerequisites
* A Desired Vm Sku must be known.
* A set of clusters must be known.

## Contract Example
This example below uses three filters:
* KnownClusterFilterProvider: This is the appropiate entry point for the set of known clusters.
* TipClusterFilterProvider: This is used to generate a set of clusters that can support another tip session.
* HealthyNodeFilterProvider: This is used to generate a set of nodes that are healthy and empty.

``` json
{
    // The name of the query should be unique, and descriptive.
    "name": "KnownclusterQuery",
    "nodeCount": 6,
    "nodeAffinity": "SameRack",
    "filters": [
        {
            // Quota limit Filter provider searches for subscriptions, regions, and vmskus that
            // have enough quota in the execution environment
            "type": "Juno.EnvironmentSelection.SubscriptionFilters.QuotaLimitFilterProvider",
            "parameters": {
                // Supply hints to the Quota limit filter provider on which vmsku to search for.
                "includeVmSku": "Standard_D2s_v3",
                // Refer to the regions supplied by the property parameters.
                "includeRegion": "$.external.regions"
            }
        },
        {
            "type": "Juno.EnvironmentSelection.ClusterSelectionFilters.KnownClusterFilterProvider",
            "parameters": {
                // Refer to regions supplied by the subscription providers.
                "includeRegion": "$.subscription.regionSearchSpace"
                // Reference the list of clusters supplied by the property parameters.
                "includeCluster": "$.external.knownClusters"
            }
        },
        {
            // Vm Sku provider searches for clusters that can deploy one of the vms
            // in the includeVmSku which reside in one of the regions in the regionList
            "type": "Juno.EnvironmentSelection.ClusterSelectionFilters.VmSkuFilterProvider",
            "parameters": {
                // Refer to regions supplied by the subscription providers.
                "includeRegion": "$.subscription.regionSearchSpace"
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
                // Use cluster(s) found from the intersection of the tip cluster/vm sku/known cluster providers.
                "includeCluster": "$.cluster.clusters"
            }
        }
    ],
    "parameters": {
        "regions": "Region1, Region2",
        "knownClusters": "Cluster1, Cluster2"
    }
}
```