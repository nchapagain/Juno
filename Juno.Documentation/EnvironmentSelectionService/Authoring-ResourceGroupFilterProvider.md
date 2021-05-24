<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Environment Filters</div>
<br/>

## Resource Group Filter Provider

The following documentation describes how to get started authoring the environment filter:
`ResourceGroupFilterProvider`

### Preliminaries
It is important to understand the context of individual providers in an environment query:  

[Authoring Environment Queries](./Authoring-EnvironmentQueries.md)

### Description
The Resource Group filter provider returns subscriptions that do not exceed the azure resource group limit
of 980. 


### Filter Authoring
The following section details how to author the environment filter.

#### Type
The type must be as follows: `Juno.EnvironmentSelection.SubscriptionFilters.ResourceGroupFilterProvider`

### Parameters
There are several supported parameters:

| Parameter Name | Required | Values | Default Value | Description
|----------------|------|---|
| resourceGroupLimit | No | > 0 | 980 | return subscriptions whose RG count is less than this.
| includeSubscription | No | Comma seperated string of Subscriptions | All Subscriptions | Subscriptions to evaluate

### Outputs
Subscription filters are able to store and pass on information to node selection filters.
This allows the node selection filters to make dynamic and well intentioned choices.

*No Outputs for the Resource Group Provider*

### Example Provider
Below is an example provider. 

```
{
    "type": "Juno.EnvironmentSelection.SubscriptionFilters.ResourceGroupFilterProvider"
}
```