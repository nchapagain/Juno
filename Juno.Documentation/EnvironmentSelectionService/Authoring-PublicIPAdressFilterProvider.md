<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Environment Filters</div>
<br/>

## Public IP Address Filter Provider

The following documentation describes how to get started authoring the environment filter:
`PublicIPAddressFilterProvider`

### Preliminaries
It is important to understand the context of individual providers in an environment query:  

[Authoring Environment Queries](./Authoring-EnvironmentQueries.md)

### Description
The Public IP Address filter provider returns subscriptions that do not exceed the 
[Azure public IP adress limit](https://docs.microsoft.com/en-us/azure/azure-resource-manager/management/azure-subscription-service-limits?toc=/azure/virtual-network/toc.json#publicip-address)
of 1000. 


### Filter Authoring
The following section details how to author the environment filter.

#### Type
The type must be as follows: `Juno.EnvironmentSelection.SubscriptionFilters.PublicIPAddressFilterProvider`

### Parameters
There are several supported parameters:

| Parameter Name | Required | Values | Default Value | Description |
|----------------|----------|--------| 
| publicIpAddressLimit | No | > 0 | 980 | return subscriptions whose Public IP count is less than this. |
| includeSubscription | No | Comma seperated string of Subscriptions | All Subscriptions | Subscriptions to evaluate |

### Outputs
Subscription filters are able to store and pass on information to node selection filters.
This allows the node selection filters to make dynamic and well intentioned choices.

*No Outputs for the Public IP address Filter Provider*

### Example Provider
Below is an example provider. 

```
{
    "type": "Juno.EnvironmentSelection.SubscriptionFilters.PublicIPAddressFilterProvider",
    "parameters": {
        "publicIpAddressLimit": 900
    }
}
```