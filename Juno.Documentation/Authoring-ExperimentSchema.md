# Juno Experiment Schema
The following documentation describes the schema of a Juno experiment.

## Schema Sections
A Juno experiment is divided into different sections, each providing definitions required to define the requirements of a part of an experiment E2E execution. 
The core sections include the experiment description and the workflow definition. Each of these sections are described in more detail below.

| Field               | Descriptions                                     |
| :------------------ | :----------------------------------------------- |
| $schema             | A URI that defines the location of the schema definition. This property is optional.
| name                | A name for the experiment.
| description         | A description of the experiment.
| contentVersion      | The version of the experiment. Users can use this value to identify different versions of their experiments.
| metadata            | A set of key/value pairs that provide information or context about the purpose of the experiment. Metadata is purely informational. This property is optional.
| parameters          | A set of key/value pairs that each define a 'shared parameter'. Shared parameters can be referenced any number of times by steps/components in the experiment workflow. Shared parameters are helpful for reducing duplication in experiment definitions where a specific parameter is used more than once. This property is not required.
| workflow            | Describes the set of individual steps for the experiment in the order for which they should be executed (see Experiment Workflow Section).

### Experiment Description Section
The first section of the experiment JSON document provides identifiers and descriptions for the experiment as a whole. This includes a name and description
of the experiment, any metadata and any shared parameters.

``` json
{
    "$schema": "http://juno-prod01.westus2/workflows/v1.0/schema",
    "contentVersion": "1.0.0",
    "name": "IPU2020.1 microcode update experiment.",
    "description": "Experiment to validate the difference in performance and reliability of nodes after IPU2020.1 microcode update.",
    "metadata": {
        "teamName": "CSI AIR",
        "email": "csiair@microsoft.com",
        "owners": "rashah"
    },
    "parameters": {
        "subscription": "C3BEA1FC-3025-4212-B548-7EB11C82B014",
        "vmSku": "Standard_DS2_V2",
        "vmCount": 5,
        "nodeCPUId": "56345",
        "IPUPFServicePath": "\\\\reddog\\builds\\some\\path\\to\\PF\\service\\folder"
    },
    "workflow": [ ... ]
}
```

##### Parameter References
Parameters are used in an experiment definition to enable the author to reference parameter values throughout experiment workflow steps. Shared parameters are
referenced using a standard JPath syntax: ```$.parameters.<parameterName>```

``` json
// For example, given the following parameters defined for the experiment:
{
    "$schema": "http://juno-prod01.westus2/workflows/v1.0/schema",
    "contentVersion": "1.0.0",
    "name": "IPU2020.1 microcode update experiment.",
    "parameters": {
        "subscription": "C3BEA1FC-3025-4212-B548-7EB11C82B014",
        "vmSku": "Standard_DS2_V2",
        "vmCount": 5
    },
    "workflow": [ ... ]
}

// The parameters could be referenced in individual experiment workflow steps like so:
{
    "type": "Juno.Providers.Workloads.Any",
    "name": "Any step",
    "group": "Any group",

    "parameters": {
        "subscription": "$.parameters.subscription",
        "vmSku": "$.parameters.vmSku",
        "vmCount": "$.parameters.vmCount"
    }
}
```

### Experiment Workflow Section
The experiment **workflow** section describes the steps for an experiment as well as the group to which they are related. The steps are defined in the order
for which they should be executed during the experiment.

##### Experiment Step/Component Schema
The schema of each individual step allows the author to define the details of the step. The schema provides important information including the name, description, environment group
and the type of the provider that will handle the runtime execution of the step in the Juno system.

| Field               | Descriptions                                     |
| :------------------ | :----------------------------------------------- |
| type                | Defines the fully qualified name of the runtime experiment provider that will handle the execution of the step. A Juno experiment provider is essentially the programmatic representation of the step. This property is required.
| name                | Defines the name of the step as it will be represented in the Juno system when the experiment is in execution. This property is required.
| description         | Defines a description of the step. This is helpful for making the flow of an experiment more readable and understandable because it can be used to provide detailed context for each individual step. This property is not required but recommended for providing clarity.
| group               | Defines the experiment group for which the step is related. The execution of Juno experiments is divided into groups (e.g. A/B). The author of an experiment can define the precise environment for each group as well as precisely what happens in each group using this property. This property is required.
| parameters          | Defines a set of key/value pairs for information required by the step when in execution. This property may or may not be required by some steps.
| tags                | Defines a set of key/value pairs that provide additional metadata for a step. This information can be used to improve queryability of telemetry in the system. This property is not required.
| dependencies        | Defines a set of one or more dependencies for the step during runtime execution. For example, the step may require downloading a specific toolset from a NuGet feed before it can proceed. The schema is exactly the same as the schema for an experiment step. This property is typically not required but may be required by certain steps.

