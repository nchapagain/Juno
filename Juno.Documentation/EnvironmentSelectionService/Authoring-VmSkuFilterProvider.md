<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Environment Filters</div>
<br/>

## VM Sku Filter Provider

The following documentation describes how to get started authoring the environment filter:
`VmSkuFilterProvider`

### Preliminaries
It is important to understand the context of individual providers in an environment query:  

[Authoring Environment Queries](./Authoring-EnvironmentQueries.md)

### Description
The VM Sku filter provider returns Clusters that currently support a specific VM Skus.


### Filter Authoring
The following section details how to author the environment filter.

#### Type
The type must be as follows: `Juno.EnvironmentSelection.ClusterSelectionFilters.VmSkuFilterProvider`

### Parameters
There are several supported parameters:

| Parameter Name | Required | Values | Default Value | Description
|----------------|------|---|
| includeVmSku | If exclude does not exist | Comma seperated string of Vm Skus | N/A | Include clusters that support these VM Skus
| excludeVmSku | If include does not exist | Comma seperated string of VM Skus | N/A | Exclude clusters that support these VM Skus
| includeRegion | Yes | Comma serperated string of Regions | N/A | Include nodes that reside in these regions.
| excludeRegion | No | Comma seperated string of Regions | N/A | Exclude nodes that reside in these regions. 
| allocableVmCount | No | > 0 | 100 | Minimum number of VMs a node can support. 

### Example Provider
Below is an example provider. (_Note the parameter value for `includeRegion` and `includeVmSku` these reference the output from the
`QuotaLimitFilterProvider` see [QuotaLimitFilterProvider](./Authoring-QuotaLimitFilterProvider.md)_)

```
{
    "type": "Juno.EnvironmentSelection.ClusterSelectionFilters.VmSkuFilterProvider",
    "parameters": {
        "includeRegion": "*",
        "excludeRegion": "westus2",
        "includeVmSku": "$.subscription.vmSkuSearchSpace",
        "excludeVmSku": "Standard_D2s_v3,Standard_D2_v3,Standard_F2s"
        "allocableVmCount": 10
    }
}
```

### Systematic Parameters
There are parameters outlined above that are populated at run time. This allows ESS to use information from a group
of providers or one provider to inform the parameters for another provider. 

The systematic parameters that are referenced above:

| Name | Source | Description
----|-----|-----
`*` | [QuotaLimitFilterProvider](./Authoring-QuotaLimitFilterProvider.md) | A list of regions that are supported by the subscription selected.
`$.subscription.vmSkuSearchSpace` |  [QuotaLimitFilterProvider](./Authoring-QuotaLimitFilterProvider.md) | A list of vmskus that are supported by the subscription and regions selected.