<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Environment Filters</div>
<br/>

The following documentation describes how to get started authoring Environment Selection Service (ESS) 
Environment Queries.

### Terminology
The following terms are used throughout ESS environment query authroing documentation:
* **Node**  
  A phsyical server located in the Azure Production Environment. A node can have several attributes that 
  describe the entity. These attributes are how ESS selects the right node. An example of an attribute can be
  Cluster ID; the cluster id details which cluster that node belongs to.

* **Subscription**  
  A logical container that holds resources in the Azure cloud. For example a VM can belong to a subscription
  and that is how that VM's usage is billed and accounter for.

### Environment Query Walkthrough
The following section details the different properties in an Envrionment Query and how each of these properties
are authored. This section also describes how to author several Environment Filters
and how to combine Environment Filters to create an effective Environment Query.

``` json
{
    "name": "TEST",
    "nodeCount": 2,
    "filters": [
        ... 
    ]
}
```

#### Environment Query Property: `name`
This field is used to identify an Environment Query accross systems. This name should be globally
unique. *Environment Queries if used within a target goal should be the same as the target goal name*

#### Environment Query Property: `nodeCount`
This field is used to identify how many nodes the Environment Selection Service should return to the caller.
As of October 26th 2020, ESS is designed to return pairs of nodes, such that each pair belong to the same rack.
In result, requesting a node count that is even is the most appropiate. (If an odd number is requested ESS
chooses that number - 1 nodes to return).  
</br>
ESS has a system wide limit of a maximum of 10 nodes to be returned each request. If more than 10 nodes are requested,
ESS returns 10 nodes. (As long as there are 10 nodes present).

#### Environment Query Property: `filters`
This field is used to contain the collection of Environment Filters. For authoring of specific environment filters
please refer to the specific filter documentation on how to author.  
Environment Filters are seperated into two categories: "Node Selection" filters and "Subscription Selection" filters.
To identify which category a filter resides in the third identifier (delimited by periods) in the type definition details
this information.  
Each filter can have an individual parameter section. This section outlines parameters that are needed for execution of 
the filter. The value of these parameters are `IConvertible` and primitive types are supported. 

#### Environment Query Property: `nodeAffinity`
This property's purpose to define the affinity that each node must dislpay. There are four values that this value can be set to.
1. `SameRack`: The pairs of nodes that are returned must belong to the same rack.
2. `SameCluster`: The pairs of nodes that are returned must belong to the same cluster.
3. `DifferentCluster`: The pairs of nodes that are returned must belong to different clusters. (Same region)
4. `Any`: The nodes returned have no requirements.

##### Parameterizing Environment Filters
Environment filter parameter's may have a special value, that take the form of `$.<source>.<parameter name>`
Where source is where the parameter value is produced, this dictates when this parameter should be replaced during run time. 
The two sources that are supported are: 
1. `subscription`: These parameters are produced from subscription selection filters. These parameters
can be used in node selection filters and are populated after the subscription selection filters have returned
their results. 
2. `external`: These parameters are produced from external sources and must be in the Environment Query's
parameter dictionary before the request for an environment is made.  
3. `cluster`: These parameters are produced from the `ClusterSelectionFilters` and will be populated after the `ClusterSelectionFilters`
4. Have finished executing. (_The only parameter that can be accessed as an ouput is `clusters`_)

``` json
{
    "type": "Juno.EnvironmentSelection.SubscriptionFilters.QuotaLimitFilterProvider",
    "parameters": {
        "quotaThreshold": 1,
        "includeVmSku": "Standard_D2s_v3,Standard_D2_v3,Standard_M208s_v2"
    }
},
{
    "type": "Juno.EnvironmentSelection.SubscriptionFilters.ResourceGroupFilterProvider",
    "parameters": {
        "resourceGroupLimit": 950
    }
},
{
    "type": "Juno.EnvironmentSelection.NodeSelectionFilters.HealthyTipNodeProvider",
    "parameters": {
        "includeRegion": "*"
    }
},
```

### ESS Return Values
ESS handles varying combinations of environment filters in different ways. Those ways are:

| Query Includes | Node Results | Subscription Results |
|----------------|--------------|----------------------|
| Subscription and Node Filters | Nodes (error if none found) | Subscription (error if none found)
| Subscription Filters | None | Subscription (error if none found) |
| Node Filters | Nodes (error if none found) | None |

### Scenario Based Examples
For more in depth, scenario based examples, go to the [Scenario Summary](./Scenarios/Scenario-Summary.md) which contains
a brief description of each scenario provided in the accompanying folder.
