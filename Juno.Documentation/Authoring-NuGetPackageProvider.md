<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Juno Experiment Steps</div>
<br/>

## NuGetPackageProvider
The following documentation illustrates how to define a Juno workflow step to install a NuGet package as a dependency of an existing experiment
workflow step.

Whereas, this step can exist in the experiment workflow, it is generally intended to be used as a direct dependency of an experiment
workflow step vs. a standalone step in the workflow itself. This step can ONLY be run in the Juno Guest agent process on virtual machines in 
the environment that are part of an experiment group.

### Preliminaries
It is important to understand the basic schema of an experiment before attempting to author Juno experiment workflow steps.

[Authoring Experiments](./Authoring-Experiments.md)  
[Juno Experiment Schema](./Authoring-ExperimentSchema.md)

### Dependencies
* Virtual machines must have been deployed in the environment that can host the Juno Guest agent.
* The Juno Guest agent must have been deployed to the target virtual machines in the environment.

### Step Authoring
This section describes how to author an experiment workflow step to instruct Juno to install a NuGet package as a dependency of an experiment
workflow/workflow step.

##### Type
The 'type' must be ```Juno.Execution.Providers.Dependencies.NuGetPackageProvider```

##### Name and description
The 'name' and 'description' properties are used to describe steps in the experiment runtime workflow.  The values can be set to anything the experiment
author wishes but should generally be accurate descriptions of the purpose of the step.

##### Group
The 'group' defines the experiment group for which the runtime workflow step is associated (e.g. Group A, Group B).

##### Parameters
The following parameters can be used when authoring the experiment step.

| Name                | Required   | Data Type        | Description                |
| ------------------- | ---------- | ---------------- | -------------------------- |
| feedUri             | Yes        | string/uri       | The absolute URI to the NuGet feed where the package exists.
| packageName         | Yes        | string           | The name of the NuGet package exactly as it is defined on the feed.
| packageVersion      | Yes        | string           | The version of the NuGet package exactly as it is defined on the feed.
| personalAccessToken | No         | string           | An Azure DevOps personal access token (PAT) for a user/account that has access to the NuGet feed. This may also be a reference to a personal access token in a Juno Key Vault (recommended, see below).

**Note on Personal Access Tokens**  
Personal access tokens are considered secrets. For security purposes, a literal personal access token SHOULD NOT be placed in the experiment definition. The personal access token should
be added to a Juno Key Vault and referenced using the special syntax/format:

*[secret:keyvault]=&lt;SecretNameInKeyVault&gt;*

##### Installation Paths
NuGet packages are installed into a specific directory on the local system. The directory will exist inside of the %Temp% directory on the local
system in the following location:

``` %Temp%\Juno\NuGet\Packages```

The location of the NuGet packages path on the local system (for use when authoring experiment steps that depend on NuGet package locations), the author
can reference the location using the ```{NuGetPackagePath}``` placeholder in paths.

For Example:
```
{NuGetPackagePath}\VirtualClient\1.2.3\content\win-x64\VirtualClient.exe
```

##### Example Definitions
The following section provides examples of the structure of a valid experiment step.

<div style="color:#1569C7">
<div style="font-weight:600">Note:</div>
A backslash character ('\') in JSON text is considered an escape character. Any paths represented in JSON with backslash characters must use a double-backslash in order for JSON to consider 
the backslash as a string literal (e.g. .nuget\packages must be represented as .nuget\\packages).
</div>

``` json
In the example below, note that the path referenced in the 'command' for the VirtualClient.exe starts with the 'installationPath' to which the NuGet
package was installed. The remaining path follows standard NuGet package package semantics:

{NuGetPackagePath}\<packageName>\<packageVersion>\content\<runtime>

As shown above, the Juno NuGet package cache path on the VM can be referenced using the '{NuGetPackagePath}' placeholder.

{
    "type": "Juno.Execution.Providers.Workloads.VirtualClientWorkloadProvider",
    "name": "Run Virtual client",
    "description": "Run Virtual client",
    "group": "Group A",
    "parameters": {
        "command": "{NuGetPackagePath}\\VirtualClient\\1.0.1157.32\\content\\win-x64\\VirtualClient.exe",
        "commandArguments": "--profile=PERF-IO-V1.json --platform=Juno",
        "timeout": "24.00:00:00",
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