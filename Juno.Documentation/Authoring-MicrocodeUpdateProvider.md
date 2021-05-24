<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Juno Experiment Steps</div>
<br/>

## MicrocodeUpdateProvider
The following documentation illustrates how to define a Juno workflow step to deploy an Intel microcode update (IPU) to physical nodes in the Azure
fleet as part of a Juno experiment. This experiment step uses the PilotFish agent running on the physical node to handle the deployment of the
microcode update.

PilotFish is an agent of the AutoPilot system that enables the deployment of applications to Azure physical nodes/infrastructure. PilotFish
meets Azure security and compliance requirements. The Juno system uses PilotFish to deploy different types of payloads to Azure physical nodes
to evaluate net changes to the performance or reliability of the node for hosting customer VM workloads caused by firmware or hardware changes.

### Preliminaries
It is important to understand the basic schema of an experiment before attempting to author Juno experiment workflow steps.

[Authoring Experiments](./Authoring-Experiments.md)  
[Juno Experiment Schema](./Authoring-ExperimentSchema.md)

### Dependencies

* Active TiP sessions must have been created before this step executes. It dependes upon the existence of TiP nodes in order to determine
  where to deploy the microcode updates.
* The Juno Host agent must have been deployed to the target node(s) associated with the active TiP sessions. This step utilizes agent/child
  steps that are expected to run in the Juno Host agent process in order to explicitly verify the microcode update was applied on the node.

### Step Authoring
This section describes how to author an experiment workflow step to instruct Juno to deploy an Intel microcode update (IPU) to Azure
physical nodes associated with an experiment group.

##### Type
The 'type' must be ```Juno.Execution.Providers.Environment.MicrocodeUpdateProvider```

##### Name and description
The 'name' and 'description' properties are used to describe steps in the experiment runtime workflow.  The values can be set to anything the experiment
author wishes but should generally be accurate descriptions of the purpose of the step.

##### Group
The 'group' defines the experiment group for which the runtime workflow step is associated (e.g. Group A, Group B).

##### Parameters
The following parameters can be used when authoring the experiment step.

| Name                | Required   | Data Type        | Description                |
| ------------------- | ---------- | ---------------- | -------------------------- |
| microcodeProvider   | Yes        | string           | The manufacturer/provider of the microcode update. Currently, the only supported provider/value is "Intel".
| microcodeVersion    | Yes        | string           | The version of the IPU that is expected to be deployed on the physical node. This will be compared with the version actually deployed on the physical nodes to verify the expected IPU was successfully applied. Note that the microcode version can be found in the PF service path location/file share in a file called MicrocodeUpdate.xml.
| pfServiceName       | Yes        | string           | The name of the PilotFish service. The choice of name does not affect the deployment of the service in any way but is used to differentiate when multiple PilotFish service deployments are requested. The choice of name should be one that is meaningful to the author of the experiment.
| pfServicePath       | Yes        | string/path      | The network path on the file share where the official build of the PilotFish service for deploying the IPU/microcode changes exists.
| requestTimeout      | No         | string/timespan  | Timeout defines the amount of time to wait while attempting to verify that the PilotFish service received the deployment request successfully (e.g. that the TiP service successfully handed off to PilotFish). This timeout is independent of any other timeout defined in the parameters.
| verificationTimeout | No         | string/timespan  | Timeout defines the amount of time to wait while attempting to verify the microcode update was actually applied on the physical node (e.g. that PilotFish successfully installed the update). This timeout is independent of any other timeout defined in the parameters.

##### Example Definitions
The following section provides examples of the structure of a valid experiment step.

<div style="color:#1569C7">
<div style="font-weight:600">Note:</div>
A backslash character ('\') in JSON text is considered an escape character. Any paths represented in JSON
with backslash characters must use a double-backslash in order for JSON to consider the backslash as a string
literal (e.g. \\reddog\any\path must be represented as \\\\reddog\\any\\path).
</div>

``` json
{
    "type": "Juno.Execution.Providers.Environment.MicrocodeUpdateProvider",
    "name": "Apply IPU2020.2 Microcode Update",
    "description": "Applies the IPU2020.2 microcode update to the physical nodes in this experiment group",
    "group": "Group B",
    "parameters": {
        "microcodeProvider": "Intel",
        "microcodeVersion": "2000065",
        "pfServiceName": "IPU2020.1",
        "pfServicePath": "\\\\reddog\\Builds\\branches\\git_crc_ipu_master_latest\\release-x64\\Deployment\\App\\IPU2020.1"
    }
}

{
    "type": "Juno.Execution.Providers.Environment.MicrocodeUpdateProvider",
    "name": "Apply IPU2020.2 Microcode Update",
    "description": "Applies the IPU2020.2 microcode update to the physical nodes in this experiment group",
    "group": "Group B",
    "parameters": {
        "microcodeProvider": "Intel",
        "microcodeVersion": "2000065",
        "pfServiceName": "IPU2020.1",
        "pfServicePath": "\\\\reddog\\Builds\\branches\\git_crc_ipu_master_latest\\release-x64\\Deployment\\App\\IPU2020.1",
        "requestTimeout": "00:10:00",
        "verificationTimeout": "00:30:00"
    }
}

{
    "type": "Juno.Execution.Providers.Environment.MicrocodeUpdateProvider",
    "name": "Apply IPU2020.2 Microcode Update",
    "description": "Applies the IPU2020.2 microcode update to the physical nodes in this experiment group",
    "group": "Group B",
    "parameters": {
        "microcodeProvider": "Intel",
        "microcodeVersion": "2000065",
        "pfServiceName": "IPU2020.1",
        "pfServicePath": "\\\\reddog\\Builds\\branches\\git_crc_ipu_master_latest\\release-x64\\Deployment\\App\\IPU2020.1"
    },
    "tags": {
        "ipu": "2020.1"
    }
}
```