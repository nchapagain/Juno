<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Environment Filters</div>
<br/>

## Bios Filter Provider

The following documentation describes how to get started authoring the environment filter:
`BiosFilterProvider`

### Preliminaries
It is important to understand the context of individual providers in an environment query:  

[Authoring Environment Queries](./Authoring-EnvironmentQueries.md)

### Description
The Bios filter provider returns nodes that currently support a specific BIOS version.


### Filter Authoring
The following section details how to author the environment filter.

#### Type
The type must be as follows: `Juno.EnvironmentSelection.NodeSelectionFilters.BiosFilterProvider`

### Parameters
There are several supported parameters:

| Parameter Name | Required | Values | Default Value | Description
|----------------|------|---|
| includeBiosVersion | If exclude does not exist | Comma seperated string of BIOS Versions | N/A | Include nodes that support these BIOS Versions
| excludeBiosVersion | If include does not exist | Comma seperated string of BIOS Versions | N/A | Exclude nodes that support these BIOS Versions
| includeCluster | Yes | Comma serperated string of Clusters | N/A | Include nodes that reside in these clusters.
| excludeCluster | No | Comma seperated string of Clusters | N/A | Exclude nodes that reside in these clusters. 

### Example Provider
Below is an example provider. (_Note the parameter value for `includeCluster` this references the output from the
`ClusterSelectionFilters` see [Authoring Environment Queries](./Authoring-EnvironmentQueries.md)_)

```
{
    "type": "Juno.EnvironmentSelection.NodeSelectionFilters.BiosFilterProvider",
    "parameters": {
        "includeCluster": "$.cluster.clusters",
        "excludeCluster": "mnz20prdapp13",
        "includeBios": "C2030.BS.2A35.AT1,02.07.01.01"
    }
}
```

### Systematic Parameters
There are parameters outlined above that are populated at run time. This allows ESS to use information from a group
of providers or one provider to inform the parameters for another provider. 

The systematic parameters that are referenced above:

| Name | Source | Description
----|-----|-----
`$.cluster.clusters` | [ClusterSelectionFilters](./Authoring-EnvironmentQueries.md) | A list of clusters that are the intersection results of the ClusterSelectionFilters.
