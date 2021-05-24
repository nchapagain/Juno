<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Juno Experiment Steps</div>
<br/>

## BiosUpdateProvider
The following documentation illustrates how to define a Juno workflow step to Update BIOS on physical nodes 
in the Azure fleet as part of a Juno experiment. 

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
  steps that are expected to run in the Juno Host agent process in order to explicitly trigger the BIOS update.
* JunoBiosPayload PF package must be deployed before executing this provider. Check "Authoring-ApplyPilotFishProvider.md" to author pilotfish deployment steps.

### Step Authoring
This section describes how to author an experiment workflow step to instruct Juno to deploy an BIOS to Azure
physical nodes associated with an experiment group.

##### Type
The 'type' must be ```Juno.Execution.Providers.Payloads.BiosUpdateProvider```

##### Name and description
The 'name' and 'description' properties are used to describe steps in the experiment runtime workflow.  The values can be set to anything the experiment
author wishes but should generally be accurate descriptions of the purpose of the step.

##### Group
The 'group' defines the experiment group for which the runtime workflow step is associated (e.g. Group A, Group B).

##### Parameters
The following parameters can be used when authoring the experiment step.

| Name                | Required   | Data Type        | Description                |
| ------------------- | ---------- | ---------------- | -------------------------- |
| scenario            | Yes        | string           | Defines whethere is BIOS Update or RollBack.
| updateTimeout       | Yes        | string/timespan  | Timeout defines the amount of time to BIOS update process should take.
| stepTimeout         | No         | string/timespan  | Timeout defines the amount of time to wait while attempting to verify the BIOS update was actually applied on the physical node (e.g. that PilotFish successfully installed the update). This timeout is independent of any other timeout defined in the parameters.

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
    "type": "Juno.Execution.Providers.Payloads.BiosUpdateProvider",
    "name": "Update BIOS",
    "description": "Updates the given BIOS on the physical nodes in this experiment group",
    "group": "Group B",
    "parameters": {
        "scenario": "Update",
        "updateTimeout": "00.01:00:00",
        "stepTimeout": "00.02:00:00"
    }
}

{
    "type": "Juno.Execution.Providers.Payloads.BiosUpdateProvider",
    "name": "Update BIOS",
    "description": "Updates the given BIOS on the physical nodes in this experiment group",
    "group": "Group B",
    "parameters": {
        "scenario": "RollBack",
        "updateTimeout": "00.01:00:00"
    }
}

“//”: “This is an example to author on how to deploy pilotfish package to a physical node”
{
    "type": "Juno.Execution.Providers.Environment.ApplyPilotFishProvider",
    "name": "Install BIOS Payload",
    "description": "Installs the BIOS PF service on physical nodes inthis experiment group",
    "group": "Group B",
    "parameters": {
        "pilotfishServiceName": "JunoBiosPayload",
        "pilotfishServicePath": "\\\\reddog\\Builds\\branches\\git_csi_crc_air_payloads_master_latest\\release-x64\\Deployment\\Dev\\App\\JunoBiosPayload",
        "timeout": "00.01:00:00"
    }
}

```