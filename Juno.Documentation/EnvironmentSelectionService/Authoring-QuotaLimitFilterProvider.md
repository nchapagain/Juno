<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Environment Filters</div>
<br/>

## Quota Limit Filter Provider

The following documentation describes how to get started authoring the environment filter:
`QuotaLimitFilterProvider`

### Preliminaries
It is important to understand the context of individual providers in an environment query:  

[Authoring Environment Queries](./Authoring-EnvironmentQueries.md)

### Description
The Quota filter provider returns subscriptions that have at least one region and one vm sku combo that 
satisfy the quota limit. The "quota limit" is the number of virtual machines a certain region has sufficient quota
for. 


### Filter Authoring
The following section details how to author the environment filter.

#### Type
The type must be as follows: `Juno.EnvironmentSelection.SubscriptionFilters.QuotaLimitFilterProvider`

### Parameters
There are several supported parameters:

| Parameter Name | Required | Values | Default Value | Description
|----------------|------|---|
| includeVmSku | Yes | Comma seperated string of VM Skus | N/A | only look at quota for these vm skus
| includeSubscription | No | Comma seperated string of Subscriptions | All Subscriptions | Subscriptions to evaluate
| requiredInstances | No | > 0 | 2 |The minimum number of vms a region + vm sku combo must support.

### Outputs
Subscription filters are able to store and pass on information to node selection filters.
This allows the node selection filters to make dynamic and well intentioned choices.

| Output Name | Values | Description |
--------------|--------|----------------
| regionSearchSpace | Fabric Regions | List of regions supported by the subscription given
| vmSkuSearchSpace | External VM Sku names | List of VM Skus supported by the subscription given and supported by all regions

*Note: If the request to ESS is timing out, reducing the number of subscriptions in the `includeSubscription` field,
can non-trivially reduce the execution time*

### Example Provider
Below is an example provider. 

```
{
    "type": "Juno.EnvironmentSelection.SubscriptionFilters.QuotaLimitFilterProvider",
    "parameters": {
        "includeVmSku": "Standard_D2s_v3,Standard_D2_v3",
        "requiredInstances": 4
    }
}
```