``` json
{
    "type": "Juno.Providers.Workloads.VirtualClient",
    "name": "Run CPU stress workload",
    "description": "Runs customer-representative CPU workload on each of the VMs in the Group A environment.",
    "group": "Group A",
    "parameters": {
        "command": "/Workloads/VirtualClient/1.0.0/VirtualClient.exe --profile:default_cpu_profile --runtime:24:00:00",
        "timeout": "24:00:00"
    },
    "tags": [
        "workloadType": "cpu",
        "workloads": "GeekBench"
    ],
    "dependencies": [
        {
            "type": "Juno.Providers.Dependencies.NuGetPackage",
            "name": "VirtualClient NuGet Package",
            "description": "NuGet package containing the VirtualClient.exe toolset.",
            "parameters": {
                "feedUri": "https://any.nuget.feed/_packaging/any/nuget/v3/index.json",
                "packageName": "VirtualClient",
                "packageVersion": "1.0.0",
                "personalAccessToken": "[secret:keyvault]=AnyNuGetFeedAccessToken",
                "installationPath": "/Workloads/1.0.0"
            }
        }
    ]
}
```

##### Types of Experiment Steps
There are 5 different types of providers that can exist in a Juno experiment workflow.

* **Environment criteria**  
  Environment criteria steps are used to select the physical or virtual entities (e.g. clusters, nodes, VMs) that will be used in each Juno
  environment/experiment group to host experiment runtime requirements. For example, a particular environment criteria step might be responsible
  for selecting clusters in Azure data centers that have physical nodes or blades with SKUs that have a certain CPU and that can support a specific
  VM SKU.

  ``` json
  {
    "type": "Juno.Providers.Environment.ClusterSelectionCriteria",
    "name": "Skylake Clusters",
    "description": "Selection criteria to identify clusters with nodes that have Intel Skylake CPUs.",
    "group": "*",
    "parameters": {
        "nodeCpuId": "50654"
    }
  }
  ```

* **Environment setup**  
  Environment setup steps are used to describe the build-out of the actual environment (e.g. establishing TiP sessions, creating VMs) once the physical and
  virtual entities have been selected. For example, a specific environment setup step might be responsible for establishing a TiP (test-in-production)
  session for a specific node or for deploying a set of VMs to that node through ARM.

  ``` json
  {
    "type": "Juno.Providers.Environment.TipSessionProvider",
    "name": "Create TiP Session",
    "description": "Create TiP session to isolate the physical node.",
    "group": "Group A",
    "parameters": {
        "anyParameter": "any value"
    }
  }
  ```

* **Environment cleanup**  
  Environment cleanup steps define how and when to tear down/cleanup environments used in experiments. The Juno system enables authors of experiments to 
  be explicit about the cleanup of the environment to support flexibility with scenarios that require changing the environment during the course of the
  experiment.

  ``` json
  {
    "type": "Juno.Providers.Environment.TipSessionCleanupProvider",
    "name": "Cleanup TiP Session",
    "description": "Cleanup TiP session so the node can be returned to production customer capacity.",
    "group": "Group A",
    "parameters": {
        "cleanUpCondition": "OnTimeExpiry",
        "durationInHours": 24
    }
  }
  ```

* **Experiment Payload**  
  Payload steps describe the change or "treatment" to apply to physical entities (e.g. nodes) in the environment. For example, the step might define an
  Intel microcode update (IPU) to apply to the physical node before running workloads in VMs on that node.

  ``` json
   {
        "type": "Juno.Providers.Payloads.PFServiceApplication",
        "name": "Apply IPU2020.1",
        "description": "Applies the Intel 2020.1 microcode update to the physical node/blade.",
        "group": "Group B",
        "parameters": {
            "buildPath": "\\\\reddog\\builds\\some\\path\\to\\PF\\service\\ipu2020.1"
        }
   }
  ```

