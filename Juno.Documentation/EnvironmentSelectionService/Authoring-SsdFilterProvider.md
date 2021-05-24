<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Environment Filters</div>
<br/>

## SSD Family Filter Provider

The following documentation describes how to get started authoring the environment filter:
`SsdFamilyFilterProvider`

### Preliminaries
It is important to understand the context of individual providers in an environment query:  

[Authoring Environment Queries](./Authoring-EnvironmentQueries.md)

### Description
The SSD filter provider returns nodes that contain SSD with the desired SSD family and drive type.


### Filter Authoring
The following section details how to author the environment filter.

#### Type
The type must be as follows: `Juno.EnvironmentSelection.NodeSelectionFilters.SsdFamilyFilterProvider`

### Parameters
There are several supported parameters:

| Parameter Name | Required | Values | Default Value | Description
|----------------|------|---|
| incldueFamily | Yes | One SSD Family | N/A | Include nodes that contain an SSD that belongs to this family.
| driveType | Yes | 'System' and/or 'Data' | N/A | The SSD on node must be of this drive type.
| includeCluster | Yes | Comma serperated string of Clusters | N/A | Include nodes that reside in these clusters.
| excludeCluster | No | Comma seperated string of Clusters | N/A | Exclude nodes that reside in these clusters. 

### Example Provider
Below is an example provider. (_Note the parameter value for `includeCluster` this references the output from the
`SsdFamilyFilterProvider` see [Authoring Environment Queries](./Authoring-EnvironmentQueries.md)_)

```
{
    "type": "Juno.EnvironmentSelection.NodeSelectionFilters.SsdFamilyFilterProvider",
    "parameters": {
        "includeCluster": "$.cluster.clusters",
        "excludeCluster": "mnz20prdapp13",
        "includeFamily": "PM983",
        "driveType": "System, Data"
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
