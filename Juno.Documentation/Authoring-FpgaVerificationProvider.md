<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Juno Experiment Steps</div>
<br/>

## FpgaVerificationProvider
The following documentation illustrates how to define a Juno workflow step to check the version of FPGA.

### Preliminaries
It is important to understand the basic schema of an experiment beforehand.

[Authoring Experiments](./Authoring-Experiments.md)  
[Juno Experiment Schema](./Authoring-ExperimentSchema.md)

### Dependencies
This step is run only after the FPGA package is deployed. The FPGA package should contain the FPGAReader, FPGADiagnostics tools

### Step Authoring
This section describes how to author an experiment workflow step to instruct Juno to check specific version deails of the fpga.

##### Type
The 'type' must be ```Juno.Execution.Providers.Verification.FpgaVerificationProvider```

##### Name and description
The 'name' and 'description' properties are used to describe steps in the experiment runtime workflow.  The values can be set to anything the experiment
author wishes but should generally be accurate descriptions of the purpose of the step.

##### Group
The 'group' defines the experiment group for which the runtime workflow step is associated (e.g. Group A, Group B). For this step, the group can
be defined for a specific group (Group A, Group B) or as all groups '*'. When applying the step to all groups, the selected clusters and nodes will be a 
pool from which other steps will select when isolating physical nodes in the environment.

##### Parameters
The following parameters will be used when creating the experiment step.

| Name                 | Required   | Data Type         | Description                                      |
| -------------------- | ---------- | ----------------- | -------------------------------------------------|
| boardName            | Yes        | string            | Defines the name of the FPGA board.              |
| roleId               | Yes        | string            | Defines the FPGA role id.                        |
| roleVer              | Yes        | string            | Defines the FPGA role version.                   |
| isGolden             | Yes        | bool              | FPGA Golden Image Requirement.                   |
| timeout              | No         | string/timespan   | Default timeout for the version check to finish  |

##### Example Definitions
``` json

{
    "type": "Juno.Execution.Providers.Verification.FpgaVerificationProvider",
    "name": "Verifies the FPGA Health Information",
    "description": "Verifies the FPGA Board name, Role Id and Golden Image requirement",
    "group": "A",
    "parameters": {
        "boardName": "LongsPeak",
        "roleId": "0x601d",
        "roleVersion": "0xca7b030d",
        "isGolden": true
      }
}
```

