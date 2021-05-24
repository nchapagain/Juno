<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Juno Experiment Steps</div>
<br/>

## GenevaConfigProvider
The following documentation illustrates how to define a Juno workflow step to configure Geneva Monitoring Agent and it's extensions on virtual machines running as part of Juno experiment.

### Preliminaries
It is important to understand the basic schema of an experiment beforehand.

[Authoring Experiments](./Authoring-Experiments.md)  
[Juno Experiment Schema](./Authoring-ExperimentSchema.md)

### Dependencies

* Virtual machines must have been deployed in the environment that can host the Juno Guest agent.
* The Juno Guest agent must have been deployed to the target virtual machines in the environment.
* The subscription must use the default Azure security policies that install MA

### Step Authoring
This section describes how to author an experiment workflow step to instruct Juno to configure Geneva agent

##### Type
The 'type' must be ```Juno.Execution.Providers.Environment.GenevaConfigProvider```

##### Name and description
The 'name' and 'description' properties are used to describe steps in the experiment runtime workflow.  The values can be set to anything the experiment
author wishes but should generally be accurate descriptions of the purpose of the step.

##### Group
The 'group' defines the experiment group for which the runtime workflow step is associated (e.g. Group A, Group B).

##### Parameters
The following parameters will be used when authoring the experiment step.

| Name                   | Required   | Data Type        | Description                |
| ---------------------- | ---------- | ---------------- | -------------------------- |
| certificateKey         | Yes        | string           | The certificate name for the certificate stored in keyvault
| certificateThumbprint  | Yes        | string           | The thumbprint of the certificate used to connect to MA
| genevaEndpoint         | Yes        | string           | Geneva endpoint. Currently it is test for all test VMs.
| genevaTenantName       | Yes        | string           | Geneva tenant name to use
| genevaAccountName      | Yes        | string           | Geneva account name to send data to
| genevaNamespace        | Yes        | string           | Geneva namespace to send data to
| genevaRegion           | Yes        | string           | Geneva region to send data to
| genevaConfigVersion    | Yes        | string           | Geneva configuration version to use. You can find this in the Jarvis portal (https://aka.ms/jarvis) under Manage -> Warm Path -> Configurations
| genevaRoleName         | Yes        | string           | Geneva rolename used to group together data
| timeout                | No         | timespan         | Timeout for the step to give up on configuring the geneva monitoring agent

##### Example Definitions
``` json
{
    "type": "Juno.Execution.Providers.Workloads.GenevaConfigProvider",
    "name": "Configure MA",
    "description": "Configure monitoring agents",
    "group": "Group A",
    "parameters": {
        "certificateKey": "juno-dev01-monagent",
        "certificateThumbprint": "81554E50AB39BA758465839824307907E167CB2F",
        "genevaTenantName": "crcExperiments"
        "genevaAccountName": "crcair",
        "genevaNamespace": "crcair",
        "genevaRegion": "westus2"
        "genevaConfigVersion": "1.0",
        "genevaRoleName": "junoGuestVM",
        "timeout": "00.00:30:00"
    }
}

```

