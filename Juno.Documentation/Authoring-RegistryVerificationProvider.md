<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Juno Experiment Steps</div>
<br/>

## RegistryVerificationProvider
The following documentation illustrates how to define a Juno workflow step to verify the value of an Windows registry key on physical nodes 
in the Azure fleet as part of a Juno experiment. 

### Preliminaries
It is important to understand the basic schema of an experiment before attempting to author Juno experiment workflow steps.

[Authoring Experiments](./Authoring-Experiments.md)  
[Juno Experiment Schema](./Authoring-ExperimentSchema.md)

### Dependencies

* Active TiP sessions must have been created before this step executes. It dependes upon the existence of TiP nodes.
* The Juno Host agent must have been deployed to the target node(s) associated with the active TiP sessions. This step utilizes agent/child
  steps that are expected to run in the Juno Host agent process in order to verify the MSR register.

### Step Authoring
This section describes how to author an experiment workflow step to instruct Juno to verify the registry key value on Azure
physical nodes associated with an experiment group.

##### Type
The 'type' must be ```Juno.Execution.Providers.Payloads.RegistryVerificationProvider```

##### Name and description
The 'name' and 'description' properties are used to describe steps in the experiment runtime workflow.  The values can be set to anything the experiment
author wishes but should generally be accurate descriptions of the purpose of the step.

##### Group
The 'group' defines the experiment group for which the runtime workflow step is associated (e.g. Group A, Group B).

##### Parameters
The following parameters can be used when authoring the experiment step.

| Name                | Required   | Data Type        | Description                |
| ------------------- | ---------- | ---------------- | -------------------------- |
| keyName             | Yes        | string           | Full path of the registry key. Eg: "HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0"
| valueName           | Yes        | string           | The registry value name. Eg: "Update Revision"
| type                | Yes        | string           | C# type name that maps to a registry value type. Eg: REG_SZ maps to "System.String", REG_DWORD maps to "System.Int32" and REG_BINARY maps to "System.Array"
| expectedValue       | Yes        | string           | The value of the registry key expected. Eg: "GenuineIntel"

##### Example Definitions
The following section provides examples of the structure of a valid experiment step.
``` json
{
    "type": "Juno.Execution.Providers.Verification.RegistryVerificationProvider",
    "name": "Verify Registry value",
    "description": "Verify that the CPU manufacturer on the physical host is Intel in this experiment group",
    "group": "Group B",
    "parameters": {
        "keyName": "HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0",
        "valueName": "VendorIdentifier",
        "type": "System.String",
        "expectedValue": "GenuineIntel"
    }
}

{
    "type": "Juno.Execution.Providers.Verification.RegistryVerificationProvider",
    "name": "Verify Registry value",
    "description": "Verify that the uCode version on the physical host is as expected in this experiment group",
    "group": "Group B",
    "parameters": {
        "keyName": "HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0",
        "valueName": "Update Revision",
        "type": "System.Array",
        "expectedValue": "00-00-00-00-1F-00-00-0B"
    }
}
```