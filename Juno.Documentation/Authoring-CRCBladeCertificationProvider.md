<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Juno Experiment Steps</div>
<br/>

## FPGAReconfigProvider
The following documentation illustrates how to define a Juno workflow step to certify a blade after a CRC experiment
### Preliminaries
It is important to understand the basic schema of an experiment beforehand.

[Authoring Experiments](./Authoring-Experiments.md)  
[Juno Experiment Schema](./Authoring-ExperimentSchema.md)

### Dependencies
This step is run only after CRCBladeCertificationAgent is dropped on the node. The CRCBladeCertificationAgent should collect current node config when
it is dropped..

### Step Authoring
This section describes how to author an experiment workflow step to instruct Juno to certify a blade.

##### Type
The 'type' must be ```Juno.Execution.Providers.Payloads.CRCBladeCertificationProvider```

##### Name and description
The 'name' and 'description' properties are used to describe steps in the experiment runtime workflow.  The values can be set to anything the experiment
author wishes but should generally be accurate descriptions of the purpose of the step.

##### Group
The 'group' defines the experiment group for which the runtime workflow step is associated (e.g. Group A, Group B). For this step, the group can
be defined for a specific group (Group A, Group B) or as all groups '*'. When applying the step to all groups, the selected clusters and nodes will be a 
pool from which other steps will select when isolating physical nodes in the environment.

##### Parameters
The following parameters will be used when creating the experiment step.

| Name                 | Required   | Data Type         | Description                |
| -------------------- | ---------- | ----------------- | -------------------------- |
| timeout              | No         | string/timespan   | Defines a timeout for certifying the blade.

##### Example Definitions
``` json

{
    "type": "Juno.Execution.Providers.Payloads.CRCBladeCertificationProvider",
    "name": "Certify blade",
    "description": "Certifies the blade used in CRC experiment",
    "group": "B"    
}
```