* **Experiment Workload**  
  Workload steps describe the workload/stress that will be placed on the systems under test in order to validate the the net changes to the system caused
  by the application of a payload. For example, a workload step might define a customer-representative CPU, IO or memory workload to run on VMs after having
  applied a microcode update to the physical node underneath.

  ``` json
  {
    "type": "Juno.Providers.Workloads.VirtualClient",
    "name": "Run CPU stress workload",
    "description": "Runs customer-representative CPU workload on each of the VMs in the Group A environment.",
    "group": "Group A",
    "parameters": {
        "command": "/Workloads/VirtualClient/1.0.0/VirtualClient.exe --profile:default_cpu_profile --runtime:24:00:00",
        "timeout": "24:00:00"
    },
    "tags": [
        "workloadType": "cpu",
        "workloads": "GeekBench"
    ],
    "dependencies": [
        {
            "type": "Juno.Providers.Dependencies.NuGetPackage",
            "name": "VirtualClient NuGet Package",
            "description": "NuGet package containing the VirtualClient.exe toolset.",
            "parameters": {
                "feedUri": "https://any.nuget.feed/_packaging/any/nuget/v3/index.json",
                "packageName": "VirtualClient",
                "packageVersion": "1.0.0",
                "personalAccessToken": "[secret:keyvault]=AnyNuGetFeedAccessToken",
                "installationPath": "/Workloads/1.0.0"
            }
        }
    ]
  }
  ```

### Experiment Examples
The following examples illustrate a few examples of common types of experiments/experiment workflows.

**Example 'A' Experiment Workflow**  
In the workflow below, there is only a single experiment group (Group A). In this example scenario, the same group is used to evaluate the 
effects of 2 different payloads on the same set of hosts (e.g. nodes/blades).

``` json
{
    "$schema": "http://juno-prod01.westus2/workflows/v1.0/schema",
    "contentVersion": "1.0.0",
    "name": "IPU2019.2 vs. IPU2020.1 update comparison.",
    "description": "Validate the difference between IPU2019.2 vs. IPU2020.1 microcode updates.",
    "metadata": {
        "teamName": "CSI AIR",
        "email": "csiair@microsoft.com",
        "owners": "rashah"
    },
    "parameters": {
        "subscription": "C3BEA1FC-3025-4212-B548-7EB11C82B014",
        "vmSku": "Standard_DS2_V2",
        "vmCount": 5,
        "nodeCPUId": "56345",
        "IPU2019PFServicePath": "\\\\reddog\\builds\\some\\path\\to\\PF\\service\\ipu2019.2",
        "IPU2020PFServicePath": "\\\\reddog\\builds\\some\\path\\to\\PF\\service\\ipu2020.1"
    },
    "workflow": [
      {
        "type": "Juno.Providers.Environment.ExampleCriteriaProvider",
        "name": "Cluster selection",
        "description": "Select clusters in the Azure fleet that meet the criteria of the experiment.",
        "group": "Group A",
        "parameters": {
            "nodeCPUId": "$.parameters.nodeCPUId",
            "vmSku": "$.parameters.vmSku",
            "minAvailableVmCount": 10
        },
        "tags": {
            "tag1": "AnyTag"
        }
      },
      {
        "type": "Juno.Providers.Environment.ExampleSetupProvider",
        "name": "Tip Node session provider",
        "description": "Acquire an isolated node in one of the clusters selected to run Group A steps.",
        "group": "Group A",
        "parameters": {
            "anyParameter": true
        },
        "tags": {
            "tag1": "AnyTag"
        }
      },
      {
        "type": "Juno.Providers.Environment.ExampleSetupProvider",
        "name": "Group A Virtual Machines",
        "description": "Create virtual machines on isolated TiP nodes to run Group A workloads.",
        "group": "Group A",
        "parameters": {
            "subscription": "$.metadata.subscription",
            "vmSku": "$.parameters.vmSku",
            "vmCount": "$.parameters.vmCount"
        },
        "tags": {
            "tag1": "AnyTag"
        }
      },
      {
        "type": "Juno.Providers.Payloads.ExamplePayloadProvider",
        "name": "Apply IPU2019.2 Payload",
        "description": "Applies the IPU2019.2 payload to the experiment group.",
        "group": "Group A",
        "parameters": {
            "buildPath": "$.parameters.IPU2019PFServicePath"
        },
        "tags": {
            "ipu": "IPU2019.2"
        }
      },
      {
        "type": "Juno.Providers.Workloads.ExampleWorkloadProvider",
        "name": "Run CPU workload",
        "description": "Customer-representative CPU workload",
        "group": "Group A",
        "parameters": {
            "command": "/Workloads/workload.exe -profile:basic-cpu-workload -runtime:24:00:00"
        },
        "tags": {
            "tag1": "AnyTag"
        },
        "dependencies": [
            {
                "type": "Juno.Providers.ExampleDependency",
                "name": "Workload",
                "description": "Workload.exe NuGet package",
                "parameters": {
                    "feedUri": "https://any.nuget.feed/_packaging/any/nuget/v3/index.json",
                    "packageName": "Workload",
                    "packageVersion": "2.2.0",
                    "personalAccessToken": "[secret:azurekeyvault]=AnyNuGetFeedAccessToken",
                    "installationPath": "/Workloads"
                },
                "tags": {
                    "tag1": "AnyTag"
                }
            }
        ]
      },
      {
        "type": "Juno.Providers.Payloads.ExamplePayloadProvider",
        "name": "Apply IPU2020.1 Payload",
        "description": "Applies the IPU2020.1 payload to the experiment group.",
        "group": "Group A",
        "parameters": {
            "buildPath": "$.parameters.IPU2020PFServicePath"
        },
        "tags": {
            "ipu": "IPU2020.1"
        }
      },
      {
        "type": "Juno.Providers.Workloads.ExampleWorkloadProvider",
        "name": "Run CPU workload",
        "description": "Customer-representative CPU workload",
        "group": "Group A",
        "parameters": {
            "command": "/Workloads/workload.exe -profile:basic-cpu-workload -runtime:24:00:00"
        },
        "tags": {
            "tag1": "AnyTag"
        },
        "dependencies": [
            {
                "type": "Juno.Providers.ExampleDependency",
                "name": "Workload",
                "description": "Workload.exe NuGet package",
                "parameters": {
                    "feedUri": "https://any.nuget.feed/_packaging/any/nuget/v3/index.json",
                    "packageName": "Workload",
                    "packageVersion": "2.2.0",
                    "personalAccessToken": "[secret:azurekeyvault]=AnyNuGetFeedAccessToken",
                    "installationPath": "/Workloads"
                },
                "tags": {
                    "tag1": "AnyTag"
                }
            }
        ]
      },
      {
        "type": "Juno.Providers.Environment.ExampleCleanupProvider",
        "name": "Cleanup environment resources used in the experiment.",
        "group": "Group A",
        "parameters": {
            "cleanUpCondition": "OnTimeExpiry",
            "durationInHours": 24
        },
        "tags": {
            "tag1": "AnyTag"
        }
      }
  ]
}
```

