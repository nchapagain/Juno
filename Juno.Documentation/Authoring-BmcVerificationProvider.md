<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Juno Experiment Steps</div>
<br/>

## BmcVerificationProvider
The following documentation illustrates how to define a Juno workflow step to Verify Bmc version on physical nodes 
in the Azure fleet as part of a Juno experiment. 

### Preliminaries
It is important to understand the basic schema of an experiment before attempting to author Juno experiment workflow steps.

[Authoring Experiments](./Authoring-Experiments.md)  
[Juno Experiment Schema](./Authoring-ExperimentSchema.md)

### Dependencies

* Active TiP sessions must have been created before this step executes. It dependes upon the existence of TiP nodes in order to determine
  where to deploy the microcode updates.
* The Juno Host agent must have been deployed to the target node(s) associated with the active TiP sessions. This step utilizes agent/child
  steps that are expected to run in the Juno Host agent process in order to explicitly trigger the Bmc update.

### Step Authoring
This section describes how to author an experiment workflow step to instruct Juno to Verify Bmc version on Azure
physical nodes associated with an experiment group.

##### Type
The 'type' must be ```Juno.Execution.Providers.Verification.BmcVerifciationProvider```

##### Name and description
The 'name' and 'description' properties are used to describe steps in the experiment runtime workflow.  The values can be set to anything the experiment
author wishes but should generally be accurate descriptions of the purpose of the step.

##### Group
The 'group' defines the experiment group for which the runtime workflow step is associated (e.g. Group A, Group B).

##### Parameters
The following parameters can be used when authoring the experiment step.

| Name                | Required   | Data Type        | Description                |
| ------------------- | ---------- | ---------------- | -------------------------- |
| BmcVersion         | Yes        | string           | Bmc version to match on Physical host

##### Example Definitions
The following section provides examples of the structure of a valid experiment step.
``` json
{
    "type": "Juno.Execution.Providers.Verification.BmcVerificationProvider",
    "name": "Verify Bmc version",
    "description": "Verify desired Bmc version is on the physical host in this experiment group",
    "group": "Group B",
    "parameters": {
        "BmcVersion": "1.18"
    }
}

{
    "type": "Juno.Execution.Providers.Verification.BmcVerificationProvider",
    "name": "Verify Bmc version",
    "description": "Verify desired Bmc version is on the physical host in this experiment group",
    "group": "Group B",
    "parameters": {
        "BmcVersion": "1.18"
    }
}

```