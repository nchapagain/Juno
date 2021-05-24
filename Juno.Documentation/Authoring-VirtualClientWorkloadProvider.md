<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Juno Experiment Steps</div>
<br/>

## VirtualClientWorkloadProvider
The following documentation illustrates how to define a Juno workflow step to run virtual client workload on virtual machine as part of Juno experiment.

### Preliminaries
It is important to understand the basic schema of an experiment beforehand.

[Authoring Experiments](./Authoring-Experiments.md)  
[Juno Experiment Schema](./Authoring-ExperimentSchema.md)

### Dependencies

* Virtual machines must have been deployed in the environment that can host the Juno Guest agent.
* The Juno Guest agent must have been deployed to the target virtual machines in the environment.
* The VirtualClient.exe NuGet package must be installed on the virtual machines. This package can be installed by defining it as a dependency of the Virtual Client workload step (see examples below).

  [Juno NuGet Package Dependencies](./Authoring-NuGetPackageProvider.md)

### Step Authoring
This section describes how to author an experiment workflow step to instruct Juno to install a NuGet package as a dependency of an experiment
workflow/workflow step.

##### Type
The 'type' must be ```Juno.Execution.Providers.Workloads.VirtualClientWorkloadProvider```

##### Name and description
The 'name' and 'description' properties are used to describe steps in the experiment runtime workflow.  The values can be set to anything the experiment
author wishes but should generally be accurate descriptions of the purpose of the step.

##### Group
The 'group' defines the experiment group for which the runtime workflow step is associated (e.g. Group A, Group B).

##### Parameters
The following parameters will be used when authoring the experiment step.

| Name                     | Required   | Data Type        | Description                |
| ------------------------ | ---------- | ---------------- | -------------------------- |
| command                  | Yes        | string           | The relative path to the VirtualClient.exe (see dependency paths notes below).
| commandArguments         | Yes        | string           | The command-line arguments to supply to the VirtualClient.exe (e.g. --profile=PERF-IO-V1.json).
| duration                 | Yes        | timespan         | A timespan representing the amount of time the VirtualClient.exe should be allowed to run before it will be stopped/terminated (e.g. 01.00:00:00).
| includeSpecifications    | No         | boolean          | True or false whether the provider should pass specifications (e.g. disk specifications) to the Virtual Client. Default = true.
| timeout                  | No         | timespan         | A timespan that defines the absolute timeout for the step as a whole (e.g. 01.01:00:00). This takes priority over the 'duration' parameter.
| timeoutMinStepsSucceeded | No         | int/number       | Optional parameter defines the minimum number of workload steps running the VirtualClient.exe that must finish successfully in order for the overall experiment step (for the experiment group) to be considered a success. Default = All workload steps must finish successfully.
| eventHubConnectionString | No         | string           | Optional parameter defines either the EventHub connection string, or the eventHubConnectionstring secret name in AzureKeyVault.
| applicationInsightsInstrumentationKey | No | string      | Optional parameter defines either the ApplicationInsights InstrumentationKey, or the ApplicationInsights InstrumentationKey secret name in AzureKeyVault.

##### Example Definitions
``` json
{
    "type": "Juno.Execution.Providers.Workloads.VirtualClientWorkloadProvider",
    "name": "Run Virtual client",
    "description": "Run Virtual client",
    "group": "Group A",
    "parameters": {
        "command": "packages\\VirtualClient\\1.0.1157.32\\content\\win-x64\\VirtualClient.exe",
        "commandArguments": "--profile=PERF-IO-V1.json --platform=Juno",
        "duration": "01.00:00:00",
        "timeout": "01.01:00:00"
    }
}

// ..With a NuGet package as a dependency of the provider.
{
    "type": "Juno.Execution.Providers.Workloads.VirtualClientWorkloadProvider",
    "name": "Run Virtual client",
    "description": "Run Virtual client",
    "group": "Group A",
    "parameters": {
        "command": "{NuGetPackagePath}\\VirtualClient\\1.0.1157.32\\content\\win-x64\\VirtualClient.exe",
        "commandArguments": "--profile=PERF-IO-V1.json --platform=Juno",
        "duration": "01.00:00:00",
        "timeout": "01.01:00:00"
    },
    "dependencies": [
        "type": "Juno.Execution.Providers.Dependencies.NuGetPackageProvider",
        "name": "Install Virtual Client",
        "description": "Install Virtual Client NuGet package",
        "group": "Group A",
        "parameters": {
            "feedUri": "https://msazure.pkgs.visualstudio.com/_packaging/CRC/nuget/v3/index.json",
            "packageName": "VirtualClient",
            "packageVersion": "1.0.1157.32",
            "personalAccessToken": "[secret:keyvault]=NugetAccessToken"
        }
    ]
}
```

