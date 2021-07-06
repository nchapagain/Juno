<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Environment Filters</div>
<br/>

## Zero Successful Executions On Node Filter Provider

The following documentation describes how to get started authoring the environment filter:
`ZeroSuccessfulExecutionsOnNodeFilterProvider`

### Preliminaries
It is important to understand the context of individual providers in an environment query:  

[Authoring Environment Queries](./Authoring-EnvironmentQueries.md)

### Description
The Zero Successful Executions On Node Filter Provider returns nodes that haven't run a given experiment successfully.


### Filter Authoring
The following section details how to author the environment filter.

#### Type
The type must be as follows: `Juno.EnvironmentSelection.NodeSelectionFilters.ZeroSuccessfulExecutionsOnNodeFilterProvider`

### Parameters
There are several supported parameters:

| Parameter Name | Required | Values | Default Value | Description
|----------------|------|---|
| includeExperimentName | Yes | Experiment name | N/A | Name of the experiment whose nodes will not be returned
| includeCluster | Yes | Comma serperated string of Clusters | N/A | Include nodes that reside in these clusters.
| excludeCluster | No | Comma seperated string of Clusters | N/A | Exclude nodes that reside in these clusters. 

### Example Provider
Below is an example provider. (_Note the parameter value for `includeCluster` this references the output from the
`ClusterSelectionFilters` see [Authoring Environment Queries](./Authoring-EnvironmentQueries.md)_)

```
{
    "type": "Juno.EnvironmentSelection.NodeSelectionFilters.ZeroSuccessfulExecutionsOnNodeFilterProvider",
    "parameters": {
        "includeCluster": "$.cluster.clusters",
        "excludeCluster": "mnz20prdapp13",
        ""includeExperimentName": "MCAR_Gen5_Phase2_3A26"
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
