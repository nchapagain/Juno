<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Juno Experiment Steps</div>
<br/>

## PassThroughNodeSelectionProvider
The following documentation illustrates how to define a Juno workflow step to select a set of nodes from the provided list of candidate nodes
as options for running experiments.

### Preliminaries
It is important to understand the basic schema of an experiment beforehand.

[Authoring Experiments](./Authoring-Experiments.md)  
[Juno Experiment Schema](./Authoring-ExperimentSchema.md)

### Dependencies
This step is typically one of the first steps in a Juno experiment workflow. It has no special dependencies other than those related to quota and availability
restrictions inside individual data center regions (e.g. supported VM families and SKUs).

### Step Authoring
This section describes how to author an experiment workflow step to instruct Juno to query for environment clusters and nodes that can support the
requirements of the experiment.

##### Type
The 'type' must be ```Juno.Execution.Providers.Environment.PassThroughNodeSelectionProvider```

##### Name and description
The 'name' and 'description' properties are used to describe steps in the experiment runtime workflow.  The values can be set to anything the experiment
author wishes but should generally be accurate descriptions of the purpose of the step.

##### Group
The 'group' defines the experiment group for which the runtime workflow step is associated (e.g. Group A, Group B). For this step, the group can
be defined for a specific group (Group A, Group B) or as all groups '*'. When applying the step to all groups, the selected clusters and nodes will be a 
pool from which other steps will select when isolating physical nodes in the environment.

##### Parameters
The following parameters will be used when authoring the experiment step.

| Name                | Required   | Data Type        | Description                |
| ------------------- | ---------- | ---------------- | -------------------------- |
| nodes               | Yes        | string           | Comma separated string list of candidate nodes

##### Data Center Nomenclature
The following links can be used to identify valid values for the parameters noted above:

##### Example Definitions
``` json

{
    "type": "Juno.Execution.Providers.Environment.PassThroughClusterSelectionProvider",
    "name": "Define Environment Nodes",
    "description": "Define nodes in the Azure fleet that can support the requirements of the experiment.",
    "group": "*",
    "parameters": {
        "nodes": "5f462341-2e96-4d3a-8809-d3ecde429495,238160dd-0be9-448a-a0c7-1321e5cbce22"
    }
}
```

