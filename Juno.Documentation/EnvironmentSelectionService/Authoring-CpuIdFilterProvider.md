<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Environment Filters</div>
<br/>

## CPU ID Filter Provider

The following documentation describes how to get started authoring the environment filter:
`CpuIdFilterProvider`

### Preliminaries
It is important to understand the context of individual providers in an environment query:  

[Authoring Environment Queries](./Authoring-EnvironmentQueries.md)

### Description
The CPU ID filter provider returns nodes whose CPU has a specific ID.


### Filter Authoring
The following section details how to author the environment filter.

#### Type
The type must be as follows: `Juno.EnvironmentSelection.NodeSelectionFilters.CpuIdFilterProvider`

### Parameters
There are several supported parameters:

| Parameter Name | Required | Values | Default Value | Description
|----------------|------|---|
| includeCpuId | If exclude does not exist | Comma seperated string of Cpu Ids | N/A | Include nodes whose CPU is in the list
| excludeCpuId | If include does not exist | Comma seperated string of Cpu Ids | N/A | Exclude nodes whose CPU is in the list
| includeCluster | Yes | Comma serperated string of Clusters | N/A | Include nodes that reside in these clusters.
| excludeCluster | No | Comma seperated string of Clusters | N/A | Exclude nodes that reside in these clusters. 

### Example Provider
Below is an example provider. (_Note the parameter value for `includeCluster` this references the output from the
`ClusterSelectionFilters` see [Authoring Environment Queries](./Authoring-EnvironmentQueries.md)_)

```
{
    "type": "Juno.EnvironmentSelection.NodeSelectionFilters.CpuIdFilterProvider",
    "parameters": {
        "includeCluster": "$.cluster.clusters",
        "excludeCluster": "mnz20prdapp13",
        "includeCpuId": "50654,50657",
        "excludeCpuId": "406f1"
    }
}
```