<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Juno Experiment Steps</div>
<br/>

## FPGAReconfigProvider
The following documentation illustrates how to define a Juno workflow step to flash an FPGA to the specified image.

### Preliminaries
It is important to understand the basic schema of an experiment beforehand.

[Authoring Experiments](./Authoring-Experiments.md)  
[Juno Experiment Schema](./Authoring-ExperimentSchema.md)

### Dependencies
This step is run only after the FPGA package is dropped on the node. The FPGA package should contain the FPGAMgmt, FPGADiagnostics tools and a cab file containing the relevant FPGA images.

### Step Authoring
This section describes how to author an experiment workflow step to instruct Juno to flash the FPGA to the specified image.

##### Type
The 'type' must be ```Juno.Execution.Providers.Payloads.FPGAFlashProvider```

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
| imageFileName        | Yes        | string            | Defines the name of the FPGA cab file containing the relevant images to flash. This must be relative to the FPGA management tools directory.
| timeout              | No         | string/timespan   | Defines a timeout for verifying the Juno Host agent is successfully installed and heartbeating.

##### Example Definitions
``` json

{
    "type": "Juno.Execution.Providers.Payloads.FPGAFlashProvider",
    "name": "Reconfigures the FPGA to golden",
    "description": "Reconfigures the FPGA associated with the blade to golden state",
    "group": "B",
    "parameters":{
       "imageFileName" : "BSLImages.cab"
    }
}
```

