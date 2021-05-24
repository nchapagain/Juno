<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Environment Filters</div>
<br/>

## CRP Enablement Filter Provider

The following documentation describes how to get started authoring the environment filter:
`VmSkuFilterProvider`

### Preliminaries
It is important to understand the context of individual providers in an environment query:  

[Authoring Environment Queries](./Authoring-EnvironmentQueries.md)

### Description
The CRP enablement filter provider returns clusters that are CRP Enabled.


### Filter Authoring
The following section details how to author the environment filter.

#### Type
The type must be as follows: `Juno.EnvironmentSelection.ClusterSelectionFilters.CrpEnablementFilterProvider`

### Example Provider
Below is an example provider. 

```
{
    "type": "Juno.EnvironmentSelection.ClusterSelectionFilters.CrpEnablementFilterProvider",
    "parameters": {
        "includeRegion": "*",
        "excludeRegion": "westus2",
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
