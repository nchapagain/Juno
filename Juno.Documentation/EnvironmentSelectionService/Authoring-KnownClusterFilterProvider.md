<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Environment Filters</div>
<br/>

## Known Cluster Filter Provider

The following documentation describes how to get started authoring the environment filter:
`KnownClusterFilterProvider`

### Preliminaries
It is important to understand the context of individual providers in an environment query:  

[Authoring Environment Queries](./Authoring-EnvironmentQueries.md)

### Description
The known cluster filter provider is a filter that returns clusters provided in the parameter section of the filter.
This filter should only be used in scenarios where an author wants to target a set of specific clusters.  
  
An author may feel compelled to exclude this filter and place their desired list of clusters in the nodeselction filters.
Since ESS will conduct a set intersection with any other cluster filters, 
it is more appropiate to include this provider instead of the aforementioned. 


### Filter Authoring
The following section details how to author the environment filter.

#### Type
The type must be as follows: `Juno.EnvironmentSelection.ClusterSelectionFilters.KnownClusterFilterProvider`

### Parameters
Supported parameters:

| Parameter Name | Required | Values | Default Value | Description
|----------------|------|---|
| includeRegion | Yes | Comma serperated string of Regions | N/A | Include clusters that reside in these regions.
| excludeRegion | No | Comma seperated string of Regions | N/A | Exclude clusters that reside in these regions.
| includeCluster | Yes | Comma serperated string of clusters | N/A | Include clusters that appear in this list.
| excludeCluster | No | Comma seperated string of clusters | N/A | Exclude clusters that appear in this list.

### Example Provider
Below is an example provider. (_Note the parameter value for `includeRegion` this references the output from the
`QuotaLimitFilterProvider` see [QuotaLimitFilterProvider](./Authoring-QuotaLimitFilterProvider.md)_)

```
{
    "type": "Juno.EnvironmentSelection.ClusterSelectionFilters.KnownClusterFilterProvider",
    "parameters": {
        "includeRegion": "*",
        "excludeRegion": "West US 2",
        "includeCluster": "mnz20prdapp13" 
    }
}
```

### Systematic Parameters
There are parameters outlined above that are populated at run time. This allows ESS to use information from a group
of providers or one provider to inform the parameters for another provider. 

The systematic parameters that are referenced above:

| Name | Source | Description
----|-----|-----
`*` | [QuotaLimitFilterProvider](./Authoring-QuotaLimitFilterProvider.md) | A list of regions that are supported by the subscription selected
