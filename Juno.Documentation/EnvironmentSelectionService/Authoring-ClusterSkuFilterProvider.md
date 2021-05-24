<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Environment Filters</div>
<br/>

## Cluster Sku Filter Provider

The following documentation describes how to get started authoring the environment filter:
`ClusterSkuFilterProvider`

### Preliminaries
It is important to understand the context of individual providers in an environment query:  

[Authoring Environment Queries](./Authoring-EnvironmentQueries.md)

### Description
The Cluster Sku Filter provider filters and returns all clusters that use a certain cluster sku. 


### Filter Authoring
The following section details how to author the environment filter.

#### Type
The type must be as follows: `Juno.EnvironmentSelection.ClusterSelectionFilters.ClusterSkuFilterProvider`

### Parameters
Supported parameters:

| Parameter Name | Required | Values | Default Value | Description
|----------------|------|---|
| includeRegion | Yes | Comma serperated string of Regions | N/A | Include clusters that reside in these regions.
| excludeRegion | No | Comma seperated string of Regions | N/A | Exclude clusters that reside in these regions.
| includeClusterSku | yes | Comma seperated string of Cluster Skus | N/A | Include clusters that have one of the given skus.
| excludeClusterSku | No | Comma seperated string of Cluster Skus | N/A | Exclude clusters that have one of the given skus.

### Example Provider
Below is an example provider. (_Note the parameter value for `includeRegion` this references the output from the
`QuotaLimitFilterProvider` see [QuotaLimitFilterProvider](./Authoring-QuotaLimitFilterProvider.md)_)

```
{
    "type": "Juno.EnvironmentSelection.ClusterSelectionFilters.ClusterSkuFilterProvider",
    "parameters": {
        "includeRegion": "*",
        "excludeRegion": "West US 2",
        "includeClusterSku": "hpc" 
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
