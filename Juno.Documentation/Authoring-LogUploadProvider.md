<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Juno Experiment Steps</div>
<br/>

## LogUploadProvider
The following documentation illustrates how to define a Juno workflow step to upload a log file on the host either 
as telemetry to kusto or as file to storage.

### Preliminaries
It is important to understand the basic schema of an experiment before attempting to author Juno experiment workflow steps.

[Authoring Experiments](./Authoring-Experiments.md)  
[Juno Experiment Schema](./Authoring-ExperimentSchema.md)

### Dependencies
* The Juno Host agent must have been deployed to the target nodes in the environment.

### Step Authoring
This section describes how to author an experiment workflow step to instruct Juno to upload a log file.

##### Type
The 'type' must be ```Juno.Execution.Providers.Payloads.LogUploadProvider```

##### Name and description
The 'name' and 'description' properties are used to describe steps in the experiment runtime workflow.  The values can be set to anything the experiment
author wishes but should generally be accurate descriptions of the purpose of the step.

##### Group
The 'group' defines the experiment group for which the runtime workflow step is associated (e.g. Group A, Group B).

##### Parameters
The following parameters can be used when authoring the experiment step.

| Name                | Required   | Data Type        | Description                |
| ------------------- | ---------- | ---------------- | -------------------------- |
| payload             | Yes        | string           | The name of the payload where the log file exists. Eg: JunoHostAgent
| fileName            | Yes        | string           | The name of the file to upload.
| uploadToKusto       | No         | boolean          | Specifies if the log file should go to kusto. Default is false. Log files fo to storage.

##### Example Definitions
The following section provides examples of the structure of a valid experiment step.

``` json
{
    "type": "Juno.Execution.Providers.Payloads.LogUploadProvider",
    "name": "Upload the log file",
    "description": "Upload the log file",
    "group": "Group A",
    "parameters": {
        "payload": "JunoCustomPayload",
        "fileName": "file.log",
        "uploadToKusto": "true"
    }
}
```