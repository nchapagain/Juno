<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Environment Filters</div>
<br/>

## Cluster Tip Provider

The following documentation describes how to get started authoring the environment filter:
`ClusterTipProvider`

### Preliminaries
It is important to understand the context of individual providers in an environment query:  

[Authoring Environment Queries](./Authoring-EnvironmentQueries.md)

### Description
The Cluster Tip provider filters and returns all clusters that can have another tip session launched
on them.


### Filter Authoring
The following section details how to author the environment filter.

#### Type
The type must be as follows: `Juno.EnvironmentSelection.ClusterSelectionFilters.TipClusterProvider`

### Parameters
Supported parameters:

| Parameter Name | Required | Values | Default Value | Description
|----------------|------|---|
| tipSessionsRequired | No | >0 | 2 | # of Tip Sessions a cluster must have.
| includeRegion | Yes | Comma serperated string of Regions | N/A | Include clusters that reside in these regions.
| excludeRegion | No | Comma seperated string of Regions | N/A | Exclude clusters that reside in these regions.

### Example Provider
Below is an example provider. (_Note the parameter value for `includeRegion` this references the output from the
`QuotaLimitFilterProvider` see [QuotaLimitFilterProvider](./Authoring-QuotaLimitFilterProvider.md)_)

```
{
    "type": "Juno.EnvironmentSelection.ClusterSelectionFilters.TipClusterProvider",
    "parameters": {
        "includeRegion": "*",
        "excludeRegion": "westus2",
        "tipSessionsRequired": 5 
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