**Example 'A/B' Experiment Workflow**  
In the workflow below, there are 2 experiment groups (Group A and Group B). In this example scenario, Group A environment (nodes and virtual machines)
are being used to produce a baseline result against which any changes can be validated for effects.  Group B environment nodes and virtual machines
will receive the payload or "treatment". The results from Group B will be compared against the results of Group A to determine differences in the
performance, reliability etc... of data center nodes given the deployment of the payload.

``` json
{
    "$schema": "http://juno-prod01.westus2/workflows/v1.0/schema",
    "contentVersion": "1.0.0",
    "name": "IPU2020.1 microcode update experiment.",
    "description": "Experiment to validate the difference in performance and reliability of nodes after IPU2020.1 microcode update.",
    "metadata": {
        "teamName": "CSI AIR",
        "email": "csiair@microsoft.com",
        "owners": "rashah"
    },
    "parameters": {
        "subscription": "C3BEA1FC-3025-4212-B548-7EB11C82B014",
        "vmSku": "Standard_DS2_V2",
        "vmCount": 5,
        "nodeCPUId": "56345",
        "IPUPFServicePath": "\\\\reddog\\builds\\some\\path\\to\\PF\\service\\ipu2020.1"
    },
    "workflow": [
        {
            "type": "Juno.Providers.Environment.ExampleCriteriaProvider",
            "name": "Group A Cluster selection",
            "description": "Select clusters in the Azure fleet that meet the criteria of the experiment for Group A.",
            "group": "*",
            "parameters": {
                "nodeCPUId": "$.parameters.nodeCPUId",
                "vmSku": "$.parameters.vmSku",
                "minAvailableVmCount": 10
            },
            "tags": {
                "tag1": "AnyTag"
            }
        },
        {
            "type": "Juno.Providers.Environment.ExampleSetupProvider",
            "name": "Isolate Group A Node",
            "description": "Acquire an isolated node in one of the clusters selected to run Group A steps.",
            "group": "Group A",
            "parameters": {
                "anyParameter": true
            },
            "tags": {
                "tag1": "AnyTag"
            }
        },
        {
            "type": "Juno.Providers.Environment.ExampleSetupProvider",
            "name": "Isolate Group B Node",
            "description": "Acquire an isolated node in one of the clusters selected to run Group B steps.",
            "group": "Group B",
            "parameters": {
                "anyParameter": true
            },
            "tags": {
                "tag1": "AnyTag"
            }
        },
        {
            "type": "Juno.Providers.Environment.ExampleSetupProvider",
            "name": "Group A Virtual Machines",
            "description": "Create virtual machines on isolated TiP nodes to run Group A workloads.",
            "group": "Group A",
            "parameters": {
                "subscription": "$.metadata.subscription",
                "vmSku": "$.parameters.vmSku",
                "vmCount": "$.parameters.vmCount"
            },
            "tags": {
                "tag1": "AnyTag"
            }
        },
        {
            "type": "Juno.Providers.Environment.ExampleSetupProvider",
            "name": "Group B Virtual Machines",
            "description": "Create virtual machines on isolated TiP nodes to run Group B workloads.",
            "group": "Group B",
            "parameters": {
                "subscription": "$.metadata.subscription",
                "vmSku": "$.parameters.vmSku",
                "vmCount": "$.parameters.vmCount"
            },
            "tags": {
                "tag1": "AnyTag"
            }
        },
        {
            "type": "Juno.Providers.Payloads.ExamplePayloadProvider",
            "name": "Apply IPU2020.1 Payload",
            "description": "Applies the payload to nodes in Group B.",
            "group": "Group B",
            "parameters": {
                "buildPath": "$.parameters.IPUPFServicePath"
            },
            "tags": {
                "ipu": "IPU2020.1"
            }
        },
        {
            "type": "Juno.Providers.Workloads.ExampleWorkloadProvider",
            "name": "Run CPU workload",
            "description": "Customer-representative CPU workload",
            "group": "Group A",
            "parameters": {
                "command": "/Workloads/workload.exe -profile:basic-cpu-workload -runtime:24:00:00"
            },
            "tags": {
                "tag1": "AnyTag"
            },
            "dependencies": [
                {
                    "type": "Juno.Providers.ExampleDependency",
                    "name": "Workload",
                    "description": "Workload.exe NuGet package",
                    "parameters": {
                        "feedUri": "https://any.nuget.feed/_packaging/any/nuget/v3/index.json",
                        "packageName": "Workload",
                        "packageVersion": "2.2.0",
                        "personalAccessToken": "[secret:azurekeyvault]=AnyNuGetFeedAccessToken",
                        "installationPath": "/Workloads"
                    },
                    "tags": {
                        "tag1": "AnyTag"
                    }
                }
            ]
        },
        {
            "type": "Juno.Providers.Workloads.ExampleWorkloadProvider",
            "name": "Run CPU workload",
            "description": "Customer-representative CPU workload",
            "group": "Group B",
            "parameters": {
                "command": "/Workloads/workload.exe -profile:basic-cpu-workload -runtime:24:00:00"
            },
            "tags": {
                "tag1": "AnyTag"
            },
            "dependencies": [
                {
                    "type": "Juno.Providers.ExampleDependency",
                    "name": "Workload",
                    "description": "Workload.exe NuGet package",
                    "parameters": {
                        "feedUri": "https://any.nuget.feed/_packaging/any/nuget/v3/index.json",
                        "packageName": "Workload",
                        "packageVersion": "2.2.0",
                        "personalAccessToken": "[secret:azurekeyvault]=AnyNuGetFeedAccessToken",
                        "installationPath": "/Workloads"
                    },
                    "tags": {
                        "tag1": "AnyTag"
                    }
                }
            ]
        },
        {
            "type": "Juno.Providers.Environment.ExampleCleanupProvider",
            "name": "Cleanup environment resources used in the experiment.",
            "group": "Group A",
            "parameters": {
                "cleanUpCondition": "OnTimeExpiry",
                "durationInHours": 24
            },
            "tags": {
                "tag1": "AnyTag"
            }
        },
        {
            "type": "Juno.Providers.Environment.ExampleCleanupProvider",
            "name": "Cleanup environment resources used in the experiment.",
            "group": "Group B",
            "parameters": {
                "cleanUpCondition": "OnTimeExpiry",
                "durationInHours": 24
            },
            "tags": {
                "tag1": "AnyTag"
            }
        }
    ]
}
```