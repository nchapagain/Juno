<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Environment Filters</div>
<br/>

## SoC Fip Filter Provider

The following documentation describes how to get started authoring the environment filter:
`SoCFipFilterProvider`

### Preliminaries
It is important to understand the context of individual providers in an environment query:  

[Authoring Environment Queries](./Authoring-EnvironmentQueries.md)

### Description
The SoC Fip filter provider returns nodes who are paired with  a SoC and that SoC has the desired
firmware version.

<span style="color:red">Note that this filter for best performance should be used with the `KnownClusterFilterProvider`. The SoC Fip filter only evaluates
four different clusters: `dsm06prdapp18, dsm06prdapp17, dsm06prdapp05, cdm06prdapp08`.</span>


### Filter Authoring
The following section details how to author the environment filter.

#### Type
The type must be as follows: `Juno.EnvironmentSelection.NodeSelectionFilters.SoCFipFilterProvider`

### Parameters
There are several supported parameters:

| Parameter Name | Required | Values | Default Value | Description
|----------------|------|---|
| includeFirmware | True | Comma seperated string of Fip firmwares | N/A | Include nodes whose SoC Fip component has a firmware in the list.
| excludeFirmware | False | Comma seperated string of Fip firmwares | N/A | Exclude nodes whose SoC Fip component has a firmware in the list.
| includeCluster | Yes | Comma serperated string of Clusters | N/A | Include nodes that reside in these clusters.
| excludeCluster | No | Comma seperated string of Clusters | N/A | Exclude nodes that reside in these clusters. 

### Example Provider
Below is an example provider. (_Note the parameter value for `includeCluster` this references the output from the
`ClusterSelectionFilters` see [Authoring Environment Queries](./Authoring-EnvironmentQueries.md)_)

```
{
    "type": "Juno.EnvironmentSelection.ClusterSelectionFilters.KnownClusterFilterProvider",
    "parameters": {
        "includeRegion": "Central US EUAP, Central US",
        "includeCluster": "dsm06prdapp18, dsm06prdapp17, dsm06prdapp05, cdm06prdapp08" 
    }
},
{
    "type": "Juno.EnvironmentSelection.NodeSelectionFilters.SoCFipFilterProvider",
    "parameters": {
        "includeCluster": "$.cluster.clusters",
        "excludeCluster": "mnz20prdapp13",
        "includeFirmware": "0002.03.200729-prd"
    }
}
```