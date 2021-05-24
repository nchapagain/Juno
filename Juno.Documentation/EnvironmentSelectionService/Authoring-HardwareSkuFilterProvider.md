<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Environment Filters</div>
<br/>

## Hardware Sku Filter Provider

The following documentation describes how to get started authoring the environment filter:
`HardwareSkuFilterProvider`

### Preliminaries
It is important to understand the context of individual providers in an environment query:  

[Authoring Environment Queries](./Authoring-EnvironmentQueries.md)

### Description
The Hardware sku filter provider returns nodes who run on certain hardware skus.


### Filter Authoring
The following section details how to author the environment filter.

#### Type
The type must be as follows: `Juno.EnvironmentSelection.NodeSelectionFilters.HardwareSkuFilterProvider`

### Parameters
There are several supported parameters:

| Parameter Name | Required | Values | Default Value | Description
|----------------|------|---|
| includeHwSku | Yes | Comma seperated string of hardware skus | N/A | Include nodes who run on hw skus in the list.
| excludeHwSku | No | Comma seperated string of hardware skus | N/A | Exclude nodes who run on hw skus in the list.
| includeCluster | Yes | Comma serperated string of Clusters | N/A | Include nodes that reside in these clusters.
| excludeCluster | No | Comma seperated string of Clusters | N/A | Exclude nodes that reside in these clusters. 

### Example Provider
Below is an example provider. (_Note the parameter value for `includeCluster` this references the output from the
`ClusterSelectionFilters` see [Authoring Environment Queries](./Authoring-EnvironmentQueries.md)_)

```
{
    "type": "Juno.EnvironmentSelection.NodeSelectionFilters.HardwareSkuFilterProvider",
    "parameters": {
        "includeCluster": "$.cluster.clusters",
        "excludeCluster": "mnz20prdapp13",
        "includeHwSku": "ZT_Gen4.0_HPCv2"
    }
}
```