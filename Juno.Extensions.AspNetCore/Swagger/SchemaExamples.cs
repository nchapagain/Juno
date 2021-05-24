namespace Juno.Extensions.AspNetCore.Swagger
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Juno.Contracts;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Provides examples of Juno data contracts/models used to produce
    /// OpenAPI/Swagger documentation for Juno API services.
    /// </summary>
    internal static class SchemaExamples
    {
        public static Lazy<Experiment> Experiment { get; } = new Lazy<Experiment>(() =>
        {
            return new Experiment(
                $"A/B Experiment",
                $"A basic A/B experiment",
                "1.0.0",
                schema: "http://juno-prod01.westus2/workflows/v1.0/schema",
                metadata: new Dictionary<string, IConvertible>
                {
                    ["teamName"] = "CRC AIR",
                    ["email"] = "crcair@microsoft.com",
                    ["owners"] = "alias1;alias2"
                },
                parameters: new Dictionary<string, IConvertible>
                {
                    ["vmSku"] = "Standard_DS2_V2",
                    ["vmCount"] = 5,
                    ["nodeCPUId"] = 56345,
                    ["ipuPFServicePath"] = "\\\\reddog\\builds\\some\\path\\to\\PF\\service\\folder"
                },
                workflow: new List<ExperimentComponent>
                {
                    new ExperimentComponent(
                        "Juno.Examples.ClusterSelectionCriteria",
                        "Select environment clusters",
                        "Select clusters that match the criteria for the experiment (e.g. CPU ID, minimum TiP sessions).",
                        group: "*",
                        parameters: new Dictionary<string, IConvertible>
                        {
                            ["generation"] = "gen6",
                            ["cpuId"] = "$.parameters.nodeCPUId",
                            ["minAvailableNodes"] = 4
                        },
                        tags: new Dictionary<string, IConvertible>
                        {
                            ["category"] = "IPU2020.1 Validation"
                        }),
                    new ExperimentComponent(
                        "Juno.Examples.TipSessionProvider",
                        "Acquire TiP session",
                        "Acquires a TiP session to isolate a physical node for Group A.",
                        group: "Group A"),
                    new ExperimentComponent(
                        "Juno.Examples.TipSessionProvider",
                        "Acquire TiP session",
                        "Acquires a TiP session to isolate a physical node for Group B.",
                        group: "Group B"),
                    new ExperimentComponent(
                        "Juno.Examples.VirtualMachineProvider",
                        "Group A Virtual Machines",
                        "Create VMs on the nodes associated with the TiP session for Group A.",
                        "Group A",
                        new Dictionary<string, IConvertible>
                        {
                            ["vmSku"] = "$.parameters.vmSku",
                            ["vmCount"] = "$.parameters.vmCount"
                        }),
                    new ExperimentComponent(
                        "Juno.Examples.VirtualMachineProvider",
                        "Group B Virtual Machines",
                        "Create VMs on the nodes associated with the TiP session for Group B.",
                        "Group B",
                        new Dictionary<string, IConvertible>
                        {
                            ["vmSku"] = "$.parameters.vmSku",
                            ["vmCount"] = "$.parameters.vmCount"
                        }),
                    new ExperimentComponent(
                        "Juno.Examples.PilotFishServiceApplication",
                        "Apply IPU2020.1 Payload",
                        "Install IPU2020.1 payload on the physical node for Group B.",
                        group: "Group B",
                        parameters: new Dictionary<string, IConvertible>
                        {
                            ["buildPath"] = "$.parameters.ipuPFServicePath",
                        },
                        tags: new Dictionary<string, IConvertible>
                        {
                            ["ipu"] = "IPU2020.1",
                        }),
                    new ExperimentComponent(
                        "Juno.Examples.WorkloadProvider",
                        "Run CPU Workload",
                        "Run customer-representative CPU-targeted workloads on the VMs",
                        group: "Group A",
                        parameters: new Dictionary<string, IConvertible>
                        {
                            ["command"] = "/Workloads/Workload.exe --profile:basic-cpu-workload -runtime:24:00:00",
                            ["commandTimeout"] = "24:00:00",
                        },
                        dependencies: new List<ExperimentComponent>
                        {
                            new ExperimentComponent(
                                "Juno.Examples.NuGetPackageProvider",
                                "Workload Package",
                                "Workload NuGet package containing the Workload.exe toolset.",
                                parameters: new Dictionary<string, IConvertible>
                                {
                                    ["feedUri"] = "https://any.nuget.feed/_packaging/any/nuget/v3/index.json",
                                    ["packageName"] = "Workload",
                                    ["packageVersion"] = "2.2.0",
                                    ["accessToken"] = "[secret:keyvault]=AnyNuGetFeedAccessToken",
                                    ["installationPath"] = "/Workloads"
                                })
                        }),
                    new ExperimentComponent(
                        "Juno.Examples.WorkloadProvider",
                        "Run CPU Workload",
                        "Run customer-representative CPU-targeted workloads on the VMs",
                        group: "Group B",
                        parameters: new Dictionary<string, IConvertible>
                        {
                            ["command"] = "/Workloads/Workload.exe --profile:basic-cpu-workload -runtime:24:00:00",
                            ["commandTimeout"] = "24:00:00",
                        },
                        dependencies: new List<ExperimentComponent>
                        {
                            new ExperimentComponent(
                                "Juno.Examples.NuGetPackageProvider",
                                "Workload Package",
                                "Workload NuGet package containing the Workload.exe toolset.",
                                parameters: new Dictionary<string, IConvertible>
                                {
                                    ["feedUri"] = "https://any.nuget.feed/_packaging/any/nuget/v3/index.json",
                                    ["packageName"] = "Workload",
                                    ["packageVersion"] = "2.2.0",
                                    ["accessToken"] = "[secret:keyvault]=AnyNuGetFeedAccessToken",
                                    ["installationPath"] = "/Workloads"
                                })
                        }),
                    new ExperimentComponent(
                        "Juno.Examples.TipSessionCleanupProvider",
                        "Cleanup TiP session",
                        "Cleans up the TiP session used to isolate the physical node for Group A.",
                        group: "Group A",
                        parameters: new Dictionary<string, IConvertible>
                        {
                            ["cleanupCondition"] = "OnTimeExpiry",
                            ["durationInHours"] = "24"
                        }),
                    new ExperimentComponent(
                        "Juno.Examples.TipSessionCleanupProvider",
                        "Cleanup TiP session",
                        "Cleans up the TiP session used to isolate the physical node for Group B.",
                        group: "Group B",
                        parameters: new Dictionary<string, IConvertible>
                        {
                            ["cleanupCondition"] = "OnTimeExpiry",
                            ["durationInHours"] = "24"
                        }),
                });
        });

        public static Lazy<ExperimentInstance> ExperimentInstance { get; } = new Lazy<ExperimentInstance>(() =>
        {
            return new ExperimentInstance(
                Guid.NewGuid().ToString(),
                SchemaExamples.Experiment.Value);
        });

        public static Lazy<ExperimentMetadata> ExperimentContext { get; } = new Lazy<ExperimentMetadata>(() =>
        {
            ExperimentMetadata context = new ExperimentMetadata(
                Guid.NewGuid().ToString(),
                new Dictionary<string, IConvertible>
                {
                    ["parameter1"] = "Any Value",
                    ["parameter2"] = "00:05:00"
                });

            return context;
        });

        public static Lazy<ExperimentMetadataInstance> ExperimentContextInstance { get; } = new Lazy<ExperimentMetadataInstance>(() =>
        {
            ExperimentMetadataInstance instance = new ExperimentMetadataInstance(
                Guid.NewGuid().ToString(),
                SchemaExamples.ExperimentContext.Value);

            instance.Extensions.Add("entityPool", JToken.FromObject(new List<EnvironmentEntity>
            {
                EnvironmentEntity.Cluster("azCluster01", "Group A"),
                EnvironmentEntity.Cluster("azCluster02", "Group B"),
                EnvironmentEntity.Cluster("azCluster03", "Group C")
            }));

            instance.Extensions.Add("entitiesProvisioned", JToken.FromObject(new List<EnvironmentEntity>
            {
                EnvironmentEntity.Cluster("azCluster01", "Group A"),
                EnvironmentEntity.Cluster("azCluster02", "Group B"),
                EnvironmentEntity.Node("azCluster01,node01", "Group A"),
                EnvironmentEntity.Node("azCluster01,node02", "Group A"),
                EnvironmentEntity.Node("azCluster02,node03", "Group B"),
                EnvironmentEntity.Node("azCluster02,node04", "Group B"),

                EnvironmentEntity.VirtualMachine("azCluster01,node01,vm01", "azCluster01,node01", "Group A"),
                EnvironmentEntity.VirtualMachine("azCluster01,node01,vm02", "azCluster01,node01", "Group A"),
                EnvironmentEntity.VirtualMachine("azCluster01,node02,vm01", "azCluster01,node02", "Group A"),
                EnvironmentEntity.VirtualMachine("azCluster01,node02,vm02", "azCluster01,node02", "Group A"),
                EnvironmentEntity.VirtualMachine("azCluster02,node03,vm01", "azCluster02,node03", "Group B"),
                EnvironmentEntity.VirtualMachine("azCluster02,node03,vm02", "azCluster02,node03", "Group B"),
                EnvironmentEntity.VirtualMachine("azCluster02,node03,vm01", "azCluster02,node04", "Group B"),
                EnvironmentEntity.VirtualMachine("azCluster02,node03,vm02", "azCluster02,node04", "Group B"),
            }));

            return instance;
        });

        public static Lazy<ExperimentStepInstance> ExperimentStepInstance { get; } = new Lazy<ExperimentStepInstance>(() =>
        {
            return new ExperimentStepInstance(
                Guid.NewGuid().ToString(),
                SchemaExamples.ExperimentInstance.Value.Id,
                "Group A",
                SupportedStepType.Workload,
                ExecutionStatus.Pending,
                100,
                0,
                SchemaExamples.Experiment.Value.Workflow.ElementAt(1));
        });

        public static Lazy<ProblemDetails> ProblemDetails { get; } = new Lazy<ProblemDetails>(() =>
        {
            return new ProblemDetails
            {
                Title = "Data Store Error",
                Detail = "The data store operation failed.",
                Status = StatusCodes.Status500InternalServerError,
                Type = "DataStoreError"
            };
        });

        public static Lazy<AgentIdentification> AgentIdentification { get; } = new Lazy<AgentIdentification>(() =>
        {
            return new AgentIdentification("cluster_01", "node_01", "vm_01");
        });

        public static Lazy<AgentHeartbeat> AgentHeartbeat { get; } = new Lazy<AgentHeartbeat>(() =>
         {
             return new AgentHeartbeat(SchemaExamples.AgentIdentification.Value, AgentHeartbeatStatus.Starting, AgentType.GuestAgent);
         });

        public static Lazy<AgentHeartbeatInstance> AgentHeartbeatInstance { get; } = new Lazy<AgentHeartbeatInstance>(() =>
        {
            return new AgentHeartbeatInstance(
                Guid.NewGuid().ToString(),
                SchemaExamples.AgentIdentification.Value.ToString(),
                AgentHeartbeatStatus.Starting,
                AgentType.GuestAgent,
                DateTime.UtcNow,
                DateTime.UtcNow,
                "Agent is starting");
        });
    }
}
