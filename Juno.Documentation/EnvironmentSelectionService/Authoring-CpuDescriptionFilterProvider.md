<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Environment Filters</div>
<br/>

## CPU Description Filter Provider

The following documentation describes how to get started authoring the environment filter:
`CpuDescriptionFilterProvider`

### Preliminaries
It is important to understand the context of individual providers in an environment query:  

[Authoring Environment Queries](./Authoring-EnvironmentQueries.md)

### Description
The CPU Description filter provider returns nodes whose CPU Descritpion contains or does not contain a given string.


### Filter Authoring
The following section details how to author the environment filter.

#### Type
The type must be as follows: `Juno.EnvironmentSelection.NodeSelectionFilters.CpuDescriptionFilterProvider`

### Parameters
There are several supported parameters:

| Parameter Name | Required | Values | Default Value | Description
|----------------|------|---|
| includeCpuDescription | If excludeCpuDescription is not present | string | N/A | Include nodes whose CPU description contains the string given.
| excludeCpuDescription | If includeCpuDescription is not present | string | N/A | Exclude nodes whose CPU description contains the string given.
| includeCluster | Yes | Comma serperated string of Clusters | N/A | Include nodes that reside in these clusters.
| excludeCluster | No | Comma seperated string of Clusters | N/A | Exclude nodes that reside in these clusters. 

### Example Provider
Below is an example provider. (_Note the parameter value for `includeCluster` this references the output from the
`ClusterSelectionFilters` see [Authoring Environment Queries](./Authoring-EnvironmentQueries.md)_)

```
{
    "type": "Juno.EnvironmentSelection.NodeSelectionFilters.CpuDescriptionFilterProvider",
    "parameters": {
        "includeCluster": "$.cluster.clusters",
        "excludeCluster": "mnz20prdapp13",
        "includeCpuDescription": "intel"
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